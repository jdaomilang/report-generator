using System;

namespace Demon.Report.Style
{
	public interface IStyle
	{
		string Name { get; }
		IStyle Base { get; }
		bool IsDefault { get; }
		int LineNumber { get; set; }
		int LinePosition { get; set; }
	}

	//	In all of these styles, the Base property refers to the lowermost style
	//	in the hierarchy. This way you can have a hierarchy of arbitrary depth
	//	but still render every style as a library style with a single set of
	//	overridden properties.

	public class Font : IStyle
	{
		private Font _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?._base ?? _base; } set { _base = (Font)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public string FamilyName { get; set; }
		public int Size { get; set; } //TODO: support half-point sizes like 9.5pt
		public int Weight { get; set; }
		public bool  Bold { get; set; }
		public bool Italic { get; set; }
		public bool Strikeout { get; set; }
		public bool Underline { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			string str = $"{FamilyName} {Size}pt";
			if(Bold)      str += " bold";
			if(Italic)    str += " italic";
			if(Strikeout) str += " strikeout";
			if(Underline) str += " underline";
			return str;
		}
	}

	public class Color : IStyle
	{
		private Color _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (Color)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public double Red { get; set; }
		public double Green { get; set; }
		public double Blue { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Red} {Green} {Blue}";
		}
	}

	public class Border : IStyle
	{
		private Border _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (Border)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Color Color { get; set; }
		public int Thickness { get; set; }
		public BorderPart Parts { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Parts}";
		}
	}

	public class Padding : IStyle
	{
		private Padding _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (Padding)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public int Left { get; set; }
		public int Bottom { get; set; }
		public int Right { get; set; }
		public int Top { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Left} {Bottom} {Right} {Top}";
		}
	}

	public class TextStyle : IStyle
	{
		private TextStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (TextStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Font Font { get; set; }
		public Color Color { get; set; }
		public Color BackColor { get; set; }
		public double LineSpacing { get; set; }
		public double ParagraphSpacing { get; set; }
		public TextAlignment Alignment { get; set; }
		public double SoftBreakLimit { get; set; }
		public Border Border { get; set; }
		public Padding Padding { get; set; }
		public string ListSeparator { get; set; }
		public string ListTerminator { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Font} {Alignment}";
		}
	}

	public class ListStyle : IStyle
	{
		private ListStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (ListStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public int ItemIndent { get; set; }
		public int BulletIndent { get; set; }

		public Border Border { get; set; }
		public Padding Padding { get; set; }
		public BulletStyle BulletStyle { get; set; }
		public BulletStyle SelectedBulletStyle { get; set; }
		public BulletStyle UnselectedBulletStyle { get; set; }
		public TextStyle ItemStyle { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{ItemStyle} {BulletStyle}";
		}
	}

	public class BulletStyle : IStyle
	{
		private BulletStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (BulletStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public string BulletText { get; set; }
		public ListNumberStyle NumberStyle { get; set; }
		public int StartAt { get; set; }
		public Font Font { get; set; }
		public Color Color { get; set; }
		public Padding Padding { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{BulletText} {NumberStyle}";
		}
	}

	public class PhotoStyle : IStyle
	{
		private PhotoStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (PhotoStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public int MaxWidth { get; set; }
		public int MaxHeight { get; set; }
		public Border Border { get; set; }
		public Padding Padding { get; set; }
		public TextStyle CaptionStyle { get; set; }
		public int Resolution { get; set; }
		public int Quality { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{MaxWidth} {MaxHeight}";
		}
	}

	public class TableStyle : IStyle
	{
		private TableStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (TableStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Border Border { get; set; }
		public Padding Padding { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Border} {Padding}";
		}
	}

	public class TableRowStyle : IStyle
	{
		private TableRowStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (TableRowStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Padding Padding { get; set; }
		public Color BackColor { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Padding}";
		}
	}

	public class TableCellStyle : IStyle
	{
		private TableCellStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (TableCellStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Padding Padding { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Padding}";
		}
	}

	public class LineStyle : IStyle
	{
		private LineStyle _base;

		public string Name { get; set; }
		public IStyle Base { get { return _base?.Base ?? _base; } set { _base = (LineStyle)(value.Base ?? value); } }
		public bool IsDefault { get; set; }
		public Color Color { get; set; }
		public int Thickness { get; set; }
		public Padding Padding { get; set; }
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public override string ToString()
		{
			return $"{Thickness}pt {Color}";
		}
	}

	public enum TextAlignment
	{
		Left		= 0,
		Right		= 1,
		Center	= 2,
		Justify	= 3
	}

	[Flags]
	public enum BorderPart
	{
		None			= 0x00,
		Left			= 0x01,
		Bottom		= 0x02,
		Right			= 0x04,
		Top				= 0x08,
		Outer			= 0x0f,
		InnerHorizontal	= 0x10,
		InnerVertical		= 0x20,
		Inner			= 0xf0,
		All				= 0xff
	}

	public enum PictureAlignment
	{
		TopLeft		   = 0,
		TopCenter    = 1,
		TopRight	   = 2,
		CenterLeft   = 3,
		Center       = 4,
		CenterRight  = 5,
		BottomLeft   = 6,
		BottomCenter = 7,
		BottomRight  = 8
	}

	public enum PictureScaleMode
	{
		ScaleDown		= 0,
		NaturalSize	= 1
	}
	
	public enum ListNumberStyle
	{
		Bullet     = 0,
		Number     = 1,
		AlphaLower = 2,
		AlphaUpper = 3,
		RomanLower = 4,
		RomanUpper = 5,
		GreekLower = 6,
		GreekUpper = 7
	}
}
