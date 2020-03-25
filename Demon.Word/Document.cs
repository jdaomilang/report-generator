using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Xml.Linq;
using System.Linq;
using s = Demon.Report.Style;
using t = Demon.Report.Types;

namespace Demon.Word
{
	public interface IContentContainer
	{
		IParagraph AddParagraph(s.TextStyle style, t.TrackingInfo trackingInfo);
		ITable AddTable(int width, int[] columnWidths, s.TableStyle style, t.TrackingInfo trackingInfo);
		int AddNumberingStyle(s.BulletStyle style, int level, string bulletText);
		int AddNumberingInstance(int abstractNumberingDefinitionId);
	}

	public interface IDocument : IContentContainer
	{
		/// <summary>
		/// Returns the relationship id of the new image.
		/// </summary>
		string AddImage(byte[] data);
		int GetNumbering(s.BulletStyle style, int level);
		void AddPageBreak(s.TextStyle style, t.TrackingInfo trackingInfo);
		void AddSectionProperties(SectionProperties properties);
		void AddLastSectionProperties(SectionProperties properties);
		void AddFont(Demon.Font.Font font);
	}

	internal class Document : Part, IDocument
	{
		public override string ContentType =>
			"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

		private Relationships _relationships = new Relationships("word/_rels/document.xml.rels");
		private List<IBlockContent> _content = new List<IBlockContent>();
		private List<Image> _images = new List<Image>();

		private Settings _settings;
		private Styles _styles;
		private Numberings _numberings;
		private FontTable _fontTable;

		public Document(string name)
			:base(name)
		{
			_settings   = new Settings  ("word/settings.xml" );
			_styles     = new Styles    ("word/styles.xml"   );
			_numberings = new Numberings("word/numbering.xml");
			_fontTable  = new FontTable ("word/fontTable.xml");

			//	The physical storage of the part files is /word/whatever.xml
			//	but the relationships to them are relative to the document and
			//	so are just whatever.xml
			_relationships.AddSettings  (_settings,   "settings.xml" );
			_relationships.AddStyles    (_styles,     "styles.xml"   );
			_relationships.AddNumberings(_numberings, "numbering.xml");
			_relationships.AddFontTable (_fontTable,  "fontTabl.xml" );
		}

		/// <summary>
		/// Start a new paragraph.
		/// </summary>
		public IParagraph AddParagraph(s.TextStyle style, t.TrackingInfo trackingInfo)
		{
			Paragraph paragraph = new Paragraph(style, trackingInfo);
			_content.Add(paragraph);
			return paragraph;
		}

		/// <summary>
		/// Returns the relationship id of the new image.
		/// </summary>
		public string AddImage(byte[] data)
		{
			//	Create a new part for the image data. The physical storage of
			//	the part file is in /word/images, but the relationship to it
			//	is relative to the document and so is just images/whatever.jpg
			//TODO: PNG
			string name = $"images/image{_images.Count}.jpg";
			Image image = new Image("word/" + name, data);
			_images.Add(image);

			//	Create a relationship to the part
			Relationship rel = _relationships.AddImage(image, name);
			return rel.Id;
		}

		public ITable AddTable(int width, int[] columnWidths, s.TableStyle style, t.TrackingInfo trackingInfo)
		{
			Table table = new Table(width, columnWidths, style, trackingInfo);
			_content.Add(table);
			return table;
		}

		public void AddFont(Demon.Font.Font font)
		{
			_fontTable.AddFont(font);
		}

		public int AddNumberingStyle(s.BulletStyle style, int level, string bulletText)
		{
			//	I can't find any clear statement in
			//	ECMA-376 that 9 is the maximum level, but section 17.9.12
			//	"multiLevelType" hints at it with this: "a list with multiple
			//	levels marked as singleLevel shall not be prevented from using
			//	levels 2 through 9", and a quick test in Word shows that you
			//	can't go deeper than 9.


			//	We create a Word abstract numbering definition for every numbering
			//	style found in the laid-out report. If the design specifies two
			//	styles with the same properties then we create them as two abstract
			//	numbering definitions, so that the user can restyle one of them
			//	without affecting the other. If the designer wanted them to be
			//	a single style then he wouldn't have defined them separately in
			//	the design file.
			return _numberings.AddAbstractDefinition(style, level, bulletText);
		}

		public int AddNumberingInstance(int abstractNumberingDefinitionId)
		{
			int instanceId = _numberings.AddInstance(abstractNumberingDefinitionId);
			return instanceId;
		}

		public void AddPageBreak(s.TextStyle style, t.TrackingInfo trackingInfo)
		{
			IParagraph paragraph = AddParagraph(style, trackingInfo);
			paragraph.AddBreak(null, "page");
		}

		public void AddSectionProperties(SectionProperties properties)
		{
			//	The section properties element for any section except the last
			//	one in the document must be a child of the last paragraph in
			//	that section. We add an empty paragraph for this.
			SectionBreakParagraph brk = new SectionBreakParagraph(properties);
			_content.Add(brk);
		}

		public void AddLastSectionProperties(SectionProperties properties)
		{
			//	The section properties element for the last section in the document
			//	must be a child of the body element
			_content.Add(properties);
		}

		public int GetNumbering(s.BulletStyle style, int level)
		{
			return _numberings.GetNumbering(style, level);
		}

		public override void Write(ZipArchive archive)
		{
			base.Write(archive);
			_relationships.Write(archive);
			_fontTable.Write(archive);
			_numberings.Write(archive);

			//	Write image data (photos and resources) to their own entries
			//	in the zip
			foreach(Image image in _images)
				image.Write(archive);
		}

		protected override void WriteContent()
		{
			XNamespace w   = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");
			XNamespace r   = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
			XNamespace wp  = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
			XNamespace a   = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/main");
			XNamespace pic = XNamespace.Get("http://schemas.openxmlformats.org/drawingml/2006/picture");

			XElement root = new XElement(
				w + "document",
				new XAttribute(XNamespace.Xmlns + "w",   w),
				new XAttribute(XNamespace.Xmlns + "r",   r),
				new XAttribute(XNamespace.Xmlns + "wp",  wp),
				new XAttribute(XNamespace.Xmlns + "a",   a),
				new XAttribute(XNamespace.Xmlns + "pic", pic));
			_document.Add(root);

			XElement body = new XElement(w + "body");
			root.Add(body);

			foreach(IBlockContent content in _content)
				content.Write(body);
		}
	}
}
