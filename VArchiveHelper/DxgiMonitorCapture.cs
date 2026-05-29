using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace VArchiveHelper;

internal static class DxgiMonitorCapture
{
	private const int AcquireTimeoutMs = 250;

	private const int MaxAcquireAttempts = 8;

	private static string _diagnosticLogPath;

	private static string _lastLoggedExceptionKey;

	/// <param name="writeDiagnosticLog">true면 Insert 등 실제 캡처 실패 시에만 temp에 1회 기록 (미리보기는 false).</param>
	public static Bitmap TryCapture(MonitorCaptureInfo monitor, out string error, bool writeDiagnosticLog = false)
	{
		error = null;
		try
		{
			return Capture(monitor);
		}
		catch (Exception ex)
		{
			error = FormatError(ex, writeDiagnosticLog ? WriteDxgiErrorLogOnce(ex) : null);
			return null;
		}
	}

	private static string FormatError(Exception ex, string logPath)
	{
		string msg = $"DXGI FATAL: {ex.GetType().Name}: {ex.Message}";
		return string.IsNullOrEmpty(logPath) ? msg : msg + " (log: " + logPath + ")";
	}

	/// <summary>동일 예외는 세션당 한 파일에 한 번만 기록.</summary>
	private static string WriteDxgiErrorLogOnce(Exception ex)
	{
		try
		{
			string key = ex.GetType().FullName + "|" + ex.Message;
			if (key == _lastLoggedExceptionKey && !string.IsNullOrEmpty(_diagnosticLogPath))
			{
				return _diagnosticLogPath;
			}

			string path = System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				"VArchiveHelper-dxgi.log");
			string block = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
			System.IO.File.AppendAllText(path, block);
			_lastLoggedExceptionKey = key;
			_diagnosticLogPath = path;
			return path;
		}
		catch
		{
			return null;
		}
	}

	private static Bitmap Capture(MonitorCaptureInfo monitor)
	{
		using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
		IDXGIAdapter1 adapter = null;
		IDXGIOutput1 output1 = null;

		try
		{
			for (int adapterIndex = 0; ; adapterIndex++)
			{
				var enumAdapterResult = factory.EnumAdapters1(adapterIndex, out var candidateAdapter);
				if (!enumAdapterResult.Success)
				{
					break;
				}

				if (candidateAdapter == null)
				{
					continue;
				}

				bool matched = false;
				try
				{
					for (int outputIndex = 0; ; outputIndex++)
					{
						var enumOutputResult = candidateAdapter.EnumOutputs(outputIndex, out var candidateOutput);
						if (!enumOutputResult.Success)
						{
							break;
						}

						if (candidateOutput == null)
						{
							continue;
						}

						var desc = candidateOutput.Description;
						var desktop = desc.DesktopCoordinates;
						var outputBounds = new Rectangle(
							desktop.Left,
							desktop.Top,
							desktop.Right - desktop.Left,
							desktop.Bottom - desktop.Top);
						if (!MonitorGeometry.OutputMatchesMonitor(outputBounds, monitor))
						{
							candidateOutput.Dispose();
							continue;
						}

						// EnumAdapters1은 이미 IDXGIAdapter1 — 동일 타입으로 QueryInterface 시
						// Vortice에서 네이티브 포인터가 null인 래퍼가 나와 D3D11CreateDevice가 NRE 남.
						adapter = candidateAdapter;
						try
						{
							output1 = candidateOutput as IDXGIOutput1
								?? candidateOutput.QueryInterface<IDXGIOutput1>();
						}
						catch (Exception qex)
						{
							candidateOutput.Dispose();
							throw new InvalidOperationException("STAGE=QueryInterface(Output1)", qex);
						}
						finally
						{
							// output1이 별도 COM 참조를 잡았으면 IDXGIOutput만 해제
							if (output1 != null && !ReferenceEquals(output1, candidateOutput))
							{
								candidateOutput.Dispose();
							}
						}

						if (output1 == null)
						{
							candidateOutput.Dispose();
							continue;
						}

						matched = true;
						break;
					}
				}
				finally
				{
					if (!matched)
					{
						candidateAdapter.Dispose();
					}
				}

				if (matched)
				{
					break;
				}
			}

			if (output1 == null)
			{
				throw new InvalidOperationException("DXGI: 해당 모니터 출력을 찾지 못했습니다.");
			}

			using var device = CreateD3D11Device(output1, adapter);
			using var context = device.ImmediateContext;
			{
				if (context == null)
				{
					throw new InvalidOperationException("DXGI: D3D11 컨텍스트 생성 실패");
				}

				Exception lastError = null;
				IDXGIOutputDuplication duplication = null;

				try
				{
					try
					{
						duplication = output1.DuplicateOutput(device);
					}
					catch (Exception dupEx)
					{
						throw new InvalidOperationException("STAGE=DuplicateOutput(first)", dupEx);
					}
					if (duplication == null)
					{
						throw new InvalidOperationException("DXGI: DuplicateOutput 실패");
					}

					for (int attempt = 0; attempt < MaxAcquireAttempts; attempt++)
					{
						var frameAcquired = false;
						var hr = duplication.AcquireNextFrame(
							AcquireTimeoutMs,
							out _,
							out IDXGIResource desktopResource);

						if (hr == Vortice.DXGI.ResultCode.WaitTimeout)
						{
							continue;
						}

						if (hr == Vortice.DXGI.ResultCode.AccessLost)
						{
							// 전체화면 전환/해상도 변경 시 흔함 → duplication 재생성 후 재시도
							duplication.Dispose();
							try
							{
								duplication = output1.DuplicateOutput(device);
							}
							catch (Exception dupEx)
							{
								throw new InvalidOperationException("STAGE=DuplicateOutput(recreate-after-AccessLost)", dupEx);
							}
							if (duplication == null)
							{
								throw new InvalidOperationException("DXGI: AccessLost 후 DuplicateOutput 실패");
							}
							continue;
						}

						if (hr.Failure)
						{
							lastError = new InvalidOperationException($"DXGI AcquireNextFrame: {hr}");
							continue;
						}

						if (desktopResource == null)
						{
							lastError = new InvalidOperationException("DXGI: desktopResource 가 null");
							continue;
						}

						try
						{
							frameAcquired = true;
							ID3D11Texture2D desktopTexture;
							try
							{
								desktopTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
							}
							catch (Exception texEx)
							{
								throw new InvalidOperationException("STAGE=QueryInterface(Texture2D)", texEx);
							}
							using (desktopTexture)
							{
							if (desktopTexture == null)
							{
								lastError = new InvalidOperationException("DXGI: QueryInterface(ID3D11Texture2D) 실패");
								continue;
							}

							var textureDesc = desktopTexture.Description;
							var stagingDesc = textureDesc;
							stagingDesc.BindFlags = BindFlags.None;
							stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
							stagingDesc.Usage = ResourceUsage.Staging;
							stagingDesc.MiscFlags = 0;

							ID3D11Texture2D staging;
							try
							{
								staging = device.CreateTexture2D(stagingDesc);
							}
							catch (Exception stEx)
							{
								throw new InvalidOperationException("STAGE=CreateTexture2D(staging)", stEx);
							}
							using (staging)
							{
							if (staging == null)
							{
								lastError = new InvalidOperationException("DXGI: CreateTexture2D(staging) 실패");
								continue;
							}

							try
							{
								context.CopyResource(staging, desktopTexture);
							}
							catch (Exception copyEx)
							{
								throw new InvalidOperationException("STAGE=CopyResource", copyEx);
							}

							var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
							try
							{
								return CopyMappedToBitmap(
									mapped.DataPointer,
									(int)mapped.RowPitch,
									textureDesc.Width,
									textureDesc.Height);
							}
							finally
							{
								context.Unmap(staging, 0);
							}
							}
							}
						}
						catch (Exception frameEx)
						{
							lastError = new InvalidOperationException("DXGI: 프레임 처리 중 예외 - " + frameEx.GetType().Name + ": " + frameEx.Message, frameEx);
							continue;
						}
						finally
						{
							if (frameAcquired)
							{
								try
								{
									duplication.ReleaseFrame();
								}
								catch (Exception relEx)
								{
									lastError = new InvalidOperationException("STAGE=ReleaseFrame", relEx);
								}
							}
							desktopResource?.Dispose();
						}
					}

					throw lastError ?? new InvalidOperationException("DXGI: 프레임 획득 시간 초과");
				}
				finally
				{
					duplication?.Dispose();
				}
			}
		}
		finally
		{
			output1?.Dispose();
			adapter?.Dispose();
		}
	}

	/// <summary>
	/// Desktop Duplication 샘플과 동일: 기본 하드웨어 어댑터로 장치 생성.
	/// EnumAdapters1 어댑터 래퍼는 Vortice에서 NativePointer가 비어 NRE를 유발할 수 있음.
	/// </summary>
	private static ID3D11Device CreateD3D11Device(IDXGIOutput1 output1, IDXGIAdapter1 enumeratedAdapter)
	{
		var flags = DeviceCreationFlags.BgraSupport;
		FeatureLevel[] levels = { FeatureLevel.Level_11_0 };

		if (TryCreateDevice(null, DriverType.Hardware, flags, levels, out ID3D11Device device))
		{
			return device;
		}

		if (enumeratedAdapter != null
			&& enumeratedAdapter.NativePointer != IntPtr.Zero
			&& TryCreateDevice(enumeratedAdapter, DriverType.Unknown, flags, levels, out device))
		{
			return device;
		}

		using (IDXGIAdapter1 parentAdapter = output1.GetParent<IDXGIAdapter1>())
		{
			if (parentAdapter != null
				&& parentAdapter.NativePointer != IntPtr.Zero
				&& TryCreateDevice(parentAdapter, DriverType.Unknown, flags, levels, out device))
			{
				return device;
			}
		}

		throw new InvalidOperationException("DXGI: D3D11 장치를 만들 수 없습니다.");
	}

	private static bool TryCreateDevice(
		IDXGIAdapter adapter,
		DriverType driverType,
		DeviceCreationFlags flags,
		FeatureLevel[] featureLevels,
		out ID3D11Device device)
	{
		device = null;
		var result = D3D11.D3D11CreateDevice(
			adapter,
			driverType,
			flags,
			featureLevels,
			out device);
		return result.Success && device != null;
	}

	private static Bitmap CopyMappedToBitmap(IntPtr source, int rowPitch, int width, int height)
	{
		var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		var locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
		try
		{
			int destStride = locked.Stride;
			int rowBytes = width * 4;
			for (int y = 0; y < height; y++)
			{
				IntPtr srcRow = IntPtr.Add(source, y * rowPitch);
				IntPtr dstRow = IntPtr.Add(locked.Scan0, y * destStride);
				CopyMemory(dstRow, srcRow, (uint)rowBytes);
			}
		}
		finally
		{
			bitmap.UnlockBits(locked);
		}

		return bitmap;
	}

	[DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
	private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
}
