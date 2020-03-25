using System.Collections.Generic;

namespace Demon.PDF
{
	/// <summary>
	/// A collection of PDF fonts, searchable by family name,
	/// bold and italic.
	/// </summary>
	internal class FontCache
	{
		private Demon.Font.FontCache _underlying;
		private HashSet<Demon.PDF.Font> _pdfFonts = new HashSet<Demon.PDF.Font>();

		public FontCache(Demon.Font.FontCache underlying)
		{
			_underlying = underlying;

			//	Wrap all the underlying fonts in PDF stuff
			foreach(Demon.Font.Font underlyingFont in underlying.Fonts)
			{
				Demon.PDF.Font pdfFont = null;
				Demon.Font.TrueTypeFont ttf = underlyingFont as Demon.Font.TrueTypeFont;
				Demon.Font.Type1Font    t1  = underlyingFont as Demon.Font.Type1Font;
				if(ttf != null)
					pdfFont = new Demon.PDF.TrueTypeFont(ttf);
				else if(t1 != null)
					pdfFont = new Demon.PDF.Type1Font(t1);
				_pdfFonts.Add(pdfFont);
			}
		}

		public Demon.PDF.Font GetFont(string familyName, bool bold, bool italic)
		{
			foreach(Demon.PDF.Font font in _pdfFonts)
			{
				if(string.Compare(font.FamilyName, familyName, true) != 0) continue;
				if(font.Bold != bold) continue;
				if(font.Italic != italic) continue;
//				if(font.Underline != underline)	continue;
//				if(font.Strikeout != strikeout)	continue;

				return font;
			}

			string msg = "Font '" + familyName + "'";
			if(bold)		msg += " bold";
			if(italic)		msg += " italic";
//			if(underline)	msg += " underline";
//			if(strikeout)	msg += " strikeout";
			msg += " not found, and no substitute found either.";
			throw new System.Exception(msg);
		}

		public void AliasFonts()
		{
			//	Alias the PDF fonts and add them to the PDF font cache
			int nextAlias = 1;
			foreach(Demon.PDF.Font font in _pdfFonts)
			{
				font.Alias = "F" + nextAlias++;

				//	If it's a True Type font then add the Type 0 font wrapper. When we
				//	write the page we'll add both fonts as page resources.
				TrueTypeFont ttf = font as TrueTypeFont;
				if(ttf != null)
					ttf.RootFont.Alias = "F" + nextAlias++;
			}
		}

		public List<Demon.PDF.Font> Fonts
		{
			get
			{
				return new List<Demon.PDF.Font>(_pdfFonts);
			}
		}
	}
}
