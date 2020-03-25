using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace Demon.Word
{
	internal abstract class File
	{
		protected XDocument _document;
		public abstract string Name { get; }

		public File()
		{
			//	Prepare the XML document
			XDeclaration declaration = new XDeclaration("1.0", "UTF-8", "yes");
			_document = new XDocument(declaration);
		}

		/// <summary>
		/// Create an XML document in the given zip archive and write content
		/// to it.
		/// </summary>
		public virtual void Write(ZipArchive archive)
		{
			//	Write the content to the XML document. This is type-specific.
			WriteContent();

			//	Write the XML document to the zip archive
			ZipArchiveEntry entry = archive.CreateEntry(Name, CompressionLevel.NoCompression);
			using(Stream stream = entry.Open())
			{
				XmlWriterSettings settings = new XmlWriterSettings();
				settings.Encoding = new UTF8Encoding(false);
				XmlWriter writer = XmlWriter.Create(stream, settings);
				_document.WriteTo(writer);
				writer.Flush();
			}
		}

		/// <summary>
		/// Write the file's contents to its XML document.
		/// </summary>
		protected virtual void WriteContent()
		{
		}
	}
}
