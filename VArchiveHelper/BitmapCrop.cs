using System.Drawing;
using System.Drawing.Imaging;

namespace VArchiveHelper;

internal static class BitmapCrop
{
	public static Bitmap Crop(Bitmap source, Rectangle region)
	{
		if (region.X == 0 && region.Y == 0 && region.Width == source.Width && region.Height == source.Height)
		{
			return new Bitmap(source);
		}

		var cropped = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
		using (Graphics graphics = Graphics.FromImage(cropped))
		{
			graphics.DrawImage(
				source,
				new Rectangle(0, 0, region.Width, region.Height),
				region,
				GraphicsUnit.Pixel);
		}

		return cropped;
	}
}
