using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wheat;

/// <summary>
/// The mascot that sits on the desktop: borderless, transparent, always on top.
/// Character: an original hand-drawn floppy disk.
/// Click = open the speech bubble and type right there, drag = move, right-click = menu.
/// The full chat window is optional and still available from the menu.
/// </summary>
public class MascotForm : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HTCAPTION = 2;

    enum BubbleMode { Hidden, Message, Input }

    static readonly Color Key = Color.Fuchsia; // this color becomes transparent
    static readonly Color Paper = Color.FromArgb(255, 255, 225);

    // Layout: speech bubble on top, the 190x175 floppy drawing shifted to the bottom centre.
    const int FormW = 320, FormH = 326;
    const int MascotX = 65, MascotY = 152;
    static readonly Rectangle BubbleRect = new(4, 4, 312, 212);
    static readonly Rectangle MascotRect = new(MascotX + 55, MascotY + 92, 80, 76);

    readonly System.Windows.Forms.Timer _blinkTimer = new() { Interval = 3000 };
    readonly System.Windows.Forms.Timer _eyeTimer = new() { Interval = 160 };
    readonly System.Windows.Forms.Timer _bubbleTimer = new() { Interval = 7000 };
    readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    readonly Stopwatch _watch = new();
    readonly NotifyIcon _tray = new();
    readonly TextBox _reply = new();
    readonly TextBox _input = new();
    ToolStripMenuItem? _hideItem;
    ToolStripMenuItem? _startupItem;

    ChatForm? _chat;
    CancellationTokenSource? _cts;
    BubbleMode _mode = BubbleMode.Hidden;
    bool _blink, _busy;

    /// <summary>One AI client shared by the bubble and the chat window, so they keep one conversation.</summary>
    public AiClient Client { get; } = new();

    public MascotForm()
    {
        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Key;
        TransparencyKey = Key;
        DoubleBuffered = true;
        Font = new Font("Microsoft Sans Serif", 8.25f);
        ClientSize = new Size(FormW, FormH);
        StartPosition = FormStartPosition.Manual;
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);

        // ---- Speech bubble controls (shown only while the bubble is open) ----
        _reply.Multiline = true;
        _reply.ReadOnly = true;
        _reply.ScrollBars = ScrollBars.Vertical;
        _reply.BorderStyle = BorderStyle.None;
        _reply.BackColor = Paper;
        _reply.TabStop = false;
        _reply.Visible = false;

        _input.BorderStyle = BorderStyle.FixedSingle;
        _input.Visible = false;
        _input.KeyDown += OnInputKeyDown;

        Controls.Add(_reply);
        Controls.Add(_input);

        // ---- Right-click menu (shared with the tray icon) ----
        var ctx = new ContextMenuStrip { RenderMode = ToolStripRenderMode.System };
        ctx.Items.Add("Type to assistant", null, (_, _) => OpenInput());
        ctx.Items.Add("Open chat window", null, (_, _) => OpenChat());
        ctx.Items.Add("Settings...", null, (_, _) => { using var f = new SettingsForm(); f.ShowDialog(); });
        _startupItem = new ToolStripMenuItem("Start with Windows");
        _startupItem.Click += (_, _) => ToggleStartup();
        ctx.Items.Add(_startupItem);
        _hideItem = new ToolStripMenuItem("Hide mascot");
        _hideItem.Click += (_, _) => SetMascotVisible(!Visible);
        ctx.Items.Add(_hideItem);
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("Exit", null, (_, _) => Application.Exit());
        ctx.Opening += (_, _) => _startupItem.Checked = StartupManager.IsEnabled();
        ContextMenuStrip = ctx;

        // ---- System tray icon ----
        _tray.Icon = AppInfo.AppIcon ?? MakeIcon();
        _tray.Text = AppInfo.Name;
        _tray.ContextMenuStrip = ctx;
        _tray.DoubleClick += (_, _) => OpenInput();
        _tray.Visible = true;

        // ---- Timers ----
        _blinkTimer.Tick += (_, _) => { _blink = true; Invalidate(MascotRect); _eyeTimer.Start(); };
        _eyeTimer.Tick += (_, _) => { _blink = false; _eyeTimer.Stop(); Invalidate(MascotRect); };
        _bubbleTimer.Tick += (_, _) =>
        {
            _bubbleTimer.Stop();
            if (_mode == BubbleMode.Message) ShowBubble(BubbleMode.Hidden);
        };
        _clock.Tick += (_, _) => Invalidate(new Rectangle(8, 190, 304, 22)); // refreshes the "Thinking... 12s" hint
        _blinkTimer.Start();

        Shown += (_, _) =>
            Say(!AppSettings.Ready
                ? "Hi! Right-click me > Settings... and enter your API key."
                : "Hi! Click me and type what you need.");
    }

    /// <summary>Fallback 16x16 floppy icon for the tray (used only if the exe icon cannot be read).</summary>
    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var navy = new SolidBrush(Color.FromArgb(32, 64, 160));
            g.FillRectangle(navy, 1, 1, 14, 14);
            g.FillRectangle(Brushes.Silver, 4, 1, 8, 5);
            g.FillRectangle(Brushes.White, 3, 9, 10, 6);
            g.FillRectangle(Brushes.Black, 5, 11, 2, 2);
            g.FillRectangle(Brushes.Black, 9, 11, 2, 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    // ------------------------------------------------------------------
    //  Public API used by ChatForm
    // ------------------------------------------------------------------

    public void OpenChat()
    {
        if (_chat == null || _chat.IsDisposed) _chat = new ChatForm(this);
        _chat.Show();
        if (_chat.WindowState == FormWindowState.Minimized) _chat.WindowState = FormWindowState.Normal;
        _chat.Activate();
    }

    /// <summary>Opens the speech bubble with a text box, ready to type.</summary>
    public void OpenInput()
    {
        SetMascotVisible(true);
        if (_reply.TextLength == 0) _reply.Text = "Hi! Type a message below and press Enter.";
        ShowBubble(BubbleMode.Input);
    }

    /// <summary>Shows a short message in the bubble (it hides itself after a few seconds).</summary>
    public void Say(string text)
    {
        if (_mode == BubbleMode.Input)
        {
            AppendLog("Assistant: " + text + "\n");
            return;
        }

        _reply.Text = NormalizeNewLines(text);
        _reply.Select(0, 0);
        ShowBubble(BubbleMode.Message);
        _bubbleTimer.Interval = Math.Clamp(6000 + text.Length * 40, 6000, 30000);
        _bubbleTimer.Stop();
        _bubbleTimer.Start();
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        Invalidate();
    }

    // ------------------------------------------------------------------
    //  Speech bubble
    // ------------------------------------------------------------------

    void ShowBubble(BubbleMode mode)
    {
        _mode = mode;
        var show = mode != BubbleMode.Hidden;
        var input = mode == BubbleMode.Input;

        _reply.Visible = show;
        _input.Visible = input;
        if (show)
        {
            _reply.SetBounds(10, 10, 300, input ? 148 : 196);
            _input.SetBounds(10, 164, 300, 22);
        }
        if (mode != BubbleMode.Message) _bubbleTimer.Stop();

        Invalidate();
        if (input)
        {
            Activate();
            _input.Focus();
        }
    }

    void SetMascotVisible(bool visible)
    {
        Visible = visible;
        if (_hideItem != null) _hideItem.Text = visible ? "Hide mascot" : "Show mascot";
    }

    static string NormalizeNewLines(string s) => s.Replace("\r\n", "\n").Replace("\n", "\r\n");

    static string Shorten(string s, int max) => s.Length <= max ? s : s[..(max - 3)] + "...";

    void AppendLog(string line)
    {
        if (_mode == BubbleMode.Hidden) ShowBubble(BubbleMode.Message);

        _reply.AppendText(NormalizeNewLines(line) + "\r\n");
        if (_reply.TextLength > 30000) _reply.Text = _reply.Text[^20000..]; // keep the log from growing forever
        _reply.SelectionStart = _reply.TextLength;
        _reply.ScrollToCaret();
    }

    async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            await SendFromBubbleAsync();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            if (_busy) _cts?.Cancel();                 // Esc while thinking = stop
            else ShowBubble(BubbleMode.Hidden);        // otherwise = close the bubble
        }
    }

    async Task SendFromBubbleAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0 || Client.Busy) return;

        if (!AppSettings.Ready)
        {
            AppendLog("Assistant: Please add an API key first (right-click me > Settings...).\n");
            return;
        }

        _input.Clear();
        AppendLog("You: " + text);

        _cts = new CancellationTokenSource();
        _watch.Restart();
        _clock.Start();
        SetBusy(true);

        void OnNote(string s) => AppendLog("  > " + Shorten(s, 100));
        Tools.Note += OnNote;

        try
        {
            var reply = await Client.AskAsync(text, _cts.Token);
            AppendLog("Assistant: " + reply + "\n");
        }
        catch (OperationCanceledException) when (_cts?.IsCancellationRequested == true)
        {
            AppendLog("(Stopped)\n");
        }
        catch (Exception ex)
        {
            AppendLog("Error: " + Shorten(ex.Message, 300) + "\n");
        }
        finally
        {
            Tools.Note -= OnNote;
            _clock.Stop();
            _watch.Stop();
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
            if (_mode == BubbleMode.Input) _input.Focus();
        }
    }

    void ToggleStartup()
    {
        try
        {
            StartupManager.Set(!StartupManager.IsEnabled());
            Say(StartupManager.IsEnabled()
                ? "OK, I will start with Windows."
                : "OK, autostart is off.");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not change the startup setting:\n\n" + ex.Message, AppInfo.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------
    //  Mouse and painting
    // ------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        var onMascot = MascotRect.Contains(e.Location);

        // If the position did not change after the drag loop ends, it was a click
        var before = Location;
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        if (onMascot && Location == before)
        {
            if (_mode == BubbleMode.Input) ShowBubble(BubbleMode.Hidden);
            else OpenInput();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
        g.Clear(Key);

        // ---- Speech bubble (classic tooltip style) ----
        if (_mode != BubbleMode.Hidden)
        {
            var r = BubbleRect;
            using var paper = new SolidBrush(Paper);
            g.FillRectangle(paper, r);
            g.DrawRectangle(Pens.Black, r);

            var cx = MascotX + 95; // horizontal centre of the mascot
            var by = r.Bottom;
            g.FillPolygon(paper, new[] { new Point(cx - 12, by), new Point(cx + 12, by), new Point(cx, by + 22) });
            g.DrawLine(Pens.Black, cx - 12, by, cx, by + 22);
            g.DrawLine(Pens.Black, cx + 12, by, cx, by + 22);
            using var paperPen = new Pen(Paper);
            g.DrawLine(paperPen, cx - 11, by, cx + 11, by); // erase the border line where the tail joins the bubble

            if (_mode == BubbleMode.Input)
            {
                using var small = new Font("Microsoft Sans Serif", 7.5f);
                var hint = _busy
                    ? $"Thinking... {(int)_watch.Elapsed.TotalSeconds}s    Esc = stop"
                    : "Enter = send    Esc = close";
                g.DrawString(hint, small, Brushes.DimGray, 10, 192);
            }
        }

        // ---- Floppy-disk mascot ----
        g.TranslateTransform(MascotX, MascotY);

        using var navy = new SolidBrush(Color.FromArgb(32, 64, 160));
        using var metal = new SolidBrush(Color.FromArgb(192, 192, 192));
        using var slot = new SolidBrush(Color.FromArgb(64, 64, 64));

        g.FillRectangle(navy, 55, 92, 80, 76);
        g.DrawRectangle(Pens.Black, 55, 92, 80, 76);

        g.FillRectangle(metal, 73, 92, 44, 26);          // metal shutter
        g.DrawRectangle(Pens.Black, 73, 92, 44, 26);
        g.FillRectangle(slot, 101, 96, 9, 18);           // shutter slot

        g.FillRectangle(Brushes.White, 65, 128, 60, 38); // label
        g.DrawRectangle(Pens.Black, 65, 128, 60, 38);

        // Eyes
        if (_blink || _busy)
        {
            g.DrawLine(Pens.Black, 78, 140, 86, 140);
            g.DrawLine(Pens.Black, 104, 140, 112, 140);
        }
        else
        {
            g.FillEllipse(Brushes.Black, 79, 133, 7, 10);
            g.FillEllipse(Brushes.Black, 104, 133, 7, 10);
        }

        // Mouth
        if (_busy) g.DrawEllipse(Pens.Black, 91, 150, 8, 8);
        else g.DrawArc(Pens.Black, 82, 144, 26, 12, 0, 180);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false; // no ghost icon left behind
            _tray.Dispose();
            _blinkTimer.Dispose();
            _eyeTimer.Dispose();
            _bubbleTimer.Dispose();
            _clock.Dispose();
            _cts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
