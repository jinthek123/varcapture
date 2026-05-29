using System.Drawing;
using System.Drawing.Drawing2D;

namespace VArchiveHelper;

internal static class BitmapScale
{
	public static Bitmap ResizeHighQuality(Bitmap source, int width, int height)
	{
		if (source.Width == width && source.Height == height)
		{
			return new Bitmap(source);
		}

		var result = new Bitmap(width, height, source.PixelFormat);
		using (Graphics graphics = Graphics.FromImage(result))
		{
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.CompositingQuality = CompositingQuality.HighQuality;
			graphics.DrawImage(source, 0, 0, width, height);
		}

		return result;
	}
}
