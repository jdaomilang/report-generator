using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Demon.Word
{
	internal class AppProperties : Part
	{
		private Dictionary<string, AppProperty> _props = new Dictionary<string, AppProperty>();
		public override string ContentType => "application/vnd.openxmlformats-officedocument.extended-properties+xml";

		public AppProperties(string name)
			:base(name)
		{
		}

		protected override void WriteContent()
		{
			XNamespace ns = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
			XNamespace vt = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes");

			XElement root = new XElement(
				XName.Get("Properties", ns.NamespaceName),
				new XAttribute(XNamespace.Xmlns + "vt", vt));
			_document.Add(root);

			foreach(AppProperty prop in _props.Values)
				prop.Write(root);
		}
	}

	internal abstract class AppProperty
	{
		public abstract void Write(XElement root);
	}
}
