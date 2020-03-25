using System;
using System.Text;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	internal static class JsonHelper
	{
		public static void AssertToken(JsonReader reader, JsonToken expected)
		{
			if(reader.TokenType != expected)
				throw new DesignException
					($"Expected {expected}, got {reader.TokenType} at {reader.Path}.",
					new LineNumberCapture(reader));
		}

		public static void AssertToken(JsonReader reader, params JsonToken[] expected)
		{
			foreach(JsonToken token in expected)
				if(reader.TokenType == token)
					return;
			
			StringBuilder sb = new StringBuilder();
			sb.Append("Expected one of [");
			foreach(JsonToken token in expected)
			{
				sb.Append(token);
				sb.Append(' ');
			}
			sb.Length -= 1; // remove trailing space
			sb.Append("], got ");
			sb.Append(reader.TokenType);
			sb.Append(" at ");
			sb.Append(reader.Path);
			sb.Append('.');
			throw new DesignException(sb.ToString(), new LineNumberCapture(reader));
		}

		public static string ReadString(JsonReader reader)
		{
			bool ok = reader.Read();
			AssertToken(reader, JsonToken.String);
			return (string)reader.Value;
		}

		public static int? ReadInteger(JsonReader reader)
		{
			bool ok = reader.Read();
			AssertToken(reader, JsonToken.Integer);
			if(reader.Value != null)
				return (int)(long)reader.Value;
			else
				return null;
		}

		public static double? ReadDouble(JsonReader reader)
		{
			bool ok = reader.Read();
			AssertToken(reader, JsonToken.Float, JsonToken.Integer);
			switch(reader.TokenType)
			{
				case JsonToken.Float:
					return (double)reader.Value;
				case JsonToken.Integer:
					return (double)(long)reader.Value;
				default:
					return null;
			}
		}

		public static float? ReadFloat(JsonReader reader)
		{
			bool ok = reader.Read();
			AssertToken(reader, JsonToken.Float, JsonToken.Integer);
			if(reader.Value != null)
				return (float)reader.Value;
			else
				return null;
		}

		public static bool? ReadBoolean(JsonReader reader)
		{
			bool ok = reader.Read();
			AssertToken(reader, JsonToken.Boolean);
			if(reader.Value != null)
				return (bool)reader.Value;
			else
				return null;
		}

#if false
		public static string ReadStringElement(XElement elem, string path)
		{
			if(elem == null) return null;
			XElement child = elem.Element(path);
			if(child == null) return null;
			return child.Value;
		}

		public static int? ReadIntElement(XElement elem, string path)
		{
			int? i = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				i = int.Parse(s);
			return i;
		}

		public static double? ReadDoubleElement(XElement elem, string path)
		{
			double? d = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				d = double.Parse(s);
			return d;
		}

		public static bool? ReadBoolElement(XElement elem, string path)
		{
			bool? b = null;
			string s = ReadStringElement(elem, path);
			if(!string.IsNullOrWhiteSpace(s))
				b = bool.Parse(s);
			return b;
		}
#endif

		public static Nullable<T> ReadEnum<T>(JsonReader reader) where T : struct
		{
			Nullable<T> t = null;
			string s = reader.ReadAsString();
			if(!string.IsNullOrWhiteSpace(s))
				t = (T)Enum.Parse(typeof(T), s);
			return t;
		}

#if false
		public static string ReadStringAttribute(XElement elem, string name)
		{
			if(elem == null) return null;
			XAttribute attr = elem.Attribute(name);
			if(attr == null) return null;
			return attr.Value;
		}

		public static int? ReadIntAttribute(XElement elem, string name)
		{
			int? i = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				i = int.Parse(s);
			return i;
		}

		public static double? ReadDoubleAttribute(XElement elem, string name)
		{
			double? d = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				d = double.Parse(s);
			return d;
		}

		public static bool? ReadBoolAttribute(XElement elem, string name)
		{
			bool? b = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				b = bool.Parse(s);
			return b;
		}

		public static Nullable<T> ReadEnumAttribute<T>(XElement elem, string name) where T : struct
		{
			Nullable<T> t = null;
			string s = ReadStringAttribute(elem, name);
			if(!string.IsNullOrWhiteSpace(s))
				t = (T)Enum.Parse(typeof(T), s);
			return t;
		}
#endif
	}
	
	/// <summary>
	/// Captures the current line number position from an object and makes it
	/// available via this object's ILineNumber interface.
	/// </summary>
	public class LineNumberCapture : ILineNumber
	{
		public int LineNumber { get; private set; }
		public int LinePosition { get; private set; }
		
		/// <summary>
		/// Extracts the current line number information from a JsonReader, if the
		/// reader is actually a JsonTextReader. (Line number is not available in
		/// a plain JsonReader.)
		/// </summary>
		public LineNumberCapture(JsonReader reader)
		{
			JsonTextReader textreader = reader as JsonTextReader;
			if(textreader != null)
			{
				LineNumber   = textreader.LineNumber;
				LinePosition = textreader.LinePosition;
			}
		}

	}
}
