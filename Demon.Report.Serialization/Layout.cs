using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public abstract class Layout : ILineNumber
	{
		public abstract LayoutType Type { get; set; }
		public string id;
		public string parentid;
		public int? ordinal;
		public string name;
		public string source;

		protected List<Layout> _sublayouts = new List<Layout>();

		public List<Condition> Conditions = new List<Condition>();
		public PageBreakRules PageBreakRules;
		public List<KeyValuePair<string, string>> TermDictionary = new List<KeyValuePair<string, string>>();

		public bool? traceLayout;
		public bool? traceText;
		public bool? tracePath;
		public bool? traceOutline;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Layout()
		{
		}

		public Layout(XElement element)
		{
			id       = XmlHelper.ReadStringAttribute(element, "id"      );
			parentid = XmlHelper.ReadStringAttribute(element, "parentid");
			ordinal  = XmlHelper.ReadIntAttribute   (element, "ordinal" );
			name     = XmlHelper.ReadStringAttribute(element, "name"    );
			source   = XmlHelper.ReadStringAttribute(element, "source"  );

			traceLayout  = XmlHelper.ReadBoolAttribute(element, "traceLayout" );
			traceText    = XmlHelper.ReadBoolAttribute(element, "traceText"   );
			tracePath    = XmlHelper.ReadBoolAttribute(element, "tracePath"   );
			traceOutline = XmlHelper.ReadBoolAttribute(element, "traceOutline");

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Layout(JsonReader reader)
		{
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;
				LoadProperty(reader, propName);
			}
		}

		protected Layout(Layout src)
		{
			id     = src.id;
			name   = src.name;
			source = src.source;

			foreach(Layout sub in src._sublayouts)
			{
				Layout copy = sub.Copy();
				_sublayouts.Add(copy);
			}

			foreach(Condition condition in src.Conditions)
			{
				Condition copy = null;
				switch(condition.Type)
				{
					case ConditionType.EmptyLayoutCondition:
						copy = new EmptyLayoutCondition((EmptyLayoutCondition)condition);
						break;
					case ConditionType.OptionSelectedCondition:
						copy = new OptionSelectedCondition((OptionSelectedCondition)condition);
						break;
					case ConditionType.ContentSelectedCondition:
						copy = new ContentSelectedCondition((ContentSelectedCondition)condition);
						break;
					case ConditionType.DocTagCondition:
						copy = new DocTagCondition((DocTagCondition)condition);
						break;
					case ConditionType.ContentDocTagCondition:
						copy = new ContentDocTagCondition((ContentDocTagCondition)condition);
						break;
					case ConditionType.ItemCountCondition:
						copy = new ItemCountCondition((ItemCountCondition)condition);
						break;
					case ConditionType.PhotoCountCondition:
						copy = new PhotoCountCondition((PhotoCountCondition)condition);
						break;
				}
				Conditions.Add(copy);
			}

			if(src.PageBreakRules != null)
				PageBreakRules = new PageBreakRules(src.PageBreakRules);

			foreach(KeyValuePair<string, string> term in src.TermDictionary)
				TermDictionary.Add(new KeyValuePair<string, string>(term.Key, term.Value));

			traceLayout  = src.traceLayout;
			traceText    = src.traceText;
			tracePath    = src.tracePath;
			traceOutline = src.traceOutline;

			LineNumber   = src.LineNumber;
			LinePosition = src.LinePosition;
		}

		public static Layout Load(XElement element)
		{
			if(element == null) return null;

			Layout layout = null;
			LayoutType type = LayoutType.None;
			bool ok = Enum.TryParse<LayoutType>(element.Name.LocalName, out type);
			switch(type)
			{
				case LayoutType.ChapterLayout:
					layout = new ChapterLayout(element);
					break;
				case LayoutType.GroupLayout:
					layout = new GroupLayout(element);
					break;
				case LayoutType.TextLayout:
					layout = new TextLayout(element);
					break;
				case LayoutType.ListLayout:
					layout = new ListLayout(element);
					break;
				case LayoutType.TableLayout:
					layout = new TableLayout(element);
					break;
				case LayoutType.TableRowLayout:
					layout = new TableRowLayout(element);
					break;
				case LayoutType.TableCellLayout:
					layout = new TableCellLayout(element);
					break;
				case LayoutType.PhotoTableLayout:
					layout = new PhotoTableLayout(element);
					break;
				case LayoutType.LineLayout:
					layout = new LineLayout(element);
					break;
				case LayoutType.SpaceLayout:
					layout = new SpaceLayout(element);
					break;
				case LayoutType.PictureLayout:
					layout = new PictureLayout(element);
					break;
				default:
					throw new DesignException($"Unrecognized layout type '{element.Name.LocalName}'.", element);
			}

			XNamespace ns = element.GetDefaultNamespace();
			layout.LoadSubLayouts    (element?.Element(ns + "Layouts"       ));
			layout.LoadConditions    (element?.Element(ns + "Conditions"    ));
			layout.LoadPageBreakRules(element?.Element(ns + "PageBreakRules"));
			layout.LoadTermDictionary(element?.Element(ns + "TermDictionary"));
			return layout;
		}

		protected void LoadSubLayouts(XElement root)
		{
			if(root == null) return;
			foreach(XElement element in root.Elements())
			{
				Layout layout = Layout.Load(element);
				if(layout != null)
					_sublayouts.Add(layout);
			}
		}

		protected void LoadConditions(XElement root)
		{
			if(root == null) return;
			foreach(XElement element in root.Elements())
			{
				Condition condition = Condition.Load(element);
				if(condition != null)
				{
					//	Create our conditions collection now that we know
					//	we need it
					if(Conditions == null)
						Conditions = new List<Condition>();

					Conditions.Add(condition);
				}
			}
		}

		protected void LoadPageBreakRules(XElement root)
		{
			if(root == null) return;
			PageBreakRules = new PageBreakRules(root);
		}

		protected void LoadTermDictionary(XElement root)
		{
			if(root == null) return;
			TermDictionary = new List<KeyValuePair<string, string>>();
			foreach(XElement element in root.Elements())
			{
				string key = element.Attribute("key").Value;
				string value = element.Value;
				TermDictionary.Add(new KeyValuePair<string, string>(key, value));
			}
		}

		public static Layout Load(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartObject);

			//	We depend on the LayoutType property being first so that we can
			//	know what type to create
			reader.Read();
			string propName = (string)reader.Value;
			if(propName != "LayoutType")
				throw new DesignException($"Expected LayoutType property, got '{propName}'.", new LineNumberCapture(reader));
				
			Layout layout = null;
			string typename = JsonHelper.ReadString(reader);
			LayoutType type = LayoutType.None;
			bool ok = Enum.TryParse<LayoutType>(typename, out type);
			switch(type)
			{
				case LayoutType.ChapterLayout:
					layout = new ChapterLayout(reader);
					break;
				case LayoutType.GroupLayout:
					layout = new GroupLayout(reader);
					break;
				case LayoutType.TextLayout:
					layout = new TextLayout(reader);
					break;
				case LayoutType.ListLayout:
					layout = new ListLayout(reader);
					break;
				case LayoutType.TableLayout:
					layout = new TableLayout(reader);
					break;
				case LayoutType.TableRowLayout:
					layout = new TableRowLayout(reader);
					break;
				case LayoutType.TableCellLayout:
					layout = new TableCellLayout(reader);
					break;
				case LayoutType.PhotoTableLayout:
					layout = new PhotoTableLayout(reader);
					break;
				case LayoutType.LineLayout:
					layout = new LineLayout(reader);
					break;
				case LayoutType.SpaceLayout:
					layout = new SpaceLayout(reader);
					break;
				case LayoutType.PictureLayout:
					layout = new PictureLayout(reader);
					break;
				default:
					throw new DesignException($"Unrecognized layout type '{typename}'.", new LineNumberCapture(reader));
			}
			return layout;
		}

		protected virtual bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "id":
					id = JsonHelper.ReadString(reader);
					return true;
				case "parentid":
					parentid = JsonHelper.ReadString(reader);
					return true;
				case "ordinal":
					ordinal = JsonHelper.ReadInteger(reader);
					return true;
				case "name":
					name = JsonHelper.ReadString(reader);
					return true;
				case "lineNumber":
					LineNumber = JsonHelper.ReadInteger(reader) ?? 0;
					return true;
				case "linePosition":
					LinePosition = JsonHelper.ReadInteger(reader) ?? 0;
					return true;
				case "source":
					source = JsonHelper.ReadString(reader);
					return true;
				case "traceLayout":
					traceLayout = JsonHelper.ReadBoolean(reader);
					return true;
				case "traceText":
					traceText = JsonHelper.ReadBoolean(reader);
					return true;
				case "tracePath":
					tracePath = JsonHelper.ReadBoolean(reader);
					return true;
				case "traceOutline":
					traceOutline = JsonHelper.ReadBoolean(reader);
					return true;
				case "Layouts":
					reader.Read(); // advance to the start array token
					LoadSubLayouts(reader);
					return true;
				case "Conditions":
					reader.Read(); // advance to the start array token
					LoadConditions(reader);
					return true;
				case "PageBreakRules":
					reader.Read(); // advance to the start object token
					LoadPageBreakRules(reader);
					return true;
				case "TermDictionary":
					reader.Read(); // advance to the start array token
					LoadTermDictionary(reader);
					return true;
				default:
					//	Not a base class property - let the derived class load it
					return false;
			}
		}

		protected void LoadSubLayouts(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);

				Layout layout = Layout.Load(reader);
				_sublayouts.Add(layout);
			}
		}

		protected void LoadConditions(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);
				reader.Read(); // advance to the start object token

				Condition condition = Condition.Load(reader);
				Conditions.Add(condition);
			}
		}

		protected void LoadPageBreakRules(JsonReader reader)
		{
			PageBreakRules = new PageBreakRules(reader);
		}

		protected void LoadTermDictionary(JsonReader reader)
		{
			TermDictionary = new List<KeyValuePair<string, string>>();

			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);
				reader.Read(); // advance to the first property

				string key = (string)reader.Value;
				string value = JsonHelper.ReadString(reader);
				TermDictionary.Add(new KeyValuePair<string, string>(key, value));

				reader.Read(); // read the end object token
			}
		}

		public virtual void Validate()
		{
			//TODO: other checks

			foreach(Layout layout in _sublayouts)
				layout.Validate();
		}

		public Layout FindSubLayout(string id)
		{
			foreach(Layout layout in _sublayouts)
			{
				if(layout.id == id) return layout;

				Layout found = layout.FindSubLayout(id);
				if(found != null) return found;
			}
			return null;
		}

		public List<Layout> FindSubLayouts(string name)
		{
			List<Layout> layouts = new List<Layout>();
			foreach(Layout layout in _sublayouts)
			{
				if(layout.name == name)
					layouts.Add(layout);
				
				layouts.AddRange(layout.FindSubLayouts(name));
			}
			return layouts;
		}

		/// <summary>
		///	Make a copy of this layout and its metadata, but do not copy
		///	any sublayouts.
		/// </summary>
		protected virtual Layout Copy()
		{
			Layout copy = null;
			switch(Type)
			{
				case LayoutType.ChapterLayout:
					copy = new ChapterLayout((ChapterLayout)this);
					break;
				case LayoutType.GroupLayout:
					copy = new GroupLayout((GroupLayout)this);
					break;
				case LayoutType.TextLayout:
					copy = new TextLayout((TextLayout)this);
					break;
				case LayoutType.ListLayout:
					copy = new ListLayout((ListLayout)this);
					break;
				case LayoutType.TableLayout:
					copy = new TableLayout((TableLayout)this);
					break;
				case LayoutType.TableRowLayout:
					copy = new TableRowLayout((TableRowLayout)this);
					break;
				case LayoutType.TableCellLayout:
					copy = new TableCellLayout((TableCellLayout)this);
					break;
				case LayoutType.PhotoTableLayout:
					copy = new PhotoTableLayout((PhotoTableLayout)this);
					break;
				case LayoutType.PictureLayout:
					copy = new PictureLayout((PictureLayout)this);
					break;
				case LayoutType.SpaceLayout:
					copy = new SpaceLayout((SpaceLayout)this);
					break;
				case LayoutType.LineLayout:
					copy = new LineLayout((LineLayout)this);
					break;
			}
			return copy;
		}

		public abstract void WriteXml(XmlWriter writer);

		protected void WriteXml_BaseAttributes(XmlWriter writer)
		{
			if(id != null)
				writer.WriteAttributeString("id", id);
			if(parentid != null)
				writer.WriteAttributeString("parentid", parentid);
			if(ordinal != null)
				writer.WriteAttributeString("ordinal", ordinal.ToString());
			if(name != null)
				writer.WriteAttributeString("name", name);
			if(source != null)
				writer.WriteAttributeString("source", source);

			//	XML requires bools in lowercase
			if(traceLayout != null)
				writer.WriteAttributeString("traceLayout", traceLayout.Value ? "true" : "false");
			if(traceText != null)
				writer.WriteAttributeString("traceText", traceText.Value ? "true" : "false");
			if(tracePath != null)
				writer.WriteAttributeString("tracePath", tracePath.Value ? "true" : "false");
			if(traceOutline != null)
				writer.WriteAttributeString("traceOutline", traceOutline.Value ? "true" : "false");
		}

		protected void WriteXml_BaseElements(XmlWriter writer)
		{
			if(_sublayouts.Count > 0)
			{
				writer.WriteStartElement("Layouts");
				foreach(Layout layout in _sublayouts)
					layout.WriteXml(writer);
				writer.WriteEndElement();
			}

			if(Conditions.Count > 0)
			{
				writer.WriteStartElement("Conditions");
				foreach(Condition condition in Conditions)
					condition.WriteXml(writer);
				writer.WriteEndElement();
			}

			if(PageBreakRules != null)
			{
				writer.WriteStartElement("PageBreakRules");
				PageBreakRules.WriteXml(writer);
				writer.WriteEndElement();
			}

			if(TermDictionary.Count > 0)
			{
				writer.WriteStartElement("TermDictionary");
				foreach(KeyValuePair<string, string> term in TermDictionary)
				{
					writer.WriteStartElement("Term");
					writer.WriteAttributeString("key", term.Key);
					writer.WriteString(term.Value);
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
			}
		}
		
		public abstract void WriteJson(JsonWriter writer);

		protected void WriteJson_BaseProperties(JsonWriter writer)
		{
			writer.WritePropertyName("LayoutType");
			writer.WriteValue(Type.ToString());

			if(id != null)
			{
				writer.WritePropertyName("id");
				writer.WriteValue(id);
			}
			if(parentid != null)
			{
				writer.WritePropertyName("parentid");
				writer.WriteValue(parentid);
			}
			if(ordinal != null)
			{
				writer.WritePropertyName("ordinal");
				writer.WriteValue(ordinal);
			}
			if(name != null)
			{
				writer.WritePropertyName("name");
				writer.WriteValue(name);
			}
			if(source != null)
			{
				writer.WritePropertyName("source");
				writer.WriteValue(source);
			}

			writer.WritePropertyName("lineNumber");
			writer.WriteValue(LineNumber);
			writer.WritePropertyName("linePosition");
			writer.WriteValue(LinePosition);

			if(traceLayout != null)
			{
				writer.WritePropertyName("traceLayout");
				writer.WriteValue(traceLayout);
			}
			if(traceText != null)
			{
				writer.WritePropertyName("traceText");
				writer.WriteValue(traceText);
			}
			if(tracePath != null)
			{
				writer.WritePropertyName("tracePath");
				writer.WriteValue(tracePath);
			}
			if(traceOutline != null)
			{
				writer.WritePropertyName("traceOutline");
				writer.WriteValue(traceOutline);
			}

			if(_sublayouts.Count > 0)
			{
				writer.WritePropertyName("Layouts");
				writer.WriteStartArray();
				foreach(Layout layout in _sublayouts)
				{
					writer.WriteStartObject();
					layout.WriteJson(writer);
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}

			if(Conditions.Count > 0)
			{
				writer.WritePropertyName("Conditions");
				writer.WriteStartArray();
				foreach(Condition condition in Conditions)
				{
					writer.WriteStartObject();
					condition.WriteJson(writer);
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}

			if(PageBreakRules != null)
			{
				writer.WritePropertyName("PageBreakRules");
				writer.WriteStartObject();
				PageBreakRules.WriteJson(writer);
				writer.WriteEndObject();
			}

			if(TermDictionary.Count > 0)
			{
				writer.WritePropertyName("TermDictionary");
				writer.WriteStartArray();
				foreach(KeyValuePair<string, string> term in TermDictionary)
				{
					writer.WriteStartObject();
					writer.WritePropertyName(term.Key);
					writer.WriteValue(term.Value);
					writer.WriteEndObject();
				}
				writer.WriteEndArray();
			}
		}
	}

	public class ChapterLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.ChapterLayout; } set {} }

		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
		public Background Background;
		public Header Header;
		public Footer Footer;
		public PageMetrics PageMetrics;
		public bool? renderEmpty;
		public bool? drawRules;

		public ChapterLayout()
		{
		}

		public ChapterLayout(XElement element)
			:base(element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			Background  = Background .Load(element.Element(ns + "Background" ));
			PageMetrics = PageMetrics.Load(element.Element(ns + "PageMetrics"));
			Header      = Header.Load(element.Element(ns + "Header"));
			Footer      = Footer.Load(element.Element(ns + "Footer"));
			renderEmpty = XmlHelper.ReadBoolAttribute(element, "renderEmpty");
			drawRules   = XmlHelper.ReadBoolAttribute(element, "drawRules");
		}
		
		public ChapterLayout(JsonReader reader)
			:base(reader)
		{
		}

		public ChapterLayout(ChapterLayout src)
			:base(src)
		{
			if(src.Background != null)
				Background = new Background(src.Background);
			if(src.Header != null)
				Header = new Header(src.Header);
			if(src.Footer != null)
				Footer = new Footer(src.Footer);
			if(src.PageMetrics != null)
				PageMetrics = new PageMetrics(src.PageMetrics);
			renderEmpty = src.renderEmpty;
			drawRules = src.drawRules;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Background":
					reader.Read(); // advance to the start array token
					Background = new Background(reader);
					return true;
				case "PageMetrics":
					reader.Read(); // advance to the start object token
					PageMetrics = new PageMetrics(reader);
					return true;
				case "Header":
					reader.Read(); // advance to the start object token
					Header = new Header(reader);
					return true;
				case "Footer":
					reader.Read(); // advance to the start object token
					Footer = new Footer(reader);
					return true;
				case "renderEmpty":
					renderEmpty = JsonHelper.ReadBoolean(reader);
					return true;
				case "drawRules":
					drawRules = JsonHelper.ReadBoolean(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			PageMetrics?.Validate();
			Background ?.Validate();

			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("ChapterLayout");
			WriteXml_BaseAttributes(writer);
			
			//	XML requires bools in lowercase
			if(renderEmpty != null)
				writer.WriteAttributeString("renderEmpty", renderEmpty.Value ? "true" : "false");
			if(drawRules != null)
				writer.WriteAttributeString("drawRules", drawRules.Value ? "true" : "false");

			Background?.WriteXml(writer);

			WriteXml_BaseElements(writer);

			Header     ?.WriteXml(writer);
			Footer     ?.WriteXml(writer);
			PageMetrics?.WriteXml(writer);

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(renderEmpty != null)
			{
				writer.WritePropertyName("renderEmpty");
				writer.WriteValue(renderEmpty);
			}
			if(drawRules != null)
			{
				writer.WritePropertyName("drawRules");
				writer.WriteValue(drawRules);
			}

			if(Background != null)
			{
				writer.WritePropertyName("Background");
				Background.WriteJson(writer);
			}
			if(Header != null)
			{
				writer.WritePropertyName("Header");
				Header.WriteJson(writer);
			}
			if(Footer != null)
			{
				writer.WritePropertyName("Footer");
				Footer.WriteJson(writer);
			}
			if(PageMetrics != null)
			{
				writer.WritePropertyName("PageMetrics");
				PageMetrics.WriteJson(writer);
			}
		}
	}

	public class TextLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.TextLayout; } set {} }

//		public string source;
		public string DefaultText;
		public double? softBreakLimit;
		public TextStyle Style;

		public TextLayout()
		{
		}

		public TextLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			DefaultText    = XmlHelper.ReadStringElement  (element, ns + "DefaultText");
			softBreakLimit = XmlHelper.ReadDoubleAttribute(element, "softBreakLimit");
			Style = TextStyle.Load(element?.Element(ns + "Style"));
		}

		public TextLayout(JsonReader reader)
			:base(reader)
		{
		}

		public TextLayout(TextLayout src)
			:base(src)
		{
			DefaultText = src.DefaultText;
			softBreakLimit = src.softBreakLimit;
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "DefaultText":
					DefaultText = JsonHelper.ReadString(reader);
					return true;
				case "softBreakLimit":
					softBreakLimit = JsonHelper.ReadDouble(reader);
					return true;
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new TextStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			if(softBreakLimit < 0.0)
				throw new DesignException($"Soft break limit must be between 0.0 and 1.0. Found {softBreakLimit}.", this);
			
			Style?.Validate();

			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("TextLayout");
			WriteXml_BaseAttributes(writer);

			if(softBreakLimit != null)
				writer.WriteAttributeString("softBreakLimit", softBreakLimit.ToString());
			if(DefaultText != null)
				writer.WriteElementString("DefaultText", DefaultText);

			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(softBreakLimit != null)
			{
				writer.WritePropertyName("softBreakLimit");
				writer.WriteValue(softBreakLimit);
			}
			if(DefaultText != null)
			{
				writer.WritePropertyName("DefaultText");
				writer.WriteValue(DefaultText);
			}

			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}

	public class ListLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.ListLayout; } set {} }

		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
		public string EmptyText;
		public ListStyle Style;
		public ListStyle EmptyStyle;

		public ListLayout()
		{
		}

		public ListLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			EmptyText  = XmlHelper.ReadStringElement(element, ns + "EmptyText");
			Style      = ListStyle.Load(element?.Element(ns + "Style"));
			EmptyStyle = ListStyle.Load(element?.Element(ns + "EmptyStyle"));
		}

		public ListLayout(JsonReader reader)
			:base(reader)
		{
		}

		public ListLayout(ListLayout src)
			:base(src)
		{
			EmptyText = src.EmptyText;
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
			EmptyStyle = src.EmptyStyle;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "EmptyText":
					EmptyText = JsonHelper.ReadString(reader);
					return true;
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new ListStyle(reader);
					return true;
				case "EmptyStyle":
					reader.Read(); // advance to the start object token
					EmptyStyle = new ListStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			Style     ?.Validate();
			EmptyStyle?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("ListLayout");
			WriteXml_BaseAttributes(writer);

			if(EmptyText != null)
				writer.WriteElementString("EmptyText", EmptyText);
			
			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style,      "Style");
			Serialization.Style.WriteStyle(writer, EmptyStyle, "EmptyStyle");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(EmptyText != null)
			{
				writer.WritePropertyName("EmptyText");
				writer.WriteValue(EmptyText);
			}

			Serialization.Style.WriteStyle(writer, Style,      "Style");
			Serialization.Style.WriteStyle(writer, EmptyStyle, "EmptyStyle");
		}
	}

	public class GroupLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.GroupLayout; } set {} }

		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
//		public string source;

		public GroupLayout()
		{
		}

		public GroupLayout(XElement element)
			:base(element)
		{
		}

		public GroupLayout(JsonReader reader)
			:base(reader)
		{
		}

		public GroupLayout(GroupLayout src)
			:base(src)
		{
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			//	Group layout has no custom properties
			throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
		}

		public override void Validate()
		{
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("GroupLayout");
			WriteXml_BaseAttributes(writer);
			WriteXml_BaseElements(writer);
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);
		}
	}

	public class PhotoTableLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.PhotoTableLayout; } set {} }

//		public string source;
		public int? Columns;
		public int? MaxPhotoWidth;
		public int? MaxPhotoHeight;
		public int? Resolution;
		public bool? Merge;
		public PhotoStyle Style;

		public PhotoTableLayout()
		{
		}

		public PhotoTableLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Columns        = XmlHelper.ReadIntElement (element, ns + "Columns");
			MaxPhotoWidth  = XmlHelper.ReadIntElement (element, ns + "MaxPhotoWidth");
			MaxPhotoHeight = XmlHelper.ReadIntElement (element, ns + "MaxPhotoHeight");
			Resolution     = XmlHelper.ReadIntElement (element, ns + "Resolution");
			Merge          = XmlHelper.ReadBoolElement(element, ns + "Merge");
			Style = PhotoStyle.Load(element?.Element(ns + "Style"));
		}

		public PhotoTableLayout(JsonReader reader)
			:base(reader)
		{
		}

		public PhotoTableLayout(PhotoTableLayout src)
			:base(src)
		{
			Columns = src.Columns;
			MaxPhotoWidth = src.MaxPhotoWidth;
			MaxPhotoHeight = src.MaxPhotoHeight;
			Resolution = src.Resolution;
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Columns":
					Columns = JsonHelper.ReadInteger(reader);
					return true;
				case "MaxPhotoWidth":
					MaxPhotoWidth = JsonHelper.ReadInteger(reader);
					return true;
				case "MaxPhotoHeight":
					MaxPhotoHeight = JsonHelper.ReadInteger(reader);
					return true;
				case "Resolution":
					Resolution = JsonHelper.ReadInteger(reader);
					return true;
				case "Merge":
					Merge = JsonHelper.ReadBoolean(reader);
					return true;
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new PhotoStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			//TODO: source, width, height, resolution

			Style?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("PhotoTableLayout");
			WriteXml_BaseAttributes(writer);

			if(Columns != null)
				writer.WriteElementString("Columns", Columns.ToString());
			if(MaxPhotoWidth != null)
				writer.WriteElementString("MaxPhotoWidth", MaxPhotoWidth.ToString());
			if(MaxPhotoHeight != null)
				writer.WriteElementString("MaxPhotoHeight", MaxPhotoHeight.ToString());
			if(Resolution != null)
				writer.WriteElementString("Resolution", Resolution.ToString());
			if(Merge != null)
				writer.WriteElementString("Merge", Merge.Value ? "true" : "false");

			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Columns != null)
			{
				writer.WritePropertyName("Columns");
				writer.WriteValue(Columns);
			}
			if(MaxPhotoWidth != null)
			{
				writer.WritePropertyName("MaxPhotoWidth");
				writer.WriteValue(MaxPhotoWidth);
			}
			if(MaxPhotoHeight != null)
			{
				writer.WritePropertyName("MaxPhotoHeight");
				writer.WriteValue(MaxPhotoHeight);
			}
			if(Resolution != null)
			{
				writer.WritePropertyName("Resolution");
				writer.WriteValue(Resolution);
			}
			if(Merge != null)
			{
				writer.WritePropertyName("Merge");
				writer.WriteValue(Merge);
			}

			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}

	public class TableLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.TableLayout; } set {} }

		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
		public int? headerRows;
		public TableStyle Style;
		public List<ColumnDefinition> Columns = new List<ColumnDefinition>();

		public TableLayout()
		{
		}

		public TableLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			LoadColumns(element.Element(ns + "ColumnDefinitions"));
			headerRows = XmlHelper.ReadIntAttribute(element, "headerRows");
			Style = TableStyle.Load(element?.Element(ns + "Style"));
		}

		public TableLayout(JsonReader reader)
			:base(reader)
		{
		}

		public TableLayout(TableLayout src)
			:base(src)
		{
			headerRows = src.headerRows;
			foreach(ColumnDefinition column in src.Columns)
				Columns.Add(new ColumnDefinition(column));
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "ColumnDefinitions":
					reader.Read(); // advance to the start array token
					LoadColumns(reader);
					return true;
				case "headerRows":
					headerRows = JsonHelper.ReadInteger(reader);
					return true;
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new TableStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		private void LoadColumns(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			foreach(XElement elem in element.Elements(ns + "ColumnDefinition"))
			{
				double? width = XmlHelper.ReadFloatElement(elem, ns + "Width");
				if(width != null)
					Columns.Add(new ColumnDefinition(width.Value));
			}
		}

		private void LoadColumns(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);

			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);

				reader.Read();
				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;
				if(propName != "ColumnDefinition")
					throw new DesignException($"Expected ColumnDefinition, found {propName}.", new LineNumberCapture(reader));

				reader.Read(); // read to the start object token
				ColumnDefinition column = new ColumnDefinition(reader);
				Columns.Add(column);

				JsonHelper.AssertToken(reader, JsonToken.EndObject);
				reader.Read(); // advance past the end object token
			}
		}

		public override void Validate()
		{
			foreach(Layout layout in _sublayouts)
				if(layout.Type != LayoutType.TableRowLayout)
					throw new DesignException($"Expected {LayoutType.TableRowLayout} layout type, found {layout.Type}.", layout);

			//TODO: header rows

			Style?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("TableLayout");
			WriteXml_BaseAttributes(writer);

			if(headerRows != null)
				writer.WriteAttributeString("headerRows", headerRows.ToString());

			writer.WriteStartElement("ColumnDefinitions");
			foreach(ColumnDefinition column in Columns)
				column.WriteXml(writer);
			writer.WriteEndElement();

			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			writer.WritePropertyName("ColumnDefinitions");
			writer.WriteStartArray();
			foreach(ColumnDefinition column in Columns)
				column.WriteJson(writer);
			writer.WriteEndArray();

			if(headerRows != null)
			{
				writer.WritePropertyName("headerRows");
				writer.WriteValue(headerRows);
			}
			
			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}

	public class ColumnDefinition
	{
		public double Width;

		public ColumnDefinition(double width)
		{
			Width = width;
		}

		public ColumnDefinition(ColumnDefinition src)
		{
			Width = src.Width;
		}

		public ColumnDefinition(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartObject);

			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;

				JsonHelper.AssertToken(reader, JsonToken.PropertyName);
				string propName = (string)reader.Value;

				switch(propName)
				{
					case "Width":
						Width = JsonHelper.ReadDouble(reader) ?? 0.0;
						break;
					default:
						throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
				}
			}
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("ColumnDefinition");
			writer.WriteStartElement("Width");
			writer.WriteValue(Width);
			writer.WriteEndElement();
			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("ColumnDefinition");
			writer.WriteStartObject();
			writer.WritePropertyName("Width");
			writer.WriteValue(Width);
			writer.WriteEndObject();
			writer.WriteEndObject();
		}
	}

	public class TableRowLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.TableRowLayout; } set {} }

		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
		public TableRowStyle Style;

		public TableRowLayout()
		{
		}

		public TableRowLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Style = TableRowStyle.Load(element?.Element(ns + "Style"));
		}

		public TableRowLayout(JsonReader reader)
			:base(reader)
		{
		}

		public TableRowLayout(TableRowLayout src)
			:base(src)
		{
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new TableRowStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			foreach(Layout layout in _sublayouts)
				if(layout.Type != LayoutType.TableCellLayout)
					throw new DesignException($"Expected {LayoutType.TableCellLayout} layout type, found {layout.Type}.", layout);

			Style?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("TableRowLayout");
			WriteXml_BaseAttributes(writer);
			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}

	public class TableCellLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.TableCellLayout; } set {} }

		public int? colSpan;
		public List<Layout> Layouts { get { return _sublayouts; } set { _sublayouts = value; } }
		public TableCellStyle Style;

		public TableCellLayout()
		{
		}

		public TableCellLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			colSpan = XmlHelper.ReadIntAttribute(element, "colSpan");
			Style = TableCellStyle.Load(element?.Element(ns + "Style"));
		}

		public TableCellLayout(JsonReader reader)
			:base(reader)
		{
		}

		public TableCellLayout(TableCellLayout src)
			:base(src)
		{
			colSpan = src.colSpan;
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "colSpan":
					colSpan = JsonHelper.ReadInteger(reader);
					return true;
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new TableCellStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			Style?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("TableCellLayout");
			WriteXml_BaseAttributes(writer);

			if(colSpan != null)
				writer.WriteAttributeString("colSpan", colSpan.ToString());

			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(colSpan != null)
			{
				writer.WritePropertyName("colSpan");
				writer.WriteValue(colSpan);
			}

			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}

	public class PictureLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.PictureLayout; } set {} }

		public string @ref;
		public string filename;
		public int? Width;
		public int? Height;
		public PictureAlignment? Alignment;
		public ScaleMode? ScaleMode;

		public PictureLayout()
		{
		}

		public PictureLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			@ref      = XmlHelper.ReadStringAttribute(element, "ref");
			filename  = XmlHelper.ReadStringElement(element, ns + "Filename");
			Width     = XmlHelper.ReadIntElement(element, ns + "Width");
			Height    = XmlHelper.ReadIntElement(element, ns + "Height");
			Alignment = XmlHelper.ReadEnumElement<PictureAlignment>(element, ns + "Alignment");
			ScaleMode = XmlHelper.ReadEnumElement<ScaleMode>(element, ns + "ScaleMode");
		}

		public PictureLayout(JsonReader reader)
			:base(reader)
		{
		}

		public PictureLayout(PictureLayout src)
			:base(src)
		{
			@ref = src.@ref;
			filename = src.filename;
			Width = src.Width;
			Height = src.Height;
			Alignment = src.Alignment;
			ScaleMode = src.ScaleMode;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "ref":
					@ref = JsonHelper.ReadString(reader);
					return true;
				case "filename":
					filename = JsonHelper.ReadString(reader);
					return true;
				case "Width":
					Width = JsonHelper.ReadInteger(reader);
					return true;
				case "Height":
					Height = JsonHelper.ReadInteger(reader);
					return true;
				case "Alignment":
					Alignment = JsonHelper.ReadEnum<PictureAlignment>(reader);
					return true;
				case "ScaleMode":
					ScaleMode = JsonHelper.ReadEnum<ScaleMode>(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			//TODO: maybe issue a warning of both Ref and Filename are
			//supplied. This isn't an error.

			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("PictureLayout");
			WriteXml_BaseAttributes(writer);

			if(@ref != null)
				writer.WriteAttributeString("ref", @ref);
			if(filename != null)
				writer.WriteAttributeString("filename", filename);

			if(Width != null)
				writer.WriteElementString("Width", Width.ToString());
			if(Height != null)
				writer.WriteElementString("Height", Height.ToString());
			if(Alignment != null)
				writer.WriteElementString("Alignment", Alignment.ToString());
			if(ScaleMode != null)
				writer.WriteElementString("ScaleMode", ScaleMode.ToString());

			WriteXml_BaseElements(writer);
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(@ref != null)
			{
				writer.WritePropertyName("ref");
				writer.WriteValue(@ref);
			}
			if(filename != null)
			{
				writer.WritePropertyName("filename");
				writer.WriteValue(filename);
			}

			if(Width != null)
			{
				writer.WritePropertyName("Width");
				writer.WriteValue(Width);
			}
			if(Height != null)
			{
				writer.WritePropertyName("Height");
				writer.WriteValue(Height);
			}
			if(Alignment != null)
			{
				writer.WritePropertyName("Alignment");
				writer.WriteValue(Alignment.ToString());
			}
			if(ScaleMode != null)
			{
				writer.WritePropertyName("ScaleMode");
				writer.WriteValue(ScaleMode.ToString());
			}
		}
	}

	public class SpaceLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.SpaceLayout; } set {} }

		public int? Height;

		public SpaceLayout()
		{
		}

		public SpaceLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Height = XmlHelper.ReadIntElement(element, ns + "Height");
		}

		public SpaceLayout(JsonReader reader)
			:base(reader)
		{
		}

		public SpaceLayout(SpaceLayout src)
			:base(src)
		{
			Height = src.Height;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Height":
					Height = JsonHelper.ReadInteger(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			//TODO
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("SpaceLayout");
			WriteXml_BaseAttributes(writer);

			if(Height != null)
				writer.WriteElementString("Height", Height.ToString());

			WriteXml_BaseElements(writer);
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);

			if(Height != null)
			{
				writer.WritePropertyName("Height");
				writer.WriteValue(Height);
			}
		}
	}

	public class LineLayout : Layout
	{
		public override LayoutType Type { get { return LayoutType.LineLayout; } set {} }
		public LineStyle Style;

		public LineLayout()
		{
		}

		public LineLayout(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Style = LineStyle.Load(element?.Element(ns + "Style"));
		}

		public LineLayout(JsonReader reader)
			:base(reader)
		{
		}

		public LineLayout(LineLayout src)
			:base(src)
		{
			Style = src.Style; // styles are immutable in this library, so a direct copy is fine
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			//	If it's a base class property then let the base class load it,
			//	otherwise load it ourself
			bool loaded = base.LoadProperty(reader, propName);
			if(loaded) return true;

			switch(propName)
			{
				case "Style":
					reader.Read(); // advance to the start object token
					Style = new LineStyle(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.", new LineNumberCapture(reader));
			}
		}

		public override void Validate()
		{
			//TODO
			Style?.Validate();
			base.Validate();
		}

		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("LineLayout");
			WriteXml_BaseAttributes(writer);
			WriteXml_BaseElements(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			WriteJson_BaseProperties(writer);
			Serialization.Style.WriteStyle(writer, Style, "Style");
		}
	}
}
