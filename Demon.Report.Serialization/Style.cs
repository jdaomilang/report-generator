using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public abstract class Style : ILineNumber
	{
		public string id;
		public string @ref;
		public bool? isDefault;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Style()
		{
		}

		public Style(XElement element)
		{
			id        = XmlHelper.ReadStringAttribute(element, "id");
			@ref      = XmlHelper.ReadStringAttribute(element, "ref");
			isDefault = XmlHelper.ReadBoolAttribute  (element, "isDefault");

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Style(JsonReader reader)
		{
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;
				LoadProperty(reader, propName);
			}
		}

		protected virtual bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "id":
					id = JsonHelper.ReadString(reader);
					return true;
				case "ref":
					@ref = JsonHelper.ReadString(reader);
					return true;
				case "isDefault":
					isDefault = JsonHelper.ReadBoolean(reader);
					return true;
				default:
					//	Not a base class property - let the derived class load it
					return false;
			}
		}

		public virtual void Validate()
		{
		}

		public virtual void WriteXml(XmlWriter writer)
		{
		}

		protected void WriteXml_BaseAttributes(XmlWriter writer)
		{
			if(id != null)
				writer.WriteAttributeString("id", id);
			if(@ref != null)
				writer.WriteAttributeString("ref", @ref);
			if(isDefault != null)
				writer.WriteAttributeString("isDefault", isDefault.Value ? "true" : "false"); // XML requires bools in lowercase
		}

		protected void WriteXml_BaseElements(XmlWriter writer)
		{
		}

		/// <summary>
		/// Write a private style definition or a reference to a shared style.
		/// </summary>
		public static void WriteStyle<T>(XmlWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			if(style.id == null)
				WriteStyleDefinition(writer, style, elementName);
			else
				WriteStyleReference(writer, style, elementName);
		}

		public static void WriteStyleDefinition<T>(XmlWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			writer.WriteStartElement(elementName);
			style.WriteXml(writer);
			writer.WriteEndElement();
		}

		public static void WriteStyleReference<T>(XmlWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			writer.WriteStartElement(elementName);
			writer.WriteAttributeString("ref", style.id);
			writer.WriteEndElement();
		}

		public abstract void WriteJson(JsonWriter writer);

		/// <summary>
		/// Write a private style definition or a reference to a shared style.
		/// </summary>
		public static void WriteStyle<T>(JsonWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			if(style.id == null)
				WriteStyleDefinition(writer, style, elementName);
			else
				WriteStyleReference(writer, style, elementName);
		}

		public static void WriteStyleDefinition<T>(JsonWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			writer.WritePropertyName(elementName);
			writer.WriteStartObject();
			style.WriteJson(writer);
			writer.WriteEndObject();
		}

		public static void WriteStyleReference<T>(JsonWriter writer, T style, string elementName) where T : Style
		{
			if(style == null) return;

			writer.WritePropertyName(elementName);
			writer.WriteStartObject();
			writer.WritePropertyName("ref");
			writer.WriteValue(style.id);
			writer.WriteEndObject();
		}

		protected void WriteJson_BaseProperties(JsonWriter writer)
		{
			if(id != null)
			{
				writer.WritePropertyName("id");
				writer.WriteValue(id);
			}
			if(@ref != null)
			{
				writer.WritePropertyName("ref");
				writer.WriteValue(@ref);
			}
			if(isDefault != null)
			{
				writer.WritePropertyName("isDefault");
				writer.WriteValue(isDefault);
			}
		}
	}

	public class Styles
	{
		public List<Font>           Fonts           = new List<Font>();
		public List<Color>          Colors          = new List<Color>();
		public List<Border>         Borders         = new List<Border>();
		public List<Padding>        Paddings        = new List<Padding>();
		public List<TextStyle>      TextStyles      = new List<TextStyle>();
		public List<ListStyle>      ListStyles      = new List<ListStyle>();
		public List<PhotoStyle>     PhotoStyles     = new List<PhotoStyle>();
		public List<TableStyle>     TableStyles     = new List<TableStyle>();
		public List<TableRowStyle>  TableRowStyles  = new List<TableRowStyle>();
		public List<TableCellStyle> TableCellStyles = new List<TableCellStyle>();
		public List<BulletStyle>    BulletStyles    = new List<BulletStyle>();
		public List<LineStyle>      LineStyles      = new List<LineStyle>();

		public Styles()
		{
		}

		public void Load(XElement root)
		{
			XNamespace ns = root.GetDefaultNamespace();
			LoadFonts          (root.Element(ns + "Fonts"          ));
			LoadColors         (root.Element(ns + "Colors"         ));
			LoadBorders        (root.Element(ns + "Borders"        ));
			LoadPaddings       (root.Element(ns + "Paddings"       ));
			LoadTextStyles     (root.Element(ns + "TextStyles"     ));
			LoadBulletStyles   (root.Element(ns + "BulletStyles"   ));
			LoadListStyles     (root.Element(ns + "ListStyles"     ));
			LoadPhotoStyles    (root.Element(ns + "PhotoStyles"    ));
			LoadTableStyles    (root.Element(ns + "TableStyles"    ));
			LoadTableRowStyles (root.Element(ns + "TableRowStyles" ));
			LoadTableCellStyles(root.Element(ns + "TableCellStyles"));
			LoadLineStyles     (root.Element(ns + "LineStyles"     ));
		}

		//TODO: replace all these LoadXxxs with a generic LoadStyles
//		private List<T> LoadStyles<T>(XElement root) where T : Style, new()
//		{
//			if(root == null) return null;
//
//			List<T> styles = new List<T>();
//			foreach(XElement element in root.Element())
//			{
//				T style = new T(element);
//				styles.Add(style);
//			}
//		}

		public Styles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartObject);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				string typename = (string)reader.Value;
				reader.Read(); // advance to the start array token
				switch(typename)
				{
					case "Fonts":
						LoadFonts(reader);
						break;
					case "Colors":
						LoadColors(reader);
						break;
					case "Borders":
						LoadBorders(reader);
						break;
					case "Paddings":
						LoadPaddings(reader);
						break;
					case "TextStyles":
						LoadTextStyles(reader);
						break;
					case "BulletStyles":
						LoadBulletStyles(reader);
						break;
					case "ListStyles":
						LoadListStyles(reader);
						break;
					case "PhotoStyles":
						LoadPhotoStyles(reader);
						break;
					case "TableStyles":
						LoadTableStyles(reader);
						break;
					case "TableRowStyles":
						LoadTableRowStyles(reader);
						break;
					case "TableCellStyles":
						LoadTableCellStyles(reader);
						break;
					case "LineStyles":
						LoadLineStyles(reader);
						break;
					default:
						throw new DesignException($"Unexpected style type '{typename}'.");
				}
			}
		}

		private void LoadFonts(XElement root)
		{
			if(root == null) return;
			Fonts = new List<Font>();
			foreach(XElement element in root.Elements())
			{
				Font style = Font.Load(element);
				Fonts.Add(style);
			}
		}

		private void LoadColors(XElement root)
		{
			if(root == null) return;
			Colors = new List<Color>();
			foreach(XElement element in root.Elements())
			{
				Color style = Color.Load(element);
				Colors.Add(style);
			}
		}

		private void LoadBorders(XElement root)
		{
			if(root == null) return;
			Borders = new List<Border>();
			foreach(XElement element in root.Elements())
			{
				Border style = Border.Load(element);
				Borders.Add(style);
			}
		}

		private void LoadPaddings(XElement root)
		{
			if(root == null) return;
			Paddings = new List<Padding>();
			foreach(XElement element in root.Elements())
			{
				Padding style = Padding.Load(element);
				Paddings.Add(style);
			}
		}

		private void LoadTextStyles(XElement root)
		{
			if(root == null) return;
			TextStyles = new List<TextStyle>();
			foreach(XElement element in root.Elements())
			{
				TextStyle style = TextStyle.Load(element);
				TextStyles.Add(style);
			}
		}

		private void LoadBulletStyles(XElement root)
		{
			if(root == null) return;
			BulletStyles = new List<BulletStyle>();
			foreach(XElement element in root.Elements())
			{
				BulletStyle style = BulletStyle.Load(element);
				BulletStyles.Add(style);
			}
		}

		private void LoadListStyles(XElement root)
		{
			if(root == null) return;
			ListStyles = new List<ListStyle>();
			foreach(XElement element in root.Elements())
			{
				ListStyle style = ListStyle.Load(element);
				ListStyles.Add(style);
			}
		}

		private void LoadPhotoStyles(XElement root)
		{
			if(root == null) return;
			PhotoStyles = new List<PhotoStyle>();
			foreach(XElement element in root.Elements())
			{
				PhotoStyle style = PhotoStyle.Load(element);
				PhotoStyles.Add(style);
			}
		}

		private void LoadTableStyles(XElement root)
		{
			if(root == null) return;
			TableStyles = new List<TableStyle>();
			foreach(XElement element in root.Elements())
			{
				TableStyle style = TableStyle.Load(element);
				TableStyles.Add(style);
			}
		}

		private void LoadTableRowStyles(XElement root)
		{
			if(root == null) return;
			TableRowStyles = new List<TableRowStyle>();
			foreach(XElement element in root.Elements())
			{
				TableRowStyle style = TableRowStyle.Load(element);
				TableRowStyles.Add(style);
			}
		}

		private void LoadTableCellStyles(XElement root)
		{
			if(root == null) return;
			TableCellStyles = new List<TableCellStyle>();
			foreach(XElement element in root.Elements())
			{
				TableCellStyle style = TableCellStyle.Load(element);
				TableCellStyles.Add(style);
			}
		}

		private void LoadLineStyles(XElement root)
		{
			if(root == null) return;
			LineStyles = new List<LineStyle>();
			foreach(XElement element in root.Elements())
			{
				LineStyle style = LineStyle.Load(element);
				LineStyles.Add(style);
			}
		}

		/// <summary>
		/// Writes a styles element, even if it's empty.
		/// </summary>
		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("Styles");

			WriteStyles(writer, Fonts,           "Fonts",           "Font"          );
			WriteStyles(writer, Colors,          "Colors",          "Color"         );
			WriteStyles(writer, Borders,         "Borders",         "Border"        );
			WriteStyles(writer, Paddings,        "Paddings",        "Padding"       );
			WriteStyles(writer, TextStyles,      "TextStyles",      "TextStyle"     );
			WriteStyles(writer, BulletStyles,    "BulletStyles",    "BulletStyle"   );
			WriteStyles(writer, ListStyles,      "ListStyles",      "ListStyle"     );
			WriteStyles(writer, PhotoStyles,     "PhotoStyles",     "PhotoStyle"    );
			WriteStyles(writer, TableStyles,     "TableStyles",     "TableStyle"    );
			WriteStyles(writer, TableRowStyles , "TableRowStyles",  "TableRowStyle" );
			WriteStyles(writer, TableCellStyles, "TableCellStyles", "TableCellStyle");
			WriteStyles(writer, LineStyles,      "LineStyles",      "LineStyle"     );

			writer.WriteEndElement();
		}

		private void WriteStyles<T>(XmlWriter writer, List<T> collection, string collectionName, string styleName) where T : Style
		{
			if(collection == null || collection.Count == 0) return;
			
			writer.WriteStartElement(collectionName);
			foreach(T style in collection)
			{
				writer.WriteStartElement(styleName);
				style.WriteXml(writer);
				writer.WriteEndElement();
			}
			writer.WriteEndElement();
		}

		private void LoadFonts(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				Font style = new Font(reader);
				Fonts.Add(style);
			}
		}

		private void LoadColors(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				Color style = new Color(reader);
				Colors.Add(style);
			}
		}

		private void LoadBorders(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				Border style = new Border(reader);
				Borders.Add(style);
			}
		}

		private void LoadPaddings(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				Padding style = new Padding(reader);
				Paddings.Add(style);
			}
		}

		private void LoadTextStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				TextStyle style = new TextStyle(reader);
				TextStyles.Add(style);
			}
		}

		private void LoadBulletStyles(JsonReader reader)
		{
			BulletStyles = new List<BulletStyle>();
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				BulletStyle style = new BulletStyle(reader);
				BulletStyles.Add(style);
			}
		}

		private void LoadListStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				ListStyle style = new ListStyle(reader);
				ListStyles.Add(style);
			}
		}

		private void LoadPhotoStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				PhotoStyle style = new PhotoStyle(reader);
				PhotoStyles.Add(style);
			}
		}

		private void LoadTableStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				TableStyle style = new TableStyle(reader);
				TableStyles.Add(style);
			}
		}

		private void LoadTableRowStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				TableRowStyle style = new TableRowStyle(reader);
				TableRowStyles.Add(style);
			}
		}

		private void LoadTableCellStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				TableCellStyle style = new TableCellStyle(reader);
				TableCellStyles.Add(style);
			}
		}

		private void LoadLineStyles(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				LineStyle style = new LineStyle(reader);
				LineStyles.Add(style);
			}
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();

			WriteStyles(writer, Fonts,           "Fonts"          );
			WriteStyles(writer, Colors,          "Colors"         );
			WriteStyles(writer, Borders,         "Borders"        );
			WriteStyles(writer, Paddings,        "Paddings"       );
			WriteStyles(writer, TextStyles,      "TextStyles"     );
			WriteStyles(writer, BulletStyles,    "BulletStyles"   );
			WriteStyles(writer, ListStyles,      "ListStyles"     );
			WriteStyles(writer, PhotoStyles,     "PhotoStyles"    );
			WriteStyles(writer, TableStyles,     "TableStyles"    );
			WriteStyles(writer, TableRowStyles , "TableRowStyles" );
			WriteStyles(writer, TableCellStyles, "TableCellStyles");
			WriteStyles(writer, LineStyles,      "LineStyles"     );

			writer.WriteEndObject();
		}

		private void WriteStyles<T>(JsonWriter writer, List<T> collection, string collectionName) where T : Style
		{
			if(collection == null || collection.Count == 0) return;
			
			writer.WritePropertyName(collectionName);
			writer.WriteStartArray();
			foreach(T style in collection)
			{
				writer.WriteStartObject();
				style.WriteJson(writer);
				writer.WriteEndObject();
			}
			writer.WriteEndArray();
		}
	}

	public class TextStyle : Style
	{
		public double? LineSpacing;
		public double? ParagraphSpacing;
		public TextAlignment? Alignment;
		public double? SoftBreakLimit;
		public string ListSeparator;
		public string ListTerminator;
		public Font Font;
		public Color Color;
		public Color BackColor;
		public Border Border;
		public Padding Padding;

		public static TextStyle Load(XElement element)
		{
			if(element == null) return null;
			return new TextStyle(element);
		}

		public TextStyle()
		{
		}

		public TextStyle(XElement element)
			:base(element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			LineSpacing      = XmlHelper.ReadDoubleElement(element, ns + "LineSpacing");
			ParagraphSpacing = XmlHelper.ReadDoubleElement(element, ns + "ParagraphSpacing");
			Alignment        = XmlHelper.ReadEnumElement<TextAlignment>(element, ns + "Alignment");
			SoftBreakLimit   = XmlHelper.ReadDoubleElement(element, ns + "SoftBreakLimit");
			ListSeparator    = XmlHelper.ReadStringElement(element, ns + "ListSeparator");
			ListTerminator   = XmlHelper.ReadStringElement(element, ns + "ListTerminator");
			Font      = Font   .Load(element.Element(ns + "Font"     ));
			Color     = Color  .Load(element.Element(ns + "Color"    ));
			BackColor = Color  .Load(element.Element(ns + "BackColor"));
			Border    = Border .Load(element.Element(ns + "Border"   ));
			Padding   = Padding.Load(element.Element(ns + "Padding"  ));
		}

		public TextStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "LineSpacing":
					LineSpacing = JsonHelper.ReadDouble(reader);
					return true;
				case "ParagraphSpacing":
					ParagraphSpacing = JsonHelper.ReadDouble(reader);
					return true;
				case "Alignment":
					Alignment = JsonHelper.ReadEnum<TextAlignment>(reader);
					return true;
				case "SoftBreakLimit":
					SoftBreakLimit = JsonHelper.ReadDouble(reader);
					return true;
				case "ListSeparator":
					ListSeparator = JsonHelper.ReadString(reader);
					return true;
				case "ListTerminator":
					ListTerminator = JsonHelper.ReadString(reader);
					return true;
				case "Font":
					reader.Read(); // advance to the start object token
					Font = new Font(reader);
					return true;
				case "Color":
					reader.Read(); // advance to the start object token
					Color = new Color(reader);
					return true;
				case "BackColor":
					reader.Read(); // advance to the start object token
					BackColor = new Color(reader);
					return true;
				case "Border":
					reader.Read(); // advance to the start object token
					Border = new Border(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO

			if(SoftBreakLimit < 0.0)
				throw new DesignException($"Soft break limit must be between 0.0 and 1.0. Found {SoftBreakLimit}.", this);

			Font     ?.Validate();
			Color    ?.Validate();
			BackColor?.Validate();
			Border   ?.Validate();
			Padding  ?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			WriteStyle(writer, Font, "Font");

			if(LineSpacing != null)
				writer.WriteElementString("LineSpacing", LineSpacing.ToString());
			if(ParagraphSpacing != null)
				writer.WriteElementString("ParagraphSpacing", ParagraphSpacing.ToString());
			if(Alignment != null)
				writer.WriteElementString("Alignment", Alignment.ToString());
			if(SoftBreakLimit != null)
				writer.WriteElementString("SoftBreakLimit", SoftBreakLimit.ToString());
			if(ListSeparator != null)
				writer.WriteElementString("ListSeparator", ListSeparator);
			if(ListTerminator != null)
				writer.WriteElementString("ListTerminator", ListTerminator);
			
			WriteStyle(writer, Color,     "Color"    );
			WriteStyle(writer, BackColor, "BackColor");
			WriteStyle(writer, Border,    "Border"   );
			WriteStyle(writer, Padding,   "Padding"  );

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			WriteStyle(writer, Font, "Font");

			if(LineSpacing != null)
			{
				writer.WritePropertyName("LineSpacing");
				writer.WriteValue(LineSpacing);
			}
			if(ParagraphSpacing != null)
			{
				writer.WritePropertyName("ParagraphSpacing");
				writer.WriteValue(ParagraphSpacing);
			}
			if(Alignment != null)
			{
				writer.WritePropertyName("Alignment");
				writer.WriteValue(Alignment.ToString());
			}
			if(SoftBreakLimit != null)
			{
				writer.WritePropertyName("SoftBreakLimit");
				writer.WriteValue(SoftBreakLimit);
			}
			if(ListSeparator != null)
			{
				writer.WritePropertyName("ListSeparator");
				writer.WriteValue(ListSeparator);
			}
			if(ListTerminator != null)
			{
				writer.WritePropertyName("ListTerminator");
				writer.WriteValue(ListTerminator);
			}

			WriteStyle(writer, Color,     "Color"    );
			WriteStyle(writer, BackColor, "BackColor");
			WriteStyle(writer, Border,    "Border"   );
			WriteStyle(writer, Padding,   "Padding"  );
		}
	}
	
	public class ListStyle : Style
	{
		public TextStyle ItemStyle;
		public BulletStyle BulletStyle;
		public BulletStyle SelectedBulletStyle;
		public BulletStyle UnselectedBulletStyle;
		public int? ItemIndent;
		public int? BulletIndent;
		public Border Border;
		public Padding Padding;

		public ListStyle(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			ItemStyle    = TextStyle  .Load(element?.Element(ns + "ItemStyle"));
			BulletStyle           = BulletStyle.Load(element?.Element(ns + "BulletStyle"));
			SelectedBulletStyle   = BulletStyle.Load(element?.Element(ns + "SelectedBulletStyle"));
			UnselectedBulletStyle = BulletStyle.Load(element?.Element(ns + "UnselectedBulletStyle"));
			ItemIndent   = XmlHelper.ReadIntElement(element, ns + "ItemIndent");
			BulletIndent = XmlHelper.ReadIntElement(element, ns + "BulletIndent");
			Border       = Border .Load(element?.Element(ns + "Border"));
			Padding      = Padding.Load(element?.Element(ns + "Padding"));
		}

		public static ListStyle Load(XElement element)
		{
			if(element == null) return null;
			return new ListStyle(element);
		}

		public ListStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "ItemStyle":
					reader.Read(); // advance to the start object token
					ItemStyle = new TextStyle(reader);
					return true;
				case "BulletStyle":
					reader.Read(); // advance to the start object token
					BulletStyle = new BulletStyle(reader);
					return true;
				case "SelectedBulletStyle":
					reader.Read(); // advance to the start object token
					SelectedBulletStyle = new BulletStyle(reader);
					return true;
				case "UnselectedBulletStyle":
					reader.Read(); // advance to the start object token
					UnselectedBulletStyle = new BulletStyle(reader);
					return true;
				case "ItemIndent":
					ItemIndent = JsonHelper.ReadInteger(reader);
					return true;
				case "BulletIndent":
					BulletIndent = JsonHelper.ReadInteger(reader);
					return true;
				case "Border":
					reader.Read(); // advance to the start object token
					Border = new Border(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO: indents
			ItemStyle?.Validate();
			BulletStyle          ?.Validate();
			SelectedBulletStyle  ?.Validate();
			UnselectedBulletStyle?.Validate();
			Border ?.Validate();
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(ItemIndent != null)
				writer.WriteElementString("ItemIndent", ItemIndent.ToString());
			if(BulletIndent != null)
				writer.WriteElementString("BulletIndent", BulletIndent.ToString());
			
			WriteStyle(writer, ItemStyle, "ItemStyle");
			WriteStyle(writer, BulletStyle,          "BulletStyle");
			WriteStyle(writer, SelectedBulletStyle,   "SelectedBulletStyle");
			WriteStyle(writer, UnselectedBulletStyle, "UnselectedBulletStyle");
			WriteStyle(writer, Border,  "Border");
			WriteStyle(writer, Padding, "Padding");

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(ItemIndent != null)
			{
				writer.WritePropertyName("ItemIndent");
				writer.WriteValue(ItemIndent);
			}
			if(BulletIndent != null)
			{
				writer.WritePropertyName("BulletIndent");
				writer.WriteValue(BulletIndent);
			}

			WriteStyle(writer, ItemStyle, "ItemStyle");
			WriteStyle(writer, BulletStyle,           "BulletStyle");
			WriteStyle(writer, SelectedBulletStyle,   "SelectedBulletStyle");
			WriteStyle(writer, UnselectedBulletStyle, "UnselectedBulletStyle");
			WriteStyle(writer, Border,  "Border" );
			WriteStyle(writer, Padding, "Padding");
		}
	}
	
	public class BulletStyle : Style
	{
		public string BulletText;
		public NumberStyle? NumberStyle;
		public int? StartAt;
		public Font Font;
		public Color Color;
		public Padding Padding;

		public BulletStyle(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			BulletText  = XmlHelper.ReadStringElement(element, ns + "BulletText");
			NumberStyle = XmlHelper.ReadEnumElement<NumberStyle>(element, ns + "NumberStyle");
			StartAt     = XmlHelper.ReadIntElement(element, ns + "StartAt");
			Font    = Font   .Load(element?.Element(ns + "Font"   ));
			Color   = Color  .Load(element?.Element(ns + "Color"  ));
			Padding = Padding.Load(element?.Element(ns + "Padding"));
		}

		public static BulletStyle Load(XElement element)
		{
			if(element == null) return null;
			return new BulletStyle(element);
		}

		public BulletStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "BulletText":
					BulletText = JsonHelper.ReadString(reader);
					return true;
				case "NumberStyle":
					NumberStyle = JsonHelper.ReadEnum<NumberStyle>(reader);
					return true;
				case "StartAt":
					StartAt = JsonHelper.ReadInteger(reader);
					return true;
				case "Font":
					reader.Read(); // advance to the start object token
					Font = new Font(reader);
					return true;
				case "Color":
					reader.Read(); // advance to the start object token
					Color = new Color(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO
			Font   ?.Validate();
			Color  ?.Validate();
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(BulletText != null)
				writer.WriteElementString("BulletText", BulletText);
			if(NumberStyle != null)
				writer.WriteElementString("NumberStyle", NumberStyle.ToString());
			if(StartAt != null)
				writer.WriteElementString("StartAt", StartAt.ToString());

			WriteStyle(writer, Font,    "Font"   );
			WriteStyle(writer, Color,   "Color"  );
			WriteStyle(writer, Padding, "Padding");

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(BulletText != null)
			{
				writer.WritePropertyName("BulletText");
				writer.WriteValue(BulletText);
			}
			if(NumberStyle != null)
			{
				writer.WritePropertyName("NumberStyle");
				writer.WriteValue(NumberStyle.ToString());
			}
			if(StartAt != null)
			{
				writer.WritePropertyName("StartAt");
				writer.WriteValue(StartAt);
			}

			WriteStyle(writer, Font,    "Font"  );
			WriteStyle(writer, Color,   "Color" );
			WriteStyle(writer, Padding, "Padding");
		}
	}
	
	public class PhotoStyle : Style
	{
		public TextStyle CaptionStyle;
		public Border Border;
		public Padding Padding;

		public PhotoStyle(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			CaptionStyle = TextStyle.Load(element?.Element(ns + "CaptionStyle"));
			Border  = Border .Load(element?.Element(ns + "Border" ));
			Padding = Padding.Load(element?.Element(ns + "Padding"));
		}

		public static PhotoStyle Load(XElement element)
		{
			if(element == null) return null;
			return new PhotoStyle(element);
		}

		public PhotoStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "CaptionStyle":
					reader.Read(); // advance to the start object token
					CaptionStyle = new TextStyle(reader);
					return true;
				case "Border":
					reader.Read(); // advance to the start object token
					Border = new Border(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			CaptionStyle?.Validate();
			Border      ?.Validate();
			Padding     ?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			WriteStyle(writer, CaptionStyle, "CaptionStyle");
			WriteStyle(writer, Border,       "Border");
			WriteStyle(writer, Padding,      "Padding");

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			WriteStyle(writer, CaptionStyle,   "CaptionStyle");
			WriteStyle(writer, Border,         "Border"      );
			WriteStyle(writer, Padding,        "Padding"     );
		}
	}
	
	public class TableStyle : Style
	{
		public Border Border;
		public Padding Padding;

		public TableStyle(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Border  = Border .Load(element?.Element(ns + "Border" ));
			Padding = Padding.Load(element?.Element(ns + "Padding"));
		}

		public static TableStyle Load(XElement element)
		{
			if(element == null) return null;
			return new TableStyle(element);
		}

		public TableStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Border":
					reader.Read(); // advance to the start object token
					Border = new Border(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			Border ?.Validate();
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);
			WriteStyle(writer, Border,  "Border");
			WriteStyle(writer, Padding, "Padding");
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			WriteStyle(writer, Border,  "Border" );
			WriteStyle(writer, Padding, "Padding");
		}
	}
	
	public class TableRowStyle : Style
	{
		public Border Border;
		public Padding Padding;
		public Color BackColor;

		public TableRowStyle(XElement element)
			:base(element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			Border    = Border .Load(element.Element(ns + "Border"   ));
			Padding   = Padding.Load(element.Element(ns + "Padding"  ));
			BackColor = Color  .Load(element.Element(ns + "BackColor"));
		}

		public static TableRowStyle Load(XElement element)
		{
			if(element == null) return null;
			return new TableRowStyle(element);
		}

		public TableRowStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "BackColor":
					reader.Read(); // advance to the start object token
					BackColor = new Color(reader);
					return true;
				case "Border":
					reader.Read(); // advance to the start object token
					Border = new Border(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			Border ?.Validate();
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);
			WriteStyle(writer, Border,    "Border"   );
			WriteStyle(writer, Padding,   "Padding"  );
			WriteStyle(writer, BackColor, "BackColor");
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			WriteStyle(writer, Border,    "Border"   );
			WriteStyle(writer, Padding,   "Padding"  );
			WriteStyle(writer, BackColor, "BackColor");
		}
	}
	
	public class TableCellStyle : Style
	{
		public Padding Padding;

		public TableCellStyle(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Padding = Padding.Load(element?.Element(ns + "Padding"));
		}

		public static TableCellStyle Load(XElement element)
		{
			if(element == null) return null;
			return new TableCellStyle(element);
		}

		public TableCellStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);
			WriteStyle(writer, Padding, "Padding");
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);
			WriteStyle(writer, Padding, "Padding");
		}
	}
	
	public class LineStyle : Style
	{
		public int? Thickness;
		public Padding Padding;
		public Color Color;

		public LineStyle(XElement element)
			:base(element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			Thickness = XmlHelper.ReadIntElement(element, ns + "Thickness");
			Padding = Padding.Load(element.Element(ns + "Padding"));
			Color   = Color  .Load(element.Element(ns + "Color"  ));
		}

		public static LineStyle Load(XElement element)
		{
			if(element == null) return null;
			return new LineStyle(element);
		}

		public LineStyle(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Thickness":
					Thickness = JsonHelper.ReadInteger(reader);
					return true;
				case "Color":
					reader.Read(); // advance to the start object token
					Color = new Color(reader);
					return true;
				case "Padding":
					reader.Read(); // advance to the start object token
					Padding = new Padding(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO: thickness
			Color  ?.Validate();
			Padding?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(Thickness != null)
				writer.WriteElementString("Thickness", Thickness.ToString());
			
			WriteStyle(writer, Color,   "Color");
			WriteStyle(writer, Padding, "Padding");

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Thickness != null)
			{
				writer.WritePropertyName("Thickness");
				writer.WriteValue(Thickness);
			}

			WriteStyle(writer, Color,   "Color"  );
			WriteStyle(writer, Padding, "Padding");
		}
	}
	
	public class Font : Style
	{
		public string FamilyName;
		public int? Size;
		public bool? Bold;
		public bool? Italic;
		public bool? Underline;

		public Font()
		{
		}

		public Font(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			FamilyName = XmlHelper.ReadStringElement(element, ns + "FamilyName");
			Size       = XmlHelper.ReadIntElement   (element, ns + "Size"      );
			Bold       = XmlHelper.ReadBoolElement  (element, ns + "Bold"      );
			Italic     = XmlHelper.ReadBoolElement  (element, ns + "Italic"    );
			Underline  = XmlHelper.ReadBoolElement  (element, ns + "Underline" );
		}

		public static Font Load(XElement element)
		{
			if(element == null) return null;
			return new Font(element);
		}

		public Font(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "FamilyName":
					FamilyName = JsonHelper.ReadString(reader);
					return true;
				case "Size":
					Size = JsonHelper.ReadInteger(reader);
					return true;
				case "Bold":
					Bold = JsonHelper.ReadBoolean(reader);
					return true;
				case "Italic":
					Italic = JsonHelper.ReadBoolean(reader);
					return true;
				case "Underline":
					Underline = JsonHelper.ReadBoolean(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(FamilyName != null)
				writer.WriteElementString("FamilyName", FamilyName);
			if(Size != null)
				writer.WriteElementString("Size", Size.ToString());
			if(Bold != null)
				writer.WriteElementString("Bold", Bold.Value ? "true" : "false"); // XML requires bools in lowercase
			if(Italic != null)
				writer.WriteElementString("Italic", Italic.Value ? "true" : "false");
			if(Underline != null)
				writer.WriteElementString("Underline", Underline.Value ? "true" : "false");
			
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(FamilyName != null)
			{
				writer.WritePropertyName("FamilyName");
				writer.WriteValue(FamilyName);
			}
			if(Size != null)
			{
				writer.WritePropertyName("Size");
				writer.WriteValue(Size);
			}
			if(Bold != null)
			{
				writer.WritePropertyName("Bold");
				writer.WriteValue(Bold);
			}
			if(Italic != null)
			{
				writer.WritePropertyName("Italic");
				writer.WriteValue(Italic);
			}
			if(Underline != null)
			{
				writer.WritePropertyName("Underline");
				writer.WriteValue(Underline);
			}
		}
	}
	
	public class Color : Style
	{
		public double? Red;
		public double? Green;
		public double? Blue;

		public Color(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Red   = XmlHelper.ReadDoubleElement(element, ns + "Red"  );
			Green = XmlHelper.ReadDoubleElement(element, ns + "Green");
			Blue  = XmlHelper.ReadDoubleElement(element, ns + "Blue" );
		}

		public static Color Load(XElement element)
		{
			if(element == null) return null;
			return new Color(element);
		}

		public Color(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Red":
					Red = JsonHelper.ReadDouble(reader);
					return true;
				case "Green":
					Green = JsonHelper.ReadDouble(reader);
					return true;
				case "Blue":
					Blue = JsonHelper.ReadDouble(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			if(Red < 0.0 || Red > 1.0)
				throw new DesignException($"Color red component {Red} out of range. Must be between 0.0 and 1.0.");
			if(Green < 0.0 || Green > 1.0)
				throw new DesignException($"Color green component {Green} out of range. Must be between 0.0 and 1.0.");
			if(Blue < 0.0 || Blue > 1.0)
				throw new DesignException($"Color blue component {Blue} out of range. Must be between 0.0 and 1.0.");
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(Red != null)
				writer.WriteElementString("Red", Red.ToString());
			if(Green != null)
				writer.WriteElementString("Green", Green.ToString());
			if(Blue != null)
				writer.WriteElementString("Blue", Blue.ToString());
			
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Red != null)
			{
				writer.WritePropertyName("Red");
				writer.WriteValue(Red);
			}
			if(Green != null)
			{
				writer.WritePropertyName("Green");
				writer.WriteValue(Green);
			}
			if(Blue != null)
			{
				writer.WritePropertyName("Blue");
				writer.WriteValue(Blue);
			}
		}
	}
	
	public class Border : Style
	{
		public Stroke Stroke;
		public Color Color;
		public BorderParts Parts;

		public Border(XElement element)
			:base(element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			Stroke = Stroke     .Load(element.Element(ns + "Stroke"));
			Color  = Color      .Load(element.Element(ns + "Color" ));
			Parts  = BorderParts.Load(element.Element(ns + "Parts" ));
		}

		public static Border Load(XElement element)
		{
			if(element == null) return null;
			return new Border(element);
		}

		public Border(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Stroke":
					reader.Read(); // advance to the start object token
					Stroke = new Stroke(reader);
					return true;
				case "Color":
					reader.Read(); // advance to the start object token
					Color = new Color(reader);
					return true;
				case "Parts":
					reader.Read(); // advance to the start object token
					Parts = new BorderParts(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO
			Color?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(Stroke != null)
			{
				writer.WriteStartElement("Stroke");
				Stroke.WriteXml(writer);
				writer.WriteEndElement();
			}
			if(Color != null)
			{
				writer.WriteStartElement("Color");
				Color.WriteXml(writer);
				writer.WriteEndElement();
			}
			if(Parts != null)
			{
				writer.WriteStartElement("Parts");
				Parts.WriteXml(writer);
				writer.WriteEndElement();
			}
			
			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Stroke != null)
			{
				writer.WritePropertyName("Stroke");
				writer.WriteStartObject();
				Stroke.WriteJson(writer);
				writer.WriteEndObject();
			}
			if(Color != null)
			{
				writer.WritePropertyName("Color");
				writer.WriteStartObject();
				Color.WriteJson(writer);
				writer.WriteEndObject();
			}
			if(Parts != null)
			{
				writer.WritePropertyName("Parts");
				writer.WriteStartObject();
				Parts.WriteJson(writer);
				writer.WriteEndObject();
			}
		}
	}

	public class Stroke : ILineNumber
	{
		public int? Thickness;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Stroke()
		{
		}

		public Stroke(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Thickness = XmlHelper.ReadIntElement(element, ns + "Thickness");

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public static Stroke Load(XElement element)
		{
			if(element == null) return null;
			return new Stroke(element);
		}

		public Stroke(JsonReader reader)
		{
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;
				LoadProperty(reader, propName);
			}
		}

		private bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "Thickness":
					Thickness = JsonHelper.ReadInteger(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			if(Thickness != null)
				writer.WriteElementString("Thickness", Thickness.ToString());
		}

		public void WriteJson(JsonWriter writer)
		{
			if(Thickness != null)
			{
				writer.WritePropertyName("Thickness");
				writer.WriteValue(Thickness);
			}
		}
	}

	public class BorderParts : ILineNumber
	{
		public bool? Left;
		public bool? Bottom;
		public bool? Right;
		public bool? Top;
		public bool? InnerHorizontal;
		public bool? InnerVertical;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public BorderParts()
		{
		}

		public BorderParts(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Left   = XmlHelper.ReadBoolElement(element, ns + "Left"  );
			Bottom = XmlHelper.ReadBoolElement(element, ns + "Bottom");
			Right  = XmlHelper.ReadBoolElement(element, ns + "Right" );
			Top    = XmlHelper.ReadBoolElement(element, ns + "Top"   );
			InnerHorizontal = XmlHelper.ReadBoolElement(element, ns + "InnerHorizontal");
			InnerVertical   = XmlHelper.ReadBoolElement(element, ns + "InnerVertical"  );

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public static BorderParts Load(XElement element)
		{
			if(element == null) return null;
			return new BorderParts(element);
		}

		public BorderParts(JsonReader reader)
		{
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;
				LoadProperty(reader, propName);
			}
		}

		private bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "Left":
					Left = JsonHelper.ReadBoolean(reader);
					return true;
				case "Bottom":
					Bottom = JsonHelper.ReadBoolean(reader);
					return true;
				case "Right":
					Right = JsonHelper.ReadBoolean(reader);
					return true;
				case "Top":
					Top = JsonHelper.ReadBoolean(reader);
					return true;
				case "InnerHorizontal":
					InnerHorizontal = JsonHelper.ReadBoolean(reader);
					return true;
				case "InnerVertical":
					InnerVertical = JsonHelper.ReadBoolean(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			//	XML requires bools in lowercase
			if(Left != null)
				writer.WriteElementString("Left", Left.Value ? "true" : "false");
			if(Bottom != null)
				writer.WriteElementString("Bottom", Bottom.Value ? "true" : "false");
			if(Right != null)
				writer.WriteElementString("Right", Right.Value ? "true" : "false");
			if(Top != null)
				writer.WriteElementString("Top", Top.Value ? "true" : "false");
			if(InnerHorizontal != null)
				writer.WriteElementString("InnerHorizontal", InnerHorizontal.Value ? "true" : "false");
			if(InnerVertical != null)
				writer.WriteElementString("InnerVertical", InnerVertical.Value ? "true" : "false");
		}

		public void WriteJson(JsonWriter writer)
		{
			if(Left != null)
			{
				writer.WritePropertyName("Left");
				writer.WriteValue(Left);
			}
			if(Bottom != null)
			{
				writer.WritePropertyName("Bottom");
				writer.WriteValue(Bottom);
			}
			if(Right != null)
			{
				writer.WritePropertyName("Right");
				writer.WriteValue(Right);
			}
			if(Top != null)
			{
				writer.WritePropertyName("Top");
				writer.WriteValue(Top);
			}
			if(InnerHorizontal != null)
			{
				writer.WritePropertyName("InnerHorizontal");
				writer.WriteValue(InnerHorizontal);
			}
			if(InnerVertical != null)
			{
				writer.WritePropertyName("InnerVertical");
				writer.WriteValue(InnerVertical);
			}
		}
	}

	public class Padding : Style
	{
		public int? Left;
		public int? Bottom;
		public int? Right;
		public int? Top;

		public Padding()
		{
			Left   = 0;
			Bottom = 0;
			Right  = 0;
			Top    = 0;
		}

		public Padding(int left, int bottom, int right, int top)
		{
			Left   = left;
			Bottom = bottom;
			Right  = right;
			Top    = top;
		}

		public Padding(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Left   = XmlHelper.ReadIntElement(element, ns + "Left"  );
			Bottom = XmlHelper.ReadIntElement(element, ns + "Bottom");
			Right  = XmlHelper.ReadIntElement(element, ns + "Right" );
			Top    = XmlHelper.ReadIntElement(element, ns + "Top"   );
		}

		public static Padding Load(XElement element)
		{
			if(element == null) return null;
			return new Padding(element);
		}

		public Padding(JsonReader reader)
			:base(reader)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "Left":
					Left = JsonHelper.ReadInteger(reader);
					return true;
				case "Bottom":
					Bottom = JsonHelper.ReadInteger(reader);
					return true;
				case "Right":
					Right = JsonHelper.ReadInteger(reader);
					return true;
				case "Top":
					Top = JsonHelper.ReadInteger(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public override void Validate()
		{
			//TODO
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			WriteXml_BaseAttributes(writer);

			if(Left != null)
				writer.WriteElementString("Left", Left.ToString());
			if(Bottom != null)
				writer.WriteElementString("Bottom", Bottom.ToString());
			if(Right != null)
				writer.WriteElementString("Right", Right.ToString());
			if(Top != null)
				writer.WriteElementString("Top", Top.ToString());

			WriteXml_BaseElements(writer);
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Left != null)
			{
				writer.WritePropertyName("Left");
				writer.WriteValue(Left);
			}
			if(Bottom != null)
			{
				writer.WritePropertyName("Bottom");
				writer.WriteValue(Bottom);
			}
			if(Right != null)
			{
				writer.WritePropertyName("Right");
				writer.WriteValue(Right);
			}
			if(Top != null)
			{
				writer.WritePropertyName("Top");
				writer.WriteValue(Top);
			}
		}
	}
}
