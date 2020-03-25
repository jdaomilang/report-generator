using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Demon.Font;

namespace Demon.PDF
{
	public class TrueTypeFont : Font, IIndirectObject
	{
		private Type0Font _root;
		private Demon.Font.TrueTypeFont _underlying;
		private FontDescriptor _descriptor;
		private ToUnicode _toUnicode;
		private FontStream _fontStream;
		private Demon.PDF.FontWidths _widths;

		public override string FamilyName { get { return _underlying.FamilyName; } }
		public override bool Bold { get { return _underlying.Bold; } }
		public override bool Italic { get { return _underlying.Italic; } }

		public FontDescriptor Descriptor { get { return _descriptor; } }
		public Type0Font RootFont { get { return _root;}}
		public ToUnicode ToUnicode { get { return _toUnicode; } }
		public Demon.PDF.FontWidths Widths { get { return _widths; } }

		public string PostScriptName { get { return _underlying.PostScriptName; }}
		public override ushort UnitsPerEm { get { return _underlying.UnitsPerEm; }}


		public TrueTypeFont(Demon.Font.TrueTypeFont underlying)
		{
			_underlying = underlying;

			_toUnicode = new ToUnicode();
			_fontStream = new FontStream(_underlying.FontFile.ReadAllBytes());
			_widths = new FontWidths(_underlying.Widths);

			_descriptor = new FontDescriptor(
				_underlying.PostScriptName, _underlying.Flags, _fontStream,
				_underlying.ItalicAngle, _underlying.Ascender, _underlying.Descender,
				_underlying.CapHeight, _underlying.XAvgCharWidth, _underlying.AdvanceWidthMax,
				_underlying.WeightClass, _underlying.XHeight, 0,
				_underlying.BoundingBox);

			_root = new Type0Font(this);
		}

		public override void Write(Stream file, Document doc)
		{
//			RootFont.Write(file,generator);

			ObjectReference fontref = doc.GetReference(this);
			fontref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(fontref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<< /Type /Font\r\n");
			sb.Append("/Subtype /TrueType\r\n");
			
			//	/Name is obsolete - see page 413 in the PDF spec
//			sb.Append("/Name /");
//			sb.Append(_name); //TODO: escape spaces etc.
//			sb.Append("\r\n");

			sb.Append("/BaseFont /");
			sb.Append(PostScriptName); //TODO: escape spaces etc.
			sb.Append("\r\n");

//			//	Specify no encoding for "symbolic" fonts
//			if(_encoding != null)
//			{
//				sb.Append("/Encoding /");
//				sb.Append(_encoding);
//				sb.Append("\r\n");
//			}

			sb.Append("/FontDescriptor ");
			sb.Append(doc.GetReference(_descriptor).Reference);
			sb.Append("\r\n");

			sb.Append("/FirstChar ");
			sb.Append(_underlying.FirstCharIndex);
			sb.Append("\r\n");

			sb.Append("/LastChar ");
			sb.Append(_underlying.LastCharIndex);
			sb.Append("\r\n");

			sb.Append("/Widths ");
			sb.Append(doc.GetReference(_widths).Reference);
			sb.Append("\r\n");

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());

			_descriptor.Write(file, doc);
			_fontStream?.Write(file, doc);
			_widths.Write(file, doc, UnitsPerEm);

			foreach(KeyValuePair<char, ushort> glyph in _underlying.GlyphIndices) // <char, index>
				_toUnicode.Add(glyph.Value, glyph.Key); // <index, char>
			_toUnicode.Write(file,doc);
		}

		public ushort GetGlyphIndex(char c)
		{
			return _underlying.GetGlyphIndex(c);
		}

		public override void MapCharacter(char c)
		{
			_underlying.MapCharacter(c);
		}

		public override List<KernFragment> Kern(string text, int start, int end)
		{
			return _underlying.Kern(text, start, end);
		}

		/// <summary>
		/// Encode a text string in the font's encoding
		/// </summary>
		public override string Encode(string text)
		{
			//	True Type defers to its Type 0 wrapper
			throw new NotImplementedException();
		}

		public override int GetCapHeight(int fontSize)
		{
			return _underlying.GetCapHeight(fontSize);
		}

		public override int GetTextLength(string text, int start, int end, int fontSize)
		{
			return _underlying.GetTextLength(text, start, end, fontSize);
		}

		public override string ToString()
		{
			return _underlying.ToString();
		}
	}
}
