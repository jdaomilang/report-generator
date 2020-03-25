using System.Threading.Tasks;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class FixedPicture
	{
		private string _resourceId;
		private string _filename;
		private Image _image;
		private Rectangle _rect;
		private PictureAlignment _alignment;
		private PictureScaleMode _scaleMode;
		private int _quality;
		
		public string Filename { get { return _filename; } }
		public string ResourceId { get { return _resourceId; } }
		public Image Image { get { return _image; }}
		public Rectangle Bounds { get { return _rect; } }

		public FixedPicture(
			string resourceId, string filename,
			int left, int bottom, int right, int top,
			PictureAlignment alignment, PictureScaleMode scalemode, int quality)
		{
			_filename = filename;
			_resourceId = resourceId;
			_rect = new Rectangle(left, bottom, right, top);
			_alignment = alignment;
			_scaleMode = scalemode;
			_quality = quality;
		}

		/// <summary>
		/// Load the image data and resize to fit in the defined bounds.
		/// </summary>
		public void LoadContent(Generator generator)
		{
			//	Load the image data from the server
			Task.Run(async () =>
			{
				//	If we have a resource id then load the image from the design's
				//	resource cache. Otherwise, if we have an explicit filename,
				//	then load the image directly without caching.
				if(_resourceId != null)
					_image = await Image.LoadCachedPicture(generator, _resourceId);
				else
					_image = await Image.LoadPictureDirect(generator, _filename);
			}).Wait();
			Size imageSize = _image.Size;

			//	Scale to fit in our bounds
			switch(_scaleMode)
			{
				case PictureScaleMode.NaturalSize:
				{
					break;
				}
				case PictureScaleMode.ScaleDown:
				{
					//	How much do we have to scale the image down to fit
					//	in our bounds without changing the aspect ratio?
					int scaledWidth  = imageSize.Width;
					int scaledHeight = imageSize.Height;
					if(scaledWidth > _rect.Width)
					{
						double scale = (double)_rect.Width / (double)scaledWidth;
						scaledWidth  = (int)(scaledWidth  * scale);
						scaledHeight = (int)(scaledHeight * scale);
					}
					if(scaledHeight > _rect.Height)
					{
						double scale = (double)_rect.Height / (double)scaledHeight;
						scaledWidth  = (int)(scaledWidth  * scale);
						scaledHeight = (int)(scaledHeight * scale);
					}
					Size scaledSize = new Size(scaledWidth, scaledHeight);
					_image = _image.Resize(scaledSize, _quality);
					break;
				}
			}

			//	Align within our bounds
			Rectangle renderBounds = new Rectangle();
			switch(_alignment)
			{
				case PictureAlignment.TopLeft:
					renderBounds.Left = _rect.Left;
					renderBounds.Right = renderBounds.Left + _image.Size.Width;
					renderBounds.Top = _rect.Top;
					renderBounds.Bottom = renderBounds.Top - _image.Size.Height;
					break;

				case PictureAlignment.TopRight:
					renderBounds.Right = _rect.Right;
					renderBounds.Left = renderBounds.Right - _image.Size.Width;
					renderBounds.Top = _rect.Top;
					renderBounds.Bottom = renderBounds.Top - _image.Size.Height;
					break;

				case PictureAlignment.TopCenter:
					int xCentre = _rect.Left + (_rect.Width / 2);
					renderBounds.Left = xCentre - (_image.Size.Width / 2);
					renderBounds.Right = renderBounds.Left + _image.Size.Width;

					renderBounds.Top = _rect.Top;
					renderBounds.Bottom = renderBounds.Top - _image.Size.Height;
					break;

				case PictureAlignment.CenterLeft:
					renderBounds.Left = _rect.Left;
					renderBounds.Right = renderBounds.Left + _image.Size.Width;

					int yCentre = _rect.Bottom + (_rect.Height / 2);
					renderBounds.Bottom = yCentre - (_image.Size.Height / 2);
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;

				case PictureAlignment.Center:
					xCentre = _rect.Left + (_rect.Width / 2);
					renderBounds.Left = xCentre - (_image.Size.Width / 2);
					renderBounds.Right = renderBounds.Left + _image.Size.Width;

					yCentre = _rect.Bottom + (_rect.Height / 2);
					renderBounds.Bottom = yCentre - (_image.Size.Height / 2);
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;

				case PictureAlignment.CenterRight:
					renderBounds.Right = _rect.Right;
					renderBounds.Left = renderBounds.Right - _image.Size.Width;

					yCentre = _rect.Bottom + (_rect.Height / 2);
					renderBounds.Bottom = yCentre - (_image.Size.Height / 2);
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;

				case PictureAlignment.BottomLeft:
					renderBounds.Left = _rect.Left;
					renderBounds.Right = renderBounds.Left + _image.Size.Width;
					renderBounds.Bottom = _rect.Bottom;
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;

				case PictureAlignment.BottomCenter:
					xCentre = _rect.Left + (_rect.Width / 2);
					renderBounds.Left = xCentre - (_image.Size.Width / 2);
					renderBounds.Right = renderBounds.Left + _image.Size.Width;

					renderBounds.Bottom = _rect.Bottom;
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;

				case PictureAlignment.BottomRight:
					renderBounds.Right = _rect.Right;
					renderBounds.Left = renderBounds.Right - _image.Size.Width;

					renderBounds.Bottom = _rect.Bottom;
					renderBounds.Top = renderBounds.Bottom + _image.Size.Height;
					break;
			}
		}
	}
}
