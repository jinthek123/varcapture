using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VArchiveHelper;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
	private const int WM_HOTKEY = 0x0312;

	private const int HOTKEY_ID = 0x5641;

	private readonly HelperConfig _config;

	private readonly Action<string> _onStatus;

	private bool _registered;

	public HotkeyWindow(HelperConfig config, Action<string> onStatus)
	{
		_config = config;
		_onStatus = onStatus;
		CreateHandle(new CreateParams());
		RegisterInsertHotkey();
	}

	private void RegisterInsertHotkey()
	{
		if (!RegisterHotKey(Handle, HOTKEY_ID, 0, (int)Keys.Insert))
		{
			_onStatus("Insert 단축키 등록 실패 (다른 프로그램과 충돌?)");
			return;
		}
		_registered = true;
		_onStatus($"대기 중 — Insert = 2번 모니터({_config.MonitorIndex}) 캡처 → v-archive 인식");
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
		{
			string result = CapturePipeline.Run(_config);
			_onStatus(result);
			return;
		}
		base.WndProc(ref m);
	}

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	public void Dispose()
	{
		if (_registered)
		{
			UnregisterHotKey(Handle, HOTKEY_ID);
		}
		DestroyHandle();
	}
}
