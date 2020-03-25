using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using s = Demon.Report.Style;
using t = Demon.Report.Types;

namespace Demon.Word
{
	public interface IParagraph : IBlockContent
	{
		void AddRun(string text, s.Font font, s.Color color);
		void AddBreak(string clear, string type);
		void AddLineBreak();
		void AddImage(string relationshipId, int width, int height);
		void AddTable(ITable table);
		void SetNumbering(int abstractNumberId, int level);
	}

	public interface IParagraphContent
	{
		void Write(XElement paragraph);
	}

	internal class Paragraph : IParagraph
	{
		private s.TextStyle _style;
		private List<IParagraphContent> _content;
		private int _numberingId = -1;
		private int _numberingLevel = 0;
		private t.TrackingInfo _trackingInfo;

		public Paragraph(s.TextStyle style, t.TrackingInfo trackingInfo)
		{
			_style = style;
			_content = new List<IParagraphContent>();
			_trackingInfo = trackingInfo;
		}

		public void AddRun(string text, s.Font font, s.Color color)
		{
			Run run = new Run(text, font, color);
			_content.Add(run);
		}

		public void AddBreak(string clear, string type)
		{
			BreakRun br = new BreakRun(clear, type);
			_content.Add(br);
		}

		public void AddLineBreak()
		{
			AddBreak(null, null);
		}

		public void AddImage(string relId, int width, int height)
		{
			ImageReference image = new ImageReference(relId, width, height);
			_content.Add(image);
			
			//TODO: store images and runs in the same list so that they can
			//appear intermingled in the document
		}

		public void AddTable(ITable table)
		{
			_content.Add(table);
		}

		public void SetNumbering(int abstractNumberId, int level)
		{
			_numberingId = abstractNumberId;
			_numberingLevel = level;
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement para = new XElement(ns + "p");
			parent.Add(para);

			XElement props = new XElement(ns + "pPr");
			para.Add(props);

			if(_numberingId > -1)
			{
				XElement numPr = new XElement(ns + "numPr");
				props.Add(numPr);

				XElement ilvl = new XElement(
					ns + "ilvl",
					new XAttribute(ns + "val", _numberingLevel));
				numPr.Add(ilvl);

				XElement numId = new XElement(
					ns + "numId",
					new XAttribute(ns + "val", _numberingId));
				numPr.Add(numId);
			}

			string align = null;
			switch(_style.Alignment)
			{
				case s.TextAlignment.Left:    align = "start";  break;
				case s.TextAlignment.Right:   align = "end";    break;
				case s.TextAlignment.Center:  align = "center"; break;
				case s.TextAlignment.Justify: align = "both";   break;
			}
			if(align != null)
			{
				XElement prop = new XElement(
					ns + "jc",
					new XAttribute(ns + "val", align));
				props.Add(prop);
			}

			//TODO: keepLines, keepNext, pageBreakBefore

			XElement border = StyleHelpers.CreateBorder(ns, "pBdr", _style.Border, _style.Padding);
			if(border != null)
				props.Add(border);

			if(_style.BackColor != null)
			{
				int red   = (int)(_style.BackColor.Red   * 255f);
				int green = (int)(_style.BackColor.Green * 255f);
				int blue  = (int)(_style.BackColor.Blue  * 255f);
				string color = $"{red:X2}{green:X2}{blue:X2}";

				XElement shd = new XElement(
					ns + "shd",
					new XAttribute(ns + "fill", color));
				props.Add(shd);
			}

			foreach(IParagraphContent content in _content)
				content.Write(para);
		}
	}

	internal class Run : IParagraphContent
	{
		private string _text;
		private s.Font _font;
		private s.Color _color;
		public string Text { get { return _text; }}

		public Run(string text, s.Font font, s.Color color)
		{
			_text = text;
			_font = font;
			_color = color;
		}

		public void Write(XElement paragraph)
		{
			XNamespace ns = paragraph.Name.Namespace;

			XElement run = new XElement(ns + "r");
			paragraph.Add(run);

			//	Style properties in Word can be inherited in the style
			//	hierarchy, but our layouts have already collapsed the
			//	hierarchy into a single description, and so we don't
			//	need worry about inheritance in Word.

			XElement props = StyleHelpers.AddRunProperties(run, _font, _color);

			XElement text = new XElement(
				ns + "t",
				new XAttribute(XNamespace.Xml + "space", "preserve"));
			text.Value = _text;
			run.Add(text);
		}
	}

	/// <summary>
	/// An empty paragraph with section properties to define the end
	/// of a section.
	/// </summary>
	internal class SectionBreakParagraph : IBlockContent
	{
		private SectionProperties _properties;

		public SectionBreakParagraph(SectionProperties properties)
		{
			_properties = properties;
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement para = new XElement(ns + "p");
			parent.Add(para);

			XElement props = new XElement(ns + "pPr");
			para.Add(props);

			_properties.Write(props);
		}
	}

	public class 	SectionProperties : IBlockContent
	{
		private t.TrackingInfo _trackingInfo;
		public t.TrackingInfo TrackingInfo => _trackingInfo;

		public SectionProperties(t.TrackingInfo trackingInfo)
		{
			_trackingInfo = trackingInfo;
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement sectPr = new XElement(ns + "sectPr");
			parent.Add(sectPr);

			//TODO: add the section properties to the sectPr element
		}
	}


	internal class BreakRun : IParagraphContent
	{
		private string _clear;
		private string _type;

		public BreakRun(string clear, string type)
		{
			_clear = clear;
			_type = type;
		}

		public void Write(XElement paragraph)
		{
			XNamespace ns = paragraph.Name.Namespace;

			XElement run = new XElement(ns + "r");
			paragraph.Add(run);

//			XElement props = new XElement(ns + "rPr");
//			run.Add(props);

			XElement br = new XElement(ns + "br");
			run.Add(br);

			if(_clear != null)
				br.SetAttributeValue(ns + "clear", _clear);
			if(_type != null)
				br.SetAttributeValue(ns + "type", _type);
		}
	}

	internal class ImageReference : IParagraphContent
	{
		//	Ids of XML elements used to represent the image.
		//	I'm not sure whether these two ids need to be different to each other,
		//	but let's just err on the safe side.
		private int _drawingId;
		private int _picId;

		//	The name is derived from drawing id
		private string _name;
		
		//	The relationship id identifies the image data object
		private string _relId;

		private int _width;
		private int _height;

		private static int _nextId = 0;

		public ImageReference(string relId, int width, int height)
		{
			_drawingId = _nextId++;
			_picId = _nextId++;
			_name = $"image {_drawingId}";
			_relId = relId;

			//	The image's width and height are expressed in points, and in the Word
			//	document they must be in English Metric Units. There are 12,700 EMUs
			//	in a point.
			_width  = width  * 12700;
			_height = height * 12700;
		}

		public void Write(XElement paragraph)
		{
			XNamespace w   = paragraph.GetNamespaceOfPrefix("w"  );
			XNamespace r   = paragraph.GetNamespaceOfPrefix("r"  );
			XNamespace wp  = paragraph.GetNamespaceOfPrefix("wp" );
			XNamespace a   = paragraph.GetNamespaceOfPrefix("a"  );
			XNamespace pic = paragraph.GetNamespaceOfPrefix("pic");

			XElement run = new XElement(w + "r");
			paragraph.Add(run);

			XElement drawing = new XElement(w + "drawing");
			run.Add(drawing);

			XElement inline = new XElement(wp + "inline");
			drawing.Add(inline);

			XElement extent = new XElement(
				wp + "extent",
				new XAttribute("cx", _width),
				new XAttribute("cy", _height));
			inline.Add(extent);

			XElement effectExtent = new XElement(
				wp + "effectExtent",
				new XAttribute("l", 0),
				new XAttribute("t", 0),
				new XAttribute("r", 0),
				new XAttribute("b", 0));
			inline.Add(effectExtent);

			XElement docPr = new XElement(
				wp + "docPr",
				new XAttribute("id", _drawingId),
				new XAttribute("name", _name));
			inline.Add(docPr);

			XElement graphic = new XElement(a + "graphic");
			inline.Add(graphic);

			XElement graphicData = new XElement(
				a + "graphicData",
				new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/picture"));
			graphic.Add(graphicData);

			XElement picElement = new XElement(pic + "pic");
			graphicData.Add(picElement);

			XElement nvPicPr = new XElement(pic + "nvPicPr");
			picElement.Add(nvPicPr);

			XElement cNvPr = new XElement(
				pic + "cNvPr",
				new XAttribute("id", _picId),
				new XAttribute("name", _name));
			nvPicPr.Add(cNvPr);
			
			XElement cNvPicPr = new XElement(pic + "cNvPicPr");
			nvPicPr.Add(cNvPicPr);

			XElement blipFill = new XElement(pic + "blipFill");
			picElement.Add(blipFill);

			XElement blip = new XElement(
				a + "blip",
				new XAttribute(r + "embed", _relId));
			blipFill.Add(blip);

			XElement srcRect = new XElement(a + "srcRect");
			blipFill.Add(srcRect);

			XElement stretch = new XElement(a + "stretch");
			blipFill.Add(stretch);

			XElement fillRect = new XElement(a + "fillRect");
			stretch.Add(fillRect);

			XElement spPr = new XElement(pic + "spPr");
			picElement.Add(spPr);

			XElement xfrm = new XElement(a + "xfrm");
			spPr.Add(xfrm);

			XElement off = new XElement(
				a + "off",
				new XAttribute("x", 0),
				new XAttribute("y", 0));
			xfrm.Add(off);

			XElement ext = new XElement(
				a + "ext",
				new XAttribute("cx", _width),
				new XAttribute("cy", _height));
			xfrm.Add(ext);

			XElement prstGeom = new XElement(
				a + "prstGeom",
				new XAttribute("prst", "rect"));
			spPr.Add(prstGeom);

		}
	}
}
