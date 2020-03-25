using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;
using System.IO;
using System.Xml.Linq;

namespace Demon.Word
{
	internal class Settings : Part
	{
		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml";

		private Dictionary<string, Setting> _settings = new Dictionary<string, Setting>();

		public Settings(string name)
			:base(name)
		{
		}

		protected override void WriteContent()
		{
			XNamespace w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

			XElement root = new XElement(
				w + "settings",
				new XAttribute(XNamespace.Xmlns + "w", w));
			_document.Add(root);

			foreach(Setting setting in _settings.Values)
				setting.Write(root);
		}

		public void AddSetting(string name, Setting value)
		{
			_settings.Add(name, value);
		}
	}

	internal abstract class Setting
	{
		public abstract void Write(XElement root);
	}
}
