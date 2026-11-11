namespace Wheat;

/// <summary>Classic-looking settings window: provider, API key, model, URL, reasoning effort and behavior options.</summary>
public class SettingsForm : Form
{
    static readonly (string Label, string Value)[] Efforts =
    {
        ("Default", ""),
        ("Low (faster)", "low"),
        ("Off (fastest)", "none"),
    };

    readonly ComboBox _provider = new();
    readonly TextBox _key = new();
    readonly TextBox _model = new();
    readonly TextBox _url = new();
    readonly ComboBox _effort = new();
    readonly Label _hint = new();
    readonly CheckBox _autostart = new();
    readonly CheckBox _askEach = new();

    public SettingsForm()
    {
        Text = "Settings";
        if (AppInfo.AppIcon != null) Icon = AppInfo.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true; // the mascot is always on top, so the dialog must be too
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 330);
        Font = new Font("Microsoft Sans Serif", 8.25f);

        // ---- AI provider ----
        var grp = new GroupBox { Text = "AI provider", Location = new Point(8, 6), Size = new Size(404, 204) };

        grp.Controls.Add(new Label { Text = "Provider:", Location = new Point(10, 27), AutoSize = true });
        _provider.Location = new Point(100, 23);
        _provider.Width = 290;
        _provider.DropDownStyle = ComboBoxStyle.DropDownList;
        _provider.DisplayMember = nameof(Provider.Name);
        _provider.Items.AddRange(AppSettings.Providers);
        grp.Controls.Add(_provider);

        grp.Controls.Add(new Label { Text = "API key:", Location = new Point(10, 57), AutoSize = true });
        _key.Location = new Point(100, 53);
        _key.Width = 290;
        _key.UseSystemPasswordChar = true;
        grp.Controls.Add(_key);

        _hint.Location = new Point(100, 77);
        _hint.Size = new Size(290, 28);
        _hint.ForeColor = SystemColors.GrayText;
        grp.Controls.Add(_hint);

        grp.Controls.Add(new Label { Text = "Model:", Location = new Point(10, 114), AutoSize = true });
        _model.Location = new Point(100, 110);
        _model.Width = 290;
        grp.Controls.Add(_model);

        grp.Controls.Add(new Label { Text = "URL:", Location = new Point(10, 144), AutoSize = true });
        _url.Location = new Point(100, 140);
        _url.Width = 290;
        grp.Controls.Add(_url);

        grp.Controls.Add(new Label { Text = "Thinking:", Location = new Point(10, 174), AutoSize = true });
        _effort.Location = new Point(100, 170);
        _effort.Width = 290;
        _effort.DropDownStyle = ComboBoxStyle.DropDownList;
        _effort.Items.AddRange(Efforts.Select(e => (object)e.Label).ToArray());
        grp.Controls.Add(_effort);
        new ToolTip().SetToolTip(_effort,
            "Speeds up models that think before answering. Some models reject this setting; if you get an error, switch back to Default.");

        Controls.Add(grp);

        // ---- Behavior ----
        var behavior = new GroupBox { Text = "Behavior", Location = new Point(8, 214), Size = new Size(404, 74) };

        _autostart.Text = "Start with Windows (runs as administrator, no UAC prompt)";
        _autostart.Location = new Point(10, 20);
        _autostart.Size = new Size(385, 20);
        behavior.Controls.Add(_autostart);

        _askEach.Text = "Ask for confirmation before every action (safer)";
        _askEach.Location = new Point(10, 44);
        _askEach.Size = new Size(385, 20);
        behavior.Controls.Add(_askEach);

        Controls.Add(behavior);

        _autostart.Checked = StartupManager.IsEnabled();
        _askEach.Checked = AppSettings.AskBeforeActions;

        _provider.SelectedIndexChanged += (_, _) => LoadProvider((Provider)_provider.SelectedItem!);
        _provider.SelectedItem = AppSettings.Current;

        var ok = new Button { Text = "OK", Location = new Point(256, 296), Size = new Size(75, 23), DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Location = new Point(337, 296), Size = new Size(75, 23), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Apply();
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    void Apply()
    {
        var p = (Provider)_provider.SelectedItem!;
        var k = _key.Text.Trim();
        if (k.Length > 0) AppSettings.SaveKey(p.Id, k);
        var m = _model.Text.Trim();
        var u = _url.Text.Trim();
        var effort = p.Anthropic ? "" : Efforts[Math.Max(0, _effort.SelectedIndex)].Value;
        AppSettings.Configure(p, m.Length > 0 ? m : p.DefaultModel, u.Length > 0 ? u : p.DefaultUrl, effort);

        AppSettings.SetAskBeforeActions(_askEach.Checked);

        try
        {
            if (_autostart.Checked != StartupManager.IsEnabled())
                StartupManager.Set(_autostart.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not change the startup setting:\n\n" + ex.Message, "Settings",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void LoadProvider(Provider p)
    {
        _key.Clear();
        _model.Text = AppSettings.Model(p);
        _url.Text = AppSettings.Url(p);
        var saved = AppSettings.HasKey(p) ? "A key is saved; type a new one to replace it. " : "";
        _hint.Text = saved + (p.KeyOptional ? "Key optional. " + p.KeySite : "Get a key at " + p.KeySite);

        var cur = AppSettings.Effort(p);
        _effort.SelectedIndex = Math.Max(0, Array.FindIndex(Efforts, e => e.Value == cur));
        _effort.Enabled = !p.Anthropic; // not used with the Claude protocol
    }
}
