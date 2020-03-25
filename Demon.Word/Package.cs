using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.IO.Compression;
using Demon.Report.Style;

namespace Demon.Word
{
	public class Package
	{
		private ContentTypes   _contentTypes;
		private Document       _document;
		private Styles         _styles;
		private FontTable      _fonts;
		private Settings       _settings;
		private CoreProperties _coreProperties;
		private AppProperties  _appProperties;
		private Relationships  _relationships;

		private Dictionary<string, string> _docInfo;
		private Guid _fileId;

		public IDocument Document { get { return _document; }}

		public Package(Dictionary<string, string> docInfo)
		{
			_docInfo = docInfo;
			_fileId = Guid.NewGuid();

			_contentTypes   = new ContentTypes();
			_document       = new Document      ("word/document.xml" );
//TODO: should these be here in the package or in the document?
//			_styles         = new Styles        ("word/styles.xml"   );
//			_fonts          = new FontTable     ("word/fontTable.xml");
//			_settings       = new Settings      ("word/settings.xml" );
			_coreProperties = new CoreProperties("docProps/core.xml" );
			_appProperties  = new AppProperties ("docProps/app.xml"  );
			_relationships  = new Relationships ("_rels/.rels"       );

			_relationships.Add(_document,       "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
			_relationships.Add(_coreProperties, "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties");
			_relationships.Add(_appProperties,  "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties");
		}

		public Stream Write()
		{
			//	Write the package to a temporary zip file
			string zipFilename = System.IO.Path.GetTempFileName();
			ZipArchive archive = ZipFile.Open(zipFilename, ZipArchiveMode.Update);

			//	Optional components
			//		/word/settings.xml
			//		/word/fontTable.xml
			//		/word/styles.xml
			//		/docProps/core.xml
			//		/docProps/app.xml
			if(_settings != null)
			{
				_contentTypes.AddContentType(
					"/word/settings.xml",
					"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml");
				_settings.Write(archive);
			}
			if(_fonts != null)
			{
				_contentTypes.AddContentType(
					"/word/fontTable.xml",
					"application/vnd.openxmlformats-officedocument.wordprocessingml.fontTable+xml");
				_fonts.Write(archive);
			}
			if(_styles != null)
			{
				_contentTypes.AddContentType(
					"/word/styles.xml",
					"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml");
				_styles.Write(archive);
			}

			_contentTypes.AddContentType(
				"/word/numbering.xml",
				"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml");

			if(_coreProperties != null)
			{
				_contentTypes.AddContentType(
					"/docProps/core.xml",
					"application/vnd.openxmlformats-package.core-properties+xml");
				_coreProperties.Write(archive);
			}
			if(_appProperties != null)
			{
				_contentTypes.AddContentType(
					"/docProps/app.xml",
					"application/vnd.openxmlformats-officedocument.extended-properties+xml");
				_appProperties.Write(archive);
			}

			//	Required components:
			//		/[Content_Types].xml
			//		/_rels/.rels
			//		/word/document.xml
			//		/word/_rels/document.xml.rels (written as part of the document)
			_contentTypes.Write(archive);
			_relationships.Write(archive);
			_document.Write(archive);

			//	Close the temporary zip file, then reopen it with the
			//	delete-on-close flag set, and return it. When the caller
			//	disposes of the stream, the temporary file will be deleted.
			archive.Dispose();
			return new FileStream(
				zipFilename, FileMode.Open, FileAccess.Read,
				FileShare.None, 4096, FileOptions.DeleteOnClose);
		}

		public string GetFileId()
		{
			return _fileId.ToString("N");
		}
	}
}
