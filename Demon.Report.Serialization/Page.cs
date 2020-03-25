using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public class Header : ILineNumber
	{
		public GroupLayout GroupLayout;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Header()
		{
		}

		public Header(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			XElement groupElement = element?.Element(ns + "GroupLayout");
			if(groupElement != null)
				GroupLayout = (GroupLayout)Layout.Load(groupElement);

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Header(JsonReader reader)
		{
			GroupLayout = (GroupLayout)Layout.Load(reader);
		}

		public Header(Header src)
		{
			GroupLayout = new GroupLayout(src.GroupLayout);
		}

		public static Header Load(XElement element)
		{
			if(element == null) return null;
			return new Header(element);
		}

		public void Validate()
		{
			GroupLayout?.Validate();
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("Header");
			GroupLayout?.WriteXml(writer);
			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();
			GroupLayout?.WriteJson(writer);
			writer.WriteEndObject();
		}
	}

	public class Footer : ILineNumber
	{
		public GroupLayout GroupLayout;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Footer()
		{
		}

		public Footer(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			XElement tableElement = element?.Element(ns + "GroupLayout");
			if(tableElement != null)
				GroupLayout = (GroupLayout)Layout.Load(tableElement);

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Footer(JsonReader reader)
		{
			GroupLayout = (GroupLayout)Layout.Load(reader);
		}

		public Footer(Footer src)
		{
			GroupLayout = new GroupLayout(src.GroupLayout);
		}

		public static Footer Load(XElement element)
		{
			if(element == null) return null;
			return new Footer(element);
		}

		public void Validate()
		{
			GroupLayout?.Validate();
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("Footer");
			GroupLayout?.WriteXml(writer);
			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();
			GroupLayout?.WriteJson(writer);
			writer.WriteEndObject();
		}
	}

	public class Background : ILineNumber
	{
		public List<Picture> Pictures;
		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Background()
		{
		}

		public Background(XElement element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			foreach(XElement child in element.Elements(ns + "Picture"))
			{
				if(Pictures == null)
					Pictures = new List<Picture>();

				Picture picture = Picture.Load(child);
				Pictures.Add(picture);
			}

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Background(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);

				//	Create our pictures collection now that we know we need it
				if(Pictures == null)
					Pictures = new List<Picture>();

				Picture picture = new Picture(reader);
				Pictures.Add(picture);
			}
		}

		public Background(Background src)
		{
			Pictures = new List<Picture>(src.Pictures.Count);
			foreach(Picture picture in src.Pictures)
				Pictures.Add(new Picture(picture));
		}

		public static Background Load(XElement element)
		{
			if(element == null) return null;
			return new Background(element);
		}

		public void Validate()
		{
			if(Pictures != null)
				foreach(Picture picture in Pictures)
					picture.Validate();
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("Background");
			if(Pictures != null)
				foreach(Picture picture in Pictures)
					picture.WriteXml(writer);
			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartArray();
			if(Pictures != null)
			{
				foreach(Picture picture in Pictures)
				{
					writer.WriteStartObject();
					picture.WriteJson(writer);
					writer.WriteEndObject();
				}
			}
			writer.WriteEndArray();
		}
	}

	public class PageMetrics : ILineNumber
	{
		public Rectangle MediaBox;
		public Rectangle BodyBox;
		public Rectangle HeaderBox;
		public Rectangle FooterBox;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public PageMetrics()
		{
			MediaBox  = new Rectangle();
			BodyBox   = new Rectangle();
			HeaderBox = new Rectangle();
			FooterBox = new Rectangle();
		}

		public PageMetrics(PageMetrics src)
		{
			MediaBox  = new Rectangle(src.MediaBox);
			BodyBox   = new Rectangle(src.BodyBox);
			HeaderBox = new Rectangle(src.HeaderBox);
			FooterBox = new Rectangle(src.FooterBox);
		}

		public PageMetrics(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			MediaBox  = new Rectangle(element?.Element(ns + "MediaBox" ));
			BodyBox   = new Rectangle(element?.Element(ns + "BodyBox"  ));
			HeaderBox = new Rectangle(element?.Element(ns + "HeaderBox"));
			FooterBox = new Rectangle(element?.Element(ns + "FooterBox"));

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public PageMetrics(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartObject);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;
				JsonHelper.AssertToken(reader, JsonToken.PropertyName);

				string propName = (string)reader.Value;
				LoadProperty(reader, propName);
			}
		}

		private void LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "MediaBox":
					reader.Read(); // advance to the start object token
					MediaBox = new Rectangle(reader);
					break;
				case "BodyBox":
					reader.Read(); // advance to the start object token
					BodyBox = new Rectangle(reader);
					break;
				case "HeaderBox":
					reader.Read(); // advance to the start object token
					HeaderBox = new Rectangle(reader);
					break;
				case "FooterBox":
					reader.Read(); // advance to the start object token
					FooterBox = new Rectangle(reader);
					break;
				default:
					throw new DesignException($"Unrecognized PageMetrics property '{propName}'.");
			}
		}

		public static PageMetrics Load(XElement element)
		{
			if(element == null) return null;
			return new PageMetrics(element);
		}

		public void Validate()
		{
			MediaBox ?.Validate();
			BodyBox  ?.Validate();
			HeaderBox?.Validate();
			FooterBox?.Validate();
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("PageMetrics");

			writer.WriteStartElement("MediaBox");
			writer.WriteAttributeString("left",   MediaBox.left  .ToString());
			writer.WriteAttributeString("bottom", MediaBox.bottom.ToString());
			writer.WriteAttributeString("right",  MediaBox.right .ToString());
			writer.WriteAttributeString("top",    MediaBox.top   .ToString());
			writer.WriteEndElement();

			writer.WriteStartElement("BodyBox");
			writer.WriteAttributeString("left",   BodyBox.left  .ToString());
			writer.WriteAttributeString("bottom", BodyBox.bottom.ToString());
			writer.WriteAttributeString("right",  BodyBox.right .ToString());
			writer.WriteAttributeString("top",    BodyBox.top   .ToString());
			writer.WriteEndElement();

			writer.WriteStartElement("HeaderBox");
			writer.WriteAttributeString("left",   HeaderBox.left  .ToString());
			writer.WriteAttributeString("bottom", HeaderBox.bottom.ToString());
			writer.WriteAttributeString("right",  HeaderBox.right .ToString());
			writer.WriteAttributeString("top",    HeaderBox.top   .ToString());
			writer.WriteEndElement();

			writer.WriteStartElement("FooterBox");
			writer.WriteAttributeString("left",   FooterBox.left  .ToString());
			writer.WriteAttributeString("bottom", FooterBox.bottom.ToString());
			writer.WriteAttributeString("right",  FooterBox.right .ToString());
			writer.WriteAttributeString("top",    FooterBox.top   .ToString());
			writer.WriteEndElement();

			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();

			writer.WritePropertyName("MediaBox");
			writer.WriteStartObject();
			writer.WritePropertyName("left");
			writer.WriteValue(MediaBox.left);
			writer.WritePropertyName("bottom");
			writer.WriteValue(MediaBox.bottom);
			writer.WritePropertyName("right");
			writer.WriteValue(MediaBox.right);
			writer.WritePropertyName("top");
			writer.WriteValue(MediaBox.top);
			writer.WriteEndObject();

			writer.WritePropertyName("BodyBox");
			writer.WriteStartObject();
			writer.WritePropertyName("left");
			writer.WriteValue(BodyBox.left);
			writer.WritePropertyName("bottom");
			writer.WriteValue(BodyBox.bottom);
			writer.WritePropertyName("right");
			writer.WriteValue(BodyBox.right);
			writer.WritePropertyName("top");
			writer.WriteValue(BodyBox.top);
			writer.WriteEndObject();

			writer.WritePropertyName("HeaderBox");
			writer.WriteStartObject();
			writer.WritePropertyName("left");
			writer.WriteValue(HeaderBox.left);
			writer.WritePropertyName("bottom");
			writer.WriteValue(HeaderBox.bottom);
			writer.WritePropertyName("right");
			writer.WriteValue(HeaderBox.right);
			writer.WritePropertyName("top");
			writer.WriteValue(HeaderBox.top);
			writer.WriteEndObject();

			writer.WritePropertyName("FooterBox");
			writer.WriteStartObject();
			writer.WritePropertyName("left");
			writer.WriteValue(FooterBox.left);
			writer.WritePropertyName("bottom");
			writer.WriteValue(FooterBox.bottom);
			writer.WritePropertyName("right");
			writer.WriteValue(FooterBox.right);
			writer.WritePropertyName("top");
			writer.WriteValue(FooterBox.top);
			writer.WriteEndObject();

			writer.WriteEndObject();
		}
	}

	public class PageBreakRules : ILineNumber
	{
		public bool? NewPage;
		public bool? KeepWithNext;
		public int? MinLines;
		public double? MaxPosition;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public PageBreakRules()
		{
		}

		public PageBreakRules(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartObject);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;
				JsonHelper.AssertToken(reader, JsonToken.PropertyName);

				switch(reader.Value)
				{
					case "NewPage":
						NewPage = JsonHelper.ReadBoolean(reader);
						break;
					case "KeepWithNext":
						KeepWithNext = JsonHelper.ReadBoolean(reader);
						break;
					case "MinLines":
						MinLines = JsonHelper.ReadInteger(reader);
						break;
					case "MaxPosition":
						MaxPosition = JsonHelper.ReadDouble(reader);
						break;
					default:
						throw new DesignException($"Unrecognized PageBreakRules property '{reader.Value}'.");
				}
			}
		}

		public PageBreakRules(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			NewPage      = XmlHelper.ReadBoolElement  (element, ns + "NewPage"     );
			KeepWithNext = XmlHelper.ReadBoolElement  (element, ns + "KeepWithNext");
			MinLines     = XmlHelper.ReadIntElement   (element, ns + "MinLines"    );
			MaxPosition  = XmlHelper.ReadDoubleElement(element, ns + "MaxPosition" );

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public PageBreakRules(PageBreakRules src)
		{
			NewPage = src.NewPage;
			KeepWithNext = src.KeepWithNext;
			MinLines = src.MinLines;
			MaxPosition = src.MaxPosition;
		}

		public static PageBreakRules Load(XElement element)
		{
			if(element == null) return null;
			return new PageBreakRules(element);
		}

		public void Validate()
		{
			if(MinLines < 0)
				throw new DesignException($"Min lines rule {MinLines} out of range. Must be between greater than or equal to zero.");
			if(MaxPosition < 0.0 || MaxPosition > 1.0)
				throw new DesignException($"Max position rule {MaxPosition} out of range. Must be between 0.0 and 1.0.");
		}

		public void WriteXml(XmlWriter writer)
		{
			if(NewPage != null)
				writer.WriteElementString("NewPage", NewPage.Value ? "true" : "false"); // XML requires bools in lowercase
			if(KeepWithNext != null)
				writer.WriteElementString("KeepWithNext", KeepWithNext.Value ? "true" : "false");
			if(MinLines != null)
				writer.WriteElementString("MinLines", MinLines.ToString());
			if(MaxPosition != null)
				writer.WriteElementString("MaxPosition", MaxPosition.ToString());
		}

		public void WriteJson(JsonWriter writer)
		{
			if(NewPage != null)
			{
				writer.WritePropertyName("NewPage");
				writer.WriteValue(NewPage);
			}
			if(KeepWithNext != null)
			{
				writer.WritePropertyName("KeepWithNext");
				writer.WriteValue(KeepWithNext);
			}
			if(MinLines != null)
			{
				writer.WritePropertyName("MinLines");
				writer.WriteValue(MinLines);
			}
			if(MaxPosition != null)
			{
				writer.WritePropertyName("MaxPosition");
				writer.WriteValue(MaxPosition);
			}
		}
	}
}
