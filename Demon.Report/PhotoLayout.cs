using System.Threading.Tasks;
using Demon.Core;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class PhotoLayout : Layout
	{
		private Photo _photo;
		private Image _photoData;
		private Rectangle _photoBounds;
		private Size _maxPhotoSize;
		private PhotoStyle _style;

		public string PhotoId { get { return _photo.Id; }}
		public Image PhotoData { get { return _photoData; }}
		public Size PhotoRenderedSize { get { return new Size(_photoBounds.Width, _photoBounds.Height); }}
		public Rectangle PhotoBounds { get { return _photoBounds; }}
		public override IStyle Style { get { return _style; }}
		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}

		public override LayoutType LayoutType { get {return LayoutType.Photo;} }

		public PhotoLayout(
			Photo photo, Size maxPhotoSize,
			PhotoStyle style,
			Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			_photo = photo;
			_maxPhotoSize = maxPhotoSize;
			_style = style;

			//	A photo layout doesn't have its own conditions because it's not
			//	defined in the design file. Any conditions governing the inclusion
			//	of a photo are evaluated in the photo table layout when it loads
			//	its content. So static conditions are implicitly satisfied here.
			_staticConditionsSatisfied = true;
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public PhotoLayout(PhotoLayout src)
			:base(src)
		{
			_photo = src._photo;
			_maxPhotoSize = src._maxPhotoSize;
			_style = src._style;
		}

		/// <summary>
		/// Load the photo data and resize to fit in the available cell space.
		/// </summary>
		public override void LoadContent()
		{
			if(_photo == null) return;
			//	No need to check content conditions because they were already
			//	checked in the photo table layout

			Size photoSize;
			switch(_generator.ReportMimeType)
			{
				case Demon.Core.MimeType.PDF:
				case Demon.Core.MimeType.Word:
				default:
					//	Load the photo image data from the server
					Task.Run(async () =>
					{
						_photoData = await Image.LoadPhoto(_generator, _photo.Id, PhotoType.Full, _trackingInfo.Name);
					}).Wait();
					photoSize = _photoData.Size;
					break;

				case Demon.Core.MimeType.HTML:
				case Demon.Core.MimeType.SVG:
					//	If we have the size stored in the database then just use it, otherwise
					//	load the photo from the server and get its size from there. This is
					//	a temporary measure to let us experiment to see whether there's a
					//	useful performance gain to be had by storing the size in the database.
					if(_photo.Width != null && _photo.Height != null)
					{
						photoSize = new Size(_photo.Width.Value, _photo.Height.Value);
					}
					else
					{
						Task.Run(async () =>
						{
							_photoData = await Image.LoadPhoto(_generator, _photo.Id, PhotoType.Full, _trackingInfo.Name);
						}).Wait();
						photoSize = _photoData.Size;
					}
					break;
			}

			//	How much do we have to scale the image down? Its size is subject to three
			//	constraints: cell width and padding (_photoBounds), style max width, style
			//	max height.
			int scaledWidth  = photoSize.Width;
			int scaledHeight = photoSize.Height;
			int maxPhotoWidth = _photoBounds.Width;
			if(scaledWidth > maxPhotoWidth)
			{
				double scale = (double)maxPhotoWidth / (double)scaledWidth;
				scaledWidth  = (int)(scaledWidth  * scale);
				scaledHeight = (int)(scaledHeight * scale);
			}
			if(scaledWidth > _maxPhotoSize.Width)
			{
				double scale = (double)_maxPhotoSize.Width / (double)scaledWidth;
				scaledWidth  = (int)(scaledWidth  * scale);
				scaledHeight = (int)(scaledHeight * scale);
			}
			if(scaledHeight > _maxPhotoSize.Height)
			{
				double scale = (double)_maxPhotoSize.Height / (double)scaledHeight;
				scaledWidth  = (int)(scaledWidth  * scale);
				scaledHeight = (int)(scaledHeight * scale);
			}

			//	Calculate the box for the photo. We've already worked out the maximum
			//	box, in _photoBounds, but we want the box that exactly fits the
			//	photo so that we don't stretch it.
			int boxWidth = scaledWidth;
			int boxHeight = scaledHeight;
			int boxLeft = _bounds.Left + (_style?.Padding?.Left ?? 0);
			int boxBottom = _bounds.Top - (_style?.Padding?.Top ?? 0) - boxHeight;
			int boxRight = boxLeft + boxWidth;
			int boxTop = boxBottom + boxHeight;
			_photoBounds = new Rectangle(boxLeft, boxBottom, boxRight, boxTop);

			if(_photoData != null)
			{
				//	Scale the image down to a sensible size. We use the default PDF
				//	user space unit of 1/72 inch. So to draw the photo at the layout's
				//	designed resolution, we scale the photo as follows:
				//		pixels = size x user units x resolution
				int storedWidth  = (int)((double)scaledWidth  / 72.0 * _style.Resolution);
				int storedHeight = (int)((double)scaledHeight / 72.0 * _style.Resolution);

				//	Now double the size in each direction so that the photo can
				//	be enlarged somewhat by the viewer. It will still be displayed
				//	at the layout's designed size.
				storedHeight *= 2;
				storedWidth  *= 2;

				Size scaledSize = new Size(storedWidth, storedHeight);
				_photoData = _photoData.Resize(scaledSize, _style.Quality);
			}
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);
				_bounds = new Rectangle(bounds);

				//	Apply cell padding
				bounds.Left  += _style?.Padding?.Left  ?? 0;
				bounds.Right -= _style?.Padding?.Right ?? 0;
				bounds.Top   -= _style?.Padding?.Top   ?? 0;
				bounds.Bottom = bounds.Top;

				//	The photo must fit in the space left after our padding
				_photoBounds = new Rectangle(bounds);

				LoadContent();

				//TODO: center the picture?

				_bounds.Bottom = _photoBounds.Bottom - (_style?.Padding?.Bottom ?? 0);

				HandleEmpty();
				Trace("end _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);

				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//	Take a note of how much we're shifting
				int offset = _bounds.Top - top;

				//	Reposition ourself
				_bounds.Top = top;
				_bounds.Bottom -= offset;
			
				//	Reposition our photo
				_photoBounds.Top -= offset;
				_photoBounds.Bottom -= offset;
				Trace("end _bounds={0}", _bounds);
			}
		}

		public override bool IsEmpty()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				return _photo == null;
			}
		}

		public override bool CollapseTopSpace()
		{
			return true; // stop collapsing
		}
	}
}
