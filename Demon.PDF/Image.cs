using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using Demon.Report.Types;

namespace Demon.PDF
{
	internal class Image : IIndirectObject
	{
		private ColorSpace _colorSpace;
		private Filter _filter;
		private Demon.Report.Types.Size _size;
		private byte[] _bits;
		private int _bitsPerPixel;
		private Rectangle _position;
		private Image _mask;
		private string _name;
		public string Name { get {return _name; }}
		public Image Mask { get { return _mask; }}

		public Image(
			string name, byte[] bits, int bitsPerPixel,
			Demon.Report.Types.Size size, Rectangle position,
			ColorSpace colorSpace, Filter filter, Image mask)
		{
			_name = name;
			_bits = bits;
			_bitsPerPixel = bitsPerPixel;

			_colorSpace = colorSpace;
			_filter = filter;
			_mask = mask;

			_size = size;
			_position = position;
		}

		public byte[] GetStream()
		{
			return _bits;
		}

		public void Write(Stream file, ObjectReference imageRef, ObjectReference maskRef)
		{
			imageRef.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(imageRef.Id);
			sb.Append(" obj\r\n<<");
			sb.Append("/Type /XObject\r\n");
			sb.Append("/Subtype /Image\r\n");

			sb.Append("/Width ");
			sb.Append(_size.Width);
			sb.Append("\r\n");

			sb.Append("/Height ");
			sb.Append(_size.Height);
			sb.Append("\r\n");

			sb.Append("/Length ");
			sb.Append(_bits.Length);
			sb.Append("\r\n");

			sb.Append("/BitsPerComponent ");
			sb.Append(_bitsPerPixel);
			sb.Append("\r\n");

			switch(_colorSpace)
			{
				case ColorSpace.DeviceRGB:
					sb.Append("/ColorSpace /DeviceRGB\r\n");
					break;
				case ColorSpace.DeviceGray:
					sb.Append("/ColorSpace /DeviceGray\r\n");
					break;
			}

			switch(_filter)
			{
				case Filter.DCT:
					sb.Append("/Filter /DCTDecode\r\n");
					break;
				case Filter.Flate:
					sb.Append("/Filter /FlateDecode\r\n");
				break;
			}
			if(maskRef != null)
			{
				sb.Append("/SMask ");
				sb.Append(maskRef.Reference);
				sb.Append("\r\n");
			}

			sb.Append(">>\r\n");
			
			sb.Append("stream\r\n");
			
			//	Write the string now so that we can write the binary data
			Document.WriteData(file,sb.ToString());
			sb.Clear();

			file.Write(_bits,0,_bits.Length);

			sb.Append("\r\nendstream\r\n");
			sb.Append("endobj\r\n");
			Document.WriteData(file,sb.ToString());
		}

		public static bool IsPng(byte[] buf)
		{
			UInt64 header = BitConverter.ToUInt64(buf, 0);
			return header == 0x0a1a0a0d474e5089; // little-endian
		}

		public static void SeparateAlphaChannel(byte[] png, out byte[] rgb, out byte[] alpha)
		{
			MemoryStream s = new MemoryStream(png);
			Bitmap bmp = new Bitmap(s);
			
			MemoryStream rgbStream   = new MemoryStream(bmp.Width * bmp.Height * 3);
			MemoryStream alphaStream = new MemoryStream(bmp.Width * bmp.Height);
			
			bool hasAlpha = false;
			switch(bmp.PixelFormat)
			{
				case PixelFormat.Format24bppRgb:
					//	8 bits per channel, no alpha
					SeparateAlphaChannel_LockBits(bmp, rgbStream, alphaStream);
					hasAlpha = false;
					break;

				case PixelFormat.Canonical:
				case PixelFormat.Format32bppArgb:
					//	8 bits per channel, with alpha
					SeparateAlphaChannel_LockBits(bmp, rgbStream, alphaStream);
					hasAlpha = true;
					break;

				case PixelFormat.Format32bppRgb:
					//	8 bits per channel, with an unused alpha byte
					SeparateAlphaChannel_LockBits(bmp, rgbStream, alphaStream);
					hasAlpha = false;
					break;

				default:
					//	Do it the slow way
					SeparateAlphaChannel_GetPixel(bmp, rgbStream, alphaStream);
					hasAlpha = true;
					break;
			}

			rgbStream  .Seek(0, SeekOrigin.Begin);
			alphaStream.Seek(0, SeekOrigin.Begin);
			rgb = Compression.Compress(rgbStream).GetBuffer();
			alpha = hasAlpha ? Compression.Compress(alphaStream).GetBuffer() : null;

			//	A quick performance test with a release build gave these results.
			//	Generally the LockBits method out-performed the GetPixel method
			//	by a factor of three or four.
			//
			//	Image size  Iterations  LockBits   GetPixel
			//	  50 x  66        1      0.001       0.002
			//	  50 x  66       10      0.006       0.020
			//	  50 x  66      100      0.067       0.215
			//	  50 x  66     1000      0.645       2.151
			//	 100 x 132        1      0.002       0.007
			//	 100 x 132       10      0.022       0.080
			//	 100 x 132      100      0.219       0.831
			//	 100 x 132     1000      2.193       8.370
			//	 150 x 198        1      0.005       0.017
			//	 150 x 198       10      0.051       0.214
			//	 150 x 198      100      0.487       1.826
			//	 150 x 198     1000      4.909      18.398
			//
			//	int[] iterations = { 1, 10, 100, 1000 };
			//	foreach(int iteration in iterations)
			//	{
			//		System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();
			//		timer.Start();
			//		for(int x = 0; x < iteration; ++x)
			//		{
			//			byte[] r;
			//			byte[] a;
			//			Image.SeparateAlphaChannel(bits, out r, out a);
			//		}
			//		timer.Stop();
			//		string msg = $"{size.Width} x {size.Height} run {iteration} times: {timer.Elapsed:mmss\\.fff}\r\n";
			//		File.AppendAllText(@"c:\users\ciaran\desktop\getpixel.txt", msg);
			//		System.Diagnostics.Debugger.Log(0, null, msg);
			//	}
			//
			//	In these tests I commented out compression calls so that I just measured
			//	the channel separation stuff.
		}

		/// <summary>
		/// Separate the RGB channels from the alpha channel, using the
		/// high-performance unmanaged method. This method is suitable
		/// only for simple 24- and 32-bit pixel formats.
		/// </summary>
		private unsafe static void SeparateAlphaChannel_LockBits(Bitmap bmp, MemoryStream rgbStream, MemoryStream alphaStream)
		{
			int bitsPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat);
			int bytesPerPixel = bitsPerPixel / 8;

			System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
			BitmapData raw = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);
			try
			{
				for(int y = 0; y < bmp.Height; ++y)
				{
					byte* line = (byte*)raw.Scan0 + (raw.Stride * y);
					for(int x = 0; x < bmp.Width; ++x)
					{
						byte* pixel = &line[x * bytesPerPixel];

						//	Bytes are always in reverse order: ARGB => BGRA
						byte b = *pixel++;
						byte g = *pixel++;
						byte r = *pixel++;
						byte a = (bytesPerPixel == 4) ? *pixel++ : (byte)0xff;

						rgbStream.WriteByte(r);
						rgbStream.WriteByte(g);
						rgbStream.WriteByte(b);
						alphaStream.WriteByte(a);
					}
				}
			}
			finally
			{
				bmp.UnlockBits(raw);
			}
		}

		/// <summary>
		/// Separate the RGB channels from the alpha channel, using the slow
		/// but reliable GetPixel method.
		/// </summary>
		private static void SeparateAlphaChannel_GetPixel(Bitmap bmp, MemoryStream rgbStream, MemoryStream alphaStream)
		{
			for(int y = 0; y < bmp.Height; ++y)
			{
				for(int x = 0; x < bmp.Width; ++x)
				{
					System.Drawing.Color color = bmp.GetPixel(x, y); // this call can be expensive
					rgbStream.WriteByte(color.R);
					rgbStream.WriteByte(color.G);
					rgbStream.WriteByte(color.B);
					alphaStream.WriteByte(color.A);
				}
			}
		}

		public string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Document.Space(indentLevel));
			sb.Append("Image size=");
			sb.Append(_size.Width);
			sb.Append("x");
			sb.Append(_size.Height);
			sb.Append(" pos=");
			sb.Append(_position.Specification);
			sb.Append("\r\n");
			return sb.ToString();
		}
	}

	internal enum ColorSpace
	{
		None       = 0,
		DeviceRGB  = 1,
		DeviceGray = 2
	}

	internal enum Filter
	{
		None  = 0,
		DCT   = 1,
		Flate = 2
	}

	internal class ImageFragment : ContentFragment
	{
		private string _imageName;
		private Rectangle _rect;

		public ImageFragment(string imageName, Rectangle rect)
		{
			_imageName = imageName;
			_rect = rect;
		}

		public override byte[] GetStream()
		{
			//	The rectangle's Y coordinate is the bottom of the image
			string s =
				$"q\r\n{_rect.Width} 0 0 {_rect.Height} {_rect.Left} {_rect.Bottom} cm\r\n/{_imageName} Do\r\nQ\r\n";
			return Encoding.UTF8.GetBytes(s);
		}

		public override string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Document.Space(indentLevel));
			sb.Append("Image ");
			sb.Append(_imageName);
			sb.Append(" ");
			sb.Append(_rect);
			sb.Append("\r\n");
			return sb.ToString();
		}
	}
}
