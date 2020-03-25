using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Demon.Font
{
	public interface IFont //TODO: don't think we need this interface
	{
		string FileName { get; }
		string PostScriptName { get; }
		string FamilyName { get; }
		bool Bold { get;}
		bool Italic { get;}
		int UnderlinePosition { get; }
		int UnderlineThickness { get; }

		/// <summary>
		/// In font units
		/// </summary>
		short Ascender { get; }

		/// <summary>
		/// In font units
		/// </summary>
		short Descender { get; }

		/// <summary>
		/// In font units
		/// </summary>
		short XHeight { get; }

		/// <summary>
		/// In font units
		/// </summary>
		short CapHeight { get; }

		/// <summary>
		/// In font units
		/// </summary>
		short LineGap { get; }

		/// <summary>
		/// In font units
		/// </summary>
		ushort UnitsPerEm { get; }

		int GetTextLength(string text, int start, int end, int fontSize);
		int GetDefaultLineSpacing(int fontSize);
		int GetCharWidth(char c);
		int GetAverageWidth(int fontsize);
		int GetMaxWidth(int fontSize);
		int GetAscender(int fontSize);
		int GetDescender(int fontSize);
		int GetXHeight(int fontSize);
		int GetCapHeight(int fontSize);
		int GetUnderlinePosition(int fontSize);
		float GetUnderlineThickness(int fontSize);
		List<KernFragment> Kern(string plain, int start, int end);
		int GetKernAdjustment(char charLeft, char charRight);
		void Subset();
		void MapCharacter(char c);
	}

	public abstract class Font : IFont
	{
		protected string _filename;
		protected string _postScriptName; // e.g. Verdana-Bold
		protected string _familyName; // e.g. Verdana
		public string FileName { get { return _filename; }}
		public string PostScriptName { get { return _postScriptName; }}
		public string FamilyName { get { return _familyName; }}

		protected bool _bold;
		protected bool _italic;

		public virtual bool Bold { get { return _bold; }}
		public  virtual bool Italic { get { return _italic; }}
		public abstract int UnderlinePosition { get; }
		public abstract int UnderlineThickness { get; }

		/// <summary>
		/// In font units
		/// </summary>
		public abstract short Ascender { get; }
		/// <summary>
		/// In font units
		/// </summary>
		public abstract short Descender { get; }
		/// <summary>
		/// In font units
		/// </summary>
		public abstract short XHeight { get; }
		/// <summary>
		/// In font units
		/// </summary>
		public abstract short CapHeight { get; }
		/// <summary>
		/// In font units
		/// </summary>
		public abstract short LineGap { get; }
		/// <summary>
		/// In font units
		/// </summary>
		public abstract ushort UnitsPerEm { get; }

		/// <summary>
		/// The size required to fit the given text, in font units
		/// </summary>
		/// <param name="end">One past the end of the string.</param>
		public int GetTextLength(string text, int start, int end, int fontSize)
		{
			//	Find the length
			int width = 0;
			for(int x = start; x < end; ++x)
				width += GetCharWidth(text[x]);
			width *= fontSize;

			//	Adjust for any kerning. Note that kerning adjustments
			//	are expressed in the negative sense: a positive adjustment
			//	is subtracted from the displacement. So to adjust the
			//	width we add the adjustment, not subtract.
			List<KernFragment> fragments = Kern(text,start,end);
			foreach(KernFragment fragment in fragments)
				width += fragment.Adjust;

			return width;
		}

		/// <summary>
		/// In user/text space units
		/// </summary>
		public int GetDefaultLineSpacing(int fontSize)
		{
			double height = (double)Ascender - (double)Descender + (double)LineGap;
			height *= fontSize;
			height /= UnitsPerEm;
			return (int)height;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public abstract int GetCharWidth(char c);

		/// <summary>
		/// In font units
		/// </summary>
		public abstract int GetAverageWidth(int fontsize);

		/// <summary>
		/// In font units
		/// </summary>
		public abstract int GetMaxWidth(int fontSize);

		/// <summary>
		/// In user space units
		/// </summary>
		public int GetAscender(int fontSize)
		{
			return Ascender * fontSize / UnitsPerEm;
		}

		/// <summary>
		/// In user space units
		/// </summary>
		public int GetDescender(int fontSize)
		{
			double d = (double)Descender;
			double f = (double)fontSize;
			double u = (double)UnitsPerEm;
			double r = Math.Round(d * f / u);
			return (int)r;
		}

		/// <summary>
		/// In user space units
		/// </summary>
		public int GetXHeight(int fontSize)
		{
			return XHeight * fontSize / UnitsPerEm;
		}

		/// <summary>
		/// In user space units
		/// </summary>
		public int GetCapHeight(int fontSize)
		{
			return CapHeight * fontSize / UnitsPerEm;
		}


		/// <summary>
		/// Get the Y offset of the centre of the underline from the text baseline.
		/// </summary>
		public int GetUnderlinePosition(int fontSize)
		{
			//	Get the defined position, in font units. This is the distance from
			//	the baseline to the top of the underline.
			int pos = UnderlinePosition;

			//	We want to return the centre of the underline, not the top
			pos -= (int)(((double)UnderlineThickness) / (double)2);

			//	Convert to user units
			pos = (int)(((double)(pos * fontSize) / (double)UnitsPerEm));

			//	If the position leaves no space (in user units) between the baseline
			//	and the underline then add some space now
			if(pos > -1)
				pos = -1;

			return pos;
		}

		public float GetUnderlineThickness(int fontSize)
		{
			return ((float)(UnderlineThickness * fontSize) / (float)UnitsPerEm);
		}

		/// <param name="end">One past the end.</param>
		public List<KernFragment> Kern(string plain, int start, int end)
		{
			List<KernFragment> fragments = new List<KernFragment>();

			//	Work through the string looking for pairs of characters
			//	for which we have kerning information.
			
			//	We set the adjustment when we discover a kerning pair.
			//	The first fragment will start at the start of the string
			//	and will have zero adjustment.
			KernFragment fragment = new KernFragment();

			int pos = start;
			while(pos < (end - 1)) // -1 to allow for the right-hand character
			{
				char left  = plain[pos];
				char right = plain[pos + 1]; // this is why we said -1 in the loop condition
				int adjust = GetKernAdjustment(left,right);
				if(adjust != 0)
				{
					//	This new adjustment figure is for the next
					//	substring that we'll extract. Right now
					//	fill in the current fragment and store it,
					//	and then start the next fragment.
					fragment.Text = plain.Substring(start, pos-start+1);
					fragments.Add(fragment);

					fragment = new KernFragment();
					fragment.Adjust = adjust;
					fragment.Text = null;
					start = pos + 1;
				}

				++pos;
			}
			//	Add the last fragment
			int len = end > start ? end - start : plain.Length - start;
			fragment.Text = plain.Substring(start, len);
			fragments.Add(fragment);
			return fragments;
		}

		/// <summary>
		/// Get the kerning adjustment between two characters, in font units.
		/// </summary>
		public abstract int GetKernAdjustment(char charLeft, char charRight);

		public abstract void Subset();

		public abstract void MapCharacter(char c);

		public override string ToString()
		{
			string str = _postScriptName;
			if(_bold)
				str += " bold";
			if(_italic)
				str += " italic";
			return str;
		}
	}

	public class FontWidths
	{
		private ushort[] _widths;

		public FontWidths(ushort[] widths)
		{
			_widths = widths;
		}

		public int Count { get {return _widths.Length;}}

		public ushort this[int index]
		{
			get
			{
				return _widths[index];
			}
		}
	}

	public class FontFile
	{
		private byte[] _bits;

		public FontFile(byte[] bits)
		{
			_bits = bits;
		}

		public Stream ReadAllBytes()
		{
			return new MemoryStream(_bits, false);
		}

		public byte ReadByte(ref uint p)
		{
			byte value = _bits[p++];
			return value;
		}

		/// <summary>
		/// Read a two-byte character
		/// </summary>
		public char ReadChar(ref uint p)
		{
			char value = (char)0;
			for(int x = 0; x < 2; ++x)
			{
				value <<= 8;
				value += (char)_bits[p++];
			}
			return value;
		}

		public ushort ReadUShort(ref uint p)
		{
			ushort value = 0;
			for(int x = 0; x < 2; ++x)
			{
				value <<= 8;
				value += _bits[p++];
			}
			return value;
		}

		public short ReadShort(ref uint p)
		{
			short value = 0;
			for(int x = 0; x < 2; ++x)
			{
				value <<= 8;
				value += _bits[p++];
			}
			return value;
		}

		public uint ReadULong(ref uint p)
		{
			uint value = 0;
			for(int x = 0; x < 4; ++x)
			{
				value <<= 8;
				value += _bits[p++];
			}
			return value;
		}

		public ulong ReadLongDateTime(ref uint p)
		{
			ulong value = 0;
			for(int x = 0; x < 8; ++x)
			{
				value <<= 8;
				value += _bits[p++];
			}
			return value;
		}

		/// <summary>
		/// Type "Fixed" means fixed-point 16.16
		/// </summary>
		public float ReadFixed(ref uint p)
		{
			float integral = (float)ReadUShort(ref p);
			float partial = (float)ReadUShort(ref p);
			float value = integral + (partial != 0 ? (1 / partial) : 0);
			return value;
		}

		public float ReadFixedVersion(ref uint p)
		{
			int major = ReadUShort(ref p);
			int minor = ReadUShort(ref p);
			string s = string.Format("{0}.{1}",major,minor);
			float version = 0.0f;
			float.TryParse(s, out version);
			return version;
		}

		public string ReadStringUcs2(ref uint p, int len)
		{
			string value = "";
			for(int x = 0; x < len; x += 2)
			{
				byte b1 = _bits[p++];
				byte b2 = _bits[p++];
				char c = (char)b1;
				c <<= 8;
				c += (char)b2;
				value += c;
			}
			return value;
		}

		public string ReadStringAscii(ref uint p, int len)
		{
			string value = "";
			for(int x = 0; x < len; ++x)
				value += (char)_bits[p++];
			return value;
		}

		public static uint ReadUInt32(byte[] buf, uint offset)
		{
			uint value = 0;
			for(int x = 0; x < 4; ++x)
			{
				value <<= 8;
				value += buf[offset + x];
			}
			return value;
		}

		public static ushort ReadUInt16(byte[] buf, uint offset)
		{
			ushort value = 0;
			for(int x = 0; x < 2; ++x)
			{
				value <<= 8;
				value += buf[offset + x];
			}
			return value;
		}

		public static string ReadStringAscii(byte[] buf, uint offset, uint len)
		{
			StringBuilder value = new StringBuilder((int)len);
			for(int x = 0; x < len; ++x)
				value.Append((char)buf[offset + x]);
			return value.ToString();
		}

		public static string ReadStringUcs2(byte[] buf, uint offset, uint len)
		{
			StringBuilder value = new StringBuilder((int)len);
			for(int x = 0; x < len; x += 2)
			{
				byte b1 = buf[offset + x];
				byte b2 = buf[offset + x + 1];
				char c = (char)b1;
				c <<= 8;
				c += (char)b2;
				value.Append(c);
			}
			return value.ToString();
		}
	}

	/// <summary>
	/// This class implements the character-code-to-glyph-index mapping. It could
	/// be extended to support font subsetting because it contains the set of all
	/// character codes used by the font.
	/// </summary>
	public class CharMap : Dictionary<char, ushort>, ICharMap
	{
	}

	public interface ICharMap : IDictionary<char, ushort>
	{
	}

	[Flags]
	public enum FontEmbeddingLicenseFlags : ushort
	{
		Installable  = 0x0000,
		Restricted   = 0x0002,
		PreviewPrint = 0x0004,
		Editable     = 0x0008,
		NoSubsetting = 0x0100,
		BitmapOnly   = 0x0200
	}

	public class FontInfo
	{
		private string _familyName;
		private string _faceName;
		private string _filename;
		private FontType _type;
		private bool _bold;
		private bool _italic;
		private bool _underline;
		private bool _strikeout;

		public string FaceName { get { return _faceName; } }
		public string FamilyName { get { return _familyName; }}
		public string FileName { get { return _filename; } }
		public FontType Type { get { return _type; } }
		public bool Bold { get { return _bold; } }
		public bool Italic { get { return _italic; }}
		public bool Underline { get { return _underline; }}
		public bool StrikeOut { get { return _strikeout; }}

		public FontInfo(string familyName, string faceName, string filename, FontType type,
						bool bold, bool italic, bool underline, bool strikeout)
		{
			_familyName = familyName;
			_faceName = faceName;
			_filename = filename;
			_type = type;
			_bold = bold;
			_italic = italic;
			_underline = underline;
			_strikeout = strikeout;
		}
	}

	public enum FontType
	{
		Unknown,
		Type1,
		TrueType
	}

	public class KernFragment
	{
		public int Adjust;
		public string Text;
	}

	public class BoundingBox
	{
		public int Left;
		public int Bottom;
		public int Right;
		public int Top;

		public BoundingBox(int left, int bottom, int right, int top)
		{
			Left   = left;
			Bottom = bottom;
			Right  = right;
			Top    = top;
		}
	}
}
