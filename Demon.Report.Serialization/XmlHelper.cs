using System;
using System.Xml.Linq;

namespace Demon.Report.Serialization
{
	internal static class XmlHelper
	{
		public static string ReadStringElement(XElement elem, XName path)
		{
			if(elem == null) return null;
			XElement child = elem.Element(path);
			if(child == null) return null;
			return child.Value;
		}

		public static int? ReadIntElement(XElement elem, XName path)
		{
			int? i = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				i = int.Parse(s);
			return i;
		}

		public static double? ReadDoubleElement(XElement elem, XName path)
		{
			double? d = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				d = double.Parse(s);
			return d;
		}

		public static float? ReadFloatElement(XElement elem, XName path)
		{
			float? f = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				f = float.Parse(s);
			return f;
		}

		public static bool? ReadBoolElement(XElement elem, XName path)
		{
			bool? b = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				b = bool.Parse(s);
			return b;
		}

		public static Nullable<T> ReadEnumElement<T>(XElement elem, XName path) where T : struct
		{
			Nullable<T> t = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				t = (T)Enum.Parse(typeof(T), s);
			return t;
		}

		public static string ReadStringAttribute(XElement elem, XName name)
		{
			if(elem == null) return null;
			XAttribute attr = elem.Attribute(name);
			if(attr == null) return null;
			return attr.Value;
		}

		public static int? ReadIntAttribute(XElement elem, XName name)
		{
			int? i = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				i = int.Parse(s);
			return i;
		}

		public static double? ReadDoubleAttribute(XElement elem, XName name)
		{
			double? d = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				d = double.Parse(s);
			return d;
		}

		public static float? ReadFloatAttribute(XElement elem, XName name)
		{
			float? f = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				f = float.Parse(s);
			return f;
		}

		public static bool? ReadBoolAttribute(XElement elem, XName name)
		{
			bool? b = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				b = bool.Parse(s);
			return b;
		}

		public static Nullable<T> ReadEnumAttribute<T>(XElement elem, XName name) where T : struct
		{
			Nullable<T> t = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				t = (T)Enum.Parse(typeof(T), s);
			return t;
		}
	}
}
