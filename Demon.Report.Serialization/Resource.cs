using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public class Resource : ILineNumber
	{
		public string id;
		public string Filename;

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public Resource()
		{
		}

		public Resource(XElement element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			id       = XmlHelper.ReadStringAttribute(element, "id");
			Filename = XmlHelper.ReadStringElement  (element, ns + "Filename");

			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public static Resource Load(XElement element)
		{
			if(element == null) return null;
			return new Resource(element);
		}

		public Resource(JsonReader reader)
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

		public Resource(Resource src)
		{
			id = src.id;
			Filename = src.Filename;
		}

		private bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "id":
					id = JsonHelper.ReadString(reader);
					return true;
				case "Filename":
					Filename = JsonHelper.ReadString(reader);
					return true;
				default:
					throw new DesignException($"Unrecognized property '{propName}'.");
			}
		}

		public void Validate()
		{
			//TODO
		}

		public void WriteXml(XmlWriter writer)
		{
			if(id != null)
				writer.WriteAttributeString("id", id);
			if(Filename != null)
				writer.WriteElementString("Filename", Filename);
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WritePropertyName("id");
			writer.WriteValue(id);
			writer.WritePropertyName("Filename");
			writer.WriteValue(Filename);
		}
	}
}
