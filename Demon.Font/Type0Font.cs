using System;

namespace Demon.Font
{
	/// <summary>
	/// A Type 0 font is a composite font. Its font data is stored in the descendant
	/// CIDFont contained within the Type 0 font. A composite font supports Unicode
	/// characters, whereas simple fonts can handle only single-byte characters.
	/// (A simple font can handle non-ASCII characters by mapping them into its
	/// glyph set, but it still expects the text to be in single-byte format.)
	/// </summary>
	public class Type0Font : Font
	{
		//	Here in the report types library we don't need an implementation
		//	of CIDFont because its implementation only becomes necessary
		//	when we're writing the font to an output document, and so it
		//	needs PDF- and Word-specific implementations. So we don't even
		//	define the type here in this library.

		protected IFont _descendant;
		public IFont Descendant { get { return _descendant;}}

		public Type0Font(IFont descendant)
		{
			_descendant = descendant;
		}

		public override int GetCharWidth(char c)
		{
			throw new NotImplementedException();
		}

		public override void Subset()
		{
		}

		public override void MapCharacter(char c)
		{
			_descendant.MapCharacter(c);
		}

		public override int GetKernAdjustment(char charLeft, char charRight)
		{
			return _descendant.GetKernAdjustment(charLeft,charRight);
		}

		public override bool Bold { get { return _descendant.Bold; }}
		public  override bool Italic { get { return _descendant.Italic; }}
		public override int UnderlinePosition { get { return _descendant.UnderlinePosition; }}
		public override int UnderlineThickness { get { return _descendant.UnderlineThickness; }}

		public override ushort UnitsPerEm { get { throw new NotImplementedException(); } }
		public override short Ascender { get { throw new NotImplementedException(); } }
		public override short Descender { get { throw new NotImplementedException(); } }
		public override short XHeight { get { throw new NotImplementedException(); } }
		public override short CapHeight { get { throw new NotImplementedException(); } }
		public override short LineGap { get { throw new NotImplementedException(); } }

		public override int GetAverageWidth(int fontsize)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// In font units
		/// </summary>
		public override int GetMaxWidth(int fontSize)
		{
			throw new NotImplementedException();
		}
	}
}
