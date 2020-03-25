using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Demon.Report.Serialization
{
	public class DesignSerializer
	{
		public static Stream ConvertDesignFile(Stream designFile, string inputType, string outputType)
		{
			if(inputType == outputType) return designFile;

			switch(inputType)
			{
				case Demon.Core.MimeType.JSON:
				{
					using(StreamReader sr = new StreamReader(designFile))
					{
						JsonReader reader = new JsonTextReader(sr);
						Design design = new Design(reader);
						return Serialize(design, outputType);
					}
				}

				case Demon.Core.MimeType.XML:
				{
					XDocument doc = XDocument.Load(designFile, LoadOptions.SetLineInfo);
					Design design = new Design(doc);
					return Serialize(design, outputType);
				}

				default:
					throw new ArgumentException($"Cannot load design file with MIME type {inputType}.");
			}
		}

		public static string XmlToJson(Stream xml)
		{
			XDocument doc = XDocument.Load(xml, LoadOptions.SetLineInfo);
			Design design = new Design(doc);
			string json = SerializeJSON(design);
			return json;
		}

		public static string JsonToXml(Stream json)
		{
			using(StreamReader sr = new StreamReader(json))
			{
				JsonReader reader = new JsonTextReader(sr);
				Design design = new Design(reader);
				string xml = SerializeXML(design);
				return xml;
			}
		}

		private static Stream Serialize(Design design, string mimeType)
		{
			switch(mimeType)
			{
				case Demon.Core.MimeType.XML:
				{
					MemoryStream stream = new MemoryStream();
					XmlWriterSettings settings = new XmlWriterSettings();
					settings.Encoding = Encoding.UTF8;
					settings.Indent = true;
					settings.IndentChars = " ";
					using(XmlWriter writer = XmlWriter.Create(stream, settings))
					{
						string ns = "http://www.demoninspect.com/schemas/report-design";
						System.Xml.Serialization.XmlSerializer serializer =
							new System.Xml.Serialization.XmlSerializer(design.GetType(), ns);
						serializer.Serialize(writer, design);
					}
					stream.Capacity = (int)stream.Length;
					stream.Seek(0, SeekOrigin.Begin);
					return stream;
				}

				case Demon.Core.MimeType.JSON:
				{
					MemoryStream stream = new MemoryStream();
					using(StreamWriter sw = new StreamWriter(stream))
					{
						JsonTextWriter jw = new JsonTextWriter(sw);
						jw.Formatting = Newtonsoft.Json.Formatting.Indented;

						design.WriteJson(jw);
						
						jw.Flush();
						stream.Seek(0, SeekOrigin.Begin);
						return stream;
					}
				}

				default:
					throw new ArgumentException($"Cannot serialize design to MIME type {mimeType}.");
			}
		}

		private static string SerializeXML(Design design)
		{
			MemoryStream stream = new MemoryStream();
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.Encoding = Encoding.UTF8;
			settings.Indent = true;
			settings.IndentChars = " ";
			using(XmlWriter writer = XmlWriter.Create(stream, settings))
			{
				string ns = "http://www.demoninspect.com/schemas/report-design";
				System.Xml.Serialization.XmlSerializer serializer =
					new System.Xml.Serialization.XmlSerializer(design.GetType(), ns);
				serializer.Serialize(writer, design);
			}
			stream.Capacity = (int)stream.Length;
			return Encoding.UTF8.GetString(stream.GetBuffer());
		}

		private static string SerializeJSON(Design design)
		{
			using(StringWriter sw = new StringWriter())
			{
				JsonTextWriter jw = new JsonTextWriter(sw);
				jw.Formatting = Newtonsoft.Json.Formatting.Indented;

				design.WriteJson(jw);
				return sw.ToString();
			}
		}
		
		public static void ValidateXML(Stream xml)
		{
			XDocument doc = XDocument.Load(xml, LoadOptions.SetLineInfo);
			Design design = new Design(doc);
			design.Validate();
		}

		public static void ValidateJSON(Stream json)
		{
			using(StreamReader sr = new StreamReader(json, Encoding.Default, true, 4096, true))
			{
				JsonReader reader = new JsonTextReader(sr);
				Design design = new Design(reader);
			}
		}
	}
}
