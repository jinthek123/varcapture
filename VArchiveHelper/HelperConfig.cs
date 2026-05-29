using System;
using System.IO;
using System.Text.Json;

namespace VArchiveHelper;

internal sealed class HelperConfig
{
	public int MonitorIndex { get; set; } = 1;

	public int TargetWidth { get; set; } = 2560;

	public int TargetHeight { get; set; } = 1440;

	public string VArchiveExePath { get; set; } = @"D:\v-archive_v0.64\v-archive.exe";

	public string VArchiveProcessName { get; set; } = "v-archive";

	public int ClipboardSettleMs { get; set; } = 80;

	public int AfterClipboardBeforeRecognizeMs { get; set; } = 120;

	public int VArchiveStartupWaitMs { get; set; } = 2500;

	/// <summary>DXGI Desktop Duplication 사용 (전체화면 게임 권장).</summary>
	public bool UseDxgiCapture { get; set; } = true;

	/// <summary>true면 DXGI 실패 시 즉시 실패(=GDI로 넘어가지 않음).</summary>
	public bool RequireDxgiCapture { get; set; } = true;

	/// <summary>125% 배율 등에서 논리(2048) 대신 물리(2560) 픽셀로 캡처.</summary>
	public bool UsePhysicalPixels { get; set; } = true;

	/// <summary>DJMAX 등 게임 프로세스 이름(확장자 없음). UseGameWindowCapture=true 일 때만.</summary>
	public string GameProcessName { get; set; } = "";

	/// <summary>창 크롭 캡처(잘림 유발 가능). 기본은 모니터 전체(DXGI/GDI)만.</summary>
	public bool UseGameWindowCapture { get; set; } = false;

	/// <summary>미리보기 실시간 갱신 간격(ms).</summary>
	public int PreviewIntervalMs { get; set; } = 500;

	public static string SettingsPath =>
		Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

	public static HelperConfig Load()
	{
		if (!File.Exists(SettingsPath))
		{
			return new HelperConfig();
		}
		try
		{
			string json = File.ReadAllText(SettingsPath);
			return JsonSerializer.Deserialize<HelperConfig>(json) ?? new HelperConfig();
		}
		catch
		{
			return new HelperConfig();
		}
	}

	public void Save()
	{
		var options = new JsonSerializerOptions { WriteIndented = true };
		File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, options));
	}
}
