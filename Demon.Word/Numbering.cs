using System;
using System.Collections.Generic;
using System.Xml.Linq;
using s = Demon.Report.Style;

namespace Demon.Word
{
	internal class Numberings : Part
	{
		private Dictionary<s.BulletStyle, Dictionary<int, AbstractNumberingDefinition>> _abstractDefinitions;
		private List<NumberingDefinitionInstance> _instances;

		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml";

		public Numberings(string name)
			:base(name)
		{
			_abstractDefinitions = new Dictionary<s.BulletStyle, Dictionary<int, AbstractNumberingDefinition>>();
			_instances = new List<NumberingDefinitionInstance>();
		}

		/// <summary>
		/// Create and add a new Word numbering style to represent the given design
		/// bullet style at the given level. If the bullet style already has a
		/// representation at this level then return that representation's id.
		/// </summary>
		public int AddAbstractDefinition(s.BulletStyle style, int level, string bulletText)
		{
			Dictionary<int, AbstractNumberingDefinition> inner = null;
			AbstractNumberingDefinition def = null;
			bool found = _abstractDefinitions.TryGetValue(style, out inner);
			if(found)
			{
				found = inner.TryGetValue(level, out def);
				if(found)
					return def.Id;
			}

			if(inner == null)
			{
				inner = new Dictionary<int, AbstractNumberingDefinition>();
				_abstractDefinitions.Add(style, inner);
			}
			def = new AbstractNumberingDefinition(style, level, bulletText);
			inner.Add(level, def);
			return def.Id;
		}

		/// <summary>
		/// Get the abstract Word numbering definition that maps to the given design style.
		/// </summary>
		public int GetNumbering(s.BulletStyle style, int level)
		{
			Dictionary<int, AbstractNumberingDefinition> inner = null;
			AbstractNumberingDefinition def = null;
			bool found = _abstractDefinitions.TryGetValue(style, out inner);
			if(found)
			{
				found = inner.TryGetValue(level, out def);
				if(found)
					return def.Id;
			}
			throw new Exception($"Numbering style {style}@{level} not found.");
		}

		public int AddInstance(int abstractNumberingDefinitionId)
		{
			NumberingDefinitionInstance instance = new NumberingDefinitionInstance(abstractNumberingDefinitionId);
			_instances.Add(instance);
			return instance.Id;
		}

		protected override void WriteContent()
		{
			XNamespace w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

			XElement root = new XElement(
				w + "numbering",
				new XAttribute(XNamespace.Xmlns + "w", w));
			_document.Add(root);

			foreach(Dictionary<int, AbstractNumberingDefinition> inner in _abstractDefinitions.Values)
				foreach(AbstractNumberingDefinition def in inner.Values)
					def.Write(root);

			foreach(NumberingDefinitionInstance instance in _instances)
				instance.Write(root);
		}
	}

	internal class AbstractNumberingDefinition
	{
		private int _id;
		private int _level;
		private s.BulletStyle _style;
		private string _text;

		private static int _nextId = 0;

		public int Id { get { return _id; }}

		public AbstractNumberingDefinition(s.BulletStyle style, int level, string text)
		{
			_id = _nextId++;
			_level = level;
			_style = style;
			_text = text;
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement abstractNum = new XElement(
				ns + "abstractNum",
				new XAttribute(ns + "abstractNumId", _id));
			parent.Add(abstractNum);

			XElement multi = new XElement(
				ns + "multiLevelType",
				new XAttribute(ns + "val", "multiLevel"));
			abstractNum.Add(multi);

			XElement lvl = new XElement(
				ns + "lvl",
				new XAttribute(ns + "ilvl", _level));
			//	We use level zero to mean "no indent" (that is, not a list
			//	at all) but Word starts numbering levels at zero, so we
			//	subtract one here
			abstractNum.Add(lvl);
				
			string formatName = GetNumFmt(_style.NumberStyle);
			XElement numFmt = new XElement(
				ns + "numFmt",
				new XAttribute(ns + "val", formatName));
			lvl.Add(numFmt);

			XElement lvlText = new XElement(
				ns + "lvlText",
				new XAttribute(ns + "val", _text));
			lvl.Add(lvlText);

			XElement rPr = StyleHelpers.AddRunProperties(lvl, _style.Font, _style.Color);

			//TODO: tabs?
		}

		private static string GetNumFmt(s.ListNumberStyle style)
		{
			switch(style)
			{
				case s.ListNumberStyle.Bullet:     return "bullet";
				case s.ListNumberStyle.Number:     return "decimal";
				case s.ListNumberStyle.AlphaLower: return "lowerLetter";
				case s.ListNumberStyle.AlphaUpper: return "upperLetter";
				case s.ListNumberStyle.RomanLower: return "lowerRoman";
				case s.ListNumberStyle.RomanUpper: return "upperRoman";
//				case s.ListNumberStyle.GreekLower: return "";
//				case s.ListNumberStyle.GreekUpper: return "";
				default: return "decimal";
			}
		}
	}

	internal class NumberingDefinitionInstance
	{
		private int _id;
		private int _abstractNumberingDefinitionId;

		//	Instance ids start at 1. I can't find a direct statement of this
		//	rule, but section 17.9.18 says this:
		//		"A value of 0 for the val attribute shall never be used to point
		//		to a numbering definition instance, and shall instead only be used
		//		to designate the removal of numbering properties at a particular
		//		level in the style hierarchy."
		private static int _nextId = 1;

		public int Id { get { return _id; }}

		public NumberingDefinitionInstance(int abstractNumberingDefinitionId)
		{
			_id = _nextId++;
			_abstractNumberingDefinitionId = abstractNumberingDefinitionId;
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement num = new XElement(
				ns + "num",
				new XAttribute(ns + "numId", _id));
			parent.Add(num);

			XElement abstractNumId = new XElement(
				ns + "abstractNumId",
				new XAttribute(ns + "val", _abstractNumberingDefinitionId));
			num.Add(abstractNumId);

			XElement multiLevelType = new XElement(
				ns + "multiLevelType",
				new XAttribute(ns + "val", "multiLevel"));
			abstractNumId.Add(multiLevelType);
		}
	}
}
