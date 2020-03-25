using System.IO;
using System.Text.RegularExpressions;

namespace Demon.Font
{
	public class Type1Font : Font
	{
#pragma warning disable 0649 // field never assigned a value
		protected string _encoding;
#pragma warning restore 0649
		protected ushort _flags;
		protected ushort _firstChar;
		protected ushort _lastChar;
		/// <summary>Widths of characters, in font units</summary>
		protected FontWidths _widths;
		protected ushort _unitsPerEm;
		/// <summary>In font units</summary>
		protected short _ascender;
		/// <summary>In font units</summary>
		protected short _descender;
		/// <summary>In font units</summary>
		protected ushort _advanceWidthMax;
		/// <summary>In font units</summary>
		protected short _avgCharWidth;
		protected ushort _weight;
		/// <summary>In font units</summary>
		protected short _xHeight;
		/// <summary>In font units</summary>
		protected short _capHeight;
		protected short _underlinePosition;
		protected short _underlineThickness;
		/// <summary>In font units</summary>
		protected BoundingBox _bbox;
		protected bool _isFixedPitch;
		protected float _italicAngle;

		public string Encoding { get { return _encoding; } }
		public FontWidths Widths { get { return _widths; } }
		public override ushort UnitsPerEm { get { return _unitsPerEm; } }
		public override short Ascender { get { return _ascender; } }
		public override short Descender { get { return _descender; } }
		public override short XHeight { get { return _xHeight; } }
		public override short CapHeight { get { return _capHeight; } }
		public short XAvgCharWidth { get { return _avgCharWidth; }}
		public ushort AdvanceWidthMax { get { return _advanceWidthMax; }}
		public ushort WeightClass { get { return _weight; }}
		public BoundingBox BoundingBox { get { return _bbox; }}
		public override short LineGap { get { return 0; } }
		public override int UnderlinePosition { get { return _underlinePosition; }}
		public override int UnderlineThickness { get { return _underlineThickness; }}
		public float ItalicAngle { get { return _italicAngle; }}
		public ushort Flags { get { return _flags; }}
		public ushort FirstChar { get { return _firstChar; }}
		public ushort LastChar { get { return _lastChar; }}

		/// <summary>
		/// Load a Type 1 font from its AFM file
		/// </summary>
		public Type1Font(string filename)
		{
			Load(filename);

			//_encoding = "WinAnsiEncoding";
			_flags = 32;
			_unitsPerEm = 1000;
		}

		protected virtual void Load(string filename)
		{
			//TODO: Load the actual font, not just its AFM file. The AFM file
			//is only useful to us for the standard 14 fonts.

			ushort[] widths = new ushort[256];
			ushort widthIndex = 0;
			int sumWidths = 0;

			using(StreamReader file = File.OpenText(filename))
			{
				while(!file.EndOfStream)
				{
					string line = file.ReadLine();
					
					MatchCollection mc = Regex.Matches(line, @"^FamilyName (.+)");
					if(mc.Count == 1)
					{
						_familyName = mc[0].Groups[1].Captures[0].Value;
						continue;
					}

					mc = Regex.Matches(line, @"^FontName (.+)");
					if(mc.Count == 1)
					{
						_postScriptName = mc[0].Groups[1].Captures[0].Value;
						continue;
					}

					mc = Regex.Matches(line, @"^Weight (\w+)");
					if(mc.Count == 1)
					{
						switch(mc[0].Groups[1].Captures[0].Value.ToLower())
						{
							case "thin":        _weight = 100; break;
							case "extra-light": _weight = 200; break;
							case "light":       _weight = 300; break;
							case "normal":      _weight = 400; break;
							case "regular":     _weight = 400; break;
							case "medium":      _weight = 500; break;
							case "semi-bold":   _weight = 600; break;
							case "demi-bold":   _weight = 600; break;
							case "bold":        _weight = 700; break;
							case "extra-bold":  _weight = 800; break;
							case "ultra-bold":  _weight = 800; break;
							case "black":       _weight = 900; break;
							case "heavy":       _weight = 900; break;
							default:            _weight = 400; break;
						}
						_bold = _weight >= 600;
						continue;
					}

					mc = Regex.Matches(line, @"^ItalicAngle (-?\d+)");
					if(mc.Count == 1)
					{
						float.TryParse(mc[0].Groups[1].Captures[0].Value, out _italicAngle);
						_italic = _italicAngle != 0.0;
						continue;
					}

					mc = Regex.Matches(line, @"^IsFixedPitch (\w+)");
					if(mc.Count == 1)
					{
						bool.TryParse(mc[0].Groups[1].Captures[0].Value, out _isFixedPitch);
						continue;
					}

					mc = Regex.Matches(line, @"^UnderlinePosition (-?\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _underlinePosition);
						continue;
					}

					mc = Regex.Matches(line, @"^UnderlineThickness (-?\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _underlineThickness);
						continue;
					}

					mc = Regex.Matches(line, @"^FontBBox ([0-9\-]+) ([0-9\-]+) ([0-9\-]+) ([0-9\-]+)");
					if(mc.Count == 1)
					{
						int left = 0;
						int bottom = 0;
						int right = 0;
						int top = 0;

						int.TryParse(mc[0].Groups[1].Captures[0].Value, out left);
						int.TryParse(mc[0].Groups[2].Captures[0].Value, out bottom);
						int.TryParse(mc[0].Groups[3].Captures[0].Value, out right);
						int.TryParse(mc[0].Groups[4].Captures[0].Value, out top);

						_bbox = new BoundingBox(left, bottom, right, bottom);

						continue;
					}

					mc = Regex.Matches(line, @"^CapHeight (\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _capHeight);
						continue;
					}

					mc = Regex.Matches(line, @"^XHeight (\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _xHeight);
						continue;
					}

					mc = Regex.Matches(line, @"^Ascender (\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _ascender);
						continue;
					}

					mc = Regex.Matches(line, @"^Descender (-?\d+)");
					if(mc.Count == 1)
					{
						short.TryParse(mc[0].Groups[1].Captures[0].Value, out _descender);
						continue;
					}

					//	Widths
					mc = Regex.Matches(line, @"^C (\d+) ; WX (\d+)");
					if(mc.Count == 1)
					{
						ushort c;	
						ushort w;
						ushort.TryParse(mc[0].Groups[1].Captures[0].Value, out c);
						ushort.TryParse(mc[0].Groups[2].Captures[0].Value, out w);
						
						if(_firstChar == 0) _firstChar = c;
						_lastChar = c;

						widths[widthIndex] = w;
						sumWidths += w;
						_avgCharWidth = (short)(sumWidths / (widthIndex + 1));
						if(w > _advanceWidthMax) _advanceWidthMax = w;

						++widthIndex;
					}
				}
			}

			_widths = new FontWidths(widths);
		}

		/// <summary>
		/// Load information about the font, but don't load the glyph data.
		/// </summary>
		public static FontInfo GetFontInfo(string filename)
		{
			string familyName = null;
			string fontName = null;
			bool bold = false;
			bool italic = false;
#pragma warning disable 0219 // variable assigned but never used
			bool underline = false;
			bool strikeout = false;
#pragma warning restore 0219

			using(StreamReader file = File.OpenText(filename))
			{
				//	Type 1 starts with "StartFontMetrics"
				string line = file.ReadLine();
				if(!line.StartsWith("StartFontMetrics"))
					return null;

				//	Find the font name line
				while(!file.EndOfStream)
				{
					line = file.ReadLine();

					MatchCollection mc = Regex.Matches(line, @"^FamilyName (.+)");
					if(mc.Count == 1)
					{
						familyName = mc[0].Groups[1].Captures[0].Value;
						continue;
					}

					mc = Regex.Matches(line, @"^FontName (.+)");
					if(mc.Count == 1)
					{
						fontName = mc[0].Groups[1].Captures[0].Value;
						continue;
					}

					mc = Regex.Matches(line, @"^Weight (\w+)");
					if(mc.Count == 1)
					{
						int weight = 0;
						switch(mc[0].Groups[1].Captures[0].Value.ToLower())
						{
							case "thin":        weight = 100; break;
							case "extra-light": weight = 200; break;
							case "light":		weight = 300; break;
							case "normal":		weight = 400; break;
							case "regular":		weight = 400; break;
							case "medium":		weight = 500; break;
							case "semi-bold":	weight = 600; break;
							case "demi-bold":	weight = 600; break;
							case "bold":		weight = 700; break;
							case "extra-bold":	weight = 800; break;
							case "ultra-bold":	weight = 800; break;
							case "black":		weight = 900; break;
							case "heavy":		weight = 900; break;
							default:			weight = 400; break;
						}
						bold = weight >= 600;
						continue;
					}

					mc = Regex.Matches(line, @"^ItalicAngle (-?\d+)");
					if(mc.Count == 1)
					{
						float angle;
						float.TryParse(mc[0].Groups[1].Captures[0].Value, out angle);
						italic = angle != 0.0;
						continue;
					}
				}
			}
			if(fontName == null) return null;

			FontInfo info = new FontInfo(familyName,fontName,filename,FontType.Type1,bold,italic,false,false);
			return info;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetCharWidth(char c)
		{
			int index = c - _firstChar;
			int width = _widths[index];
			return width;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetAverageWidth(int fontsize)
		{
			int avg = _avgCharWidth * fontsize;
			return avg;
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetMaxWidth(int fontSize)
		{
			int max = _advanceWidthMax * fontSize;
			return max;
		}

		public override int GetKernAdjustment(char charLeft, char charRight)
		{
			return 0;
		}

		public override void Subset()
		{
		}

		public override void MapCharacter(char c)
		{
		}
	}
}
