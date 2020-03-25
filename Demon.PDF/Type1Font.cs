using System.IO;
using System.Text;
using System.Collections.Generic;
using Demon.Font;

namespace Demon.PDF
{
	public class Type1Font : Font, IIndirectObject
	{
		private Demon.Font.Type1Font _underlying;
		private FontWidths _widths;
		private FontDescriptor _descriptor;

		public override string FamilyName { get { return _underlying.FamilyName; } }
		public override bool Bold { get { return _underlying.Bold; } }
		public override bool Italic { get { return _underlying.Italic; } }
		public override ushort UnitsPerEm { get { return _underlying.UnitsPerEm; }}

		public FontDescriptor Descriptor { get { return _descriptor; } }

		public Type1Font(Demon.Font.Type1Font underlying)
		{
			_underlying = underlying;

			_descriptor = new FontDescriptor(
				_underlying.PostScriptName, _underlying.Flags, null,
				_underlying.ItalicAngle, _underlying.Ascender, _underlying.Descender,
				_underlying.CapHeight, _underlying.XAvgCharWidth, _underlying.AdvanceWidthMax,
				_underlying.WeightClass, _underlying.XHeight, 0,
				_underlying.BoundingBox);

			//	Underlying widths are reloaded during base.Load
			_widths = new FontWidths(_underlying.Widths);
		}

		public override void Write(Stream file, Document doc)
		{
			ObjectReference fontref = doc.GetReference(this);
			fontref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(fontref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<<\r\n/Type /Font\r\n");
			sb.Append("/Subtype /Type1\r\n");

			//	/Name is obsolete - see page 413 in the PDF spec
//			sb.Append("/Name /");
//			sb.Append(_name); //TODO: escape spaces etc.
//			sb.Append("\r\n");

			sb.Append("/BaseFont /");
			sb.Append(_underlying.PostScriptName); //TODO: escape spaces etc.
			sb.Append("\r\n");

			//	Specify no encoding for "symbolic" fonts
			if(_underlying.Encoding != null)
			{
				sb.Append("/Encoding /");
				sb.Append(_underlying.Encoding);
				sb.Append("\r\n");
			}

			sb.Append("/FontDescriptor ");
			sb.Append(doc.GetReference(_descriptor).Reference);
			sb.Append("\r\n");

			sb.Append("/FirstChar ");
			sb.Append(_underlying.FirstChar);
			sb.Append("\r\n");

			sb.Append("/LastChar ");
			sb.Append(_underlying.LastChar);
			sb.Append("\r\n");

			sb.Append("/Widths ");
			sb.Append(doc.GetReference(_widths).Reference);
			sb.Append("\r\n");

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());

			_descriptor.Write(file, doc);
			//_fontFile?.Write(file,generator); in a Type 1 font there is no font file
			_widths.Write(file, doc, _underlying.UnitsPerEm);

			//	Typically a Type 1 font doesn't need a ToUnicode map because
			//	it uses a standard encoding
			//_toUnicode.Write(file,generator);
		}

		public override string Encode(string text)
		{
			//	Type 1 fonts always encode to single-byte. Technically we
			//	should take the font's actual encoding into account, but
			//	at least for the proof of concept we'll just assume that
			//	the encoding is plain ASCII.
			string escaped = Escape(text);
			byte[] bytes = Encoding.ASCII.GetBytes(escaped);
			string encoded = Encoding.ASCII.GetString(bytes);
			return "(" + encoded + ")";
		}

		/// <param name="end">One past the end.</param>
		public override List<KernFragment> Kern(string text, int start, int end)
		{
			return _underlying.Kern(text, start, end);
		}

		public override void MapCharacter(char c)
		{
			_underlying.MapCharacter(c);
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
