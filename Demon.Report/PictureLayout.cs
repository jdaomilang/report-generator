using System.Threading.Tasks;
using System.Xml.Linq;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class PictureLayout : Layout
	{
		private string _filename;
		private string _resourceId;
		private Image _image;
		private int _width;
		private int _height;
		private Rectangle _imageBounds;
		private PictureAlignment _alignment;
		private PictureScaleMode _scaleMode;
		private int _quality;
//		private PictureStyle _style;
		
//		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
//		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}

		public override LayoutType LayoutType { get { return LayoutType.Picture; } }
		
		public Image Image { get { return _image; }}
		public string Filename { get { return _filename; } }
		public string ResourceId { get { return _resourceId; } }
		public Rectangle ImageBounds { get { return _imageBounds; } }

		public PictureLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public PictureLayout(PictureLayout src)
			:base(src)
		{
			_filename = src._filename;
			_resourceId = src._resourceId;
			_image = src._image; // OK to copy by reference because the source layout will be dropped
			_width = src._width;
			_height = src._height;
			_imageBounds = src._imageBounds;
			_alignment = src._alignment;
			_scaleMode = src._scaleMode;
			_quality = src._quality;
//			_style = src._style;
		}

		public override void Load(XElement root)
		{
			base.Load(root);
			XNamespace ns = root.GetDefaultNamespace();
//			_style = _generator.ReportDesign.LoadStyle<PhotoStyle>(root.Element(ns + "Style"));
//			if (_style == null) _style = _generator.ReportDesign.DefaultPhotoStyle;

			//	The picture can be specified either with an explicit filename or with
			//	a reference to a resource
			_filename = _generator.ReportDesign.LoadString(root.Attribute("filename"));
			_resourceId = _generator.ReportDesign.LoadString(root.Attribute("ref"));

			_width = _generator.ReportDesign.LoadInt(root.Element(ns + "Width")) ?? 0;
			_height = _generator.ReportDesign.LoadInt(root.Element(ns + "Height")) ?? 0;
			_alignment = _generator.ReportDesign.LoadEnum<PictureAlignment>(root.Element(ns + "Alignment" ));
			_scaleMode = _generator.ReportDesign.LoadEnum<PictureScaleMode>(root.Element(ns + "ScaleMode" ));
			_quality = _generator.ReportDesign.LoadInt(root.Element(ns + "Quality")) ?? 0;
		}

		public override void LoadContent()
		{
			//	Load the image data from the server
			//TODO: have an image cache that can store one copy of each
			//separate image that we use, at the maximum resolution necessary
			//to satisfy all references to it. And only write that one
			//instance to the PDF, with appropriate references.
			Task.Run(async () =>
			{
				//	If we have a resource id then load the image from the design's
				//	resource cache. Otherwise, if we have an explicit filename,
				//	then load the image directly without caching.
				if(_resourceId != null)
					_image = await Image.LoadCachedPicture(_generator,_resourceId);
				else
					_image = await Image.LoadPictureDirect(_generator,_filename);
			}).Wait();
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("startng bounds={0}", bounds);
				_bounds = new Rectangle(bounds);

				//	Apply padding
	//			bounds.Left  += _style?.Padding?.Left  ?? 0;
	//			bounds.Right -= _style?.Padding?.Right ?? 0;
	//			bounds.Top   -= _style?.Padding?.Top   ?? 0;
	//			bounds.Bottom = bounds.Top;

				//	The image must fit in the space left after our padding
				_imageBounds = new Rectangle(bounds);

				ResizeImage();

				//	Align the picture box. We support horizontal alignments but
				//	ignore any vertical component of the specified alignment
				//	because the page strategy that runs throughout the entire
				//	report is to work within given horizontal bounds and to use
				//	whatever height is required to render the data.
				switch(_alignment)
				{
					case PictureAlignment.TopLeft:
					case PictureAlignment.CenterLeft:
					case PictureAlignment.BottomLeft:
						//	Nothing to do because the picture is naturally aligned top left
						break;

					case PictureAlignment.TopCenter:
					case PictureAlignment.Center:
					case PictureAlignment.BottomCenter:
						int offset = (bounds.Width - _imageBounds.Width) / 2;
						_imageBounds.Left += offset;
						_imageBounds.Right += offset;
						break;

					case PictureAlignment.TopRight:
					case PictureAlignment.CenterRight:
					case PictureAlignment.BottomRight:
						int width = _imageBounds.Width;
						_imageBounds.Right = bounds.Right;
						_imageBounds.Left = _imageBounds.Right - width;
						break;
				}

				_bounds.Bottom = _imageBounds.Bottom ;// - (_style?.Padding?.Bottom ?? 0);

				HandleEmpty();
				Trace("endng _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("startng _bounds={0}", _bounds);

				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("endng _bounds={0}", _bounds);
					return;
				}

				//	Take a note of how much we're shifting
				int offset = _bounds.Top - top;

				//	Reposition ourself
				_bounds.Top = top;
				_bounds.Bottom -= offset;
			
				//	Reposition our photo
				_imageBounds.Top -= offset;
				_imageBounds.Bottom -= offset;
				Trace("endng _bounds={0}", _bounds);
			}
		}

		private void ResizeImage()
		{
			switch(_scaleMode)
			{
				case PictureScaleMode.NaturalSize:
				{
					break;
				}
				case PictureScaleMode.ScaleDown:
				{
					//	How much do we have to scale the image down? The size is constrained
					//	by our layout size and by the available width
					int scaledWidth = _image.Size.Width;
					int scaledHeight = _image.Size.Height;
					int maxImageWidth = _imageBounds.Width;
					if(scaledWidth > maxImageWidth)
					{
						double scale = (double)maxImageWidth / (double)scaledWidth;
						scaledWidth = (int)(scaledWidth * scale);
						scaledHeight = (int)(scaledHeight * scale);
					}
					if(scaledWidth > _width)
					{
						double scale = (double)_width / (double)scaledWidth;
						scaledWidth = (int)(scaledWidth * scale);
						scaledHeight = (int)(scaledHeight * scale);
					}
					if(scaledHeight > _height)
					{
						double scale = (double)_height / (double)scaledHeight;
						scaledWidth = (int)(scaledWidth * scale);
						scaledHeight = (int)(scaledHeight * scale);
					}

					//	Calculate the box for the image. We've already worked out the maximum
					//	box, in _imageBounds, but we want the box that exactly fits the
					//	image so that we don't stretch it.
					int boxWidth = scaledWidth;
					int boxHeight = scaledHeight;
					int boxLeft = _bounds.Left ;// + (_style?.Padding?.Left ?? 0);
					int boxBottom = _bounds.Top /* - (_style?.Padding?.Top ?? 0) */ - boxHeight;
					int boxRight = boxLeft + boxWidth;
					int boxTop = boxBottom + boxHeight;
					_imageBounds = new Rectangle(boxLeft, boxBottom, boxRight, boxTop);

					//	Scale the image down to the new size
					Size scaledSize = new Size(_imageBounds.Width, _imageBounds.Height);
					_image = _image.Resize(scaledSize, _quality);

					break;
				}
			}
		}

		protected override PageDisposition SetPageSplitIndex(Rectangle bodyBox, ref bool honourKeepWithNext)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				PageDisposition disposition = PageDisposition.ThisPage;
				if(_bounds.Bottom < bodyBox.Bottom)
					disposition = PageDisposition.Overflow;
				//	Our bottom padding has already been factored in by Layout.AssessPageBreak
				return disposition;
			}
		}

		public override bool IsEmpty()
		{
			return _image == null;
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			return false;
		}

		public override bool CollapseTopSpace()
		{
			return true; // stop collapsing
		}

		public override Bump BumpPageSplitIndex()
		{
			//	We never split a picture
			return Bump.Impossible;
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				//	We never split a picture, so on a page break we always move all
				//	of our content into a copy of ourself, and return that copy. This
				//	leaves ourself empty, and the base class implementation will then
				//	remove us from our container.
				PictureLayout copy = new PictureLayout(this);
				_image = null; // empty ourself
				return copy;
			}
		}
	}
}
