using System.Threading;
using System.Windows.Forms;

namespace UzbekKeyboardHelper;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "UzbekKeyboardHelper_7F3A9B2C", out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "O'zbek Keyboard Helper allaqachon ishlamoqda!",
                "Xabar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp());
    }
}
