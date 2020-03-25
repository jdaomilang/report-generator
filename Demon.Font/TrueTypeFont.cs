using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Demon.Font
{
	public class TrueTypeFont : Font
	{
		private FontFile _fontFile;
		private FontWidths _widths;
		private BoundingBox _bbox;
		private Type0Font _root;
		private short[] _lsbs;
		private HeadTable _head;
		private HheaTable _hhea;
		private CmapTable _cmap;
		private MaxpTable _maxp;
		private KernTable _kern;
		private NameTable _names;
		private Os2Table _os2;
		private PostTable _post;
		private ushort _flags;
		private CharMap _glyphIndices;

		public FontFile FontFile { get { return _fontFile;} }
		public FontWidths Widths { get { return _widths; } }
		public override ushort UnitsPerEm { get { return _head.UnitsPerEm; } }
		public override short Ascender { get { return _os2.TypoAscender; } }
		public override short Descender { get { return _os2.TypoDescender; } }
		public override short XHeight { get { return _os2.XHeight; } }
		public override short CapHeight
		{
			get
			{
				if(_os2.Version >= 2)
					return _os2.CapHeight;
				else
					return (short)(_os2.TypoAscender - _os2.TypoDescender);
			}
		}
		public short XAvgCharWidth { get { return _os2.XAvgCharWidth; }}
		public ushort AdvanceWidthMax { get { return _hhea.AdvanceWidthMax; }}
		public ushort WeightClass { get { return _os2.WeightClass; }}
		public BoundingBox BoundingBox { get { return _bbox; }}
		public override short LineGap { get { return _os2.TypoLineGap; } }
		public override int UnderlinePosition { get { return _post.UnderlinePosition; }}
		public override int UnderlineThickness { get { return _post.UnderlineThickness; }}
		public float ItalicAngle { get { return _post.ItalicAngle; }}
		public ushort Flags { get { return _flags; }}
		public bool IsFixedPitch { get { return _post.IsFixedPitch != 0; }}
		public ICharMap GlyphIndices { get { return _glyphIndices; }}
		public ushort FirstCharIndex { get { return _os2.FirstCharIndex; }}
		public ushort LastCharIndex { get { return _os2.LastCharIndex; }}
		public uint[] UnicodeRanges { get { return _os2.UnicodeRanges; }}
		public uint[] CodePageRanges { get { return _os2.CodePageRanges; }}
		public byte[] Panose
		{
			get
			{
				byte[] panose = new byte[10];
				panose[0] = _os2.Panose.FamilyType;
				panose[1] = _os2.Panose.SerifStyle;
				panose[2] = _os2.Panose.Weight;
				panose[3] = _os2.Panose.Proportion;
				panose[4] = _os2.Panose.Contrast;
				panose[5] = _os2.Panose.StrokeVariation ;
				panose[6] = _os2.Panose.ArmStyle;
				panose[7] = _os2.Panose.Letterform;
				panose[8] = _os2.Panose.Midline;
				panose[9] = _os2.Panose.XHeight;
				return panose;
			}
		}
		public Type0Font RootFont { get { return _root;}}


		public TrueTypeFont(string filename)
		{
			_root = new Type0Font(this);

			_glyphIndices = new CharMap();

			_filename = filename;
			byte[] bits = File.ReadAllBytes(filename);
			_fontFile = new FontFile(bits);
			Load();
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetCharWidth(char c)
		{
			ushort glyphIndex = GetGlyphIndex(c);

			//	If the number of widths is less than the number of glyphs, then
			//	the last width applies to all glyphs after that. This is common
			//	in mono-spaced fonts, but can also apply in variable-pitch fonts.
			if(glyphIndex >= _widths.Count)
				glyphIndex = (ushort)(_widths.Count - 1);
			
			int width = _widths[glyphIndex];
			return width;
		}

		public virtual ushort GetGlyphIndex(char c)
		{
			ushort glyphIndex = 0;
			bool found = _glyphIndices.TryGetValue(c, out glyphIndex);
			if(found) return glyphIndex;

			//	1.	In the font's cmap table find the subtable appropriate to our
			//			platform. Windows UCS-2 is (3,1). For now we assume that the
			//			cmap is in format 4 "segment mapping to delta values".
			//
			//	2.	In the subtable find the segment that contains the character
			//			code in question. Each segment has a start character code and
			//			an end character code, and the segments are ordered by ascending
			//			end character code.
			//
			//	3.	Find the character code in the segment. Take note of its index
			//			in the segment - call this the character code offset.
			//
			//	4.	If the segment range offset is zero, add the segment delta to
			//			to the character code. This gives you the glyph index.
			//
			//	5.	If the segment range offset is not zero then apply this
			//			formula:
			//
			//			-	Go to the start of the range offset array.
			//			-	Move up to the selected segment's location within that array.
			//			-	Add the range offset value.
			//			-	Add the character code offset.
			//			-	The two-byte value at this location is the glyph index.
			//
			//	We've already found the subtable and the segments when we loaded
			//	the cmap, so steps 1 and 2 are already done.

			CmapSegment segment = null;
			uint rangeLength = 0;
			char start = (char)0;
			int numSegments = _cmap.SubTable.SegCountX2 / 2;
			for(int x = 0; x < numSegments; ++x)
			{
				char end = (char)_cmap.SubTable[x].End;
				if(end >= c)
				{
					start = (char)_cmap.SubTable[x].Start;
					if(start > c)
						return 0; // character not in this font
					segment = _cmap.SubTable[x];
					rangeLength = (uint)(end - start + 1);
					break;
				}
			}
			if(segment == null)
				throw new Exception("Didn't find glyph index, and not .notdef either.");

			if(segment.IdRangeOffset == 0)
			{
				//	The documentation says "If the idRangeOffset is 0, the idDelta value
				//	is added directly to the character code offset (i.e. idDelta[i] + c)".
				//	The text says to add the "character code offset" but the clarification
				//	"i.e. idDelta[i] + c" says to add the character code itself. Our
				//	testing shows the clarification to be correct and the text to be wrong.
				glyphIndex = (ushort)(segment.IdDelta + c);
			}
			else
			{
				//	The True Type documentation for cmap gives this C expression
				//	for finding the glyph index:
				//
				//		*(idRangeOffset[i]/2 
				//		+ (c - startCount[i]) 
				//		+ &idRangeOffset[i])
				int idIndex = -numSegments + segment.Index + (segment.IdRangeOffset / 2) + c - segment.Start;
				glyphIndex = _cmap.SubTable.GlyphIdArray[idIndex];

				//	The documentation says "If the value obtained from the indexing
				//	operation is not 0 (which indicates missingGlyph), idDelta[i] is
				//	added to it to get the glyph index." In the sample verdana.ttf
				//	that I've been using, and in a few others that I checked, there
				//	is no range where the range offset is non-zero and the delta is
				//	also non-zero. That is, if delta is zero then range offset is
				//	non-zero, and vice versa. So if we get here then the delta is
				//	always zero, but let's follow the spec anyway in case some
				//	other font is different.
				if(glyphIndex != 0)
					glyphIndex += (ushort)segment.IdDelta;
			}

			_glyphIndices.Add(c, glyphIndex);
			return glyphIndex;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetAverageWidth(int fontsize)
		{
			int avg = _os2.XAvgCharWidth * fontsize;
			return avg;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetMaxWidth(int fontSize)
		{
			int max = _hhea.AdvanceWidthMax * fontSize;
			return max;
		}

		/// <summary>
		/// Get the kern adjustment in text units.
		/// </summary>
		public override int GetKernAdjustment(char charLeft, char charRight)
		{
			if(_kern == null) return 0;
			if(_kern.NumTables == 0) return 0;

			ushort glyphIndexLeft = GetGlyphIndex(charLeft);
			ushort glyphIndexRight = GetGlyphIndex(charRight);
			int value = 0;
			for(ushort x = 0; x < _kern.NumTables; ++x)
			{
				if(_kern.Subtables[x].Version == 0)
				{
					KernFormat0 sub0 = (KernFormat0)_kern.Subtables[x];
					value = sub0.GetKernAdjustment(glyphIndexLeft,glyphIndexRight);
					if(value != 0) break;
				}
			}
			return value;
		}

		protected virtual void Load()
		{
			uint p = 0; // byte pointer into the file
			uint fontVersion     = _fontFile.ReadULong (ref p);
			ushort numFontTables = _fontFile.ReadUShort(ref p);
			ushort searchRange   = _fontFile.ReadUShort(ref p);
			ushort entrySelector = _fontFile.ReadUShort(ref p);
			ushort rangeShift    = _fontFile.ReadUShort(ref p);

			//	Load the various tables that we're interested in.
			//	We have to load some tables before others. For example:
			//
			//		-	several tables depend on head.unitsPerEm
			//		-	hmtx depends on hhea.numberOfHMetrics.
			//		-	hmtx depends on maxp.numGlyphs
			//		-	post depends on maxp.numGlyphs
			//		- cmap depends on name (for exception text)
			//
			uint startTables = p;
			uint headOffset = FindTableOffset("head", startTables, numFontTables);
			uint maxpOffset = FindTableOffset("maxp", startTables, numFontTables);
			uint hheaOffset = FindTableOffset("hhea", startTables, numFontTables);
			uint hmtxOffset = FindTableOffset("hmtx", startTables, numFontTables);
			uint cmapOffset = FindTableOffset("cmap", startTables, numFontTables);
			uint os2Offset  = FindTableOffset("OS/2", startTables, numFontTables);
			uint postOffset = FindTableOffset("post", startTables, numFontTables);
			uint nameOffset = FindTableOffset("name", startTables, numFontTables);
			uint kernOffset = FindTableOffset("kern", startTables, numFontTables);
			LoadHead(headOffset);
			LoadMaxp(maxpOffset);
			LoadPost(postOffset);
			LoadHhea(hheaOffset);
			LoadHmtx(hmtxOffset);
			LoadKern(kernOffset);
			LoadOs2(os2Offset);
			LoadName(nameOffset);
			LoadCmap(cmapOffset);

			_flags = 0;
			if(_post.IsFixedPitch != 0) _flags |= 0x0001;

			//	https://www.microsoft.com/typography/otspec/ibmfc.htm
			//	There are several serif classes: 1, 2, 3, 4, 5, 7. Class
			//	8 is sans serif.
			bool serif = false;
#pragma warning disable 0219 // variable assigned but never used
			bool script = false;
#pragma warning restore 0219
			byte cls = (byte)(_os2.FamilyClass >> 8);
			switch(cls)
			{
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 7:
					serif = true;
					break;
				case 8:
					serif = false;
					break;
				case 10:
					script = true;
					break;
			}
			byte subclass = (byte)_os2.FamilyClass;

			if(serif) _flags |= 0x0002;
			_flags |= 0x0004; // symbolic - not restricted to the Latin character set
			//TODO: script/cursive
			if(_post.ItalicAngle != 0.0) _flags |= 0x40;
			//TODO: all cap
			//TODO: small cap
			//TODO: force bold
		}

		private uint FindTableOffset(string name, uint startTables, ushort numTables)
		{
			uint p = startTables;
			for(ushort x = 0; x < numTables; ++x)
			{
				string tag    = _fontFile.ReadStringAscii(ref p,4);
				uint checksum = _fontFile.ReadULong(ref p);
				uint start    = _fontFile.ReadULong(ref p);
				uint length   = _fontFile.ReadULong(ref p);
				//	Have to read all elements of the table, to advance p

				if(tag == name)
					return start;
			}
			return 0;
		}
		
		private void LoadMaxp(uint fileOffset)
		{
			uint p = fileOffset;
			float version = _fontFile.ReadFixedVersion(ref p);
			_maxp = new MaxpTable(version);
			_maxp.NumGlyphs             = _fontFile.ReadUShort(ref p);
			_maxp.MaxPoints             = _fontFile.ReadUShort(ref p);
			_maxp.MaxContours           = _fontFile.ReadUShort(ref p);
			_maxp.MaxCompositePoints    = _fontFile.ReadUShort(ref p);
			_maxp.MaxCompositeContours  = _fontFile.ReadUShort(ref p);
			_maxp.MaxZones              = _fontFile.ReadUShort(ref p);
			_maxp.MaxTwilightPoints     = _fontFile.ReadUShort(ref p);
			_maxp.MaxStorage            = _fontFile.ReadUShort(ref p);
			_maxp.MaxFunctionDefs       = _fontFile.ReadUShort(ref p);
			_maxp.MaxInstructionDefs    = _fontFile.ReadUShort(ref p);
			_maxp.MaxStackElements      = _fontFile.ReadUShort(ref p);
			_maxp.MaxSizeOfInstructions = _fontFile.ReadUShort(ref p);
			_maxp.MaxComponentElements  = _fontFile.ReadUShort(ref p);
			_maxp.MaxComponentDepth     = _fontFile.ReadUShort(ref p);
		}

		private void LoadOs2(uint fileOffset)
		{
			//	Some of these measures are defined as signed short in the OS/2 table
			//	but their counterparts everywhere else are unsigned short, so
			//	here we convert to unsigned.

			uint p = fileOffset;
			ushort version = _fontFile.ReadUShort(ref p);
			_os2 = new Os2Table(version);

			_os2.XAvgCharWidth = _fontFile.ReadShort(ref p);
			_os2.WeightClass   = _fontFile.ReadUShort(ref p);
			_os2.WidthClass    = _fontFile.ReadUShort(ref p);
			
			_os2.Type = (FontEmbeddingLicenseFlags)_fontFile.ReadUShort(ref p);
			
			_os2.YSubscriptXSize     = _fontFile.ReadShort(ref p);
			_os2.YSubscriptYSize     = _fontFile.ReadShort(ref p);
			_os2.YSubscriptXOffset   = _fontFile.ReadShort(ref p);
			_os2.YSubscriptYOffset   = _fontFile.ReadShort(ref p);
			_os2.YSuperscriptXSize   = _fontFile.ReadShort(ref p);
			_os2.YSuperscriptYSize   = _fontFile.ReadShort(ref p);
			_os2.YSuperscriptXOffset = _fontFile.ReadShort(ref p);
			_os2.YSuperscriptYOffset = _fontFile.ReadShort(ref p);
			_os2.YStrikeoutSize      = _fontFile.ReadShort(ref p);
			_os2.YStrikeoutPosition  = _fontFile.ReadShort(ref p);

			_os2.FamilyClass = _fontFile.ReadShort(ref p);
			
			_os2.Panose.FamilyType      = _fontFile.ReadByte(ref p);
			_os2.Panose.SerifStyle      = _fontFile.ReadByte(ref p);
			_os2.Panose.Weight          = _fontFile.ReadByte(ref p);
			_os2.Panose.Proportion      = _fontFile.ReadByte(ref p);
			_os2.Panose.Contrast        = _fontFile.ReadByte(ref p);
			_os2.Panose.StrokeVariation = _fontFile.ReadByte(ref p);
			_os2.Panose.ArmStyle        = _fontFile.ReadByte(ref p);
			_os2.Panose.Letterform      = _fontFile.ReadByte(ref p);
			_os2.Panose.Midline         = _fontFile.ReadByte(ref p);
			_os2.Panose.XHeight         = _fontFile.ReadByte(ref p);
			//_xHeight = panoseXHeight;

			for(int x = 0; x < 4; ++x)
				_os2.UnicodeRanges[x] = _fontFile.ReadULong(ref p);
			
			for(int x = 0; x < 4; ++x)
				_os2.VendId[x] = _fontFile.ReadByte(ref p);
			
			_os2.Selection      = _fontFile.ReadUShort(ref p);
			_os2.FirstCharIndex = _fontFile.ReadUShort(ref p);
			_os2.LastCharIndex  = _fontFile.ReadUShort(ref p);

			//	These figures are not quite the same thing as their namesakes
			//	in the hhea table. Not sure which to use.
			//	For line spacing, use this ascender, descender and linegap, not
			//	the corresponding figures in hhea.
			_os2.TypoAscender  = _fontFile.ReadShort(ref p);
			_os2.TypoDescender = _fontFile.ReadShort(ref p);
			_os2.TypoLineGap   = _fontFile.ReadShort(ref p);
			_os2.WinAscent     = _fontFile.ReadUShort(ref p);
			_os2.WinDescent    = _fontFile.ReadUShort(ref p);

			//	Version 0 ends here
			if(_os2.Version == 0) return;

			for(int x = 0; x < 2; ++x)
				_os2.CodePageRanges[x] = _fontFile.ReadULong(ref p);
			
			//	Version 1 ends here
			if(_os2.Version == 1) return;

			_os2.XHeight   = _fontFile.ReadShort(ref p);
			_os2.CapHeight = _fontFile.ReadShort(ref p);

			_os2.DefaultChar = _fontFile.ReadUShort(ref p);
			_os2.BreakChar   = _fontFile.ReadUShort(ref p);

			_os2.MaxContext = _fontFile.ReadUShort(ref p);

			//	The version 2 table stops here. Later versions have other fields
			//	after this point, but we're not interested in them.
		}

		private void LoadPost(uint fileOffset)
		{
			uint p = fileOffset;
			float version = _fontFile.ReadFixedVersion(ref p);
			_post = new PostTable(version,_maxp.NumGlyphs);

			_post.ItalicAngle        = _fontFile.ReadFixed(ref p);
			_post.UnderlinePosition  = _fontFile.ReadShort(ref p);
			_post.UnderlineThickness = _fontFile.ReadShort(ref p);

			_post.IsFixedPitch = _fontFile.ReadULong(ref p);
			
			_post.MinMemType42 = _fontFile.ReadULong(ref p);
			_post.MaxMemType42 = _fontFile.ReadULong(ref p);
			_post.MinMemType1  = _fontFile.ReadULong(ref p);
			_post.MaxMemType1  = _fontFile.ReadULong(ref p);
			
			//	A version 3 post table can omit the glyph names. Let's try that.
		}

		private void LoadHead(uint fileOffset)
		{
			_head = new HeadTable();
			uint p = fileOffset;

			_head.MajorVersion = _fontFile.ReadUShort(ref p);
			_head.MinorVersion = _fontFile.ReadUShort(ref p);
			_head.FontRevision = _fontFile.ReadFixedVersion(ref p);
			_head.ChecksumAdjustment= _fontFile.ReadULong(ref p);
			_head.MagicNumber = _fontFile.ReadULong(ref p);
			
			//	These TrueType font flags are not the same thing
			//	as PDF font descriptor flags
			_head.Flags = _fontFile.ReadUShort(ref p);
			
			_head.UnitsPerEm = _fontFile.ReadUShort(ref p);
			
			_head.Created  = _fontFile.ReadLongDateTime(ref p);
			_head.Modified = _fontFile.ReadLongDateTime(ref p);

			//	The documentation doesn't say that these figures are
			//	in font units, but it seems to make sense that they
			//	would be, and an example PDF produced by Word seems
			//	to think so too.
			_head.XMin = _fontFile.ReadShort(ref p);
			_head.YMin = _fontFile.ReadShort(ref p);
			_head.XMax = _fontFile.ReadShort(ref p);
			_head.YMax = _fontFile.ReadShort(ref p);

			
			_head.MacStyle = _fontFile.ReadUShort(ref p);
			//	Italic seems to be indicated in a few places:
			//	here in head.macStyle, in post.italicAngle, and
			//	in os2.fsSelection. The documentation says that
			//	OS/2 fsSelection is the correct place to read
			//	bold and italic on Windows: see the comment near
			//	the end of the page at
			//	https://www.microsoft.com/typography/otspec/head.htm
			_bold          = (_head.MacStyle & 0x0001) != 0;
			_italic        = (_head.MacStyle & 0x0002) != 0;
			bool underline = (_head.MacStyle & 0x0004) != 0;
			bool outline   = (_head.MacStyle & 0x0008) != 0;
			bool shadow    = (_head.MacStyle & 0x0010) != 0;
			bool condensed = (_head.MacStyle & 0x0020) != 0;
			bool extended  = (_head.MacStyle & 0x0040) != 0;

			_head.LowestRecPPEm     = _fontFile.ReadUShort(ref p);
			_head.FontDirectionHint = _fontFile.ReadShort(ref p);
			_head.IndexToLocFormat  = _fontFile.ReadShort(ref p);
			_head.GlyphDataFormat   = _fontFile.ReadShort(ref p);
			
			_bbox = new BoundingBox(_head.XMin, _head.YMin, _head.XMax, _head.YMax);
		}

		private void LoadHhea(uint fileOffset)
		{
			_hhea = new HheaTable();
			uint p = fileOffset;

			_hhea.MajorVersion = _fontFile.ReadUShort(ref p);
			_hhea.MinorVersion = _fontFile.ReadUShort(ref p);
			
			_hhea.Ascender            = _fontFile.ReadShort(ref p);
			_hhea.Descender           = _fontFile.ReadShort(ref p);
			_hhea.LineGap             = _fontFile.ReadShort(ref p);
			_hhea.AdvanceWidthMax     = _fontFile.ReadUShort(ref p);
			_hhea.MinLeftSideBearing  = _fontFile.ReadShort(ref p);
			_hhea.MinRightSideBearing = _fontFile.ReadShort(ref p);
			_hhea.XMaxExtent          = _fontFile.ReadShort(ref p);
			
			_hhea.CaretSlopeRise = _fontFile.ReadShort(ref p);
			_hhea.CaretSlopeRun  = _fontFile.ReadShort(ref p);
			_hhea.CaretOffset    = _fontFile.ReadShort(ref p);

			p += 8; // reserved

			_hhea.MetricDataFormat = _fontFile.ReadShort(ref p);
			_hhea.NumberOfHMetrics = _fontFile.ReadUShort(ref p);
		}

		private void LoadCmap(uint fileOffset)
		{
			uint p = fileOffset;
			ushort version = _fontFile.ReadUShort(ref p);
			_cmap = new CmapTable(version);
			
			CmapEncodingRecord encoding = null;
			_cmap.NumTables = _fontFile.ReadUShort(ref p);
			for(int x = 0; x < _cmap.NumTables; ++x)
			{
				_cmap.Subtables[x].PlatformId = _fontFile.ReadUShort(ref p);
				_cmap.Subtables[x].EncodingId = _fontFile.ReadUShort(ref p);
				_cmap.Subtables[x].Offset     = _fontFile.ReadULong(ref p);

				if((_cmap.Subtables[x].PlatformId == 3) && (_cmap.Subtables[x].EncodingId == 1))
				{
					//	We're only interested in the format 4 table
					encoding = _cmap.Subtables[x];
					break;
				}
			}
			if(encoding == null) throw new Exception($"Cmap subtable (3,1) not found in font {_familyName}.");

			//	Jump to the segment
			p = fileOffset + encoding.Offset;
			ushort format = _fontFile.ReadUShort(ref p);
			CmapSegmentTable subtable = new CmapSegmentTable(format);
			_cmap.SubTable = subtable;
			subtable.Length        = _fontFile.ReadUShort(ref p);
			subtable.Language      = _fontFile.ReadUShort(ref p);
			subtable.SegCountX2    = _fontFile.ReadUShort(ref p);
			subtable.SearchRange   = _fontFile.ReadUShort(ref p);
			subtable.EntrySelector = _fontFile.ReadUShort(ref p);
			subtable.RangeShift    = _fontFile.ReadUShort(ref p);

			int numSegments = subtable.SegCountX2 / 2;
			for(ushort x = 0; x < numSegments; ++x)
				subtable[x].Index = x;
			for(int x = 0; x < numSegments; ++x)
				subtable[x].End = _fontFile.ReadUShort(ref p);
			p += 2; // padding
			for(int x = 0; x < numSegments; ++x)
				subtable[x].Start = _fontFile.ReadUShort(ref p);
			for(int x = 0; x < numSegments; ++x)
				subtable[x].IdDelta = _fontFile.ReadUShort(ref p);
			for(int x = 0; x < numSegments; ++x)
				subtable[x].IdRangeOffset = _fontFile.ReadUShort(ref p);

			//	The number of ids in the id array is the sum of the numbers
			//	of codes in each range with a non-zero range offset
			int numGlyphIds = 0;
			foreach(CmapSegment segment in subtable)
				if(segment.IdRangeOffset > 0)
					numGlyphIds += segment.End - segment.Start + 1;
			subtable.GlyphIdArray = new ushort[numGlyphIds];
			for(int x = 0; x < numGlyphIds; ++x)
				subtable.GlyphIdArray[x] = _fontFile.ReadUShort(ref p);
		}

		private void LoadHmtx(uint fileOffset)
		{
			ushort[] widths = new ushort[_hhea.NumberOfHMetrics];
			_lsbs = new short[_maxp.NumGlyphs];
			
			uint p = fileOffset;
			//	Read the main lists of advance widths and left-side bearings
			for(int x = 0; x < _hhea.NumberOfHMetrics; ++x)
			{
				widths[x] = _fontFile.ReadUShort(ref p);
				_lsbs [x] = _fontFile.ReadShort(ref p);
			}

			//	Read any extra left-side bearings
			for(int x = _hhea.NumberOfHMetrics; x < _maxp.NumGlyphs; ++x)
				_lsbs[x] = _fontFile.ReadShort(ref p);

			//	Fake the hmetrics.
			//
			//	True Type supports an optimization for fixed-pitch fonts (and
			//	others where it's helpful) in that the widths array in the
			//	hmtx table can be shorter than the the number of glyphs, in
			//	which case the last width applies to all higher-numbered glyphs.
			//
			//	But PDF, or perhaps just Adobe Reader 11 which I've got on my
			//	computer) seems not to support this optimization. If a font
			//	uses the optimisation then we can read the glyph widths and
			//	calculate text widths and positions, but when the PDF is displayed
			//	there is too much space between one glyph and the next - about
			//	twice as much space as expected.
			//
			//	So if we find that the font makes use of the optimisation then
			//	we override it by adding the last width explicitly for all 
			//	higher-numbered glyphs.
			//
			//	Incidentally, if a font uses the optimization then the Windows API
			//	function CreateFontPackage doesn't seem to produce a subset - it
			//	just returns the full font. (But I'm not quite sure about this.)
			//
			//	If you're curious, the fonts I used for this analysis were
			//		cour.ttf		Courier New		too wide	3 widths
			//		cpsr45w.ttf		CourierPS		OK			385 widths
			//
			if(widths.Length < _lsbs.Length)
			{
				ushort[] faked = new ushort[_lsbs.Length];
				for(int x = 0; x < widths.Length; ++x)
					faked[x] = widths[x];
				ushort last = widths[widths.Length-1];
				for(int x = widths.Length; x < faked.Length; ++x)
					faked[x] = last;
				widths = faked;
			}

			_widths = new FontWidths(widths);
		}

		private void LoadKern(uint fileOffset)
		{
			if(fileOffset == 0) return;

			_kern = new KernTable();
			uint p = fileOffset;
			_kern.Version = _fontFile.ReadUShort(ref p);
			_kern.NumTables = _fontFile.ReadUShort(ref p);
			_kern.Subtables = new KernSubtable[_kern.NumTables];
			for(ushort x = 0; x < _kern.NumTables; ++x)
			{
				ushort version = _fontFile.ReadUShort(ref p);
				switch(version)
				{
					case 0:
						KernFormat0 sub0 = new KernFormat0();
						_kern.Subtables[x] = sub0;
						sub0.Version = version;
						sub0.Length = _fontFile.ReadUShort(ref p);
						sub0.Coverage = _fontFile.ReadUShort(ref p);
						sub0.NumPairs = _fontFile.ReadUShort(ref p);
						sub0.SearchRange = _fontFile.ReadUShort(ref p);
						sub0.EntrySelector = _fontFile.ReadUShort(ref p);
						sub0.RangeShift = _fontFile.ReadUShort(ref p);
						sub0.Pairs = new KernPair[sub0.NumPairs];
						for(ushort y = 0; y < sub0.NumPairs; ++y)
						{
							ushort left = _fontFile.ReadUShort(ref p);
							ushort right = _fontFile.ReadUShort(ref p);
							short value = _fontFile.ReadShort(ref p);
							sub0.Pairs[y] = new KernPair(left,right,value);
						}
						break;
//					case 2:
//						_kern.Subtables[x] = new KernFormat2();
//						break;
				}
			}
		}

		private void LoadName(uint fileOffset)
		{
			uint p = fileOffset;

			ushort format = _fontFile.ReadUShort(ref p);
			ushort count = _fontFile.ReadUShort(ref p);

			_names = new NameTable(format,count);
			_names.StringOffset = _fontFile.ReadUShort(ref p);

			//	For now we just ignore the differences between type 0 and type 1
			//	naming tables
			for(ushort x = 0; x < count; ++x)
			{
				_names.NameRecord[x].PlatformId = _fontFile.ReadUShort(ref p);
				_names.NameRecord[x].EncodingId = _fontFile.ReadUShort(ref p);
				_names.NameRecord[x].LanguageId = _fontFile.ReadUShort(ref p);
				_names.NameRecord[x].NameId     = _fontFile.ReadUShort(ref p);
				_names.NameRecord[x].Length     = _fontFile.ReadUShort(ref p);
				_names.NameRecord[x].Offset     = _fontFile.ReadUShort(ref p);
				
				uint str = fileOffset + _names.StringOffset + _names.NameRecord[x].Offset;

				//	If the platform is 0 or 3 then it's Unicode BMP UCS-2.
				//	Anything else we'll treat as ANSI, even though in fact there's
				//	a lot more complexity than that.
				if((_names.NameRecord[x].PlatformId == 0) || (_names.NameRecord[x].PlatformId == 3))
					_names.Names[x] = _fontFile.ReadStringUcs2(ref str, _names.NameRecord[x].Length);
				else
					_names.Names[x] = _fontFile.ReadStringAscii(ref str, _names.NameRecord[x].Length);
			}

			_familyName = _names.Names[1];
			_postScriptName = _names.Names[6];
		}

		/// <summary>
		/// Replace this font with a subset of itself, using only the characters
		/// that have already been mapped in its glyph character map. Call this
		/// method after mapping all the text in the document but before encoding
		/// the text in the PDF, because the encoding should use the subset glyph
		/// indices.
		/// </summary>
		public override void Subset()
		{
			//	Get the subset of characters that we've used
			ushort[] chars = new ushort[_glyphIndices.Count];
			int x = 0;
			foreach(char c in _glyphIndices.Keys)
				chars[x++] = (ushort)c;

			byte[] originalBuf = File.ReadAllBytes(_filename);
			byte[] subset;
			uint subsetBufLen;
			uint bytesWritten;

			//	Ask Windows to create the subset file for us
			EmbedError ret = (EmbedError)CreateFontPackage(
				originalBuf, (uint)originalBuf.Length,
				out subset, out subsetBufLen, out bytesWritten,
				FontSubsetFlags.Subset, 0, FontSubsetFormat.Subset, 0,
				FontSubsetPlatform.Microsoft, FontSubsetEncoding.DontCare,
				chars, (ushort)chars.Length,
				new AllocProc(Marshal.AllocCoTaskMem),
				new ReallocProc(Marshal.ReAllocCoTaskMem),
				new FreeProc(Marshal.FreeCoTaskMem),
				IntPtr.Zero);

			//	Reload ourself based on the new subsetted font
			_fontFile = new FontFile(subset);
			_glyphIndices.Clear();
			Load();
		}

		/// <summary>
		/// Add a character to the glyph character map.
		/// </summary>
		public override void MapCharacter(Char c)
		{
			GetGlyphIndex(c);
		}

		/// <summary>
		/// Get some high-level metadata about a font file. If the font file
		/// is not a True Type font then return null.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static FontInfo GetFontInfo(string filename)
		{
			//	Load the file header. If the first four bytes aren't the
			//	True Type signature then return null.
			Stream file = File.OpenRead(filename);
			byte[] header = new byte[12];
			int ret = file.Read(header,0,12);
			uint signature = FontFile.ReadUInt32(header,0);
			if(signature != 0x00010000) return null;

			//	Find the "name" table
			uint tableOffset = 0;
			uint tableLength = 0;
			uint numTables = FontFile.ReadUInt16(header,4);
			byte[] tableInfo = new byte[16];
			for(int x = 0; x < numTables; ++x)
			{
				ret = file.Read(tableInfo,0,16);
				string name = FontFile.ReadStringAscii(tableInfo,0,4);
				if(name == "name")
				{
					tableOffset = FontFile.ReadUInt32(tableInfo,8);
					tableLength = FontFile.ReadUInt32(tableInfo,12);
					break;
				}
			}
			if(tableOffset == 0) return null;

			//	Load the name table
			file.Seek(tableOffset,SeekOrigin.Begin);
			byte[] nameTable = new byte[tableLength];
			ret = file.Read(nameTable,0,(int)tableLength);

			ushort format = FontFile.ReadUInt16(nameTable,0);
			ushort count = FontFile.ReadUInt16(nameTable,2);
			ushort stringOffset = FontFile.ReadUInt16(nameTable,4);

			string[] names = new string[count];
			for(ushort x = 0; x < count; ++x)
			{
				uint nameRecordOffset = (uint)(6 + (x * 12));
				ushort platform = FontFile.ReadUInt16(nameTable, nameRecordOffset +  0);
				ushort encoding = FontFile.ReadUInt16(nameTable, nameRecordOffset +  2);
				ushort language = FontFile.ReadUInt16(nameTable, nameRecordOffset +  4);
				ushort nameId   = FontFile.ReadUInt16(nameTable, nameRecordOffset +  6);
				ushort length   = FontFile.ReadUInt16(nameTable, nameRecordOffset +  8);
				ushort offset   = FontFile.ReadUInt16(nameTable, nameRecordOffset + 10);
				
				uint str = (uint)(stringOffset + offset);

				//	If the platform is 3 and encoding 1 then it's Unicode BMP UCS-2.
				//	Anything else we'll treat as ANSI, even though in fact there's
				//	a lot more complexity than that.
				//if((platform == 3) && (encoding == 1))
				if((platform == 0) || (platform == 3))
					names[x] = FontFile.ReadStringUcs2(nameTable,str,length);
				else
					names[x] = FontFile.ReadStringAscii(nameTable,str,length);
			}

			string familyName = count > 1 ? names[1] : null;
			string fontName   = count > 6 ? names[6] : null;

			//	Read bold/italic from the sub-family name
			bool bold = false;
			bool italic = false;
			if(count > 2)
			{
				string subfamily = names[2].ToLower();
				bold   = subfamily.Contains("bold");
				italic = subfamily.Contains("italic") || subfamily.Contains("oblique");
			}

			FontInfo info = new FontInfo(familyName,fontName,filename,FontType.TrueType,bold,italic,false,false);
			return info;
		}

		[DllImport("FontSub.dll",
					CharSet = CharSet.Unicode,
					CallingConvention = CallingConvention.Cdecl,
					SetLastError = true)]
		private static extern uint CreateFontPackage(
									[In] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] puchSrcBuffer,
									uint ulSrcBufferSize,
									[Out] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] out byte[] puchFontPackageBuffer,
									[Out] out uint pulFontPackageBuffersize,
									[Out] out uint pulBytesWritten,
									FontSubsetFlags usFlags,
									ushort usTCCIndex,
									FontSubsetFormat usSubsetFormat,
									ushort usSubsetLanguage,
									FontSubsetPlatform usSubsetPlatform,
									FontSubsetEncoding usSubsetEncoding,
									[In] [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 12)] ushort[] pusSubsetKeepList,
									ushort usSubsetKeepListCount,
									AllocProc lpfnAllocate,
									ReallocProc lpfnReallocate,
									FreeProc lpfnFree,
									IntPtr reserved);

		[UnmanagedFunctionPointer(callingConvention: CallingConvention.Cdecl)]
		private delegate IntPtr AllocProc(int size);

		[UnmanagedFunctionPointer(callingConvention: CallingConvention.Cdecl)]
		private delegate IntPtr ReallocProc(IntPtr block, int size);

		[UnmanagedFunctionPointer(callingConvention: CallingConvention.Cdecl)]
		private delegate void FreeProc(IntPtr block);
	
		[Flags]
		private enum FontSubsetFlags : ushort
		{
			Subset    = 0x0001,
			Compress  = 0x0002,
			TTC       = 0x0004,
			GlyphList = 0x0008

		}

		private enum FontSubsetFormat : ushort
		{
			Subset  = 0,
			Subset1 = 1,
			Delta   = 2
		}
	
		private enum FontSubsetPlatform : ushort
		{
			Unicode   = 0,
			Apple     = 1,
			ISO       = 2,
			Microsoft = 3
		}

		private enum FontSubsetEncoding : ushort
		{
			StandardMac = 0,
			Symbol      = 0,
			Unicode     = 1,
			DontCare    = 0xffff
		}

		private enum EmbedError : uint
		{
			E_NONE                       = 0x0000,
			E_API_NOTIMPL                = 0x0001,
			E_CHARCODECOUNTINVALID       = 0x0002,
			E_CHARCODESETINVALID         = 0x0003,
			E_DEVICETRUETYPEFONT         = 0x0004,
			E_HDCINVALID                 = 0x0006,
			E_NOFREEMEMORY               = 0x0007,
			E_FONTREFERENCEINVALID       = 0x0008,
			E_NOTATRUETYPEFONT           = 0x000A,
			E_ERRORACCESSINGFONTDATA     = 0x000C,
			E_ERRORACCESSINGFACENAME     = 0x000D,
			E_ERRORUNICODECONVERSION     = 0x0011,
			E_ERRORCONVERTINGCHARS       = 0x0012,
			E_EXCEPTION                  = 0x0013,
			E_RESERVEDPARAMNOTNULL       = 0x0014,
			E_CHARSETINVALID             = 0x0015,
			E_FILE_NOT_FOUND             = 0x0017,
			E_TTC_INDEX_OUT_OF_RANGE     = 0x0018,
			E_INPUTPARAMINVALID          = 0x0019,
			E_ERRORCOMPRESSINGFONTDATA   = 0x0100,
			E_FONTDATAINVALID            = 0x0102,
			E_NAMECHANGEFAILED           = 0x0103,
			E_FONTNOTEMBEDDABLE          = 0x0104,
			E_PRIVSINVALID               = 0x0105,
			E_SUBSETTINGFAILED           = 0x0106,
			E_READFROMSTREAMFAILED       = 0x0107,
			E_SAVETOSTREAMFAILED         = 0x0108,
			E_NOOS2                      = 0x0109,
			E_T2NOFREEMEMORY             = 0x010A,
			E_ERRORREADINGFONTDATA       = 0x010B,
			E_FLAGSINVALID               = 0x010C,
			E_ERRORCREATINGFONTFILE      = 0x010D,
			E_FONTALREADYEXISTS          = 0x010E,
			E_FONTNAMEALREADYEXISTS      = 0x010F,
			E_FONTINSTALLFAILED          = 0x0110,
			E_ERRORDECOMPRESSINGFONTDATA = 0x0111,
			E_ERRORACCESSINGEXCLUDELIST  = 0x0112,
			E_FACENAMEINVALID            = 0x0113,
			E_STREAMINVALID              = 0x0114,
			E_STATUSINVALID              = 0x0115,
			E_PRIVSTATUSINVALID          = 0x0116,
			E_PERMISSIONSINVALID         = 0x0117,
			E_PBENABLEDINVALID           = 0x0118,
			E_SUBSETTINGEXCEPTION        = 0x0119,
			E_SUBSTRING_TEST_FAIL        = 0x011A,
			E_FONTVARIATIONSIMULATED     = 0x011B,
			E_FONTFAMILYNAMENOTINFULL    = 0x011D,
			E_ADDFONTFAILED              = 0x0200,
			E_COULDNTCREATETEMPFILE      = 0x0201,
			E_FONTFILECREATEFAILED       = 0x0203,
			E_WINDOWSAPI                 = 0x0204,
			E_FONTFILENOTFOUND           = 0x0205,
			E_RESOURCEFILECREATEFAILED   = 0x0206,
			E_ERROREXPANDINGFONTDATA     = 0x0207,
			E_ERRORGETTINGDC             = 0x0208,
			E_EXCEPTIONINDECOMPRESSION   = 0x0209,
			E_EXCEPTIONINCOMPRESSION     = 0x020A
		}
	}

	internal class HeadTable
	{
		public ushort MajorVersion;
		public ushort MinorVersion;
		public float FontRevision;
		public uint ChecksumAdjustment;
		public uint MagicNumber;
		public ushort Flags;
		public ushort UnitsPerEm;
		public ulong Created;
		public ulong Modified;
		public short XMin;
		public short YMin;
		public short XMax;
		public short YMax;
		public ushort MacStyle;
		public ushort LowestRecPPEm;
		public short FontDirectionHint;
		public short IndexToLocFormat;
		public short GlyphDataFormat;
	}

	internal class HheaTable
	{
		public ushort MajorVersion;
		public ushort MinorVersion;
		public short Ascender;
		public short Descender;
		public short LineGap;
		public ushort AdvanceWidthMax;
		public short MinLeftSideBearing;
		public short MinRightSideBearing;
		public short XMaxExtent;
		public short CaretSlopeRise;
		public short CaretSlopeRun;
		public short CaretOffset;
		public short MetricDataFormat;
		public ushort NumberOfHMetrics;
	}

	internal class CmapTable
	{
		public ushort Version;
		public ushort NumTables
		{
			get
			{
				return (ushort)(Subtables != null ? Subtables.Length : 0);
			}
			set
			{
				Subtables = new CmapEncodingRecord[value];
				for(int x = 0; x < value; ++x)
					Subtables[x] = new CmapEncodingRecord();
			}
		}
		public CmapEncodingRecord[] Subtables;
		public CmapSegmentTable SubTable;
		public CmapTable(ushort version)
		{
			if(version != 0) throw new NotImplementedException("Cmap table version not zero.");
			Version = version;
		}
	}

	internal class CmapEncodingRecord
	{
		public ushort PlatformId;
		public ushort EncodingId;
		public uint Offset;
	}

	internal class CmapSegmentTable : List<CmapSegment>
	{
		public ushort Format;
		public ushort Length
		{
			get { return (ushort)Count; }
			set { Clear(); for(int x = 0; x < value; ++x) Add(new CmapSegment()); }
		}

		public ushort Language;
		public ushort SegCountX2;
		public ushort SearchRange;
		public ushort EntrySelector;
		public ushort RangeShift;
		public ushort[] GlyphIdArray;

		public CmapSegmentTable(ushort format)
		{
			if(format != 4) throw new NotImplementedException("Cmap table must be format 4.");
			Format = format;
		}
	}

	internal class CmapSegment
	{
		public ushort Index;
		public ushort Start;
		public ushort End;
		public ushort IdDelta;
		public ushort IdRangeOffset;
	}

	internal class MaxpTable
	{
		public float Version;
		public ushort NumGlyphs;
		public ushort MaxPoints;
		public ushort MaxContours;
		public ushort MaxCompositePoints;
		public ushort MaxCompositeContours;
		public ushort MaxZones;
		public ushort MaxTwilightPoints;
		public ushort MaxStorage;
		public ushort MaxFunctionDefs;
		public ushort MaxInstructionDefs;
		public ushort MaxStackElements;
		public ushort MaxSizeOfInstructions;
		public ushort MaxComponentElements;
		public ushort MaxComponentDepth;
		public MaxpTable(float version)
		{
			if(version != 1.0f) throw new Exception("Maxp table version is not 1.0");
			Version = version;
		}
	}

	internal class NameTable
	{
		public ushort Format;
		public ushort Count;
		public ushort StringOffset;
		public NameRecord[] NameRecord;
		public string[] Names;

		public NameTable(ushort format, ushort count)
		{
			if(format != 0) throw new NotImplementedException("Format must be zero.");

			Format = format;
			Count = count;
			NameRecord = new NameRecord[count];
			for(int x = 0; x < count; ++x)
				NameRecord[x] = new NameRecord();
			Names = new string[count];
		}
	}

	internal class NameRecord
	{
		public ushort PlatformId;
		public ushort EncodingId;
		public ushort LanguageId;
		public ushort NameId;
		public ushort Length;
		public ushort Offset;
	}

	internal class Os2Table
	{
		public ushort Version;
		public short XAvgCharWidth;
		public ushort WeightClass;
		public ushort WidthClass;
		public FontEmbeddingLicenseFlags Type;
		public short YSubscriptXSize;
		public short YSubscriptYSize;
		public short YSubscriptXOffset;
		public short YSubscriptYOffset;
		public short YSuperscriptXSize;
		public short YSuperscriptYSize;
		public short YSuperscriptXOffset;
		public short YSuperscriptYOffset;
		public short YStrikeoutSize;
		public short YStrikeoutPosition;
		public short FamilyClass;
		public Panose Panose = new Panose();
		public uint[] UnicodeRanges = new uint[4];
		public byte[] VendId = new byte[4];
		public ushort Selection;
		public ushort FirstCharIndex;
		public ushort LastCharIndex;
		public short TypoAscender;
		public short TypoDescender;
		public short TypoLineGap;
		public ushort WinAscent;
		public ushort WinDescent;
		public uint[] CodePageRanges = new uint[2];
		public short XHeight;
		public short CapHeight;
		public ushort DefaultChar;
		public ushort BreakChar;
		public ushort MaxContext;

		public Os2Table(ushort version)
		{
			//if(version < 2) throw new Exception("OS/2 version less than 2 not supported");
			Version = version;
		}
	}

	internal class KernTable
	{
		public ushort Version;
		public ushort NumTables;
		public KernSubtable[] Subtables;
	}

	internal class KernFormat0 : KernSubtable
	{
		public ushort NumPairs;
		public ushort SearchRange;
		public ushort EntrySelector;
		public ushort RangeShift;
		public KernPair[] Pairs;

		public short GetKernAdjustment(ushort glyphIndexLeft, ushort glyphIndexRight)
		{
			//	The kerning pairs are sorted by the 32-bit combination of their
			//	left/right values, so we can do binary search
			uint target = (uint)(glyphIndexLeft << 16) | glyphIndexRight;
			int lo = 0;
			int hi = NumPairs - 1;
			while(true)
			{
				int mid = lo + ((hi - lo) / 2);
				if(mid >= NumPairs) return 0;

				uint key = Pairs[mid].Pair;
				if(key == target)
					return Pairs[mid].Value;
				if(key < target)
					lo = mid + 1;
				else if(key > target)
					hi = mid - 1;

				if(lo > hi) return 0;
			}
		}
	}

	internal class KernSubtable
	{
		public ushort Version;
		public ushort Length;
		public ushort Coverage;
	}

	internal class KernPair
	{
		public ushort Left { get; private set; }
		public ushort Right { get; private set; }
		public uint Pair { get; }
		public short Value;
		public KernPair(ushort left, ushort right, short value)
		{
			Left = left;
			Right = right;
			Pair = (((uint)left) << 16) | (uint)right;
			Value = value;
		}
	}

	internal class Panose
	{
		public byte FamilyType;
		public byte SerifStyle;
		public byte Weight;
		public byte Proportion;
		public byte Contrast;
		public byte StrokeVariation;
		public byte ArmStyle;
		public byte Letterform;
		public byte Midline;
		public byte XHeight;
	}

	internal class PostTable
	{
		public float Version;
		public float ItalicAngle;
		public short UnderlinePosition;
		public short UnderlineThickness;
		public uint IsFixedPitch;
		public uint MinMemType42;
		public uint MaxMemType42;
		public uint MinMemType1;
		public uint MaxMemType1;
		public ushort NumberOfGlyphs;
		public ushort[] GlyphNameIndex;
//		public byte[] Names;

		public PostTable(float version, ushort numGlyphs)
		{
			if((version != 2.0f) && (version != 3.0f))
				throw new NotImplementedException("Post must be version 2.0 or 3.0");
			Version = version;
			NumberOfGlyphs = numGlyphs;
			GlyphNameIndex = new ushort[NumberOfGlyphs];
		}
	}
}
