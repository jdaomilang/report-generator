using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Demon.Word
{
	/// <summary>
	/// Section 15.2.13
	/// </summary>
	internal class FontTable : Part
	{
		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.fontTable+xml";

		private List<Font> _fonts = new List<Font>();

		public FontTable(string name)
			:base(name)
		{
		}

		public void AddFont(Demon.Font.Font font)
		{
			//	In the report library, if a single font is used with variations such as
			//	bold and italic, that gives rise to two report font objects. But in such
			//	a case we only want a single Word font object, because we're only interested
			//	in the family name. (If we were to embed the fonts then I think we'd need
			//	to store the two fonts separately.)
			bool found = false;
			foreach(Font exists in _fonts)
			{
				if(exists.FamilyName == font.FamilyName)
				{
					found = true;
					break;
				}
			}
			if(!found)
				_fonts.Add(new Font(font));
		}

		protected override void WriteContent()
		{
			XNamespace w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

			XElement root = new XElement(
				w + "fonts",
				new XAttribute(XNamespace.Xmlns + "w", w));
			_document.Add(root);

			foreach(Font font in _fonts)
				font.Write(root);
		}
	}

	/// <summary>
	/// Information about a font. Goes in the font table. Section 17.8.
	/// </summary>
	internal class Font
	{
		private Demon.Font.Font _underlying;
		public string FamilyName { get { return _underlying.FamilyName; }}

		public Font(Demon.Font.Font src)
		{
			_underlying = src;
		}

		public void Write(XElement root)
		{
			XNamespace ns = root.Name.Namespace;

			//	As far as I can make out the "name" attribute of the main "font"
			//	element should be the family name as we know it in the report
			//	library, and the "family" element's value must be one of:
			//		decorative
			//		modern
			//		roman
			//		script
			//		swiss
			//		auto (default value if omitted - seems good to me)
			//	See the definition of ST_FontFamily on page 3878.
			XElement font = new XElement(
				ns + "font",
				new XAttribute(ns + "name", _underlying.FamilyName));
			root.Add(font);

			XElement family = new XElement(
				ns + "family",
				new XAttribute(ns + "val", "auto"));
			font.Add(family);

			Demon.Font.TrueTypeFont ttf = _underlying as Demon.Font.TrueTypeFont;
			if(ttf != null)
			{
				XElement sig = new XElement(ns + "sig");
				sig.SetAttributeValue(ns + "csb0", ttf.CodePageRanges[0].ToString("X8"));
				sig.SetAttributeValue(ns + "csb1", ttf.CodePageRanges[1].ToString("X8"));
				sig.SetAttributeValue(ns + "usb0", ttf.UnicodeRanges [0].ToString("X8"));
				sig.SetAttributeValue(ns + "usb1", ttf.UnicodeRanges [1].ToString("X8"));
				sig.SetAttributeValue(ns + "usb2", ttf.UnicodeRanges [2].ToString("X8"));
				sig.SetAttributeValue(ns + "usb3", ttf.UnicodeRanges [3].ToString("X8"));
				font.Add(sig);

				XElement pitch = new XElement(
					ns + "pitch",
					new XAttribute(ns + "val", ttf.IsFixedPitch ? "fixed" : "variable"));
				font.Add(pitch);

				StringBuilder sb = new StringBuilder();
				foreach(byte b in ttf.Panose)
					sb.Append(b.ToString("X2"));
				XElement panose = new XElement(
					ns + "panose1",
					new XAttribute(ns + "val", sb.ToString()));
				font.Add(panose);

				//	I don't know how to identify and describe the character set. As far as I can
				//	make out, the only place in the font file that indicates this kind of information
				//	is the OS/2 table's ulCodePageRange field, and we've already included that
				//	in the "sig" element. A quick review of the Verdana font shows that it marks
				//	the following code pages as "functional": Latin1, Latin2, Cyrillic, Greek,
				//	Turkish, Windows Baltic, Vietnamese, Macintosh (US Roman). But how should
				//	we map these code pages to a character set? If we omit the charset element
				//	then the default of 8859-1 Latin1 is assumed, which seems ok for our purposes.
				//	Symbola gives an eclectic mix, which I suppose is kind of to be expected:
				//	Latin1, Cyrillic, Greek, OEM, IBM Greek, MS-DOS Russion, IBM Cyrillic, Greek, US.
				//
				//	https://docs.microsoft.com/en-us/typography/legacy/legacy_arabic_fonts#font-encoding-and-character-set-declarations
				//	says (in relation to "legacy Arabic" encodings, but it's the only advice I can
				//	find that seems even vaguely related to this question):
				//
				//		"There is a correspondence between CHARSET values in Windows GDI and code pages,
				//		and code pages are referenced in the ulCodePageRange fields of the OS/2 table (version
				//		1 and later). However, the ulCodePageRange fields are used to indicate logical character
				//		sets that are supported in the font, but say nothing about actual character encodings used
				//		in the font. For this class of fonts, the ulCodePageRange fields are not relevant."
				//
				//	I really don't know what to do here.
//				XElement charset = new XElement(
//					ns + "charset",
//					new XAttribute(ns + "characterSet", "8859-1"));
//				font.Add(charset);
			}
			else
			{
				XElement notTtf = new XElement(
					ns + "notTrueType",
					new XAttribute(ns + "val", "true"));
				font.Add(notTtf);
			}
		}
	}

	/// <summary>
	/// A font file embedded in the document. Section 15.2.13.
	/// </summary>
	internal class EmbeddedFont : Part
	{
		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.fontTable+xml";

		private Demon.Font.Font _underlying;

		public EmbeddedFont(string name, Demon.Font.Font src)
			:base(name)
		{
			_underlying = src;
		}

		public override void Write(ZipArchive archive)
		{
			//	Write the font to a new part file
			ZipArchiveEntry entry = archive.CreateEntry(Name, CompressionLevel.NoCompression);
			using(Stream stream = entry.Open())
			{
//				stream.Write(_data, 0, _data.Length);
			}
		}
	}
}
