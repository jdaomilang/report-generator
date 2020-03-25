using System.Collections.Generic;
using System.IO;
using System.Text;
using Demon.Report.Types;

namespace Demon.PDF
{
	public class Page : IIndirectObject
	{
		private ProcSet _procSet;
		private Rectangle _mediaBox;
		private Rectangle _bodyBox;
		private Rectangle _headerBox;
		private Rectangle _footerBox;
		private List<ContentFragment> _content;
		private FontCache _fonts;
		private List<Image> _images;
		private int _pageNumber;

		public Rectangle MediaBox {get {return _mediaBox;}}
		public Rectangle BodyBox {get {return _bodyBox;}}
		public Rectangle HeaderBox {get {return _headerBox;}}
		public Rectangle FooterBox {get {return _footerBox;}}

		internal IEnumerable<Image> Images { get { return _images; }}
		public int PageNumber { get { return _pageNumber; }}


		public Page(
			int pageNumber,
			Rectangle mediaBox, Rectangle bodyBox, Rectangle headerBox, Rectangle footerBox,
			Document doc)
		{
			doc.Pages.Add(this);
			_pageNumber = pageNumber;

			_procSet = new ProcSet();
			_procSet.Add("PDF");
			_procSet.Add("Text");
			_procSet.Add("ImageB");
			_procSet.Add("ImageC");
			_procSet.Add("ImageI");

			_fonts = doc.Fonts;
			_images = new List<Image>();

			_mediaBox  = mediaBox;
			_bodyBox   = bodyBox;
			_headerBox = headerBox;
			_footerBox = footerBox;

			_content = new List<ContentFragment>();

//			DebugOutline();
		}

		public void AddText(
			string text, int x, int y,
			string fontFamily, bool bold, bool italic, int fontSize,
			Color color)
		{
			Font font = _fonts.GetFont(fontFamily, bold, italic);
			Type1Font t1 = font as Type1Font;
			TrueTypeFont ttf = font as TrueTypeFont;
			if(t1 != null)
				AddText(text, x, y, t1, fontSize, color);
			else if(ttf != null)
				AddText(text, x, y, ttf, fontSize, color);
		}

		public void AddText(string text, int x, int y, Font font, int fontSize, Color color)
		{
			Type1Font    t1  = font as Type1Font;
			TrueTypeFont ttf = font as TrueTypeFont;
			if(t1 != null)
				AddText(text, x, y, t1, fontSize, color);
			else if(ttf != null)
				AddText(text, x, y, ttf, fontSize, color);
		}

		private void AddText(string text, int x, int y, Type1Font font, int fontSize, Color color)
		{
			Text t = new Text(text, x, y, font, fontSize, color);
			_content.Add(t);
		}

		private void AddText(string text, int x, int y, TrueTypeFont font, int fontSize, Color color)
		{
			Text t = new Text(text, x, y, font.RootFont, fontSize, color);
			_content.Add(t);
		}

		/// <param name="size">The size of the image stored in the binary data.</param>
		/// <param name="bits"></param>
		/// <param name="bitsPerChannel"></param>
		/// <param name="position">The position and size at which the image is to
		/// be rendered.</param>
		/// <returns></returns>
		public string AddImage(
			Size size,
			byte[] bits, int bitsPerChannel,
			Rectangle position)
		{
			//	If the image is a JPEG then store it as a single image. If it's
			//	a PNG then separate its RGB channels from its alpha channel and
			//	create one image for each, with the alpha image being the soft
			//	mask of the RGB image.
			string name = null;
			if(Image.IsPng(bits))
			{
				byte[] rgb;
				byte[] alpha;
				Image.SeparateAlphaChannel(bits, out rgb, out alpha);

				//	Store the alpha mask, if there is one
				Image mask = null;
				if(alpha != null)
				{
					string maskName = "Im" + (_images.Count + 1);
					mask = new Image(maskName, alpha, bitsPerChannel, size, position, ColorSpace.DeviceGray, Filter.Flate, null);
					_images.Add(mask);
				}

				//	Store the colour data, with a reference to the mask
				name = "Im" + (_images.Count + 1);
				Image image = new Image(name, rgb, bitsPerChannel, size, position, ColorSpace.DeviceRGB, Filter.Flate, mask);
				_images.Add(image);
			}
			else
			{
				//	Store the full image
				name = "Im" + (_images.Count + 1);
				Image image = new Image(name, bits, bitsPerChannel, size, position, ColorSpace.DeviceRGB, Filter.DCT, null);
				_images.Add(image);
			}

			//	Create a content fragment to show the image on the page. For JPEGs the
			//	fragment refers to the full image, of course, and for PNGs it refers
			//	to the main RGB image.
			ImageFragment frag = new ImageFragment(name, position);
			_content.Add(frag);

			return name;
		}

		public void AddImage(string name, Rectangle position)
		{
			ImageFragment frag = new ImageFragment(name, position);
			_content.Add(frag);
		}

		/// <summary>
		/// Either strokeColor or fillColor can be null, but not both.
		/// </summary>
		public void AddPath(IList<Position> points, float lineWidth, Color strokeColor, Color fillColor)
		{
			Path shape = new Path(points, lineWidth, strokeColor, fillColor);
			_content.Add(shape);
		}

		public void AddBackgroundColor()
		{
		}

		public void AddBackgroundImage()
		{
		}

		public void AddBorder()
		{
		}

		public void AddTraceOutline()
		{
		}

		public void Write(Stream file, ObjectReference pageref, ObjectReference parent, Document doc)
		{
			pageref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();

			sb.Append(pageref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<<\r\n");
			sb.Append("/Type /Page\r\n");

			//	We have a flat page structure, so the parent is always the catalog
			sb.Append("/Parent ");
			sb.Append(parent.Reference);
			sb.Append("\r\n");

			sb.Append("/MediaBox ");
			sb.Append(_mediaBox.Specification);
			sb.Append("\r\n");

			//	Write the string now because we want to write the resources and
			//	content directly to the file
			Document.WriteData(file, sb.ToString());
			sb.Clear();

			WriteResources(file,doc);

			//	Construct a content stream and write its reference to the file,
			//	but don't write its contents inside the page dictionary
			ContentStream stream = new ContentStream();
			ObjectReference cref = doc.GetReference(stream);
			Document.WriteData(file, "/Contents ");
			Document.WriteData(file, cref.Reference);
			Document.WriteData(file, "\r\n");

			//	Close the page object
			Document.WriteData(file, ">>\r\nendobj\r\n");

			//	Write the content stream, outside the page dictionary
			stream.Write(file, cref, _content);

			//	Write the images
			foreach(Image image in _images)
			{
				ObjectReference imageRef = doc.GetReference(image);
				ObjectReference maskRef = null;
				if(image.Mask != null)
					maskRef = doc.GetReference(image.Mask);
				image.Write(file, imageRef, maskRef);
			}
		}

		private void WriteResources(Stream file, Document doc)
		{
			Document.WriteData(file, "/Resources\r\n<<\r\n");
			_procSet.Write(file);
			
			//	Font references. The fonts themselves are written directly by the generator.
			if(_fonts.Fonts.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("/Font <<");
				foreach(Font font in _fonts.Fonts)
				{
					FontReference fontref = new FontReference(doc.GetReference(font));
					sb.Append(fontref.AliasReference);
					sb.Append(" ");

					TrueTypeFont ttf = font as TrueTypeFont;
					if(ttf != null)
					{
						fontref = new FontReference(doc.GetReference(ttf.RootFont));
						sb.Append(fontref.AliasReference);
						sb.Append(" ");
					}
				}
				sb.Append(">>\r\n");
				Document.WriteData(file, sb.ToString());
			}

			//	Image references. The images themselves are written elsewhere.
			if(_images.Count > 0)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("/XObject << ");
				foreach(Image img in _images)
				{
					ObjectReference imgRef = doc.GetReference(img);
					sb.Append("/");
					sb.Append(img.Name);
					sb.Append(" ");
					sb.Append(imgRef.Reference);
					sb.Append(" ");
				}
				sb.Append(">>\r\n");
				Document.WriteData(file, sb.ToString());
			}

			Document.WriteData(file, ">>\r\n");
		}

		public void ExpandDocumentProperties(Document doc)
		{
			foreach(ContentFragment frag in _content)
				frag.ExpandDocumentProperties(doc, this);
		}

		internal string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(Document.Space(indentLevel));
			sb.Append("Page\r\n");
			foreach(ContentFragment fragment in _content)
				sb.Append(fragment.Dump(indentLevel+1));
			foreach(Image image in _images)
				sb.Append(image.Dump(indentLevel+1));

			return sb.ToString();
		}

		internal void DebugOutline()
		{
			//	For debugging draw the body box, header and footer
//			Color color = new Color { Red = 0.9f, Green = 0.9f, Blue = 0.9f };
//			AddPath(_bodyBox, color, null);
//			AddPath(_headerBox, color, color);
//			AddPath(_footerBox, color, color);
		}

		private void DrawBleedMarks()
		{
			Color color = new Color { Red = 0.75f, Green = 0.75f, Blue = 0.75f };

			List<Position> path = new List<Position>();
			Position p1 = new Position(_bodyBox.Left - 20, _bodyBox.Top +  1);
			Position p2 = new Position(_bodyBox.Left -  1, _bodyBox.Top +  1);
			Position p3 = new Position(_bodyBox.Left -  1, _bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(_bodyBox.Right + 20, _bodyBox.Top  + 1);
			p2 = new Position(_bodyBox.Right +  1, _bodyBox.Top +  1);
			p3 = new Position(_bodyBox.Right +  1, _bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(_bodyBox.Left - 20, _bodyBox.Bottom -  1);
			p2 = new Position(_bodyBox.Left -  1, _bodyBox.Bottom -  1);
			p3 = new Position(_bodyBox.Left -  1, _bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(_bodyBox.Right + 20, _bodyBox.Bottom -  1);
			p2 = new Position(_bodyBox.Right +  1, _bodyBox.Bottom -  1);
			p3 = new Position(_bodyBox.Right +  1, _bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			AddPath(path, 1, color, null);
		}

		private void DrawRules()
		{
			int fontSize = 8;
			Font font = _fonts.GetFont("Helvetica", false, false);
			int fontHeight = font.GetCapHeight(fontSize);
			if(fontHeight == 0)
				fontHeight = fontSize;

			Color color = new Color { Red = 0.75f, Green = 0.75f, Blue = 0.75f };

			List<Position> path = new List<Position>();
			Position p1;
			Position p2;

			//	Side media box rule (left)
			for(int x = 0; x < _mediaBox.Height; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(_mediaBox.Left,      _mediaBox.Bottom + x);
					p2 = new Position(_mediaBox.Left + 12, _mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color ,null);

					int tx = p2.X + 2;
					int ty = p2.Y - (fontHeight / 2);
					AddText(x.ToString(), tx, ty, font, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(_mediaBox.Left,      _mediaBox.Bottom + x);
					p2 = new Position(_mediaBox.Left + 12, _mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(_mediaBox.Left,      _mediaBox.Bottom + x);
					p2 = new Position(_mediaBox.Left +  5, _mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
			}

			//	Bottom media box rule (bottom)
			for(int x = 0; x < _mediaBox.Width; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(_mediaBox.Left + x, _mediaBox.Bottom     );
					p2 = new Position(_mediaBox.Left + x, _mediaBox.Bottom + 12);
					path.Add(p1);
					path.Add(p2);
					AddPath(path,1,color,null);

					string text = x.ToString();
					int textLen = font.GetTextLength(text, 0, text.Length, fontSize) / font.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y + 2;
					AddText(text, tx, ty, font, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(_mediaBox.Left + x, _mediaBox.Bottom     );
					p2 = new Position(_mediaBox.Left + x, _mediaBox.Bottom + 12);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(_mediaBox.Left + x, _mediaBox.Bottom     );
					p2 = new Position(_mediaBox.Left + x, _mediaBox.Bottom +  5);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
			}

			//	Side body box rule (right)
			int start = _mediaBox.Bottom - _bodyBox.Bottom; // negative start to put zero at the body box bottom
			start = (start / 10) * 10;
			for(int x = start; x < _mediaBox.Height; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(_mediaBox.Right,      _bodyBox.Bottom + x);
					p2 = new Position(_mediaBox.Right - 12, _bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path,1,color,null);

					string text = x.ToString();
					int textLen = font.GetTextLength(text, 0, text.Length, fontSize) / font.UnitsPerEm;
					int tx = p2.X - 2 - textLen;
					int ty = p2.Y - (fontHeight / 2);
					AddText(x.ToString(), tx, ty, font, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(_mediaBox.Right,      _bodyBox.Bottom + x);
					p2 = new Position(_mediaBox.Right - 12, _bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(_mediaBox.Right,      _bodyBox.Bottom + x);
					p2 = new Position(_mediaBox.Right -  5, _bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
			}

			//	Bottom body box rule (top)
			start = _mediaBox.Left - _bodyBox.Left; // negative start to put zero at the body box left
			start = (start / 10) * 10;
			for(int x = start; x < _mediaBox.Width; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(_bodyBox.Left + x, _mediaBox.Top     );
					p2 = new Position(_bodyBox.Left + x, _mediaBox.Top - 12);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);

					string text = x.ToString();
					int textLen = font.GetTextLength(text, 0, text.Length, fontSize) / font.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y - 2 - fontHeight;
					AddText(text, tx, ty, font, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(_bodyBox.Left + x, _mediaBox.Top     );
					p2 = new Position(_bodyBox.Left + x, _mediaBox.Top - 12);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(_bodyBox.Left + x, _mediaBox.Top     );
					p2 = new Position(_bodyBox.Left + x, _mediaBox.Top -  5);
					path.Add(p1);
					path.Add(p2);
					AddPath(path, 1, color, null);
				}
			}
		}
	}

	/// <summary>
	/// Used in the generation phase
	/// </summary>
	public class PageList : List<Page>, IIndirectObject
	{
		public void Write(Stream file, ObjectReference reference, Document doc)
		{
			reference.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(reference.Id);
			sb.Append(" obj\r\n");
			sb.Append("<<\r\n");
			sb.Append("/Type /Pages\r\n");

			sb.Append("/Kids [ ");
			foreach(Page page in this)
			{
				ObjectReference pageref = doc.GetReference(page);
				sb.Append(pageref.Reference);
				sb.Append(" ");
			}
			sb.Append(" ]\r\n");

			sb.Append("/Count ");
			sb.Append(this.Count);
			sb.Append("\r\n");
			
//			sb.Append("/MediaBox ");
//			sb.Append(generator.Document.DefaultPageSize.Specification);
//			sb.Append("\r\n");

			sb.Append(">>\r\n");
			sb.Append("endobj\r\n");
			
			Document.WriteData(file, sb.ToString());
		}
	}
}
