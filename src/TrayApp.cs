using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace UzbekKeyboardHelper;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon        _trayIcon;
    private readonly KeyboardHook      _hook;
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _startupItem;
    private bool _enabled = true;

    public TrayApp()
    {
        _hook = new KeyboardHook();
        _hook.Start();

        _enableItem  = new ToolStripMenuItem("Yoqilgan", null, OnToggleEnabled);
        _startupItem = new ToolStripMenuItem("Kompyuter yonganda avtomatik ishga tushish",
                                              null, OnToggleStartup)
        {
            Checked = IsStartupEnabled()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enableItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("hihi.uz", null, OnOpenHihi);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Chiqish", null, OnExit);

        _trayIcon = new NotifyIcon
        {
            Icon             = BuildIcon(enabled: true),
            Text             = "O'zbek Keyboard Helper — Yoqilgan",
            ContextMenuStrip = menu,
            Visible          = true
        };

        _trayIcon.DoubleClick += OnToggleEnabled;

        if (!IsStartupEnabled())
        {
            EnableStartup();
            _startupItem.Checked = true;
        }

        if (!IsFirstRunDone())
        {
            MarkFirstRunDone();
            // Delay bermasak Windows "focus stealing" tufayli brauzer fonda qoladi
            System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ => OpenGuide());
        }

        UpdateMenuState();
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _enabled      = !_enabled;
        _hook.Enabled = _enabled;
        UpdateMenuState();
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        if (IsStartupEnabled())
        {
            DisableStartup();
            _startupItem.Checked = false;
        }
        else
        {
            EnableStartup();
            _startupItem.Checked = true;
        }
    }

    private static void OnOpenHihi(object? sender, EventArgs e) =>
        OpenUrl("https://hihi.uz");

    private static void OpenGuide() =>
        OpenUrl("https://overlordbyte.github.io/uzbek-keyboard-helper");

    private void OnExit(object? sender, EventArgs e)
    {
        _hook.Stop();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    private static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = url,
            UseShellExecute = true
        });

    private void UpdateMenuState()
    {
        _enableItem.Text = _enabled
            ? "✓  Yoqilgan  (o'chirish uchun bosing)"
            : "○  O'chirilgan  (yoqish uchun bosing)";
        _trayIcon.Icon = BuildIcon(_enabled);
        _trayIcon.Text = _enabled
            ? "O'zbek Keyboard Helper — Yoqilgan"
            : "O'zbek Keyboard Helper — O'chirilgan";
    }

    // ── Registry ─────────────────────────────────────────────────────────────

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("UzbekKeyboardHelper") is not null;
    }

    private static void EnableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.SetValue("UzbekKeyboardHelper", Application.ExecutablePath);
    }

    private static void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue("UzbekKeyboardHelper", throwOnMissingValue: false);
    }

    private static bool IsFirstRunDone()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\UzbekKeyboardHelper");
        return key?.GetValue("FirstRunDone") is not null;
    }

    private static void MarkFirstRunDone()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\UzbekKeyboardHelper");
        key?.SetValue("FirstRunDone", 1);
    }

    // ── Icon: O'zbekiston bayrog'i (16×16) ───────────────────────────────────

    private static Icon BuildIcon(bool enabled)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (!enabled)
            {
                using var b1 = new SolidBrush(Color.FromArgb(160, 160, 160));
                using var b2 = new SolidBrush(Color.FromArgb(210, 210, 210));
                using var b3 = new SolidBrush(Color.FromArgb(130, 130, 130));
                using var br = new SolidBrush(Color.FromArgb(100, 100, 100));
                g.FillRectangle(b1, 0, 0,  16, 5);
                g.FillRectangle(br, 0, 5,  16, 1);
                g.FillRectangle(b2, 0, 6,  16, 4);
                g.FillRectangle(br, 0, 10, 16, 1);
                g.FillRectangle(b3, 0, 11, 16, 5);
            }
            else
            {
                using var blueB  = new SolidBrush(Color.FromArgb(0,   159, 227));
                using var whiteB = new SolidBrush(Color.White);
                using var greenB = new SolidBrush(Color.FromArgb(30,  180,  60));
                using var redB   = new SolidBrush(Color.FromArgb(206,  17,  38));

                g.FillRectangle(blueB,  0, 0,  16, 5);
                g.FillRectangle(redB,   0, 5,  16, 1);
                g.FillRectangle(whiteB, 0, 6,  16, 4);
                g.FillRectangle(redB,   0, 10, 16, 1);
                g.FillRectangle(greenB, 0, 11, 16, 5);

                g.FillEllipse(whiteB, 1, 0, 5, 5);
                g.FillEllipse(blueB,  2, 0, 5, 5);

                DrawStar(g, whiteB, 8,  1);
                DrawStar(g, whiteB, 11, 1);
                DrawStar(g, whiteB, 8,  3);
            }
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static void DrawStar(Graphics g, Brush brush, int x, int y) =>
        g.FillRectangle(brush, x, y, 2, 2);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
