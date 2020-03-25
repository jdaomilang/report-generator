using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Demon.Word
{
	internal class CoreProperties : Part
	{
		private Dictionary<string, CoreProperty> _props = new Dictionary<string, CoreProperty>();
		public override string ContentType => "application/vnd.openxmlformats-package.core-properties+xml";

		public CoreProperties(string name)
			:base(name)
		{
		}

		protected override void WriteContent()
		{
			XNamespace cp       = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
			XNamespace dc       = XNamespace.Get("http://purl.org/dc/elements/1.1/");
			XNamespace dcterms  = XNamespace.Get("http://purl.org/dc/terms/");
			XNamespace dcmitype = XNamespace.Get("http://purl.org/dc/dcmitype/");
			XNamespace xsi      = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

			XElement root = new XElement(
				cp + "coreProperties",
				new XAttribute(XNamespace.Xmlns + "cp", cp),
				new XAttribute(XNamespace.Xmlns + "dc", dc),
				new XAttribute(XNamespace.Xmlns + "dcterms", dcterms),
				new XAttribute(XNamespace.Xmlns + "dcmitype", dcmitype),
				new XAttribute(XNamespace.Xmlns + "xsi", xsi));
			_document.Add(root);

			foreach(CoreProperty prop in _props.Values)
				prop.Write(root);
		}
	}

	internal abstract class CoreProperty
	{
		public abstract void Write(XElement root);
	}
}
