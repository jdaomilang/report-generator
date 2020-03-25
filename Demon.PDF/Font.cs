using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.IO.Compression;
using Demon.Font;

namespace Demon.PDF
{
	public abstract class Font : IIndirectObject
	{
		public abstract string FamilyName { get; }
		public abstract bool Bold { get; }
		public abstract bool Italic { get; }

		protected string _alias; // e.g. /F1
		public virtual string Alias { get { return _alias; } set { _alias = value; }}

		public abstract void Write(Stream file, Document doc);

		public abstract List<KernFragment> Kern(string text, int start, int end);
		public abstract void MapCharacter(char c);

		/// <summary>
		/// Encode a text string in the font's encoding
		/// </summary>
		public abstract string Encode(string text);

		protected static string Escape(string raw)
		{
			string escaped = raw
				.Replace( "\\", "\\\\" )
				.Replace( "(",   "\\(" )
				.Replace( ")",   "\\)" );
			return escaped;
		}

		public abstract int GetCapHeight(int fontSize);
		public abstract int GetTextLength(string text, int start, int end, int fontSize);
		public abstract ushort UnitsPerEm { get; }
	}

	public class FontDescriptor : IIndirectObject
	{
		private string _fontName;
		private ushort _flags;
		private float _italicAngle;
		private short _ascent;
		private short _descent;
		private short _capHeight;
		private short _avgWidth;
		private ushort _maxWidth;
		private ushort _fontWeight;
		private short _xHeight;
		private ushort _stemV;
		private Rectangle _bbox;
		private FontStream _fontFile;

		public int Ascent { get { return _ascent; } }
		public int Descent { get { return _descent; } }
		public int CapHeight { get { return _capHeight; } }
		public int AverageWidth { get { return _avgWidth; } }
		public int MaxWidth { get { return _maxWidth; } }

		public FontDescriptor(string fontName, ushort flags,
								FontStream fontFile,
								float italicAngle, short ascent, short descent,
								short capHeight, short avgWidth, ushort maxWidth,
								ushort fontWeight, short xHeight, ushort stemV,
								BoundingBox bbox)
		{
			_fontName = fontName;
			_flags = flags;
			_fontFile = fontFile;
			_italicAngle = italicAngle;
			_ascent = ascent;
			_descent = descent;
			_capHeight = capHeight;
			_avgWidth = avgWidth;
			_maxWidth = maxWidth;
			_fontWeight = fontWeight;
			_xHeight = xHeight;
			_stemV = stemV;
			_bbox = new Rectangle(bbox);
		}

		public void Write(Stream file, Document doc)
		{
			ObjectReference descref = doc.GetReference(this);
			descref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(descref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<< /Type /FontDescriptor\r\n");
			sb.Append("/FontName /");
			sb.Append(_fontName);
			sb.Append("\r\n");
			
			sb.Append("/Flags ");
			sb.Append(_flags);
			sb.Append("\r\n");

			sb.Append("/ItalicAngle ");
			sb.Append(_italicAngle);
			sb.Append("\r\n");

			sb.Append("/Ascent ");
			sb.Append(_ascent);
			sb.Append("\r\n");

			sb.Append("/Descent ");
			sb.Append(_descent);
			sb.Append("\r\n");

			sb.Append("/CapHeight ");
			sb.Append(_capHeight);
			sb.Append("\r\n");

			sb.Append("/AvgWidth ");
			sb.Append(_avgWidth);
			sb.Append("\r\n");

			sb.Append("/MaxWidth ");
			sb.Append(_maxWidth);
			sb.Append("\r\n");

			sb.Append("/FontWeight ");
			sb.Append(_fontWeight);
			sb.Append("\r\n");

			sb.Append("/XHeight ");
			sb.Append(_xHeight);
			sb.Append("\r\n");

			sb.Append("/StemV ");
			sb.Append(_stemV);
			sb.Append("\r\n");

			sb.Append("/FontBBox ");
			sb.Append(_bbox.Specification);
			sb.Append("\r\n");

			if(_fontFile != null)
			{
				//TODO: consider compressing the font file

				sb.Append("/FontFile2 ");
				sb.Append(doc.GetReference(_fontFile).Reference);
				sb.Append("\r\n");
			}

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());
		}
	}

	/// <summary>
	/// Puts an IIndirectObject interface on Demon.Font.FontWidths.
	/// </summary>
	public class FontWidths : IIndirectObject
	{
		Demon.Font.FontWidths _underlying;

		public FontWidths(Demon.Font.FontWidths underlying)
		{
			_underlying = underlying;
		}

		public void Write(Stream file, Document doc, ushort unitsPerEm)
		{
			ObjectReference wref = doc.GetReference(this);
			wref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(wref.Id);
			sb.Append(" obj\r\n[ ");
			for(int x = 0; x < _underlying.Count; ++x)
			{
				//	The raw widths are in font units, but we want thousandths
				//	of a text unit in the dictionary. See page 414.
				sb.Append(_underlying[x] * 1000 / unitsPerEm);
				sb.Append(" ");
			}
			sb.Append("]\r\nendobj\r\n");
			Document.WriteData(file,sb.ToString());
		}

		public int Count { get { return _underlying.Count; }}
		public ushort this[int index] { get { return _underlying[index]; }}
	}

	/// <summary>
	/// Puts an IIndirectObject interface on Demon.Font.FontFile.
	/// </summary>
	public class FontStream : IIndirectObject
	{
		private Stream _bits;

		public FontStream(Stream bits)
		{
			_bits = bits;
		}

		public void Write(Stream file, Document doc)
		{
			ObjectReference fileref = doc.GetReference(this);
			fileref.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(fileref.Id);
			sb.Append(" obj\r\n<<");
			sb.Append("/Filter /FlateDecode\r\n");
			
			Stream compressed = Compression.Compress(_bits);

			sb.Append("/Length ");
			sb.Append(compressed.Length);
			sb.Append(" /Length1 ");
			sb.Append(compressed.Length);
			sb.Append(">>\r\n");

			sb.Append("stream\r\n");
			Document.WriteData(file, sb.ToString());

			compressed.CopyTo(file);
			
			sb.Clear();
			sb.Append("\r\nendstream\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());
		}
	}

	/// <summary>
	/// Used in the generation phase
	/// </summary>
	public class FontReference
	{
		private ObjectReference _fontref;
		public FontReference(ObjectReference fontref)
		{
			_fontref = fontref;
		}
		public string AliasReference
		{
			get
			{
				Font font = _fontref.Object as Font;
				return "/" + font.Alias + " " + _fontref.Reference;
			}
		}
	}

	public class ToUnicode : Dictionary<ushort, char>, IIndirectObject
	{
		public void Write(Stream file, Document doc)
		{
			if(this.Count == 0) return;

			ObjectReference uniref = doc.GetReference(this);
			uniref.ByteOffset = file.Position;

			//	Prepare the stream so that we can find its length.
			//	The /Length value must be the length of the stream
			//	contents, starting after "stream\r\n" and ending
			//	before "\r\nendstream".
			StringBuilder sb = new StringBuilder();
			sb.Append("/CIDInit /ProcSet findresource\r\n");
			sb.Append("begin\r\n");
			sb.Append("12 dict\r\n");
			sb.Append("begin\r\n");
			sb.Append("begincmap\r\n");
			sb.Append("/CIDSystemInfo<</Registry(Adobe)/Ordering(UCS)/Supplement 0>>\r\n");
			sb.Append("def\r\n");
			sb.Append("/CMapName /Adobe-Identity-UCS\r\n");
			sb.Append("def\r\n");
			sb.Append("/CMapType 2\r\n");
			sb.Append("def\r\n");
			
			List<ushort> indices = new List<ushort>(this.Keys);
			indices.Sort();
			sb.Append("1 begincodespacerange\r\n");
			sb.Append("<");
			sb.Append(Helpers.Hex(indices[0]));
			sb.Append("><");
			sb.Append(Helpers.Hex(indices[indices.Count-1]));
			sb.Append(">\r\n");
			sb.Append("endcodespacerange\r\n");

//			sb.Append("2 beginbfrange\r\n");
//			< 0000 >< 005E >< 0020 >
//			< 005F >< 0061 >[ < 00660066 > < 00660069 > < 00660066006C > ]
//			sb.Append("endbfrange\r\n");
			
			//TODO: collect the mappings into contiguous ranges and encode
			//them more efficiently with beginbfrange/endbfrange
			foreach(ushort glyphIndex in indices)
			{
				sb.Append("1 beginbfchar\r\n");
				sb.Append("<");
				sb.Append(Helpers.Hex(glyphIndex));
				sb.Append("><");
				sb.Append(Helpers.Hex(this[glyphIndex]));
				sb.Append(">\r\n");
				sb.Append("endbfchar\r\n");
			}
			
			sb.Append("endcmap\r\n");
			sb.Append("CMapName currentdict /CMap defineresource pop\r\n");
			sb.Append("end\r\n");
			sb.Append("end");
			
			byte[] stream = Encoding.UTF8.GetBytes(sb.ToString());

			//	Write the stream to the PDF
			sb.Clear();
			sb.Append(uniref.Id);
			sb.Append(" obj\r\n");
			sb.Append("<</Length ");
			sb.Append(stream.Length);
			sb.Append(">>\r\n");
			sb.Append("stream\r\n");
			Document.WriteData(file, sb.ToString());
		
			file.Write(stream, 0, stream.Length);

			sb.Clear();
			sb.Append("\r\nendstream\r\n");
			sb.Append("\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());
		}
	}
}
