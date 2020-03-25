using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public abstract class Condition : ILineNumber
	{
		public abstract ConditionType Type { get; set; }

		public int LineNumber { get; set; }
		public int LinePosition { get; set; }

		public static Condition Load(XElement element)
		{
			Condition condition = null;
			switch(element.Name.LocalName)
			{
				case "EmptyLayoutCondition":
					condition = new EmptyLayoutCondition(element);
					break;
				case "OptionSelectedCondition":
					condition = new OptionSelectedCondition(element);
					break;
				case "ContentSelectedCondition":
					condition = new ContentSelectedCondition(element);
					break;
				case "DocTagCondition":
					condition = new DocTagCondition(element);
					break;
				case "ContentDocTagCondition":
					condition = new ContentDocTagCondition(element);
					break;
				case "ItemCountCondition":
					condition = new ItemCountCondition(element);
					break;
				case "PhotoCountCondition":
					condition = new PhotoCountCondition(element);
					break;
				default:
					throw new DesignException($"Unrecognized condition type '{element.Name.LocalName}'.", element);
			}
			return condition;
		}

		public Condition(Condition src)
		{
		}

		public static Condition Load(JsonReader reader)
		{
			//	We depend on the LayoutType property being first so that we can
			//	know what type to create
			string propName = (string)reader.Value;
			if(propName != "ConditionType")
				throw new DesignException($"Expected ConditionType property, got '{propName}'.");
				
			Condition condition = null;
			string typename = JsonHelper.ReadString(reader);
			switch(typename)
			{
				case "EmptyLayoutCondition":
					condition = new EmptyLayoutCondition(reader);
					break;
				case "OptionSelectedCondition":
					condition = new OptionSelectedCondition(reader);
					break;
				case "ContentSelectedCondition":
					condition = new ContentSelectedCondition(reader);
					break;
				case "DocTagCondition":
					condition = new DocTagCondition(reader);
					break;
				case "ContentDocTagCondition":
					condition = new ContentDocTagCondition(reader);
					break;
				case "ItemCountCondition":
					condition = new ItemCountCondition(reader);
					break;
				case "PhotoCountCondition":
					condition = new PhotoCountCondition(reader);
					break;
				default:
					throw new DesignException($"Unrecognized condition type '{typename}'.");
			}
			return condition;
		}

		public Condition(XElement element)
		{
			LineNumber   = ((IXmlLineInfo)element)?.LineNumber   ?? 0;
			LinePosition = ((IXmlLineInfo)element)?.LinePosition ?? 0;
		}

		public Condition(JsonReader reader)
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
				default:
					//	Not a base class property - let the derived class load it
					return false;
			}
		}

		public virtual void Validate()
		{
		}

		public abstract void WriteXml(XmlWriter writer);

		public virtual void WriteJson(JsonWriter writer)
		{
			writer.WritePropertyName("ConditionType");
			writer.WriteValue(Type.ToString());
		}
	}

	public class EmptyLayoutCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.EmptyLayoutCondition; } set {} }

		public int? context;
		public string refType;
		public string refId;
		public bool? Require;
		public bool? Prohibit;

		public EmptyLayoutCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			context  = XmlHelper.ReadIntAttribute(element, "context");
			refType  = XmlHelper.ReadStringAttribute(element, "refType");
			refId    = XmlHelper.ReadStringAttribute(element, "refId"  );
			Require  = XmlHelper.ReadBoolElement(element, ns + "Require");
			Prohibit = XmlHelper.ReadBoolElement(element, ns + "Prohibit");
		}

		public EmptyLayoutCondition(JsonReader reader)
			:base(reader)
		{
		}

		public EmptyLayoutCondition(EmptyLayoutCondition src)
			:base(src)
		{
			context  = src.context;
			refType  = src.refType;
			refId    = src.refId;
			Require  = src.Require;
			Prohibit = src.Prohibit;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "context":
					context = JsonHelper.ReadInteger(reader);
					return true;
				case "refType":
					refType = JsonHelper.ReadString(reader);
					return true;
				case "refId":
					refId = JsonHelper.ReadString(reader);
					return true;
				case "Require":
					Require = JsonHelper.ReadBoolean(reader);
					return true;
				case "Prohibit":
					Prohibit = JsonHelper.ReadBoolean(reader);
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
			writer.WriteStartElement("EmptyLayoutCondition");

			if(context != null)
				writer.WriteAttributeString("context", context.ToString());
			if(refType != null)
				writer.WriteAttributeString("refType", refType);
			if(refId != null)
				writer.WriteAttributeString("refId", refId);

			//	XML requires bools in lowercase
			if(Require != null)
				writer.WriteElementString("Require", Require.Value ? "true" : "false");
			if(Prohibit != null)
				writer.WriteElementString("Prohibit", Prohibit.Value ? "true" : "false");

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(context != null)
			{
				writer.WritePropertyName("context");
				writer.WriteValue(context);
			}
			if(refType != null)
			{
				writer.WritePropertyName("refType");
				writer.WriteValue(refType);
			}
			if(refId != null)
			{
				writer.WritePropertyName("refId");
				writer.WriteValue(refId);
			}
			if(Require != null)
			{
				writer.WritePropertyName("Require");
				writer.WriteValue(Require);
			}
			if(Prohibit != null)
			{
				writer.WritePropertyName("Prohibit");
				writer.WriteValue(Prohibit);
			}
		}
	}

	public class OptionSelectedCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.OptionSelectedCondition; } set {} }

		public string source;

		public bool? Require;
		public bool? Prohibit;

		public OptionSelectedCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			source   = XmlHelper.ReadStringAttribute(element, "source");
			Require  = XmlHelper.ReadBoolElement(element, ns + "Require");
			Prohibit = XmlHelper.ReadBoolElement(element, ns + "Prohibit");
		}

		public OptionSelectedCondition(JsonReader reader)
			:base(reader)
		{
		}

		public OptionSelectedCondition(OptionSelectedCondition src)
			:base(src)
		{
			source   = src.source;
			Require  = src.Require;
			Prohibit = src.Prohibit;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "source":
					source = JsonHelper.ReadString(reader);
					return true;
				case "Require":
					Require = JsonHelper.ReadBoolean(reader);
					return true;
				case "Prohibit":
					Prohibit = JsonHelper.ReadBoolean(reader);
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
			writer.WriteStartElement("OptionSelectedCondition");

			if(source != null)
				writer.WriteAttributeString("source", source);

			//	XML requires bools in lowercase
			if(Require != null)
				writer.WriteElementString("Require", Require.Value ? "true" : "false");
			if(Prohibit != null)
				writer.WriteElementString("Prohibit", Prohibit.Value ? "true" : "false");

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(source != null)
			{
				writer.WritePropertyName("source");
				writer.WriteValue(source);
			}
			if(Require != null)
			{
				writer.WritePropertyName("Require");
				writer.WriteValue(Require);
			}
			if(Prohibit != null)
			{
				writer.WritePropertyName("Prohibit");
				writer.WriteValue(Prohibit);
			}
		}
	}

	public class ContentSelectedCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.ContentSelectedCondition; } set {} }

		public bool? Require;
		public bool? Prohibit;

		public ContentSelectedCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			Require  = XmlHelper.ReadBoolElement(element, ns + "Require");
			Prohibit = XmlHelper.ReadBoolElement(element, ns + "Prohibit");
		}

		public ContentSelectedCondition(JsonReader reader)
			:base(reader)
		{
		}

		public ContentSelectedCondition(ContentSelectedCondition src)
			:base(src)
		{
			Require  = src.Require;
			Prohibit = src.Prohibit;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "Require":
					Require = JsonHelper.ReadBoolean(reader);
					return true;
				case "Prohibit":
					Prohibit = JsonHelper.ReadBoolean(reader);
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
			writer.WriteStartElement("ContentSelectedCondition");

			//	XML requires bools in lowercase
			if(Require != null)
				writer.WriteElementString("Require", Require.Value ? "true" : "false");
			if(Prohibit != null)
				writer.WriteElementString("Prohibit", Prohibit.Value ? "true" : "false");

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(Require != null)
			{
				writer.WritePropertyName("Require");
				writer.WriteValue(Require);
			}
			if(Prohibit != null)
			{
				writer.WritePropertyName("Prohibit");
				writer.WriteValue(Prohibit);
			}
		}
	}

	public class DocTagCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.DocTagCondition; } set {} }

		public string source;

		public string tag;

		public bool? Require;
		public bool? Prohibit;

		public DocTagCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			source   = XmlHelper.ReadStringAttribute(element, "source");
			tag      = XmlHelper.ReadStringAttribute(element, "tag"   );
			Require  = XmlHelper.ReadBoolElement(element, ns + "Require");
			Prohibit = XmlHelper.ReadBoolElement(element, ns + "Prohibit");
		}

		public DocTagCondition(JsonReader reader)
			:base(reader)
		{
		}

		public DocTagCondition(DocTagCondition src)
			:base(src)
		{
			source   = src.source;
			tag      = src.tag;
			Require  = src.Require;
			Prohibit = src.Prohibit;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "source":
					source = JsonHelper.ReadString(reader);
					return true;
				case "tag":
					tag = JsonHelper.ReadString(reader);
					return true;
				case "Require":
					Require = JsonHelper.ReadBoolean(reader);
					return true;
				case "Prohibit":
					Prohibit = JsonHelper.ReadBoolean(reader);
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
			writer.WriteStartElement("DocTagCondition");

			if(source != null)
				writer.WriteAttributeString("source", source);
			if(tag != null)
				writer.WriteAttributeString("tag", tag);

			//	XML requires bools in lowercase
			if(Require != null)
				writer.WriteElementString("Require", Require.Value ? "true" : "false");
			if(Prohibit != null)
				writer.WriteElementString("Prohibit", Prohibit.Value ? "true" : "false");

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(source != null)
			{
				writer.WritePropertyName("source");
				writer.WriteValue(source);
			}
			if(tag != null)
			{
				writer.WritePropertyName("tag");
				writer.WriteValue(tag);
			}
			if(Require != null)
			{
				writer.WritePropertyName("Require");
				writer.WriteValue(Require);
			}
			if(Prohibit != null)
			{
				writer.WritePropertyName("Prohibit");
				writer.WriteValue(Prohibit);
			}
		}
	}

	public class ContentDocTagCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.ContentDocTagCondition; } set {} }

		public string tag;

		public bool? Require;
		public bool? Prohibit;

		public ContentDocTagCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			tag      = XmlHelper.ReadStringAttribute(element, "tag");
			Require  = XmlHelper.ReadBoolElement(element, ns + "Require");
			Prohibit = XmlHelper.ReadBoolElement(element, ns + "Prohibit");
		}

		public ContentDocTagCondition(JsonReader reader)
			:base(reader)
		{
		}

		public ContentDocTagCondition(ContentDocTagCondition src)
			:base(src)
		{
			tag      = src.tag;
			Require  = src.Require;
			Prohibit = src.Prohibit;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "tag":
					tag = JsonHelper.ReadString(reader);
					return true;
				case "Require":
					Require = JsonHelper.ReadBoolean(reader);
					return true;
				case "Prohibit":
					Prohibit = JsonHelper.ReadBoolean(reader);
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
			writer.WriteStartElement("ContentDocTagCondition");

			if(tag != null)
				writer.WriteAttributeString("tag", tag);

			//	XML requires bools in lowercase
			if(Require != null)
				writer.WriteElementString("Require", Require.Value ? "true" : "false");
			if(Prohibit != null)
				writer.WriteElementString("Prohibit", Prohibit.Value ? "true" : "false");

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(tag != null)
			{
				writer.WritePropertyName("tag");
				writer.WriteValue(tag);
			}
			if(Require != null)
			{
				writer.WritePropertyName("Require");
				writer.WriteValue(Require);
			}
			if(Prohibit != null)
			{
				writer.WritePropertyName("Prohibit");
				writer.WriteValue(Prohibit);
			}
		}
	}

	public class ItemCountCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.ItemCountCondition; } set {} }

		public int? context;
		public string refType;
		public string refId;
		public int? Minimum;
		public int? Maximum;

		public ItemCountCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			context = XmlHelper.ReadIntAttribute(element, "context");
			refType = XmlHelper.ReadStringAttribute(element, "refType");
			refId   = XmlHelper.ReadStringAttribute(element, "refId"  );
			Minimum = XmlHelper.ReadIntElement(element, ns + "Minimum");
			Maximum = XmlHelper.ReadIntElement(element, ns + "Maximum");
		}

		public ItemCountCondition(JsonReader reader)
			:base(reader)
		{
		}

		public ItemCountCondition(ItemCountCondition src)
			:base(src)
		{
			context = src.context;
			refType = src.refType;
			refId   = src.refId;
			Minimum = src.Minimum;
			Maximum = src.Maximum;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "context":
					context = JsonHelper.ReadInteger(reader);
					return true;
				case "refType":
					refType = JsonHelper.ReadString(reader);
					return true;
				case "refId":
					refId = JsonHelper.ReadString(reader);
					return true;
				case "Minimum":
					Minimum = JsonHelper.ReadInteger(reader);
					return true;
				case "Maximum":
					Maximum = JsonHelper.ReadInteger(reader);
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
			writer.WriteStartElement("ItemCountCondition");

			if(context != null)
				writer.WriteAttributeString("context", context.ToString());
			if(refType != null)
				writer.WriteAttributeString("refType", refType);
			if(refId != null)
				writer.WriteAttributeString("refId", refId);

			if(Minimum != null)
				writer.WriteElementString("Minimum", Minimum.ToString());
			if(Maximum != null)
				writer.WriteElementString("Maximum", Maximum.ToString());

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(context != null)
			{
				writer.WritePropertyName("context");
				writer.WriteValue(context);
			}
			if(refType != null)
			{
				writer.WritePropertyName("refType");
				writer.WriteValue(refType);
			}
			if(refId != null)
			{
				writer.WritePropertyName("refId");
				writer.WriteValue(refId);
			}
			if(Minimum != null)
			{
				writer.WritePropertyName("Minimum");
				writer.WriteValue(Minimum);
			}
			if(Maximum != null)
			{
				writer.WritePropertyName("Maximum");
				writer.WriteValue(Maximum);
			}
		}
	}

	public class PhotoCountCondition : Condition
	{
		public override ConditionType Type { get { return ConditionType.PhotoCountCondition; } set {} }
		public int? context;
		public string source;
		public string refType;
		public string refId;
		public int? Minimum;
		public int? Maximum;

		public PhotoCountCondition(XElement element)
			:base(element)
		{
			XNamespace ns = element.GetDefaultNamespace();
			context = XmlHelper.ReadIntAttribute(element, "context");
			source  = XmlHelper.ReadStringAttribute(element, "source");
			refType = XmlHelper.ReadStringAttribute(element, "refType");
			refId   = XmlHelper.ReadStringAttribute(element, "refId"  );
			Minimum = XmlHelper.ReadIntElement(element, ns + "Minimum");
			Maximum = XmlHelper.ReadIntElement(element, ns + "Maximum");
		}

		public PhotoCountCondition(JsonReader reader)
			:base(reader)
		{
		}

		public PhotoCountCondition(PhotoCountCondition src)
			:base(src)
		{
			context = src.context;
			source  = src.source;
			refType = src.refType;
			refId   = src.refId;
			Minimum = src.Minimum;
			Maximum = src.Maximum;
		}

		protected override bool LoadProperty(JsonReader reader, string propName)
		{
			switch(propName)
			{
				case "context":
					context = JsonHelper.ReadInteger(reader);
					return true;
				case "source":
					source = JsonHelper.ReadString(reader);
					return true;
				case "refType":
					refType = JsonHelper.ReadString(reader);
					return true;
				case "refId":
					refId = JsonHelper.ReadString(reader);
					return true;
				case "Minimum":
					Minimum = JsonHelper.ReadInteger(reader);
					return true;
				case "Maximum":
					Maximum = JsonHelper.ReadInteger(reader);
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
			writer.WriteStartElement("PhotoCountCondition");

			if(context != null)
				writer.WriteAttributeString("context", context.ToString());
			if(source != null)
				writer.WriteAttributeString("source", source);
			if(refType != null)
				writer.WriteAttributeString("refType", refType);
			if(refId != null)
				writer.WriteAttributeString("refId", refId);

			if(Minimum != null)
				writer.WriteElementString("Minimum", Minimum.ToString());
			if(Maximum != null)
				writer.WriteElementString("Maximum", Maximum.ToString());

			writer.WriteEndElement();
		}

		public override void WriteJson(JsonWriter writer)
		{
			base.WriteJson(writer);

			if(context != null)
			{
				writer.WritePropertyName("context");
				writer.WriteValue(context);
			}
			if(source != null)
			{
				writer.WritePropertyName("source");
				writer.WriteValue(source);
			}
			if(refType != null)
			{
				writer.WritePropertyName("refType");
				writer.WriteValue(refType);
			}
			if(refId != null)
			{
				writer.WritePropertyName("refId");
				writer.WriteValue(refId);
			}
			if(Minimum != null)
			{
				writer.WritePropertyName("Minimum");
				writer.WriteValue(Minimum);
			}
			if(Maximum != null)
			{
				writer.WritePropertyName("Maximum");
				writer.WriteValue(Maximum);
			}
		}
	}
}
