using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VArchiveHelper;

internal sealed class MonitorCaptureInfo
{
	public MonitorCaptureInfo(
		Rectangle logicalBounds,
		Rectangle captureBounds,
		Size nativeResolution,
		double scaleX,
		double scaleY)
	{
		LogicalBounds = logicalBounds;
		CaptureBounds = captureBounds;
		PhysicalBounds = captureBounds;
		NativeResolution = nativeResolution;
		ScaleX = scaleX;
		ScaleY = scaleY;
	}

	public Rectangle LogicalBounds { get; }

	/// <summary>GDI/DXGI에 사용할 모니터 전체 영역 (위치=Bounds, 크기=네이티브 해상도).</summary>
	public Rectangle CaptureBounds { get; }

	public Rectangle PhysicalBounds { get; }

	public Size NativeResolution { get; }

	public double ScaleX { get; }

	public double ScaleY { get; }
}

internal static class MonitorGeometry
{
	private const int ENUM_CURRENT_SETTINGS = -1;

	private const int MDT_EFFECTIVE_DPI = 0;

	public static MonitorCaptureInfo GetCaptureInfo(Screen screen, bool usePhysicalPixels)
	{
		Rectangle logical = screen.Bounds;
		GetScale(screen, out double scaleX, out double scaleY);

		int nativeW = logical.Width;
		int nativeH = logical.Height;
		if (TryGetNativeResolution(screen.DeviceName, out int dmW, out int dmH))
		{
			nativeW = dmW;
			nativeH = dmH;
		}
		else if (usePhysicalPixels)
		{
			nativeW = (int)Math.Round(logical.Width * scaleX);
			nativeH = (int)Math.Round(logical.Height * scaleY);
		}

		var native = new Size(nativeW, nativeH);
		var capture = new Rectangle(logical.X, logical.Y, nativeW, nativeH);

		if (!usePhysicalPixels)
		{
			capture = logical;
			native = logical.Size;
		}

		return new MonitorCaptureInfo(logical, capture, native, scaleX, scaleY);
	}

	public static bool OutputMatchesMonitor(Rectangle outputBounds, MonitorCaptureInfo info)
	{
		if (outputBounds == info.LogicalBounds || outputBounds == info.CaptureBounds)
		{
			return true;
		}

		return outputBounds.Width == info.NativeResolution.Width
			&& outputBounds.Height == info.NativeResolution.Height
			&& Math.Abs(outputBounds.Left - info.LogicalBounds.Left) <= 64
			&& Math.Abs(outputBounds.Top - info.LogicalBounds.Top) <= 64;
	}

	public static void AppendDiagnostics(MonitorCaptureInfo info, System.Text.StringBuilder log)
	{
		log.AppendLine(
			$"논리: {info.LogicalBounds.Width}x{info.LogicalBounds.Height} @ ({info.LogicalBounds.Left},{info.LogicalBounds.Top})");
		log.AppendLine(
			$"캡처: {info.CaptureBounds.Width}x{info.CaptureBounds.Height} @ ({info.CaptureBounds.Left},{info.CaptureBounds.Top})  (네이티브 {info.NativeResolution.Width}x{info.NativeResolution.Height}, 배율 {info.ScaleX:0.##}x)");
	}

	private static void GetScale(Screen screen, out double scaleX, out double scaleY)
	{
		scaleX = 1;
		scaleY = 1;
		var center = new POINT
		{
			X = screen.Bounds.Left + screen.Bounds.Width / 2,
			Y = screen.Bounds.Top + screen.Bounds.Height / 2
		};
		IntPtr hMonitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
		if (hMonitor == IntPtr.Zero)
		{
			return;
		}

		if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) != 0)
		{
			return;
		}

		scaleX = dpiX / 96.0;
		scaleY = dpiY / 96.0;
	}

	private static bool TryGetNativeResolution(string deviceName, out int width, out int height)
	{
		width = 0;
		height = 0;
		var devMode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
		if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode))
		{
			return false;
		}

		width = devMode.dmPelsWidth;
		height = devMode.dmPelsHeight;
		return width > 0 && height > 0;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	private struct DEVMODE
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmDeviceName;
		public short dmSpecVersion;
		public short dmDriverVersion;
		public short dmSize;
		public short dmDriverExtra;
		public int dmFields;
		public int dmPositionX;
		public int dmPositionY;
		public int dmDisplayOrientation;
		public int dmDisplayFixedOutput;
		public short dmColor;
		public short dmDuplex;
		public short dmYResolution;
		public short dmTTOption;
		public short dmCollate;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string dmFormName;
		public short dmLogPixels;
		public int dmBitsPerPel;
		public int dmPelsWidth;
		public int dmPelsHeight;
		public int dmDisplayFlags;
		public int dmDisplayFrequency;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2;

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("Shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

	[DllImport("user32.dll", CharSet = CharSet.Ansi)]
	private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
}
