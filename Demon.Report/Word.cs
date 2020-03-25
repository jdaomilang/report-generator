using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Linq;
using Demon.Report.Types;
using Demon.Word;
using t = Demon.Report.Types;
using s = Demon.Report.Style;
using w = Demon.Word;
using Demon.Font;

namespace Demon.Report
{
	internal class Word : ReportRenderer
	{
		private Package _package;
		private IDocument _document;
		private Generator _generator;
		private int _numberingLevel = -1;
		private Stack<int> _numberingInstances = new Stack<int>();

		public Word()
		{
		}

		public override Stream Render(
			ReportLayout report,
			string documentId, string designId, string title,
			int version, DateTimeOffset timestamp,
			string photoUri, string resourceUri, bool drawRules, bool drawPageBoxes,
			Generator generator, ITrace tracer)
		{
			AssemblyName assName = Assembly.GetExecutingAssembly().GetName();
			string creator = $"Demon report generator {assName.Name} version {assName.Version.Major}.{assName.Version.Minor}.{assName.Version.Revision}";

			_generator = generator;

			Dictionary<string, string> docInfo = new Dictionary<string, string>();
			docInfo.Add("Title",          title);
			docInfo.Add("Creator",        creator);
			docInfo.Add("Producer",       creator);
			docInfo.Add("CreationDate",   $"D:{timestamp:yyyyMMddHHmmssZ}");
			docInfo.Add("Version",        $"{version}");
			docInfo.Add("DocumentId",     documentId);
			docInfo.Add("ReportDesignId", designId);

			_package = new Demon.Word.Package(docInfo);
			_document = _package.Document;

			//	Lay out the content. Even though we're going to let Word do
			//	the actual laying out, we may want to insert explicit page
			//	breaks, and we'll only get that information by doing our
			//	own layout.
			List<PageLayout> pageLayouts = report.LayOut();

			//	Write the document-level data
			RenderFonts();
			RenderStyles();
			RenderSettings();
			RenderProperties();
			RenderRelationships();

			//	Write the content to the Word document
			tracer.TraceLayoutActivity("Generate document");
			SectionProperties sectionProperties = null;
			PageLayout page = null;
			for(int pageIndex = 0; pageIndex < pageLayouts.Count; ++pageIndex)
			{
				page = pageLayouts[pageIndex];
				RenderPage(page, _document);

				//	If this is the last page in the chapter then create a section
				//	break by applying the chapter's section properties to the last
				//	paragraph in the section/chapter. (But in fact it's a lot easier
				//	to insert an empty paragraph just for this purpose.) Also, it's
				//	easier to identify the first page in chapter than the last, so
				//	we peep at the next page and check whether it's the first in the
				//	next chapter.
				int nextPageIndex = pageIndex + 1;
				if(nextPageIndex < pageLayouts.Count)
				{
					PageLayout next = pageLayouts[nextPageIndex];
					if(next.IsChapterFirstPage)
					{
						sectionProperties = new SectionProperties(page.TrackingInfo);
						//TODO: fill the properties in. This includes the header and
						//footer, page size and so on
						_document.AddSectionProperties(sectionProperties);
					}
				}
			}
			//	Apply the last section properties. These are applied to the
			//	body element, not to the last paragraph in the section.
			sectionProperties = new SectionProperties(page.TrackingInfo); //TODO: fill the properties in
			_document.AddLastSectionProperties(sectionProperties);
			
			Stream file = _package.Write();
			return file;
		}

		private void Render(Layout layout, IContentContainer container)
		{
			try
			{
				layout.PushTraceContext();

				switch(layout.LayoutType)
				{
					case LayoutType.Page:       RenderPage      ((PageLayout      )layout, container); break;
					case LayoutType.Group:      RenderGroup     ((GroupLayout     )layout, container); break;
					case LayoutType.Text:       RenderText      ((TextLayout      )layout, container); break;
					case LayoutType.PhotoTable: RenderPhotoTable((PhotoTableLayout)layout, container); break;
					case LayoutType.List:       RenderList      ((ListLayout      )layout, container); break;
					case LayoutType.ListItem:   RenderListItem  ((ListItemLayout  )layout, container); break;
					case LayoutType.Table:      RenderTable     ((TableLayout     )layout, container); break;
					case LayoutType.Picture:    RenderPicture   ((PictureLayout   )layout, container); break;
					case LayoutType.Space:      RenderSpace     ((SpaceLayout     )layout, container); break;
					case LayoutType.Line:       RenderLine      ((LineLayout      )layout, container); break;
				}
			}
			catch(Exception ex)
			{
				if(!ex.Data.Contains("TrackingInfo"))
					ex.Data.Add("TrackingInfo", layout.TrackingInfo.ToString());
				throw;
			}
			finally
			{
				layout.PopTraceContext();
			}
		}

		private void RenderPage(PageLayout pageLayout, IContentContainer container)
		{
//				if(generator.DrawRules || _layout.DrawRules)
//				{
//					DrawBleedMarks();
//					DrawRules(generator);
//				}

			foreach(Layout sublayout in pageLayout.SubLayouts)
				Render(sublayout, container);
		}

		private void RenderGroup(GroupLayout layout, IContentContainer container)
		{
			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, container);
		}

		private void RenderText(TextLayout layout, IContentContainer container)
		{
			List<Verse> verses = layout.GetFormattedText();
			if(verses == null) return;
			if(verses.Count == 0) return;

			//	A text layout always starts a new paragraph. If the layout's text
			//	was generated from embedded HTML then the first verse will be a
			//	paragraph verse and that will start a new Word paragraph, but
			//	otherwise we must start the Word paragraph explicitly.
			IParagraph paragraph = null;
			if(!(verses[0] is ParagraphVerse))
				paragraph = container.AddParagraph((s.TextStyle)layout.Style, layout.TrackingInfo);

			foreach(Verse verse in verses)
			{
				if(verse is ParagraphVerse)
					paragraph = container.AddParagraph((s.TextStyle)layout.Style, layout.TrackingInfo);
				else if(verse is LineBreakVerse)
					paragraph.AddLineBreak();
				else
					paragraph.AddRun(verse.Text, verse.Format.Font, verse.Format.Color);
			}
		}

		private void RenderPhotoTable(PhotoTableLayout layout, IContentContainer container)
		{
			//	Create a table
			s.TableStyle tableStyle = null;
			if(layout.Style != null)
			{
				s.PhotoStyle photoStyle = (s.PhotoStyle)layout.Style;
				tableStyle = new s.TableStyle
				{
					Border = photoStyle.Border,
					Padding = photoStyle.Padding
				};
			}

			int columnWidth = layout.Bounds.Width / layout.NumColumns;
			int[] columnWidths = new int[layout.NumColumns];
			for(int x = 0; x < columnWidths.Length; ++x)
				columnWidths[x] = columnWidth;

			ITable table = container.AddTable(layout.Bounds.Width, columnWidths, tableStyle, layout.TrackingInfo);

			//	Create a table row for each photo row
			foreach(PhotoRowLayout photoRow in layout.SubLayouts)
			{
				s.TableRowStyle rowStyle = null;
				if(photoRow.Style != null)
				{
					s.PhotoStyle photoStyle = (s.PhotoStyle)photoRow.Style;
					rowStyle = new s.TableRowStyle
					{
						BackColor = photoRow.BackgroundColor,
						Padding = photoStyle.Padding
					};
				}
				ITableRow tableRow = table.AddRow(rowStyle, photoRow.TrackingInfo);
				RenderPhotoRow(photoRow, tableRow);
			}
		}

		private void RenderPhotoRow(PhotoRowLayout layout, ITableRow row)
		{
			//	In the design we represent a photo row as a pair of table rows
			//	without a table: one row for the photos, and the other for the
			//	captions. This design is useful for PDF, and requires special
			//	handling of borders to make the vertical pairs of cells (photo
			//	cell and caption cell) appear as a single cell. But it's not
			//	necessary in Word because we can just add the photo and caption
			//	as two paragraphs in a single cell. So deconstruct the photo
			//	row into its pairs of cells and then render each pair as a
			//	single cell.
			for(int x = 0; x < layout.NumPhotos; ++x)
			{
				PhotoLayout photoCell   = (PhotoLayout)layout.PhotoRow  .GetSubLayoutAtIndex(x).GetSubLayoutAtIndex(0);
				TextLayout  captionCell = (TextLayout) layout.CaptionRow.GetSubLayoutAtIndex(x).GetSubLayoutAtIndex(0);

				s.TableCellStyle cellStyle = null;
				if(layout.Style != null)
				{
					s.PhotoStyle photoStyle = (s.PhotoStyle)layout.Style;
					cellStyle = new s.TableCellStyle
					{
						Padding = photoStyle.Padding
					};
				}
				ITableCell cell = row.AddCell(1, cellStyle, layout.TrackingInfo);
				RenderPhoto(photoCell,   cell);
				RenderText (captionCell, cell);
			}
		}

		private void RenderPhoto(PhotoLayout layout, IContentContainer container)
		{
			//	Add the photo part to the document
			string imageId = _document.AddImage(layout.PhotoData.RawData);

			//	Add a new paragraph and insert into it a reference to the photo.
			//
			//	A photo is always in a paragraph of its own, and the photo
			//	style's border and padding etc. are rendered on the paragraph.
//TODO: I'm not sure that that statement about styles is correct

			s.TextStyle paraStyle = null;
			if(layout.Style != null)
			{
				s.PhotoStyle photoStyle = (s.PhotoStyle)layout.Style;
				paraStyle = new s.TextStyle();
				paraStyle.Border  = photoStyle.Border;
				paraStyle.Padding = photoStyle.Padding;
			}
			IParagraph paragraph = container.AddParagraph(paraStyle, layout.TrackingInfo);
			paragraph.AddImage(imageId, layout.Bounds.Width, layout.Bounds.Height);
		}

		private void RenderList(ListLayout layout, IContentContainer container)
		{
			++_numberingLevel;
			s.ListStyle listStyle = (s.ListStyle)layout.Style;

			//	Translate the bullet text from design syntax to Word syntax. The
			//	design specification can include formatting of its own, unrelated
			//	to the list style. The easiest way to interpret such formatting
			//	is to create a text layout from the bullet text, which will
			//	separate the formatting from the text. If the bullet includes
			//	formatting then the new text layout will start with a paragraph
			//	verse, which we'll ignore. And it could contain any
			//	number of other verses, but a numbering definition in Word supports
			//	only a single run, so we just concatenate the text from all the
			//	verses, and apply the first verse format we find. Note that our
			//	report design language doesn't support including lower level numbers
			//	in the number, such as 3x in 1, 2, 3a, 3b, 3c, 4, 5 etc.
			s.BulletStyle bulletStyle = listStyle.BulletStyle;
			string bulletText = bulletStyle.BulletText
				.Replace("%", "%%")
				.Replace(ListItemLayout.BulletNumberProperty, $"%{_numberingLevel+1}");
			TextFormat bulletFormat = new TextFormat(bulletStyle.Font, bulletStyle.Color);
			TextBlock block = new TextBlock(bulletText, bulletFormat, _generator);
			bulletText = block.Verses.Select(v => v.Text).Aggregate((c,n) => c + n);
			bulletFormat = block.Verses.Select(v => v.Format).Where(f => f != null).FirstOrDefault();
			int numberingStyleId = _document.AddNumberingStyle(bulletStyle, _numberingLevel, bulletText);

			//	Create a numbering instance based on the numbering style, and add it
			//	to the paragraph. As far as I can make out, in a sequence of numbered
			//	paragraphs every paragraph refers to the one numbering instance.
			//	Any number of such sequences can get their numbering from the one
			//	underlying abstract definition, but each sequence would have its own
			//	instance. Instances are stored in the numbering part along with the
			//	abstract definitions.
			//
			//	We have to store abstract definitions in a dictionary in the document
			//	because we need random access to them so that different layouts in the
			//	design can share a single definition. But we can store instances in a
			//	simple stack in this class because each one is needed only for the
			//	current list.
			int numberingInstanceId = _document.AddNumberingInstance(numberingStyleId);
			_numberingInstances.Push(numberingInstanceId);



			//	Lists in the design can be nested - that's how we do indented list
			//	levels. But in Word each item must be its own paragraph, and the
			//	items together form the list by being contiguous paragraphs with
			//	a common numbering style. That is, Word doesn't have lists, but
			//	rather just has numbered paragraphs.
			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, container);

			_numberingInstances.Pop();
			--_numberingLevel;
		}

		private void RenderListItem(ListItemLayout layout, IContentContainer container)
		{
			//	A list item, which is based on a single design layout, can
			//	include any number of design paragraphs introduced by embedded
			//	HTML <p> tags. To render these as a single item in the Word
			//	list, they must all be rendered as a single Word paragraph.
			//	We achieve this by concatenating all the design paragraphs
			//	together, with double line break separators. The first line
			//	break starts a new line, and the second inserts some whitespace.
			//
			//	A nested list is introduced by a group layout, which is a single
			//	item in the current list and which contains its own list.
			//
			//	We only support text content in list items - no photos.
			//
			//	So the list item layout's content sublayout is always either
			//	a text layout or a group layout. And the list item is always
			//	a single Word paragraph.

			//	The item style is to be found in the item's list's style
			s.ListStyle listStyle = (s.ListStyle)layout.Style;
			s.TextStyle itemStyle = listStyle.ItemStyle;
			IParagraph paragraph = container.AddParagraph(itemStyle, layout.TrackingInfo);
			int numberingInstanceId = _numberingInstances.Peek(); // stack guaranteed not to be empty
			paragraph.SetNumbering(numberingInstanceId, _numberingLevel);

			switch(layout.ContentLayout.LayoutType)
			{
				case LayoutType.Text:
				{
					TextLayout contentLayout = (TextLayout)layout.ContentLayout;
					List<Verse> verses = contentLayout.GetFormattedText();
					if(verses == null) break;
					if(verses.Count == 0) break;
					foreach(Verse verse in verses)
					{
						if(verse is ParagraphVerse)
						{
							//	Two line breaks to look like a new paragraph without actually
							//	being a new Word paragraph. But don't do this if it's the first
							//	verse.
							if(verse == verses[0]) continue;
							paragraph.AddLineBreak();
							paragraph.AddLineBreak();
						}
						else if(verse is LineBreakVerse)
						{
							paragraph.AddLineBreak();
						}
						else
						{
							paragraph.AddRun(verse.Text, verse.Format.Font, verse.Format.Color);
						}
					}
					break;
				}
				
				case LayoutType.Group:
				{
					foreach(Layout sublayout in layout.ContentLayout.SubLayouts)
						Render(sublayout, container);
					break;
				}
				
				default:
				{
					break;
				}
			}
		}

		private void RenderTable(TableLayout layout, IContentContainer container)
		{
			int[] columnWidths = layout.ColumnWidths;

			ITable table = container.AddTable(
				layout.Bounds.Width,
				columnWidths,
				(s.TableStyle)layout.Style,
				layout.TrackingInfo);

			foreach(TableRowLayout row in layout.SubLayouts)
				RenderTableRow(row, table);
		}

		private void RenderTableRow(TableRowLayout layout, ITable table)
		{
			ITableRow row = table.AddRow((s.TableRowStyle)layout.Style, layout.TrackingInfo);
			foreach(TableCellLayout cell in layout.SubLayouts)
				RenderTableCell(cell, row);
		}

		private void RenderTableCell(TableCellLayout layout, ITableRow row)
		{
			ITableCell cell = row.AddCell(layout.ColumnSpan, (s.TableCellStyle)layout.Style, layout.TrackingInfo);
			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, cell);
		}

		private void RenderPicture(PictureLayout layout, IContentContainer container)
		{
		}

		private void RenderSpace(SpaceLayout layout, IContentContainer container)
		{
		}

		private void RenderLine(LineLayout layout, IContentContainer container)
		{
		}

		private void RenderFonts()
		{
			foreach(Font.Font font in _generator.FontCache.RealizedFonts)
				_document.AddFont(font);
		}

		private void RenderStyles()
		{
		}

		private void RenderSettings()
		{
		}

		private void RenderProperties()
		{
		}

		private void RenderRelationships()
		{
		}
	}
}
