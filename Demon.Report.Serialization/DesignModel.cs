using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public class Design : IXmlSerializable
	{
		public string id;
		public string name;
		public string inspectionTemplateId;
		public int fileFormatVersion;

		public List<Layout> Layouts = new List<Layout>();
		public Styles Styles = new Styles();
		public List<Resource> Resources = new List<Resource>();

		public Design()
		{
		}

		public Design(XDocument doc)
		{
			XElement root = doc.Root;
			XNamespace ns = root.GetDefaultNamespace();
			id   = XmlHelper.ReadStringAttribute(root, "id");
			name = XmlHelper.ReadStringAttribute(root, "name");
			inspectionTemplateId = XmlHelper.ReadStringAttribute(root, "inspectionTemplateId");
			fileFormatVersion    = XmlHelper.ReadIntAttribute   (root, "fileFormatVersion") ?? 0;

			LoadLayouts  (root.Element(ns + "Layouts"  ));
			LoadStyles   (root.Element(ns + "Styles"   ));
			LoadResources(root.Element(ns + "Resources"));

			Validate();
		}

		public Design(JsonReader reader)
		{
			//	The JSON library can't load the file into an in-memory object
			//	because it can't make sense of our polymorphic layouts, styles
			//	and conditions. So we have to read it sequentially and build
			//	ourself on the fly.
			ReadJson(reader);
			Validate();
		}

		private void LoadLayouts(XElement root)
		{
			if(root == null) return;
			foreach(XElement element in root.Elements())
			{
				Layout layout = Layout.Load(element);
				Layouts.Add(layout);
			}
		}

		private void LoadStyles(XElement root)
		{
			if(root == null) return;
			Styles.Load(root);
		}

		private void LoadResources(XElement root)
		{
			if(root == null) return;
			foreach(XElement element in root.Elements())
			{
				Resource resource = Resource.Load(element);
				Resources.Add(resource);
			}
		}

		private void LoadLayouts(JsonReader reader)
		{
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				JsonHelper.AssertToken(reader, JsonToken.StartObject);
				Layout layout = Layout.Load(reader);
				Layouts.Add(layout);
			}
		}

		private void LoadResources(JsonReader reader)
		{
			Resources = new List<Resource>();
			JsonHelper.AssertToken(reader, JsonToken.StartArray);
			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndArray) break;
				Resource resource = new Resource(reader);
				Resources.Add(resource);
			}
		}

		public void Validate()
		{
			//	The layouts must all be chapter layouts
			foreach(Layout layout in Layouts)
				if(layout.Type != LayoutType.ChapterLayout)
					throw new DesignException($"Expected {LayoutType.ChapterLayout} layout type, found {layout.Type}.", layout);

			//TODO: other checks

			foreach(Layout layout in Layouts)
				layout.Validate();
		}

		public void ReadXml(XmlReader reader)
		{
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteAttributeString("id", id);
			writer.WriteAttributeString("name", name);
			writer.WriteAttributeString("inspectionTemplateId", inspectionTemplateId);
			writer.WriteAttributeString("fileFormatVersion", fileFormatVersion.ToString());

			if(Layouts != null && Layouts.Count > 0)
			{
				writer.WriteStartElement("Layouts");
				foreach(Layout layout in Layouts)
					layout.WriteXml(writer);
				writer.WriteEndElement();
			}

			Styles.WriteXml(writer);
			
			if(Resources != null && Resources.Count > 0)
			{
				writer.WriteStartElement("Resources");
				foreach(Resource resource in Resources)
				{
					writer.WriteStartElement("Resource");
					resource.WriteXml(writer);
					writer.WriteEndElement();
				}
				writer.WriteEndElement();
			}
		}

		public System.Xml.Schema.XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadJson(JsonReader reader)
		{
			bool ok = reader.Read();
			if(!ok)
				throw new DesignException("Incomplete JSON stream.");

			//	We expect the JSON to start with an object, which is the design.
			JsonHelper.AssertToken(reader, JsonToken.StartObject);

			while(reader.Read())
			{
				if(reader.TokenType == JsonToken.EndObject) break;
				JsonHelper.AssertToken(reader, JsonToken.PropertyName);

				switch(reader.Value)
				{
					case "id":
						id = JsonHelper.ReadString(reader);
						break;
					case "name":
						name = JsonHelper.ReadString(reader);
						break;
					case "inspectionTemplateId":
						inspectionTemplateId = JsonHelper.ReadString(reader);
						break;
					case "fileFormatVersion":
						fileFormatVersion = JsonHelper.ReadInteger(reader) ?? 0;
						break;
					case "Layouts":
						reader.Read(); // advance to the start array token
						LoadLayouts(reader);
						break;
					case "Styles":
						reader.Read(); // advance to the start object token
						Styles = new Styles(reader);
						break;
					case "Resources":
						reader.Read(); // advance to the start array token
						LoadResources(reader);
						break;
					default:
						throw new DesignException($"Unrecognized property '{reader.Value}'.");
				}
			}
		}

		public void WriteJson(JsonWriter writer)
		{
			writer.WriteStartObject();

			writer.WritePropertyName("id");
			writer.WriteValue(id);
			writer.WritePropertyName("name");
			writer.WriteValue(name);
			writer.WritePropertyName("inspectionTemplateId");
			writer.WriteValue(inspectionTemplateId);
			writer.WritePropertyName("fileFormatVersion");
			writer.WriteValue(fileFormatVersion);

			writer.WritePropertyName("Layouts");
			writer.WriteStartArray();
			foreach(Layout layout in Layouts)
			{
				writer.WriteStartObject();
				layout.WriteJson(writer);
				writer.WriteEndObject();
			}
			writer.WriteEndArray();

			writer.WritePropertyName("Styles");
			Styles.WriteJson(writer);

			writer.WritePropertyName("Resources");
			writer.WriteStartArray();
			foreach(Resource resource in Resources)
			{
				writer.WriteStartObject();
				resource.WriteJson(writer);
				writer.WriteEndObject();
			}
			writer.WriteEndArray();

			writer.WriteEndObject();
		}

		public Layout FindLayout(string id)
		{
			foreach(Layout layout in Layouts)
			{
				if(layout.id == id) return layout;

				Layout found = layout.FindSubLayout(id);
				if(found != null) return found;
			}
			return null;
		}

		public List<Layout> FindLayouts(string name)
		{
			List<Layout> layouts = new List<Layout>();
			foreach(Layout layout in Layouts)
			{
				if(layout.name == name)
					layouts.Add(layout);
				
				layouts.AddRange(layout.FindSubLayouts(name));
			}
			return layouts;
		}
	}
}
