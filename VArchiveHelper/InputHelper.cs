using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VArchiveHelper;

internal static class InputHelper
{
	private const byte VK_MENU = 0x12;

	private const byte VK_INSERT = 0x2D;

	private const uint KEYEVENTF_KEYUP = 0x0002;

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

	public static void SendAltInsert()
	{
		keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
		Thread.Sleep(30);
		keybd_event(VK_INSERT, 0, 0, UIntPtr.Zero);
		keybd_event(VK_INSERT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
		Thread.Sleep(30);
		keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
	}
}
