using System.Collections.Generic;
using System.Xml.Linq;

namespace Demon.Word
{
	internal class ContentTypes : File
	{
		private Dictionary<string, string> _overrides = new Dictionary<string, string>();

		public override string Name { get { return "[Content_Types].xml"; }}

		//TODO: can we only add overrides, or can we also add defaults?
		public void AddContentType(string partName, string contentType)
		{
			_overrides.Add(partName, contentType);
		}

		protected override void WriteContent()
		{
			XNamespace ns = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types");
			XName name = XName.Get("Types", ns.NamespaceName);
			XElement typesElement = new XElement(name);
			_document.Add(typesElement);

			//	The package must contain, at a minimum, a document part and a
			//	package-level relationship to it
			WriteDefaultContentType (ns, "rels", "application/vnd.openxmlformats-package.relationships+xml");
			WriteDefaultContentType (ns, "xml",  "application/xml");
			WriteOverrideContentType(ns, "/word/document.xml",  "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
			WriteDefaultContentType (ns, "jpg", "image/jpeg");

			//	Other parts are optional
			foreach(KeyValuePair<string, string> item in _overrides)
				WriteOverrideContentType(ns, item.Key, item.Value);
		}

		/// <summary>
		/// Define the default content type to be associated with a filename extension.
		/// </summary>
		private void WriteDefaultContentType(XNamespace ns, string extension, string contentType)
		{
			XElement elem = new XElement(XName.Get("Default", ns.NamespaceName));
			elem.SetAttributeValue("Extension", extension);
			elem.SetAttributeValue("ContentType", contentType);
			_document.Root.Add(elem);
		}

		/// <summary>
		/// Override the default content type for a specific part file.
		/// </summary>
		private void WriteOverrideContentType(XNamespace ns, string partName, string contentType)
		{
			XElement elem = new XElement(XName.Get("Override", ns.NamespaceName));
			elem.SetAttributeValue("PartName", partName);
			elem.SetAttributeValue("ContentType", contentType);
			_document.Root.Add(elem);
		}
	}
}
