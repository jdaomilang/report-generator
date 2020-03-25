using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Demon.Word
{
	internal class Relationships : Part
	{
		public override string ContentType => "application/vnd.openxmlformats-package.relationships+xml";

		private Dictionary<string, Relationship> _relationships = new Dictionary<string, Relationship>();

		public Relationships(string name)
			:base(name)
		{
		}

		public Relationship Add(Part part, string type)
		{
			Relationship rel = new Relationship(part, type);
			_relationships[rel.Id] = rel;
			return rel;
		}

		public Relationship AddImage(Image image, string path)
		{
			Relationship rel = new Relationship(
				image,
				"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image",
				path);
			_relationships[rel.Id] = rel;
			return rel;
		}

		public Relationship AddSettings(Settings settings, string path)
		{
			Relationship rel = new Relationship(
				settings,
				"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings",
				path);
			_relationships[rel.Id] = rel;
			return rel;
		}

		public Relationship AddStyles(Styles styles, string path)
		{
			Relationship rel = new Relationship(
				styles,
				"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles",
				path);
			_relationships[rel.Id] = rel;
			return rel;
		}

		public Relationship AddNumberings(Numberings numberings, string path)
		{
			Relationship rel = new Relationship(
				numberings,
				"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering",
				path);
			_relationships[rel.Id] = rel;
			return rel;
		}

		public Relationship AddFontTable(FontTable fontTable, string path)
		{
			Relationship rel = new Relationship(
				fontTable,
				"http://schemas.openxmlformats.org/officeDocument/2006/relationships/fontTable",
				path);
			_relationships[rel.Id] = rel;
			return rel;
		}

		protected override void WriteContent()
		{
			XNamespace ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
			XName name = XName.Get("Relationships", ns.NamespaceName);
			XElement root = new XElement(name);
			_document.Add(root);

			foreach(Relationship rel in _relationships.Values)
				rel.Write(root, ns);
		}
	}

	internal class Relationship
	{
		private string _type;
		private string _id;
		private Part _part;
		private string _path;

		private static int _nextId = 0;
		public string Id { get { return _id; }}

		public Relationship(Part part, string type)
		{
			_type = type;
			_id = $"rId{_nextId++}";
			_part = part;
			_path = _part.Name;
		}

		public Relationship(Part part, string type, string path)
		{
			_type = type;
			_id = $"rId{_nextId++}";
			_part = part;
			_path = path;
		}

		public void Write(XElement root, XNamespace ns)
		{
			XElement elem = new XElement(XName.Get("Relationship", ns.NamespaceName));
			elem.SetAttributeValue("Id", _id);
			elem.SetAttributeValue("Type", _type);
			elem.SetAttributeValue("Target", _path);
			root.Add(elem);
		}
	}
}
