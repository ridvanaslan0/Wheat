using System.Text;

namespace Wheat;

static class Program
{
    static Mutex? _singleInstance;

    [STAThread]
    static void Main()
    {
        // Needed so command output can be decoded with the Windows OEM code page.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Application.EnableVisualStyles() is intentionally NOT called:
        // it keeps buttons and menus in the classic Win9x look.
        Application.SetCompatibleTextRenderingDefault(false);

        _singleInstance = new Mutex(true, @"Local\Wheat.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show(AppInfo.Name + " is already running. Look for the mascot on your desktop or the icon in the system tray.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // One-time permission: after this the assistant never asks again before acting
        // (unless "Ask for confirmation before every action" is switched on in Settings).
        if (!AppSettings.Consented)
        {
            var answer = MessageBox.Show(
                AppInfo.Name + " lets an AI model run commands, read and write files, and open programs on this PC " +
                "with administrator rights.\n\n" +
                "If you accept, it will NOT ask again before each action. You can turn per-action confirmations " +
                "back on at any time in Settings.\n\n" +
                "Only use AI providers you trust, and be careful with files or web pages you ask it to read.\n\n" +
                "Do you accept?",
                "One-time permission",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;
            AppSettings.SetConsented();
        }

        Application.Run(new MascotForm());
        GC.KeepAlive(_singleInstance);
    }
}
