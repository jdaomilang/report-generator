using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Demon.PDF
{
	public class Document
	{
		private Dictionary<IIndirectObject,ObjectReference> _objects;
		private Catalog _catalog;
		private long _xrefByteOffset;
		private int _nextObjectNumber;
		
		private FontCache _fonts;
		internal FontCache Fonts { get { return _fonts; }}

		private PageList _pages;
		public PageList Pages { get { return _pages; } }
		public Page CurrentPage { get { return _pages.LastOrDefault(); } }

		private Guid _fileId;
		public string FileId { get { return _fileId.ToString("N"); } }

		private DocumentInformationDictionary _docInfo;
		public DocumentInformationDictionary DocInfo { get { return _docInfo; }}


		public Document(Dictionary<string, string> docInfo, Demon.Font.FontCache fonts)
		{
			_pages = new PageList();
			_fonts = new FontCache(fonts);
			_fileId = Guid.NewGuid();
			_docInfo = new DocumentInformationDictionary(docInfo);
		}

		public Stream Write()
		{
			_objects = new Dictionary<IIndirectObject, ObjectReference>();
			_nextObjectNumber = 1;
			_xrefByteOffset = 0;

			_catalog = new Catalog();
			ObjectReference catref = GetReference(_catalog);

			_fonts.AliasFonts();

			MemoryStream stream = null;
			try
			{
				stream = new MemoryStream();
				WritePdfHeader(stream);
				WriteBinaryIndicator(stream);
				WriteBody(stream);
				WriteFonts(stream);
				WriteInfoDictionary(stream);
				WriteXrefTable(stream);
				WriteTrailer(stream, catref);
				
				stream.Seek(0,SeekOrigin.Begin);
			}
			catch(Exception)
			{
				stream?.Dispose();
				throw;
			}
			return stream;
		}

		private void WriteBody(Stream stream)
		{
			//	Write the document catalog
			ObjectReference catref = _objects[_catalog];
			_catalog.Write(stream, catref, this);

			//	Write the page list
			ObjectReference pagesref = _objects[_pages];
			_pages.Write(stream, pagesref, this);

			//	Write the pages themselves
			foreach(Page page in _pages)
			{
				ObjectReference pageref = _objects[page];
				page.Write(stream, pageref, pagesref, this);
			}
		}

		private void WriteFonts(Stream stream)
		{
			foreach(Font font in _fonts.Fonts)
			{
				//	Write the True Type or Type 1 font
				font.Write(stream, this);

				//	If it's a True Type font then write its CID and Type0 fonts
				TrueTypeFont ttf = font as TrueTypeFont;
				if(ttf != null)
					ttf.RootFont.Write(stream, this);
			}
		}

		private void WriteXrefTable(Stream stream)
		{
			//	Take a note of where the table begins, so that we can fill in
			//	the trailer
			_xrefByteOffset = stream.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append("xref\r\n");
			sb.Append(0); // first in the xref table is the dummy zero free object
			sb.Append(" ");
			sb.Append(_objects.Count + 1); // +1 for the dummy zero free object
			sb.Append("\r\n");
			WriteData(stream,sb.ToString());

			//	Write the dummy zero free object
			WriteData(stream, "0000000000 65535 f\r\n");
			//	Write the real xrefs
			foreach(ObjectReference obj in _objects.Values)
				WriteXrefEntry(stream,obj);
		}

		private void WriteXrefEntry(Stream stream, ObjectReference obj)
		{
			string data;
			if(obj.InUse)
			{
				//	For in-use entries the first item is the byte offset in the
				//	file of the referenced object
				data = $"{obj.ByteOffset:D10} {obj.Generation:D5} n\r\n";
			}
			else
			{
				//	For free entries the first item is the object number of the
				//	next free object. But we don't support updating the document,
				//	so there are no free entries and so the first item is always
				//	zero.
				data = $"0000000000 {obj.Generation:D5} f\r\n";
			}
			WriteData(stream, data);
		}

		private void WritePdfHeader(Stream stream)
		{
			string header = "%PDF-1.7\r\n";
			WriteData(stream,header);
		}

		/// <summary>
		/// Write four bytes of arbitrary binary data (greater than ASCII 128)
		/// as a hint to file transfer programs that the file contains binary
		/// data. (Recommendation of the PDF spec, page 92.)
		/// </summary>
		private void WriteBinaryIndicator(Stream stream)
		{
			WriteData(stream, "%");
			byte[] bytes = { 0xa3, 0xc0, 0xc1, 0xbb };
			stream.Write(bytes, 0, 4);
			WriteData(stream, "\r\n");
		}

		private void WriteInfoDictionary(Stream stream)
		{
			ObjectReference inforef = GetReference(_docInfo);
			_docInfo.Write(stream,inforef);
		}

		private void WriteTrailer(Stream stream, ObjectReference catalog)
		{
			ObjectReference inforef = GetReference(_docInfo);

			StringBuilder sb = new StringBuilder();
			sb.Append("trailer\r\n<<\r\n");
			sb.Append($"/Size {_objects.Count+1}\r\n");
			sb.Append($"/Root {catalog.Reference}\r\n");
			sb.Append($"/ID [ <{_fileId:N}> <{_fileId:N}> ]\r\n");
			sb.Append($"/Info {inforef.Reference}\r\n"); // what if info is empty?
			sb.Append(">>\r\nstartxref\r\n");
			sb.Append(_xrefByteOffset);
			sb.Append("\r\n%%EOF");
			WriteData(stream,sb.ToString());
		}

		public static void WriteData(Stream stream, string data)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(data);
			stream.Write(bytes,0,bytes.Length);
		}

		/// <summary>
		///	Get a reference to an indirect object. If we don't already have
		///	a reference to the object then this method creates one.
		/// </summary>
		internal ObjectReference GetReference(IIndirectObject obj)
		{
			ObjectReference oref = null;
			bool found = _objects.TryGetValue(obj, out oref);
			if(!found)
			{
				oref = new ObjectReference(obj,_nextObjectNumber++);
				_objects.Add(obj,oref);
			}
			return oref;
		}

		public static string Space(int level)
		{
			int indent = level * 4;
			char[] chars = new char[indent];
			for(int x = 0; x < indent; ++x)
				chars[x] = ' ';
			return new string(chars);
		}
	}
}
