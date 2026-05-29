using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VArchiveHelper;

internal static class Program
{
	private static readonly IntPtr PerMonitorDpiAwareV2 = new IntPtr(-4);

	[DllImport("user32.dll")]
	private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

	[STAThread]
	private static void Main()
	{
		SetProcessDpiAwarenessContext(PerMonitorDpiAwareV2);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		HelperConfig config = HelperConfig.Load();

		using var preview = new CapturePreviewForm(config);
		preview.Show();

		using var tray = new NotifyIcon
		{
			Icon = SystemIcons.Application,
			Text = "VArchiveHelper",
			Visible = true,
			ContextMenuStrip = BuildTrayMenu(preview)
		};

		using var hotkey = new HotkeyWindow(config, status =>
		{
			preview.SetInsertStatus(status);
			tray.BalloonTipTitle = "VArchiveHelper";
			tray.BalloonTipText = status;
			tray.ShowBalloonTip(2500);
			preview.RequestPreviewFromHotkey();
		});

		Application.Run(preview);
	}

	private static ContextMenuStrip BuildTrayMenu(CapturePreviewForm preview)
	{
		var menu = new ContextMenuStrip();
		menu.Items.Add("미리보기 창", null, (_, _) =>
		{
			preview.Show();
			preview.WindowState = FormWindowState.Normal;
			preview.BringToFront();
			preview.Activate();
		});
		menu.Items.Add("Insert 동작 실행", null, (_, _) => preview.RunInsertPipeline());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("종료", null, (_, _) => Application.Exit());
		return menu;
	}
}
