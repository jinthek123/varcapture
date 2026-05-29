using System;
using System.Drawing;
using System.Windows.Forms;

namespace VArchiveHelper;

internal sealed class CapturePreviewForm : Form
{
	private static readonly (string Label, int Width, int Height)[] ResolutionPresets =
	{
		("FHD", 1920, 1080),
		("QHD", 2560, 1440),
		("UHD", 3840, 2160),
		("FHD 16:10", 1920, 1200),
		("QHD 16:10", 2560, 1600),
	};

	private readonly HelperConfig _config;

	private readonly PictureBox _previewBox;

	private readonly Label _statusLabel;

	private readonly Label _detailLabel;

	private readonly Label _insertLabel;

	private readonly CheckBox _liveCheck;

	private readonly System.Windows.Forms.Timer _timer;

	private NumericUpDown _monitorIndexInput;

	private NumericUpDown _targetWidthInput;

	private NumericUpDown _targetHeightInput;

	private TextBox _vArchivePathInput;

	private CheckBox _useDxgiCheck;

	private CheckBox _requireDxgiCheck;

	private CheckBox _usePhysicalPixelsCheck;

	private Label _settingsStatusLabel;

	private Bitmap _displayImage;

	private bool _captureInProgress;

	public CapturePreviewForm(HelperConfig config)
	{
		_config = config;
		Text = "VArchiveHelper — 캡처 미리보기";
		StartPosition = FormStartPosition.CenterScreen;
		Size = new Size(1180, 720);
		MinimumSize = new Size(900, 520);

		_statusLabel = new Label
		{
			Dock = DockStyle.Top,
			Height = 28,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(8, 6, 8, 0),
			Text = "준비 중…"
		};

		_insertLabel = new Label
		{
			Dock = DockStyle.Top,
			Height = 24,
			ForeColor = Color.DarkGreen,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(8, 0, 8, 0),
			Text = "Insert: 대기"
		};

		var settingsPanel = BuildSettingsPanel();
		settingsPanel.Dock = DockStyle.Right;

		_detailLabel = new Label
		{
			Dock = DockStyle.Bottom,
			Height = 100,
			BorderStyle = BorderStyle.FixedSingle,
			Padding = new Padding(8),
			Text = ""
		};

		var buttonPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 40,
			Padding = new Padding(8, 4, 8, 4),
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};

		_liveCheck = new CheckBox
		{
			Text = "실시간 미리보기",
			Checked = false,
			AutoSize = true,
			Margin = new Padding(0, 8, 16, 0)
		};
		_liveCheck.CheckedChanged += (_, _) => UpdateTimer();

		var refreshButton = new Button
		{
			Text = "지금 새로고침",
			AutoSize = true,
			Margin = new Padding(0, 4, 8, 0)
		};
		refreshButton.Click += (_, _) => RequestPreview();

		var insertButton = new Button
		{
			Text = "Insert 동작 실행",
			AutoSize = true,
			Margin = new Padding(0, 4, 8, 0)
		};
		insertButton.Click += (_, _) => RunInsertPipeline();

		buttonPanel.Controls.Add(_liveCheck);
		buttonPanel.Controls.Add(refreshButton);
		buttonPanel.Controls.Add(insertButton);

		_previewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(32, 32, 32)
		};

		Controls.Add(_previewBox);
		Controls.Add(settingsPanel);
		Controls.Add(buttonPanel);
		Controls.Add(_detailLabel);
		Controls.Add(_insertLabel);
		Controls.Add(_statusLabel);

		_timer = new System.Windows.Forms.Timer();
		_timer.Tick += (_, _) => RequestPreview();
		UpdateTimer();

		FormClosing += (_, e) =>
		{
			if (e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				Hide();
			}
		};

		Load += (_, _) =>
		{
			LoadSettingsToUi();
			RequestPreview();
		};
	}

	private Panel BuildSettingsPanel()
	{
		var panel = new Panel
		{
			Width = 300,
			Padding = new Padding(8),
			BorderStyle = BorderStyle.FixedSingle
		};

		var layout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink
		};
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

		int row = 0;
		AddSectionHeader(layout, ref row, "v-archive 출력 (가로×세로)");

		_targetWidthInput = CreateDimensionInput();
		_targetHeightInput = CreateDimensionInput();
		AddLabeledControl(layout, ref row, "가로 (px)", _targetWidthInput);
		AddLabeledControl(layout, ref row, "세로 (px)", _targetHeightInput);

		var presetFlow = new FlowLayoutPanel
		{
			AutoSize = true,
			WrapContents = true,
			FlowDirection = FlowDirection.LeftToRight,
			Margin = new Padding(0, 4, 0, 8)
		};
		foreach ((string label, int width, int height) in ResolutionPresets)
		{
			var presetButton = new Button
			{
				Text = label,
				AutoSize = true,
				Margin = new Padding(0, 0, 4, 4),
				Tag = (width, height)
			};
			presetButton.Click += (_, _) =>
			{
				var (width, height) = ((int, int))presetButton.Tag;
				ApplyPreset((width, height));
			};
			presetFlow.Controls.Add(presetButton);
		}
		layout.Controls.Add(new Label { AutoSize = true, Text = "프리셋" }, 0, row);
		layout.Controls.Add(presetFlow, 1, row);
		layout.SetColumnSpan(presetFlow, 1);
		row++;

		var monitorPresetButton = new Button
		{
			Text = "선택 모니터 해상도",
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 8)
		};
		monitorPresetButton.Click += (_, _) => ApplyCurrentMonitorResolution();
		layout.Controls.Add(new Label(), 0, row);
		layout.Controls.Add(monitorPresetButton, 1, row);
		row++;

		AddSectionHeader(layout, ref row, "캡처 대상");

		_monitorIndexInput = new NumericUpDown
		{
			Minimum = 0,
			Maximum = 16,
			Width = 80
		};
		AddLabeledControl(layout, ref row, "모니터 인덱스", _monitorIndexInput);

		AddSectionHeader(layout, ref row, "v-archive");

		_vArchivePathInput = new TextBox { Dock = DockStyle.Fill };
		var pathRow = new TableLayoutPanel
		{
			ColumnCount = 2,
			Dock = DockStyle.Top,
			AutoSize = true
		};
		pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		pathRow.Controls.Add(_vArchivePathInput, 0, 0);
		var browseButton = new Button { Text = "찾기", AutoSize = true };
		browseButton.Click += (_, _) => BrowseVArchivePath();
		pathRow.Controls.Add(browseButton, 1, 0);
		AddLabeledControl(layout, ref row, "exe 경로", pathRow);

		_useDxgiCheck = new CheckBox { Text = "DXGI 캡처", AutoSize = true };
		_requireDxgiCheck = new CheckBox { Text = "DXGI 필수(권장)", AutoSize = true };
		_usePhysicalPixelsCheck = new CheckBox { Text = "물리 픽셀", AutoSize = true };
		layout.Controls.Add(new Label(), 0, row);
		var flagsPanel = new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false
		};
		flagsPanel.Controls.Add(_useDxgiCheck);
		flagsPanel.Controls.Add(_requireDxgiCheck);
		flagsPanel.Controls.Add(_usePhysicalPixelsCheck);
		layout.Controls.Add(flagsPanel, 1, row);
		row++;

		var applyButton = new Button
		{
			Text = "적용 및 저장",
			AutoSize = true,
			Margin = new Padding(0, 8, 0, 0)
		};
		applyButton.Click += (_, _) => ApplyAndSaveSettings();

		_settingsStatusLabel = new Label
		{
			AutoSize = true,
			ForeColor = Color.Gray,
			Margin = new Padding(0, 6, 0, 0)
		};

		layout.Controls.Add(new Label(), 0, row);
		var applyPanel = new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false
		};
		applyPanel.Controls.Add(applyButton);
		applyPanel.Controls.Add(_settingsStatusLabel);
		layout.Controls.Add(applyPanel, 1, row);

		panel.Controls.Add(layout);
		return panel;
	}

	private static NumericUpDown CreateDimensionInput()
	{
		return new NumericUpDown
		{
			Minimum = 320,
			Maximum = 7680,
			Increment = 1,
			Width = 100
		};
	}

	private static void AddSectionHeader(TableLayoutPanel layout, ref int row, string text)
	{
		var header = new Label
		{
			Text = text,
			Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
			AutoSize = true,
			Margin = new Padding(0, row == 0 ? 0 : 10, 0, 4)
		};
		layout.Controls.Add(header, 0, row);
		layout.SetColumnSpan(header, 2);
		row++;
	}

	private static void AddLabeledControl(TableLayoutPanel layout, ref int row, string label, Control control)
	{
		layout.Controls.Add(new Label
		{
			Text = label,
			AutoSize = true,
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 6, 0, 2)
		}, 0, row);
		control.Margin = new Padding(0, 2, 0, 6);
		control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		layout.Controls.Add(control, 1, row);
		row++;
	}

	private void LoadSettingsToUi()
	{
		_monitorIndexInput.Value = Math.Max(_monitorIndexInput.Minimum, Math.Min(_monitorIndexInput.Maximum, _config.MonitorIndex));
		_targetWidthInput.Value = Math.Max(_targetWidthInput.Minimum, Math.Min(_targetWidthInput.Maximum, _config.TargetWidth));
		_targetHeightInput.Value = Math.Max(_targetHeightInput.Minimum, Math.Min(_targetHeightInput.Maximum, _config.TargetHeight));
		_vArchivePathInput.Text = _config.VArchiveExePath ?? "";
		_useDxgiCheck.Checked = _config.UseDxgiCapture;
		_requireDxgiCheck.Checked = _config.RequireDxgiCapture;
		_usePhysicalPixelsCheck.Checked = _config.UsePhysicalPixels;
		_settingsStatusLabel.Text = "";
	}

	private void ApplyPreset((int Width, int Height) size)
	{
		_targetWidthInput.Value = size.Width;
		_targetHeightInput.Value = size.Height;
		_settingsStatusLabel.Text = $"프리셋 {size.Width}×{size.Height} — 적용 및 저장을 누르세요.";
	}

	private void ApplyCurrentMonitorResolution()
	{
		int index = (int)_monitorIndexInput.Value;
		Screen[] screens = Screen.AllScreens;
		if (index < 0 || index >= screens.Length)
		{
			_settingsStatusLabel.Text = $"모니터 {index} 없음 (0~{screens.Length - 1})";
			return;
		}

		MonitorCaptureInfo info = MonitorGeometry.GetCaptureInfo(screens[index], _usePhysicalPixelsCheck.Checked);
		_targetWidthInput.Value = Math.Max(_targetWidthInput.Minimum, Math.Min(_targetWidthInput.Maximum, info.NativeResolution.Width));
		_targetHeightInput.Value = Math.Max(_targetHeightInput.Minimum, Math.Min(_targetHeightInput.Maximum, info.NativeResolution.Height));
		_settingsStatusLabel.Text = $"모니터 {index} → {info.NativeResolution.Width}×{info.NativeResolution.Height}";
	}

	private void BrowseVArchivePath()
	{
		using var dialog = new OpenFileDialog
		{
			Filter = "v-archive|v-archive.exe|실행 파일|*.exe|모든 파일|*.*",
			Title = "v-archive.exe 선택"
		};
		if (!string.IsNullOrWhiteSpace(_vArchivePathInput.Text))
		{
			try
			{
				dialog.InitialDirectory = System.IO.Path.GetDirectoryName(_vArchivePathInput.Text);
				dialog.FileName = System.IO.Path.GetFileName(_vArchivePathInput.Text);
			}
			catch
			{
				// ignore invalid path
			}
		}

		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			_vArchivePathInput.Text = dialog.FileName;
		}
	}

	private void ApplyAndSaveSettings()
	{
		_config.MonitorIndex = (int)_monitorIndexInput.Value;
		_config.TargetWidth = (int)_targetWidthInput.Value;
		_config.TargetHeight = (int)_targetHeightInput.Value;
		_config.VArchiveExePath = _vArchivePathInput.Text.Trim();
		_config.UseDxgiCapture = _useDxgiCheck.Checked;
		_config.RequireDxgiCapture = _requireDxgiCheck.Checked;
		_config.UsePhysicalPixels = _usePhysicalPixelsCheck.Checked;

		try
		{
			_config.Save();
			_settingsStatusLabel.ForeColor = Color.DarkGreen;
			_settingsStatusLabel.Text = $"저장됨 — {_config.TargetWidth}×{_config.TargetHeight}";
			RequestPreview();
		}
		catch (Exception ex)
		{
			_settingsStatusLabel.ForeColor = Color.DarkRed;
			_settingsStatusLabel.Text = "저장 실패: " + ex.Message;
		}
	}

	public void SetInsertStatus(string message)
	{
		if (IsDisposed)
		{
			return;
		}

		void Apply()
		{
			_insertLabel.Text = "Insert: " + message;
		}

		if (InvokeRequired)
		{
			BeginInvoke((Action)Apply);
		}
		else
		{
			Apply();
		}
	}

	public void RunInsertPipeline()
	{
		string result = CapturePipeline.Run(_config);
		SetInsertStatus(result);
		RequestPreview();
	}

	public void RequestPreviewFromHotkey()
	{
		RequestPreview();
	}

	private void UpdateTimer()
	{
		_timer.Stop();
		if (_liveCheck.Checked)
		{
			int ms = _config.PreviewIntervalMs > 0 ? _config.PreviewIntervalMs : 500;
			_timer.Interval = ms;
			_timer.Start();
		}
	}

	private void RequestPreview()
	{
		if (_captureInProgress || IsDisposed)
		{
			return;
		}

		if (InvokeRequired)
		{
			BeginInvoke((Action)RequestPreview);
			return;
		}

		_captureInProgress = true;
		try
		{
			using CaptureResult result = ScreenCapture.CaptureForPreview(_config);
			ApplyPreview(result);
		}
		finally
		{
			_captureInProgress = false;
		}
	}

	private void ApplyPreview(CaptureResult result)
	{
		if (result.Success)
		{
			_statusLabel.Text =
				$"[{result.Method}] v-archive 전송 {result.OutputSize.Width}x{result.OutputSize.Height}  |  " +
				$"원본(모니터 전체) {result.SourceSize.Width}x{result.SourceSize.Height}  |  " +
				DateTime.Now.ToString("HH:mm:ss");
			SetPreviewImage(result.Image);
		}
		else
		{
			_statusLabel.Text = "캡처 실패 — " + result.Error + "  |  " + DateTime.Now.ToString("HH:mm:ss");
		}

		_detailLabel.Text = result.Diagnostics ?? "";
	}

	private void SetPreviewImage(Bitmap source)
	{
		var copy = new Bitmap(source);
		_displayImage?.Dispose();
		_displayImage = copy;
		_previewBox.Image = _displayImage;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_timer?.Dispose();
			_displayImage?.Dispose();
		}
		base.Dispose(disposing);
	}
}
