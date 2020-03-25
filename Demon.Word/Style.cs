using System;
using System.Collections.Generic;
using System.Xml.Linq;
using s = Demon.Report.Style;

namespace Demon.Word
{
	internal class Styles : Part
	{
		private Dictionary<string, Style> _styles = new Dictionary<string, Style>();
		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml";

		public Styles(string name)
			:base(name)
		{
		}

		protected override void WriteContent()
		{
			XNamespace w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

			XElement root = new XElement(
				w + "styles",
				new XAttribute(XNamespace.Xmlns + "w", w));
			_document.Add(root);

			foreach(Style style in _styles.Values)
				style.Write(root);
		}
	}

	internal abstract class Style
	{
		public abstract void Write(XElement root);
	}

	internal static class StyleHelpers
	{
		public static XElement CreateWidth(
			XNamespace ns,
			string elementName,
			int width)
		{
			width *= 20; // measured in dxa, which is 20ths of a point
			XElement element = new XElement(
				ns + elementName,
				new XAttribute(ns + "w", width),
				new XAttribute(ns + "type", "dxa"));
			return element;
		}

		public static XElement CreateBorder(
			XNamespace ns,
			string elementName,
			s.Border border,
			s.Padding padding)
		{
			if(border == null) return null;

			XElement element = new XElement(ns + elementName);

			int size = border.Thickness * 8; // eighths of a point
			int left   = (padding?.Left  ?? 0) * 8;
			int bottom = (padding?.Bottom?? 0) * 8;
			int right  = (padding?.Right ?? 0) * 8;
			int top    = (padding?.Top   ?? 0) * 8;
			int red    = (int)((border.Color?.Red   ?? 0f) * 255f);
			int green  = (int)((border.Color?.Green ?? 0f) * 255f);
			int blue   = (int)((border.Color?.Blue  ?? 0f) * 255f);
			string color = $"{red:X2}{green:X2}{blue:X2}";

			if((border.Parts & Report.Style.BorderPart.Left) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "left",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}
			if((border.Parts & Report.Style.BorderPart.Bottom) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "bottom",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}
			if((border.Parts & Report.Style.BorderPart.Right) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "right",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}
			if((border.Parts & Report.Style.BorderPart.Top) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "top",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}
			if((border.Parts & Report.Style.BorderPart.InnerHorizontal) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "insideH",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}
			if((border.Parts & Report.Style.BorderPart.InnerVertical) != Report.Style.BorderPart.None)
			{
				XElement side = new XElement(
					ns + "insideV",
					new XAttribute(ns + "val", "single"),
					new XAttribute(ns + "sz", size),
					new XAttribute(ns + "space", left),
					new XAttribute(ns + "color", color));
				element.Add(side);
			}

			return element;
		}

		public static XElement CreateMargin(
			XNamespace ns,
			string elementName,
			s.Padding padding)
		{
			if(padding == null) return null;

			XElement element = new XElement(ns + elementName);
			
			XElement top    = CreateWidth(ns, "top",    padding.Top   );
			XElement start  = CreateWidth(ns, "start",  padding.Left  );
			XElement bottom = CreateWidth(ns, "bottom", padding.Bottom);
			XElement end    = CreateWidth(ns, "end",    padding.Right );
			element.Add(top);
			element.Add(start);
			element.Add(bottom);
			element.Add(end);

			return element;
		}

		public static XElement AddRunProperties(XElement run, s.Font font, s.Color color)
		{
			XNamespace ns = run.Name.Namespace;

			XElement rPr = new XElement(ns + "rPr");
			run.Add(rPr);

			XElement rFonts = new XElement(
				ns + "rFonts",
				new XAttribute(ns + "ascii", font.FamilyName),
				new XAttribute(ns + "cs", font.FamilyName));
			rPr.Add(rFonts);

			//	Font size is measured in half points in Word
			XElement size = new XElement(
				ns + "sz",
				new XAttribute(ns + "val", font.Size * 2));
			rPr.Add(size);

			if(font.Bold)
			{
				XElement bold = new XElement(
					ns + "b",
					new XAttribute(ns + "val", "true"));
				rPr.Add(bold);
			}
			if(font.Italic)
			{
				XElement italic = new XElement(
					ns + "i",
					new XAttribute(ns + "val", "true"));
				rPr.Add(italic);
			}
			if(font.Underline)
			{
				XElement underline = new XElement(
					ns + "u",
					new XAttribute(ns + "val", "single"));
				rPr.Add(underline);
			}
			if(font.Strikeout)
			{
				XElement strikeout = new XElement(
					ns + "strike",
					new XAttribute(ns + "val", "true"));
				rPr.Add(strikeout);
			}

			if(color != null)
			{
				int red   = (int)(color.Red   * 255f);
				int green = (int)(color.Green * 255f);
				int blue  = (int)(color.Blue  * 255f);
				string value = $"{red:X2}{green:X2}{blue:X2}";
				XElement colorElem = new XElement(
					ns + "color",
					new XAttribute(ns + "val", value));
				rPr.Add(colorElem);
			}

			return rPr;
		}
	}
}
