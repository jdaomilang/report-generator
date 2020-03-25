using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Demon.Core.Domain;
using Demon.Path;

namespace Demon.Report
{
	/// <summary>
	/// Any layout can define a dictionary of terms for any data objects.
	/// When the layout renders an object, if it has a term defined for
	/// that object then it renders the term definition instead of the
	/// object's own text.
	/// </summary>
	internal class TermDictionary
	{
		private Dictionary<string, string> _terms = new Dictionary<string, string>();

		public void Load(XElement root, Generator generator)
		{
			if(root == null) return;

			XNamespace ns = root.GetDefaultNamespace();
			foreach(XElement elem in root.Elements(ns + "Term"))
			{
				int lineNumber = ((IXmlLineInfo)root).LineNumber;
				int linePosition = ((IXmlLineInfo)root).LinePosition;

				string key = generator.ReportDesign.LoadString(elem.Attribute("key"));
				string term = elem.Value;

				_terms.Add(key, term);
			}
		}

		public bool TryGetTerm(Reference source, out string term)
		{
			string key = $"{source.Type}:{source.Id}";
			return _terms.TryGetValue(key, out term);
		}
	}
}
