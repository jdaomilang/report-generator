using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum TextAlignment
	{
		Left,
		Right,
		Center,
		Justify
	}
	
	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum PictureAlignment
	{
		TopLeft,
		TopCenter,
		TopRight,
		CenterLeft,
		Center,
		CenterRight,
		BottomLeft,
		BottomCenter,
		BottomRight
	}

	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum ScaleMode
	{
		ScaleDown,
		NaturalSize
	}

	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum NumberStyle
	{
		Bullet,
		Number,
		AlphaLower,
		AlphaUpper,
		RomanLower,
		RomanUpper,
		GreekLower,
		GreekUpper
	}

	/// <summary>
	/// These are not the same as Demon.Report.LayoutType.
	/// </summary>
	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum LayoutType
	{
		None,
		ChapterLayout,
		GroupLayout,
		TextLayout,
		ListLayout,
		TableLayout,
		TableRowLayout,
		TableCellLayout,
		PhotoTableLayout,
		PictureLayout,
		SpaceLayout,
		LineLayout
	}

	[JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
	public enum ConditionType
	{
		None,
		EmptyLayoutCondition,
		OptionSelectedCondition,
		ContentSelectedCondition,
		DocTagCondition,
		ContentDocTagCondition,
		ItemCountCondition,
		PhotoCountCondition
	}
}
