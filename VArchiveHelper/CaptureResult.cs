using System;
using System.Drawing;

namespace VArchiveHelper;

internal sealed class CaptureResult : IDisposable
{
	public CaptureResult(
		Bitmap image,
		string method,
		string error,
		string diagnostics,
		Rectangle monitorBounds,
		Size sourceSize,
		Size outputSize)
	{
		Image = image;
		Method = method;
		Error = error;
		Diagnostics = diagnostics;
		MonitorBounds = monitorBounds;
		SourceSize = sourceSize;
		OutputSize = outputSize;
	}

	public Bitmap Image { get; }

	public string Method { get; }

	public string Error { get; }

	public string Diagnostics { get; }

	public Rectangle MonitorBounds { get; }

	public Size SourceSize { get; }

	public Size OutputSize { get; }

	public bool Success => Image != null;

	public void Dispose()
	{
		Image?.Dispose();
	}
}
