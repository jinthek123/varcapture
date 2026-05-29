using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace VArchiveHelper;

/// <summary>
/// 게임 프로세스의 최상위 창을 PrintWindow(PW_RENDERFULLCONTENT)로 캡처.
/// </summary>
internal static class GameWindowCapture
{
	private const uint PW_RENDERFULLCONTENT = 0x00000002;

	public static Bitmap TryCapture(string processName, MonitorCaptureInfo monitor, out string error)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(processName))
		{
			error = "GameProcessName 없음";
			return null;
		}

		IntPtr hwnd = FindTopLevelWindow(processName.Trim());
		if (hwnd == IntPtr.Zero)
		{
			error = $"프로세스 '{processName}' 창 없음";
			return null;
		}

		if (!GetWindowRect(hwnd, out RECT rect))
		{
			error = "GetWindowRect 실패";
			return null;
		}

		var windowBounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
		if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
		{
			error = "창 크기가 0";
			return null;
		}

		Rectangle crop = Rectangle.Intersect(windowBounds, monitor.LogicalBounds);
		if (crop.Width <= 0 || crop.Height <= 0)
		{
			error = "게임 창이 대상 모니터와 겹치지 않음";
			return null;
		}

		using Bitmap full = CaptureWindowBitmap(hwnd, windowBounds.Width, windowBounds.Height, out error);
		if (full == null)
		{
			return null;
		}

		if (crop == windowBounds)
		{
			return new Bitmap(full);
		}

		var region = new Rectangle(
			crop.Left - windowBounds.Left,
			crop.Top - windowBounds.Top,
			crop.Width,
			crop.Height);
		return BitmapCrop.Crop(full, region);
	}

	private static Bitmap CaptureWindowBitmap(IntPtr hwnd, int width, int height, out string error)
	{
		error = null;
		var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			IntPtr hdc = graphics.GetHdc();
			try
			{
				if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
				{
					error = "PrintWindow 실패";
					bitmap.Dispose();
					return null;
				}
			}
			finally
			{
				graphics.ReleaseHdc(hdc);
			}
		}

		return bitmap;
	}

	private static IntPtr FindTopLevelWindow(string processName)
	{
		Process[] processes = Process.GetProcessesByName(processName);
		try
		{
			foreach (Process process in processes)
			{
				if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
				{
					return process.MainWindowHandle;
				}
			}

			IntPtr found = IntPtr.Zero;
			foreach (Process process in processes)
			{
				uint pid = (uint)process.Id;
				EnumWindows((hwnd, _) =>
				{
					if (!IsWindowVisible(hwnd))
					{
						return true;
					}

					GetWindowThreadProcessId(hwnd, out uint windowPid);
					if (windowPid != pid)
					{
						return true;
					}

					if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero)
					{
						return true;
					}

					found = hwnd;
					return false;
				}, IntPtr.Zero);
				if (found != IntPtr.Zero)
				{
					return found;
				}
			}
		}
		finally
		{
			foreach (Process process in processes)
			{
				process.Dispose();
			}
		}

		return IntPtr.Zero;
	}

	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	private const int GW_OWNER = 4;

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
}
