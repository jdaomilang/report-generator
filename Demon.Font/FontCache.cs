using System;
using System.Collections.Generic;
using System.IO;

namespace Demon.Font
{
	public class FontCache
	{
		/// <summary>
		/// Font name in this dictionary is in lower case.
		/// </summary>
		private List<Font> _fonts;
		private List<string> _fontDirectories;
		private List<FontInfo> _fontFiles;

		public FontCache(string fontDirectory)
		{
			_fonts = new List<Font>();
			
			_fontDirectories = new List<string>();
			_fontDirectories.Add(fontDirectory);
		}

		/// <summary>
		/// Load information about all fonts found in the cache's directories.
		/// </summary>
		public void Load()
		{
			_fonts = new List<Font>();
			_fontFiles = new List<FontInfo>();

			//	Open all *.ttf and *.afm files in each of our font directories
			//	and test whether it's a font file. If it is a font file then read
			//	it and get its info. This doesn't load the font, it just
			//	reads some metadata.
			foreach(string dir in _fontDirectories)
			{
				//	True Type fonts
				string[] filenames = Directory.GetFiles(dir,"*.ttf");
				foreach(string filename in filenames)
				{
					FontInfo info = TrueTypeFont.GetFontInfo(filename);
					_fontFiles.Add(info);
				}

				//	Type 1 fonts
				filenames = Directory.GetFiles(dir,"*.afm");
				foreach(string filename in filenames)
				{
					FontInfo info = Type1Font.GetFontInfo(filename);
					_fontFiles.Add(info);
				}
			}
		}

		/// <summary>
		/// Load a named font, ready for use.
		/// </summary>
		public Font GetFont(string familyName, int weight, bool bold, bool italic, bool underline, bool strikeout)
		{
			//	If we already have this font then don't duplicate it
			Font font = null;
			foreach(Font candidate in _fonts)
			{
				if(candidate.FamilyName.ToLower() != familyName.ToLower()) continue;
				if(candidate.Bold != bold) continue;
				if(candidate.Italic != italic) continue;

				//	If we get here then we've got a match
				font = candidate;
				break;
			}
			if(font != null) return font;

			//	Search our font directories for a file that implements
			//	the specified font
			FontInfo info = FindFont(familyName, weight, bold, italic, underline, strikeout);
			if(info == null)
			{
				string msg = "Font '" + familyName + "'";
				if(bold)      msg += " bold";
				if(italic)    msg += " italic";
				if(underline) msg += " underline";
				if(strikeout) msg += " strikeout";
				msg += " not found, and no substitute found either.";
				throw new Exception(msg);
			}

			//	Create the real font
			switch(info.Type)
			{
				case FontType.TrueType:
					font = new TrueTypeFont(info.FileName);
					break;
				case FontType.Type1:
					font = new Type1Font(info.FileName);
					break;
			}
			_fonts.Add(font);
			return font;
		}

		private FontInfo FindFont(string familyName, int weight, bool bold, bool italic, bool underline, bool strikeout)
		{
			//	If we can't find an exact match on things like bold
			//	and italic, we'll settle for an alternative. The
			//	face name must match up as far as the hyphen, and
			//	then we score points for each other matching
			//	characteristic.
			FontInfo best = null;
			int bestScore = 0;

			foreach(FontInfo info in _fontFiles)
			{
				if(info.FamilyName.ToLower() == familyName.ToLower())
				{
					int score = 0;
					if(info.Bold == bold) ++score;
					if(info.Italic == italic) ++ score;
					if(info.Underline == underline) ++ score;
					if(info.StrikeOut == strikeout) ++score;
					//if(info.Weight == weight) ++score;
					if(score > bestScore)
					{
						best = info;
						bestScore = score;
					}
				}
			}
			return best;
		}

		public void Subset()
		{
			foreach(Font font in _fonts)
				font.Subset();
		}

		public List<Font> Fonts
		{
			get
			{
				return new List<Font>(_fonts);
			}
		}

		public List<Font> RealizedFonts
		{
			get
			{
				return new List<Font>(_fonts);
			}
		}
	}
}
