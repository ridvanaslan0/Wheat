using System.Diagnostics;

namespace Wheat;

/// <summary>Optional classic Windows chat window: menu bar, history, input box, status bar.</summary>
public class ChatForm : Form
{
    readonly MascotForm _mascot;
    readonly RichTextBox _history = new();
    readonly TextBox _input = new();
    readonly Button _send = new();
    readonly Button _stop = new();
    readonly ToolStripStatusLabel _status = new("Ready");
    readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    readonly Stopwatch _watch = new();
    CancellationTokenSource? _cts;

    AiClient Client => _mascot.Client;

    public ChatForm(MascotForm mascot)
    {
        _mascot = mascot;
        Text = AppInfo.Name + " - Chat";
        if (AppInfo.AppIcon != null) Icon = AppInfo.AppIcon;
        ClientSize = new Size(500, 400);
        MinimumSize = new Size(340, 280);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft Sans Serif", 8.25f);

        // ---- Menu bar ----
        var menu = new MenuStrip { RenderMode = ToolStripRenderMode.System };

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(Item("&New Chat", (_, _) => NewChat(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(Item("E&xit", (_, _) => Application.Exit()));

        var edit = new ToolStripMenuItem("&Edit");
        edit.DropDownItems.Add(Item("&Copy", (_, _) => _history.Copy(), Keys.Control | Keys.C));
        edit.DropDownItems.Add(Item("Select &All", (_, _) => { _history.Focus(); _history.SelectAll(); }, Keys.Control | Keys.A));
        edit.DropDownItems.Add(new ToolStripSeparator());
        edit.DropDownItems.Add(Item("Clear &Screen", (_, _) => _history.Clear()));

        var settings = new ToolStripMenuItem("&Settings");
        settings.DropDownItems.Add(Item("&Provider, Key and Behavior...", (_, _) => OpenSettings()));

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(Item("&About", (_, _) =>
            MessageBox.Show(
                $"{AppInfo.Name} {AppInfo.Version}\n\n" +
                "A small retro helper that chats with an AI model and can operate your PC.\n" +
                "Runs with administrator rights; it does not ask before each action unless you turn that on in Settings.",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange(new ToolStripItem[] { file, edit, settings, help });

        // ---- Chat history ----
        _history.Dock = DockStyle.Fill;
        _history.ReadOnly = true;
        _history.BackColor = SystemColors.Window;
        _history.BorderStyle = BorderStyle.Fixed3D;
        _history.Font = new Font("Courier New", 9f);

        // ---- Bottom panel: input + stop + send ----
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(4) };

        _send.Text = "&Send";
        _send.Dock = DockStyle.Right;
        _send.Width = 76;
        _send.Click += async (_, _) => await SendAsync();

        _stop.Text = "S&top";
        _stop.Dock = DockStyle.Right;
        _stop.Width = 66;
        _stop.Enabled = false;
        _stop.Click += (_, _) => _cts?.Cancel();

        _input.Multiline = true;
        _input.Dock = DockStyle.Fill;
        _input.BorderStyle = BorderStyle.Fixed3D;
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };

        // Dock order matters: Fill first, then the right-docked buttons (last added goes rightmost)
        bottom.Controls.Add(_input);
        bottom.Controls.Add(_stop);
        bottom.Controls.Add(_send);

        // ---- Status bar ----
        var statusStrip = new StatusStrip { RenderMode = ToolStripRenderMode.System };
        statusStrip.Items.Add(_status);

        // Dock order matters: Fill first, menu last
        Controls.Add(_history);
        Controls.Add(bottom);
        Controls.Add(statusStrip);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _clock.Tick += (_, _) =>
            _status.Text = $"Thinking... {(int)_watch.Elapsed.TotalSeconds} s";

        Append($"{AppInfo.Name} is ready. Type something and press Enter.\n\n", Color.Gray);
        Shown += (_, _) => _input.Focus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window only hides it; the app keeps running
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }

    static ToolStripMenuItem Item(string text, EventHandler onClick, Keys shortcut = Keys.None)
    {
        var item = new ToolStripMenuItem(text, null, onClick);
        if (shortcut != Keys.None) item.ShortcutKeys = shortcut;
        return item;
    }

    bool OpenSettings()
    {
        using var f = new SettingsForm();
        var ok = f.ShowDialog(this) == DialogResult.OK;
        if (ok) _status.Text = "Ready - " + AppSettings.Current.Name;
        return ok;
    }

    void NewChat()
    {
        if (Client.Busy) return;
        Client.Reset();
        _history.Clear();
        Append("New chat started.\n\n", Color.Gray);
    }

    void Append(string text, Color color, bool bold = false)
    {
        _history.SelectionStart = _history.TextLength;
        _history.SelectionLength = 0;
        _history.SelectionColor = color;
        _history.SelectionFont = new Font(_history.Font, bold ? FontStyle.Bold : FontStyle.Regular);
        _history.AppendText(text);
        _history.ScrollToCaret();
    }

    async Task SendAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0 || !_send.Enabled) return;

        if (Client.Busy)
        {
            _status.Text = "The assistant is busy with another request";
            return;
        }

        if (!AppSettings.Ready)
        {
            MessageBox.Show("You need to enter an API key first.", "No API key",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (!OpenSettings() || !AppSettings.Ready) return;
        }

        _input.Clear();
        Append("You: ", Color.Navy, true);
        Append(text + "\n", Color.Black);

        _send.Enabled = false;
        _input.Enabled = false;
        _stop.Enabled = true;
        _cts = new CancellationTokenSource();
        _watch.Restart();
        _clock.Start();
        _status.Text = "Thinking... 0 s";
        _mascot.SetBusy(true);

        void OnNote(string s) => Append("  > tool: " + s + "\n", Color.Gray);
        Tools.Note += OnNote;

        try
        {
            var reply = await Client.AskAsync(text, _cts.Token);
            Append("Assistant: ", Color.DarkGreen, true);
            Append(reply + "\n\n", Color.Black);
            _mascot.Say(reply);
            _status.Text = "Ready";
        }
        catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
        {
            Append("(Stopped)\n\n", Color.Gray);
            _status.Text = "Stopped";
            _mascot.Say("OK, stopped.");
        }
        catch (Exception ex)
        {
            Append("Error: " + ex.Message + "\n\n", Color.Red);
            _status.Text = "An error occurred";
            _mascot.Say("Something went wrong :(");
        }
        finally
        {
            Tools.Note -= OnNote;
            _clock.Stop();
            _watch.Stop();
            _cts?.Dispose();
            _cts = null;
            _mascot.SetBusy(false);
            _stop.Enabled = false;
            _send.Enabled = true;
            _input.Enabled = true;
            _input.Focus();
        }
    }
}
