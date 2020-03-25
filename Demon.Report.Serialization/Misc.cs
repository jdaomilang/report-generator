using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public class Picture : ILineNumber
	{
		public string @ref;
		public string filename;
		public int? Left;
		public int? Bottom;
		public int? Right;
		public int? Top;
		public PictureAlignment? Alignment;
		public ScaleMode? ScaleMode;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public static Picture Load(XElement element)
		{
			if(element == null) return null;
			return new Picture(element);
		}

		public Picture()
		{
		}

		public Picture(XElement element)
		{
			if(element == null) return;

			XNamespace ns = element.GetDefaultNamespace();
			@ref      = XmlHelper.ReadStringAttribute(element, "ref");
			filename  = XmlHelper.ReadStringAttribute(element, "filename");
			Left      = XmlHelper.ReadIntElement(element, ns + "Left"  ) ?? 0;
			Bottom    = XmlHelper.ReadIntElement(element, ns + "Bottom") ?? 0;
			Right     = XmlHelper.ReadIntElement(element, ns + "Right" ) ?? 0;
			Top       = XmlHelper.ReadIntElement(element, ns + "Top"   ) ?? 0;
			Alignment = XmlHelper.ReadEnumElement<PictureAlignment>(element, ns + "Alignment");
			ScaleMode = XmlHelper.ReadEnumElement<ScaleMode>(element, ns + "ScaleMode");

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Picture(JsonReader reader)
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

		public Picture(Picture src)
		{
			@ref      = src.@ref;
			filename  = src.filename;
			Left      = src.Left;
			Bottom    = src.Bottom;
			Right     = src.Right;
			Top       = src.Top;
			Alignment = src.Alignment;
			ScaleMode = src.ScaleMode;
		}

		private void LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "ref":
					@ref = JsonHelper.ReadString(reader);
					break;
				case "filename":
					filename = JsonHelper.ReadString(reader);
					break;
				case "Left":
					Left = JsonHelper.ReadInteger(reader);
					break;
				case "Bottom":
					Bottom = JsonHelper.ReadInteger(reader);
					break;
				case "Right":
					Right = JsonHelper.ReadInteger(reader);
					break;
				case "Top":
					Top = JsonHelper.ReadInteger(reader);
					break;
				case "Alignment":
					Alignment = JsonHelper.ReadEnum<PictureAlignment>(reader);
					break;
				case "ScaleMode":
					ScaleMode = JsonHelper.ReadEnum<ScaleMode>(reader);
					break;
				default:
					throw new DesignException($"Unrecognized Picture property '{propName}'.");
			}
		}

		public void Validate()
		{
			//TODO
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteStartElement("Picture");

			if(@ref != null)
				writer.WriteAttributeString("ref", @ref);
			if(filename != null)
				writer.WriteAttributeString("filename", filename);

			writer.WriteElementString("Left",   Left  .ToString());
			writer.WriteElementString("Bottom", Bottom.ToString());
			writer.WriteElementString("Right",  Right .ToString());
			writer.WriteElementString("Top",    Top   .ToString());

			if(Alignment != null)
				writer.WriteElementString("Alignment", Alignment.ToString());
			if(ScaleMode != null)
				writer.WriteElementString("ScaleMode", ScaleMode.ToString());

			writer.WriteEndElement();
		}

		public void WriteJson(JsonWriter writer)
		{
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

			writer.WritePropertyName("Left");
			writer.WriteValue(Left);
			writer.WritePropertyName("Bottom");
			writer.WriteValue(Bottom);
			writer.WritePropertyName("Right");
			writer.WriteValue(Right);
			writer.WritePropertyName("Top");
			writer.WriteValue(Top);

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

	public class Rectangle : ILineNumber
	{
		public int? left;
		public int? bottom;
		public int? right;
		public int? top;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Rectangle()
		{
			left   = 0;
			bottom = 0;
			right  = 0;
			top    = 0;
		}

		public Rectangle(Rectangle src)
		{
			left   = src.left;
			bottom = src.bottom;
			right  = src.right;
			top    = src.top;
		}

		public Rectangle(XElement element)
		{
			left   = XmlHelper.ReadIntAttribute(element, "left"  );
			bottom = XmlHelper.ReadIntAttribute(element, "bottom");
			right  = XmlHelper.ReadIntAttribute(element, "right" );
			top    = XmlHelper.ReadIntAttribute(element, "top"   );

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Rectangle(JsonReader reader)
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
				case "left":
					left = JsonHelper.ReadInteger(reader);
					break;
				case "bottom":
					bottom = JsonHelper.ReadInteger(reader);
					break;
				case "right":
					right = JsonHelper.ReadInteger(reader);
					break;
				case "top":
					top = JsonHelper.ReadInteger(reader);
					break;
				default:
					throw new DesignException($"Unrecognized Rectangle property '{propName}'.");
			}
		}

		public static Rectangle Load(XElement element)
		{
			if(element == null) return null;
			return new Rectangle(element);
		}

		public void Validate()
		{
			//TODO
		}
	}
}
