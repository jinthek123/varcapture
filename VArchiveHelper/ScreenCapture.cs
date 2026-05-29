using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace VArchiveHelper;

internal static class ScreenCapture
{
	public static CaptureResult CaptureForPreview(HelperConfig config)
	{
		return CaptureInternal(config, scaleToTarget: true, dxgiWriteDiagnosticLog: false);
	}

	public static Bitmap CaptureMonitorToTarget(HelperConfig config, out string error, out string methodUsed)
	{
		using CaptureResult result = CaptureInternal(config, scaleToTarget: true, dxgiWriteDiagnosticLog: true);
		error = result.Error;
		methodUsed = result.Method;
		if (!result.Success)
		{
			return null;
		}
		return new Bitmap(result.Image);
	}

	private static DateTime _previewDxgiBackoffUntilUtc;

	private static CaptureResult CaptureInternal(HelperConfig config, bool scaleToTarget, bool dxgiWriteDiagnosticLog)
	{
		var log = new StringBuilder();
		Screen[] screens = Screen.AllScreens;
		if (screens.Length == 0)
		{
			return Fail(Rectangle.Empty, Size.Empty, Size.Empty, log, "연결된 모니터가 없습니다.");
		}
		if (config.MonitorIndex < 0 || config.MonitorIndex >= screens.Length)
		{
			return Fail(Rectangle.Empty, Size.Empty, Size.Empty, log,
				$"모니터 인덱스 {config.MonitorIndex} 가 없습니다. (0~{screens.Length - 1})");
		}

		Screen screen = screens[config.MonitorIndex];
		MonitorCaptureInfo monitor = MonitorGeometry.GetCaptureInfo(screen, config.UsePhysicalPixels);
		Rectangle captureBounds = monitor.CaptureBounds;

		log.AppendLine($"모니터 인덱스 {config.MonitorIndex}:");
		MonitorGeometry.AppendDiagnostics(monitor, log);

		if (config.UseDxgiCapture)
		{
			bool skipDxgiForPreviewBackoff = !dxgiWriteDiagnosticLog
				&& DateTime.UtcNow < _previewDxgiBackoffUntilUtc;
			if (skipDxgiForPreviewBackoff)
			{
				log.AppendLine("DXGI: 미리보기 — 직전 실패로 잠시 재시도 안 함");
				if (config.RequireDxgiCapture)
				{
					return Fail(captureBounds, Size.Empty, Size.Empty, log,
						"DXGI 필수: 미리보기 백오프 중 (잠시 후 재시도)");
				}
			}
			else
			{
				using Bitmap dxgi = DxgiMonitorCapture.TryCapture(monitor, out string dxgiError, dxgiWriteDiagnosticLog);
				if (dxgi != null)
				{
					_previewDxgiBackoffUntilUtc = DateTime.MinValue;
					log.AppendLine($"DXGI: 성공 ({dxgi.Width}x{dxgi.Height}, 모니터 전체)");
					return Success(dxgi, "DXGI", captureBounds, monitor, config, scaleToTarget, log);
				}

				log.AppendLine("DXGI: 실패 — " + (dxgiError ?? "알 수 없음"));
				if (!dxgiWriteDiagnosticLog)
				{
					_previewDxgiBackoffUntilUtc = DateTime.UtcNow.AddSeconds(5);
				}

				if (config.RequireDxgiCapture)
				{
					return Fail(captureBounds, Size.Empty, Size.Empty, log,
						"DXGI 필수: " + (dxgiError ?? "알 수 없음"));
				}
			}
		}
		else
		{
			log.AppendLine("DXGI: 사용 안 함 (UseDxgiCapture=false)");
			if (config.RequireDxgiCapture)
			{
				return Fail(captureBounds, Size.Empty, Size.Empty, log, "DXGI 필수인데 UseDxgiCapture=false 입니다.");
			}
		}

		using Bitmap gdi = CaptureGdi(captureBounds, out string gdiError);
		if (gdi != null)
		{
			log.AppendLine($"GDI: 성공 ({gdi.Width}x{gdi.Height}, 모니터 전체)");
			return Success(gdi, "GDI", captureBounds, monitor, config, scaleToTarget, log);
		}
		log.AppendLine("GDI: 실패 — " + (gdiError ?? "알 수 없음"));

		if (config.UseGameWindowCapture && !string.IsNullOrWhiteSpace(config.GameProcessName))
		{
			using Bitmap window = GameWindowCapture.TryCapture(config.GameProcessName, monitor, out string windowError);
			if (window != null)
			{
				log.AppendLine($"Window: 성공 ({window.Width}x{window.Height}, 창 크롭 — 비권장)");
				return Success(window, "Window", captureBounds, monitor, config, scaleToTarget, log);
			}
			log.AppendLine("Window: 실패 — " + (windowError ?? "알 수 없음"));
		}
		else
		{
			log.AppendLine("Window: 생략 (UseGameWindowCapture=false)");
		}

		return Fail(captureBounds, Size.Empty, Size.Empty, log, "모든 캡처 방식 실패");
	}

	private static CaptureResult Success(
		Bitmap raw,
		string method,
		Rectangle bounds,
		MonitorCaptureInfo monitor,
		HelperConfig config,
		bool scaleToTarget,
		StringBuilder log)
	{
		using Bitmap physical = EnsurePhysicalSize(raw, monitor, config.UsePhysicalPixels, log);
		Bitmap output = scaleToTarget ? ScaleToTarget(physical, config) : new Bitmap(physical);
		if (scaleToTarget && (physical.Width != output.Width || physical.Height != output.Height))
		{
			log.AppendLine($"v-archive 리사이즈: {physical.Width}x{physical.Height} → {output.Width}x{output.Height}");
		}

		return new CaptureResult(output, method, null, log.ToString().TrimEnd(), bounds, physical.Size, output.Size);
	}

	private static Bitmap EnsurePhysicalSize(
		Bitmap raw,
		MonitorCaptureInfo monitor,
		bool usePhysicalPixels,
		StringBuilder log)
	{
		if (!usePhysicalPixels)
		{
			return new Bitmap(raw);
		}

		int targetW = monitor.NativeResolution.Width;
		int targetH = monitor.NativeResolution.Height;
		if (raw.Width == targetW && raw.Height == targetH)
		{
			return new Bitmap(raw);
		}

		log.AppendLine($"물리 보정: {raw.Width}x{raw.Height} → {targetW}x{targetH}");
		return BitmapScale.ResizeHighQuality(raw, targetW, targetH);
	}

	private static CaptureResult Fail(Rectangle bounds, Size source, Size output, StringBuilder log, string error)
	{
		return new CaptureResult(null, null, error, log.ToString().TrimEnd(), bounds, source, output);
	}

	private static Bitmap CaptureGdi(Rectangle bounds, out string error)
	{
		error = null;
		try
		{
			using Bitmap raw = new Bitmap(bounds.Width, bounds.Height);
			using (Graphics graphics = Graphics.FromImage(raw))
			{
				graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
			}
			return new Bitmap(raw);
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return null;
		}
	}

	private static Bitmap ScaleToTarget(Bitmap source, HelperConfig config)
	{
		if (source.Width == config.TargetWidth && source.Height == config.TargetHeight)
		{
			return new Bitmap(source);
		}
		return BitmapScale.ResizeHighQuality(source, config.TargetWidth, config.TargetHeight);
	}
}
