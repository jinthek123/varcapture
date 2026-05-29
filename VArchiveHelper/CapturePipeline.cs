using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace VArchiveHelper;

internal static class CapturePipeline
{
	public static string Run(HelperConfig config)
	{
		try
		{
			// 게임 포커스/전체화면이 깨지기 전에 먼저 캡처 (v-archive 실행·Alt+Insert는 이후)
			using Bitmap bitmap = ScreenCapture.CaptureMonitorToTarget(config, out string error, out string method);
			if (bitmap == null)
			{
				return error;
			}
			VArchiveLauncher.EnsureRunning(config);
			Clipboard.SetImage(bitmap);
			Thread.Sleep(config.ClipboardSettleMs);
			Thread.Sleep(config.AfterClipboardBeforeRecognizeMs);
			InputHelper.SendAltInsert();
			return $"OK {bitmap.Width}x{bitmap.Height} [{method}] 모니터 인덱스 {config.MonitorIndex}";
		}
		catch (Exception ex)
		{
			return "오류: " + ex.Message;
		}
	}
}
