using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Demon.Font;

namespace Demon.PDF
{
	/// <summary>
	/// A Type 0 font is a composite font. Its font data is stored in the descendant
	/// CIDFont contained within the Type 0 font. A composite font supports Unicode
	/// characters, whereas simple fonts can handle only single-byte characters.
	/// (A simple font can handle non-ASCII characters by mapping them into its
	/// glyph set, but it still expects the text to be in single-byte format.)
	/// </summary>
	public class Type0Font : Font, IIndirectObject
	{
		protected TrueTypeFont _descendant;
		protected CIDFont _cidFont;
		public CIDFont CIDFont { get { return _cidFont;}}
		public override string FamilyName { get { return _descendant.FamilyName; } }
		public override bool Bold { get { return _descendant.Bold; } }
		public override bool Italic { get { return _descendant.Italic; } }
		public override ushort UnitsPerEm { get { return _descendant.UnitsPerEm; }}

		public Type0Font(TrueTypeFont descendant)
		{
			_descendant = descendant;
			_cidFont = new CIDFont(_descendant);
		}

		public override void Write(Stream file, Document doc)
		{
			ObjectReference fontref = doc.GetReference(this);
			fontref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(fontref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<< /Type /Font\r\n");
			sb.Append("/Subtype /Type0\r\n");

			sb.Append("/BaseFont /");
			sb.Append(_descendant.PostScriptName);
			sb.Append("\r\n");

			sb.Append("/Encoding /Identity-H\r\n");

			//	The Type 0 font has a one-element array of descendant fonts.
			//	The descendant font is a CIDFont (in our case, subtype
			//	CIDFontType2.) The CIDFont points to the same descriptor
			//	as the True Type font.

			ObjectReference cidref = doc.GetReference(_cidFont);

			//	DescendantFonts is a one-element array, that one entry
			//	being a reference to the descendant font
			sb.Append("/DescendantFonts [ ");
			sb.Append(cidref.Reference);
			sb.Append(" ]\r\n");

			ObjectReference uniref = doc.GetReference(_descendant.ToUnicode);
			sb.Append("/ToUnicode ");
			sb.Append(uniref.Reference);
			sb.Append("\r\n");

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file,sb.ToString());

			_cidFont.Write(file, doc);
		}

		public override List<KernFragment> Kern(string text, int start, int end)
		{
			return _descendant.Kern(text, start, end);
		}

		public override void MapCharacter(char c)
		{
			_descendant.MapCharacter(c);
		}

		public override string Encode(string text)
		{
			//	True Type fonts always encode in two-byte codes. The text
			//	string in the PDF is not encoded in Unicode or ASCII or
			//	anything like that. Each two-byte code is a "character
			//	code" that's interpreted by the font and its associated
			//	CMap. Since we use the Identity-H CMap, the codes we
			//	want in the text string are glyph indices into the font.

			StringBuilder sb = new StringBuilder((text.Length * 2) + 6);
			sb.Append("<");
			foreach(char c in text)
			{
				ushort i = _descendant.GetGlyphIndex(c);
				sb.Append(Helpers.Hex(i));
			}
			sb.Append(">");
			return sb.ToString();
		}

		public override int GetCapHeight(int fontSize)
		{
			return _descendant.GetCapHeight(fontSize);
		}

		public override int GetTextLength(string text, int start, int end, int fontSize)
		{
			return _descendant.GetTextLength(text, start, end, fontSize);
		}

		public override string ToString()
		{
			return _descendant.ToString();
		}
	}

	public class CIDFont : IIndirectObject
	{
		private TrueTypeFont _realFont;
		private CIDFontWidths _widths;

		public CIDFont(TrueTypeFont realfont)
		{
			_realFont = realfont;
		}

		public void Write(Stream file, Document doc)
		{
			ObjectReference thisref = doc.GetReference(this);
			thisref.ByteOffset = file.Position;

			ObjectReference realref = doc.GetReference(_realFont);

			StringBuilder sb = new StringBuilder();
			sb.Append(thisref.Id);
			sb.Append(" obj\r\n<<\r\n");

			sb.Append("/Type /Font\r\n");
			sb.Append("/Subtype /CIDFontType2\r\n");

			sb.Append("/BaseFont /");
			sb.Append(_realFont.PostScriptName);
			sb.Append("\r\n");

			sb.Append("/CIDSystemInfo <</Ordering(Identity) /Registry(Adobe) /Supplement 0>>\r\n");
			
			//	Use the real font's descriptor
			ObjectReference descref = doc.GetReference(_realFont.Descriptor);
			sb.Append("/FontDescriptor ");
			sb.Append(descref.Reference);
			sb.Append("\r\n");

			sb.Append("/CIDToGIDMap /Identity\r\n");
			
			//	If we haven't already got our widths, get them now
			if(_widths == null)
			{
				int count = _realFont.Widths.Count;
				ushort[] widths = new ushort[count];
				for(int x = 0; x < count; ++x)
					widths[x] = _realFont.Widths[x];
				_widths = new CIDFontWidths(widths);
			}

			//	The widths array here is in a special CIDFont format. The data are
			//	the same as on the real font.
			ObjectReference wref = doc.GetReference(_widths);
			sb.Append("/W ");
			sb.Append(wref.Reference);
			sb.Append("\r\n");

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());

			_widths.Write(file, doc, _realFont.UnitsPerEm);
		}
	}

	public class CIDFontWidths : IIndirectObject
	{
		private ushort[] _widths;

		/// <summary>
		/// Give the widths in font units
		/// </summary>
		/// <param name="widths"></param>
		public CIDFontWidths(ushort[] widths)
		{
			//	I can't find a definitive specification of the units that
			//	Type 0 widths are expressed in, but page 203 says that
			//	most font types map 1000 units of glyph space to one unit
			//	of text space.
			_widths = new ushort[widths.Length];
			for(int x = 0; x < widths.Length; ++x)
				_widths[x] = widths[x];
		}

		public void Write(Stream file, Document doc, ushort unitsPerEm)
		{
			ObjectReference wref = doc.GetReference(this);
			wref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(wref.Id);
			sb.Append(" obj\r\n[ ");

			//	Each entry in the array is of one of these formats:
			//
			//		c [ w1 w2 ... wn ]		- for individual widths
			//		c1 cn w					- for a range of same widths
			//
			//	At least for the proof of concept let's just do it
			//	the easy way and specify each individual width.
			for(int x = 0; x < _widths.Length; ++x)
			{
				sb.Append(x);
				sb.Append("[");
				//	The raw widths are in font units, but we want thousandths
				//	of a text unit in the dictionary. See page 414.
				sb.Append(_widths[x] * 1000 / unitsPerEm);
				sb.Append("] ");
			}
			
			sb.Append(" ]\r\nendobj\r\n");
			Document.WriteData(file,sb.ToString());
		}
	}
}
