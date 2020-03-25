#define PAGE_SELECTOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Linq;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class HTML : ReportRenderer
	{
		private Generator _generator;
		private string _photoUri;
		private string _resourceUri;

		private List<TextStyle>      _textStyles;
		private List<TableStyle>     _tableStyles;
		private List<TableRowStyle>  _tableRowStyles;
		private List<TableCellStyle> _tableCellStyles;
		private List<PhotoStyle>     _photoStyles;
		private List<ListStyle>      _listStyles;

		public HTML()
		{
		}

		public override Stream Render(
			ReportLayout report,
			string documentId, string designId, string title,
			int version, DateTimeOffset timestamp,
			string photoUri, string resourceUri, bool drawRules, bool drawPageBoxes,
			Generator generator, ITrace tracer)
		{
			_generator = generator;
			_photoUri = photoUri;
			_resourceUri = resourceUri;

			//	Grab our own lists of styles. We want them to be the same
			//	instances as in the design, so that they'll be the same
			//	ones that the layouts refer to, but we want them in a
			//	collection that will let us assign unique and predictable
			//	names to them when we render them in CSS.
			_textStyles      = new List<TextStyle>     (_generator.ReportDesign.TextStyles .Values);
			_tableStyles     = new List<TableStyle>    (_generator.ReportDesign.TableStyles.Values);
			_tableRowStyles  = new List<TableRowStyle> (_generator.ReportDesign.RowStyles  .Values);
			_tableCellStyles = new List<TableCellStyle>(_generator.ReportDesign.CellStyles .Values);
			_photoStyles     = new List<PhotoStyle>    (_generator.ReportDesign.PhotoStyles.Values);
			_listStyles      = new List<ListStyle>     (_generator.ReportDesign.ListStyles .Values);
			

			tracer.TraceLayoutActivity("Generate document");
			XDocument document = new XDocument();
			document.Declaration = null;
			document.Add(new XElement("html"));
			Render(report, document.Root);

#if PAGE_SELECTOR
			//	For dev only: render buttons to select the pages. In production this
			//	would be handled by something that Ramon and Jab do in the app.
			RenderScript(document.Root.Element("head"));
			int numPages = document.Root.Element("body").Elements("div").Elements("div").Count();
			if(numPages > 0)
			{
				int maxWidth = report.SubLayouts.Max(l => ((PageLayout)l).MediaBox.Width);
				RenderPageSelectors(document.Root.Element("body"), numPages, maxWidth);
			}
#endif

			Stream stream = new MemoryStream(); //TODO: or temp file etc?
			System.Xml.XmlWriterSettings settings = new System.Xml.XmlWriterSettings();
			settings.OmitXmlDeclaration = true;
			settings.Indent = true;
			System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(stream, settings);
			document.Save(writer);
			writer.Flush();
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}

		private void Render(Layout layout, XElement container)
		{
			try
			{
				layout.PushTraceContext();

				switch(layout.LayoutType)
				{
					case LayoutType.Report:     RenderReportLayout    ((ReportLayout    )layout, container); break;
					case LayoutType.Page:       RenderPageLayout      ((PageLayout      )layout, container); break;
					case LayoutType.Group:      RenderGroupLayout     ((GroupLayout     )layout, container); break;
					case LayoutType.Text:       RenderTextLayout      ((TextLayout      )layout, container); break;
					case LayoutType.PhotoTable: RenderPhotoTableLayout((PhotoTableLayout)layout, container); break;
					case LayoutType.PhotoRow:   RenderPhotoRowLayout  ((PhotoRowLayout  )layout, container); break;
					case LayoutType.Photo:      RenderPhotoLayout     ((PhotoLayout     )layout, container); break;
					case LayoutType.List:       RenderListLayout      ((ListLayout      )layout, container); break;
					case LayoutType.ListItem:   RenderListItemLayout  ((ListItemLayout  )layout, container); break;
					case LayoutType.Table:      RenderTableLayout     ((TableLayout     )layout, container); break;
					case LayoutType.TableRow:   RenderTableRowLayout  ((TableRowLayout  )layout, container); break;
					case LayoutType.TableCell:  RenderTableCellLayout ((TableCellLayout )layout, container); break;
					case LayoutType.Picture:    RenderPictureLayout   ((PictureLayout   )layout, container); break;
					case LayoutType.Space:      RenderSpaceLayout     ((SpaceLayout     )layout, container); break;
					case LayoutType.Line:       RenderLineLayout      ((LineLayout      )layout, container); break;
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

		private void RenderReportLayout(ReportLayout report, XElement container)
		{
			XElement head = new XElement("head");
			container.Add(head);

			XElement style = new XElement("style");
			style.SetAttributeValue("type", "text/css");
			RenderLibraryStyles(style);
			head.Add(style);

			XElement body = new XElement("body");
			container.Add(body);

			XElement pages = new XElement("div");
			pages.SetAttributeValue("id", "report");
			body.Add(pages);

			foreach(PageLayout page in report.SubLayouts)
				Render(page, pages);

#if !PAGE_SELECTOR
			//	The pages have all been rendered on top of each other, because
			//	their page divs have no height. Set those heights now to separate
			//	the pages. (Couldn't do this during rendering because there's no
			//	good way to pass the position information to the RenderXxx methods.)
			for(int x = 0; x < pageLayouts.Count; ++x)
			{
				PageLayout layout = pageLayouts[x];
				XElement div = pages.Elements().ElementAt(x);
				div.SetAttributeValue("style", $"height:{layout.MediaBox.Height + 20}");
			}
#endif
		}

		private void RenderPageLayout(PageLayout pageLayout, XElement container)
		{
			XElement page = new XElement("div");
			page.SetAttributeValue("id", $"page-{pageLayout.PageNumber}");
			RenderLayoutMetadata(page, pageLayout);
			container.Add(page);

			//	Create the media box etc. before the content because the content has
			//	to go in them, but don't add the boxes to the container until 
			XElement mediaBox = new XElement("div");
			mediaBox.SetAttributeValue("name", "media-box");
			mediaBox.SetAttributeValue("style", RectangleToCss(pageLayout.MediaBox, pageLayout.MediaBox));
			page.Add(mediaBox);

			XElement bodyBox = new XElement("div");
			bodyBox.SetAttributeValue("name", "body-box");
			bodyBox.SetAttributeValue("style", RectangleToCss(pageLayout.BodyBox, pageLayout.MediaBox));
			page.Add(bodyBox);

			XElement headerBox = new XElement("div");
			headerBox.SetAttributeValue("name", "header-box");
			headerBox.SetAttributeValue("style", RectangleToCss(pageLayout.HeaderBox, pageLayout.MediaBox));
			page.Add(headerBox);

			XElement footerBox = new XElement("div");
			footerBox.SetAttributeValue("name", "footer-box");
			footerBox.SetAttributeValue("style", RectangleToCss(pageLayout.FooterBox, pageLayout.MediaBox));
			page.Add(footerBox);

			//	Background before content
			foreach(FixedPicture picture in pageLayout.Background)
				RenderFixedPicture(picture, page, pageLayout.MediaBox);

			//	Content
			foreach(Layout sublayout in pageLayout.SubLayouts)
				Render(sublayout, bodyBox);

			//	Overlay after content
			foreach(FixedPicture picture in pageLayout.Overlays)
				RenderFixedPicture(picture, page, pageLayout.MediaBox);

			//	Prevent the XML writer from self-closing the rules divs, which
			//	would be invalid HTML
			if(!mediaBox .HasElements)	mediaBox .Value = "";
			if(!bodyBox  .HasElements)	bodyBox  .Value = "";
			if(!headerBox.HasElements)	headerBox.Value = "";
			if(!footerBox.HasElements)	footerBox.Value = "";

			if(pageLayout.DrawRules)
			{
				page.SetAttributeValue("class", "page");
				mediaBox .SetAttributeValue("class", "page-metric");
				bodyBox  .SetAttributeValue("class", "page-metric");
				headerBox.SetAttributeValue("class", "page-metric");
				footerBox.SetAttributeValue("class", "page-metric");
			}
		}

		private void RenderGroupLayout(GroupLayout layout, XElement container)
		{
			foreach(Layout sublayout in layout.SubLayouts)
			{
				XElement group = new XElement("div");
				RenderLayoutMetadata(group, layout);
				container.Add(group);
				Render(sublayout, group);
			}
		}

		private void RenderTextLayout(TextLayout layout, XElement container)
		{
			if(layout == null) return;

			List<Verse> verses = layout.GetFormattedText();
			if(verses == null) return;
			if(verses.Count == 0) return;

			//	A text layout always starts a new paragraph. If the layout's text
			//	was generated from embedded HTML then the first verse will be a
			//	paragraph verse and that will start a new HTML paragraph, but
			//	otherwise we must start the HTML paragraph explicitly.
			XElement paragraph = null;
			if(!(verses[0] is ParagraphVerse))
			{
				paragraph = new XElement("p");
				RenderLayoutMetadata(paragraph, layout);
				RenderStyle(paragraph, layout.Style);
				container.Add(paragraph);
			}
			foreach(Verse verse in verses)
			{
				if(verse is ParagraphVerse)
				{
					paragraph = new XElement("p");
					RenderLayoutMetadata(paragraph, layout);
					RenderStyle(paragraph, layout.Style);
					container.Add(paragraph);
				}
				else if(verse is LineBreakVerse)
				{
					paragraph = new XElement("p");
					RenderLayoutMetadata(paragraph, layout);
					RenderStyle(paragraph, layout.Style);
					container.Add(paragraph);
//					paragraph.AddLineBreak();
				}
				else
				{
					TextStyle layoutStyle = (TextStyle)layout.Style;
					Demon.Report.Style.Font verseFont = verse.Format.Font;
					Color verseColor = verse.Format.Color;

					XElement span = new XElement("span");
					
					string font = FormatFontOverride(((TextStyle)layout.Style).Font, verse.Format.Font);
					string color = "";//FormatColorOverride(((TextStyle)layout.Style).Color, verse.Format.Color);
					string style = "";
					if(font.Length > 0)
						style += " " + font;
					if(color.Length > 0)
						style += " color: " + color + ";";
					if(style.Length > 0)
						span.SetAttributeValue("style", style);
					
					span.Value = verse.Text;
					paragraph.Add(span);
				}
			}
		}

		private void RenderPhotoTableLayout(PhotoTableLayout layout, XElement container)
		{
			XElement table = new XElement("table");
			container.Add(table);

			RenderLayoutMetadata(table, layout);
			table.SetAttributeValue("class", "photo-structure-table");

			XElement colgroup = new XElement("colgroup");
			table.Add(colgroup);
			for(int colIndex = 0; colIndex < layout.NumColumns; ++colIndex)
			{
				XElement col = new XElement("col");
				colgroup.Add(col);
				col.SetAttributeValue("style", $"column-width: {layout.ColumnWdith}px");
			}
			foreach(PhotoRowLayout row in layout.SubLayouts)
				Render(row, table);
		}

		private void RenderPhotoRowLayout(PhotoRowLayout layout, XElement container)
		{
			//	In the design we represent a photo row as a pair of table rows
			//	without a table: one row for the photos, and the other for the
			//	captions. This design is useful for PDF, and requires special
			//	handling of borders to make the vertical pairs of cells (photo
			//	cell and caption cell) appear as a single cell. But it's not
			//	necessary in HTML because we can render the photo and caption
			//	rows in a subtable.

			XElement mainRow = new XElement("tr");
			container.Add(mainRow);
			XElement mainCell = new XElement("td");
			mainRow.Add(mainCell);
			XElement innerTable = new XElement("table");
			mainCell.Add(innerTable);

			RenderLayoutMetadata(mainRow, layout);
			RenderStyle(mainRow, layout.Style);
			innerTable.SetAttributeValue("class", "photo-structure-table");

			PhotoTableLayout photoTable = (PhotoTableLayout)layout.Container;
			XElement colgroup = new XElement("colgroup");
			innerTable.Add(colgroup);
			for(int colIndex = 0; colIndex < photoTable.NumColumns; ++colIndex)
			{
				XElement col = new XElement("col");
				colgroup.Add(col);
				col.SetAttributeValue("style", $"column-width: {photoTable.ColumnWdith}px");
			}

			XElement photoRow = new XElement("tr");
			innerTable.Add(photoRow);
			photoRow.SetAttributeValue("class", "photo-structure-table");

			XElement captionRow = new XElement("tr");
			innerTable.Add(captionRow);

			for(int x = 0; x < layout.NumPhotos; ++x)
			{
				//	There's always a photo...
				PhotoLayout photoCell = (PhotoLayout)layout.PhotoRow.GetSubLayoutAtIndex(x).GetSubLayoutAtIndex(0);
				RenderPhotoLayout(photoCell, photoRow);

				//	but not always a caption
				TableCellLayout captionCell = (TableCellLayout)layout.CaptionRow.GetSubLayoutAtIndex(x);
				if(captionCell.NumSubLayouts > 0)
				{
					TextLayout caption = (TextLayout)captionCell.GetSubLayoutAtIndex(0);
					RenderTextLayout(caption, captionRow);
				}
			}
		}

		private void RenderPhotoLayout(PhotoLayout layout, XElement container)
		{
			XElement td = new XElement("td");
			container.Add(td);
			RenderLayoutMetadata(td, layout);
//			RenderStyle(td, layout.Style);
			
			//	Add the photo-structure-table class to remove borders
			td.SetAttributeValue("class", "photo-structure-table");

			XElement img = new XElement("img");
//			img.SetAttributeValue("name", layout.Id);
			container.Add(img);

#if EMBED_IMAGES
			byte[] raw = null;
			Task.Run(async()=>
			{
				if(resourceId != null)
					raw = await _generator.ReportDesign.GetResource(resourceId, _generator.ResourceService);
				else if(filename != null)
					raw = await _generator.ResourceService.GetResourceAsync(filename);
			}).Wait();

			string base64 = Convert.ToBase64String(raw);
			img.SetAttributeValue("src", $"data:{mimeType};base64,{base64}");
#else
			string src = $"{_photoUri}/{layout.PhotoId}";
			img.SetAttributeValue("src", src);
			
			img.SetAttributeValue("height", layout.PhotoRenderedSize.Height);
			img.SetAttributeValue("width",  layout.PhotoRenderedSize.Width);
#endif
		}

		private void RenderListLayout(ListLayout layout, XElement container)
		{
#if false
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
#endif
		}

		private void RenderListItemLayout(ListItemLayout layout, XElement container)
		{
#if false
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
#endif
		}

		private void RenderTableLayout(TableLayout layout, XElement container)
		{
			XElement table = new XElement("table");
			container.Add(table);
			RenderLayoutMetadata(table, layout);
			RenderStyle(table, layout.Style);
//			AddStyleFragment(table, "width: 100%;");

			XElement colgroup = new XElement("colgroup");
			table.Add(colgroup);
			foreach(int width in layout.ColumnWidths)
			{
				XElement col = new XElement("col");
				colgroup.Add(col);
				col.SetAttributeValue("style", $"column-width: {width}px");
			}

			//	We don't do header rows in HTML because the generator has already
			//	taken care of them and explicitly repeated rows as necessary
			foreach(TableRowLayout row in layout.SubLayouts)
				Render(row, table);
		}

		private void RenderTableRowLayout(TableRowLayout layout, XElement container)
		{
			XElement tr = new XElement("tr");
			container.Add(tr);
			RenderLayoutMetadata(tr, layout);
			RenderStyle(tr, layout.Style);

			foreach(TableCellLayout cell in layout.Cells)
				RenderTableCellLayout(cell, tr);
		}

		private void RenderTableCellLayout(TableCellLayout layout, XElement container)
		{
			XElement td = new XElement("td"); // we don't do <th>
			container.Add(td);
			RenderLayoutMetadata(td, layout);
			RenderStyle(td, layout.Style);
			
			if(layout.ColumnSpan > 1)
				td.SetAttributeValue("colspan", layout.ColumnSpan);

			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, td);
		}

		private void RenderPictureLayout(PictureLayout layout, XElement container)
		{
			string mimeType = "image/jpeg"; //TODO: or image/png
			RenderImage(layout.ResourceId, layout.Filename, mimeType, layout.Bounds, layout.Page.MediaBox, container);
		}

		private void RenderFixedPicture(FixedPicture picture, XElement container, Rectangle mediaBox)
		{
			string mimeType = "image/jpeg"; //TODO: or image/png
			RenderImage(picture.ResourceId, picture.Filename, mimeType, picture.Bounds, mediaBox, container);
		}

		private void RenderImage(
			string resourceId, string filename,
			string mimeType,
			Rectangle bounds, Rectangle mediaBox,
			XElement container)
		{
			XElement img = new XElement("img");
//			img.SetAttributeValue("name", layout.Id);
			container.Add(img);

#if EMBED_IMAGES
			byte[] raw = null;
			Task.Run(async()=>
			{
				if(resourceId != null)
					raw = await _generator.ReportDesign.GetResource(resourceId, _generator.ResourceService);
				else if(filename != null)
					raw = await _generator.ResourceService.GetResourceAsync(filename);
			}).Wait();

			string base64 = Convert.ToBase64String(raw);
			img.SetAttributeValue("src", $"data:{mimeType};base64,{base64}");
#else
			if(filename == null)
				filename = _generator.ReportDesign.GetResourceFilename(resourceId);
			string src = $"{_resourceUri}/{filename}";
			img.SetAttributeValue("src", src);
#endif

			string style = "position:absolute; " + RectangleToCss(bounds, mediaBox);
			img.SetAttributeValue("style", style);
		}

		private void RenderSpaceLayout(SpaceLayout layout, XElement container)
		{
		}

		private void RenderLineLayout(LineLayout layout, XElement container)
		{
		}

		private void RenderFonts()
		{
#if false
			foreach(Font.Font font in _generator.FontCache.RealizedFonts)
				_document.AddFont(font);
#endif
		}

		private void RenderLibraryStyles(XElement container)
		{
			StringBuilder sb = new StringBuilder();

			RenderPageStyles(sb);
			RenderLibraryTextStyles(sb);
			RenderLibraryTableStyles(sb);
			RenderLibraryPhotoStyles(sb);

			//TODO: render only the styles that are actually used

			sb.Replace("; ", ";\r\n");
			container.Add(sb.ToString());
		}

		private void RenderPageStyles(StringBuilder sb)
		{
			sb.Append(".page\r\n");
			sb.Append("{\r\n");
			sb.Append("position: ");
#if PAGE_SELECTOR
			sb.Append("absolute");
#else
			sb.Append("relative");
#endif
			sb.Append(";\r\n");
			sb.Append("}\r\n");

			sb.Append(".page-metric\r\n");
			sb.Append("{\r\n");
			sb.Append("position: absolute;\r\n");
			sb.Append("outline-style: dashed;\r\n");
			sb.Append("outline-width: 1px;\r\n");
			sb.Append("outline-color: lightgray;\r\n");
			sb.Append("}\r\n");
		}

		private void RenderLibraryTextStyles(StringBuilder sb)
		{
			//	Replace every style's human-friendly name with an id
			//	that we can use

			for(int x = 0; x < _textStyles.Count; ++x)
			{
				TextStyle style = _textStyles[x];
				style.Name = $"text-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryTextStyle(style, sb);
				sb.Append("}\r\n");
			}
		}

		private void RenderLibraryTableStyles(StringBuilder sb)
		{
			for(int x = 0; x < _tableStyles.Count; ++x)
			{
				TableStyle style = _tableStyles[x];
				style.Name = $"table-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				sb.Append("width: 100%;\r\n");
				sb.Append("border-collapse: collapse;\r\n");
				RenderLibraryTableStyle(style, sb);
				sb.Append("}\r\n");

				//	If the style includes inner borders then render a style for that
				if((style.Border != null) && ((style.Border.Parts & BorderPart.Inner) != BorderPart.None))
				{
					TableStyle inner = new TableStyle();
					inner.Border = new Border();
					if((style.Border.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
					{
						inner.Border.Parts |= BorderPart.Top;
						inner.Border.Parts |= BorderPart.Bottom;
					}
					if((style.Border.Parts & BorderPart.InnerVertical) != BorderPart.None)
					{
						inner.Border.Parts |= BorderPart.Left;
						inner.Border.Parts |= BorderPart.Right;
					}
					inner.Border.Thickness = style.Border.Thickness;
					inner.Border.Color = style.Border.Color;
					inner.Name = $"table-style-{x} td";
					sb.Append(".");
					sb.Append(inner.Name);
					sb.Append("\r\n");
					sb.Append("{\r\n");
					RenderLibraryTableStyle(inner, sb);
					sb.Append("}\r\n");
				}
			}

			for(int x = 0; x < _tableRowStyles.Count; ++x)
			{
				TableRowStyle style = _tableRowStyles[x];
				style.Name = $"table-row-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryTableRowStyle(style, sb);
				sb.Append("}\r\n");
			}

			for(int x = 0; x < _tableCellStyles.Count; ++x)
			{
				TableCellStyle style = _tableCellStyles[x];
				style.Name = $"table-cell-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryTableCellStyle(style, sb);
				sb.Append("}\r\n");
			}
		}

		private void RenderLibraryPhotoStyles(StringBuilder sb)
		{
			for(int x = 0; x < _photoStyles.Count; ++x)
			{
				//	A photo style is a table style, with a corresponding text style
				//	for the captions. The table and its rows have no padding, but the
				//	cells do.
				PhotoStyle style = _photoStyles[x];
				style.Name = $"photo-style-{x}";

				//	Style border, no padding
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderBorder(style.Border, sb);
				sb.Append("padding: 0;\r\n");
				sb.Append("}\r\n");

				//	A plain style for rows in the photo table: no border, no padding
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append(" td\r\n");
				sb.Append("{\r\n");
				sb.Append("border-width: 0;\r\n");
				sb.Append("padding: 0;\r\n");
				sb.Append("}\r\n");


				//	A plain style for cells in the photo table: style inner borders, style padding
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append(" td\r\n");
				sb.Append("{\r\n");
				RenderInnerBorder(style.Border, sb);
				RenderPadding(style.Padding, sb);
				sb.Append("}\r\n");

				//	The caption style needs its own CSS identity so that
				//	it can be applied to the caption div and have different
				//	border and padding to the photo div.
				style.CaptionStyle.Name = style.Name + "-caption-style";
				sb.Append(".");
				sb.Append(style.CaptionStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryTextStyle(style.CaptionStyle, sb);
				sb.Append("}\r\n");
			}

			//	Photo tables use internal tables to lay them out as we want them,
			//	and we want those internal tables to be invisible - no border, no
			//	padding and so on.
			sb.Append(".photo-structure-table table, .photo-structure-table tr, .photo-structure-table td\r\n");
			sb.Append("{\r\n");
			sb.Append("width: 100%;\r\n");
			sb.Append("border-style: none;\r\n");
			sb.Append("border-collapse: collapse;\r\n");
			sb.Append("padding: 0 0 0 0;\r\n");
			sb.Append("}\r\n");
		}

		private void RenderLibraryListtyles(StringBuilder sb)
		{
			for(int x = 0; x < _listStyles.Count; ++x)
			{
				ListStyle style = _listStyles[x];
				style.Name = $"list-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryListStyle(style, sb);
				sb.Append("}\r\n");

				//	The item and bullet styles need their own CSS identities so that
				//	they can be applied to the item and bullet divs and have different
				//	border and padding to the list div.

				style.ItemStyle.Name = style.Name + "-item-style";
				sb.Append(".");
				sb.Append(style.ItemStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				RenderLibraryTextStyle(style.ItemStyle, sb);
				sb.Append("}\r\n");

				style.BulletStyle.Name = style.Name + "-bullet-style";
				sb.Append(".");
				sb.Append(style.BulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				RenderLibraryBulletStyle(style.BulletStyle, sb);
				sb.Append("}\r\n");

				style.SelectedBulletStyle.Name = style.Name + "-selected-bullet-style";
				sb.Append(".");
				sb.Append(style.SelectedBulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				RenderLibraryBulletStyle(style.SelectedBulletStyle, sb);
				sb.Append("}\r\n");

				style.UnselectedBulletStyle.Name = style.Name + "-unselected-bullet-style";
				sb.Append(".");
				sb.Append(style.UnselectedBulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				RenderLibraryBulletStyle(style.UnselectedBulletStyle, sb);
				sb.Append("}\r\n");
			}
		}

		private void RenderLibraryTextStyle(TextStyle style, StringBuilder sb)
		{
			if(style == null) return;

			RenderFont(style.Font, sb);
			RenderColor("color",            style.Color,     sb);
			RenderColor("background-color", style.BackColor, sb);
			RenderTextAlignment(style.Alignment, sb);
			RenderLineSpacing(style.LineSpacing, sb);
//			RenderParagraphSpacing(style.ParagraphSpacing, sb);
			RenderPadding(style.Padding, sb);
			RenderBorder(style.Border, sb);
		}

		private void RenderLibraryListStyle(ListStyle style, StringBuilder sb)
		{
		}

		private void RenderLibraryTableStyle(TableStyle style, StringBuilder sb)
		{
			if(style == null) return;

			RenderBorder(style.Border, sb);
			RenderPadding(style.Padding, sb);
		}

		private void RenderLibraryTableRowStyle(TableRowStyle style, StringBuilder sb)
		{
			if(style == null) return;

			RenderPadding(style.Padding, sb);
			RenderColor("background-color", style.BackColor, sb);
		}

		private void RenderLibraryTableCellStyle(TableCellStyle style, StringBuilder sb)
		{
			if(style == null) return;

			RenderPadding(style.Padding, sb);
		}

		private void RenderLibraryLineStyle(LineStyle style, StringBuilder sb)
		{
		}

		/// <summary>
		/// Render a font. If the font is an override then only renders the overridden
		/// parts, otherwise renders the whole font.
		/// </summary>
		private void RenderFont(Demon.Report.Style.Font font, StringBuilder sb)
		{
			if(font.Base == null)
			{
				sb.Append("font-family: ");
				sb.Append(font.FamilyName);
				sb.Append("; ");
				sb.Append("font-size: ");
				sb.Append(font.Size);
				sb.Append("px; ");
				if(font.Bold)
					sb.Append("font-weight: bold; ");
				if(font.Italic)
					sb.Append("font-style: italic; ");
				if(font.Underline)
					sb.Append("text-decoration: underline; ");
				if(font.Strikeout)
					sb.Append("text-decoration: line-through; ");
			}
			else
			{
				RenderFontOverride((Style.Font)font.Base, font, sb);
			}
		}

		/// <param name="styleName">color, background-color, border-color etc.</param>
		private void RenderColor(string styleName, Color color, StringBuilder sb)
		{
			if(color == null) return;

			int red   = (int)(255 * color.Red);
			int green = (int)(255 * color.Green);
			int blue  = (int)(255 * color.Blue);
			
			sb.Append(styleName);
			sb.Append(": #");
			sb.Append(red  .ToString("x2"));
			sb.Append(green.ToString("x2"));
			sb.Append(blue .ToString("x2"));
			sb.Append("; ");
		}

		private void RenderTextAlignment(TextAlignment alignment, StringBuilder sb)
		{
			sb.Append("text-align: ");
			switch(alignment)
			{
				case TextAlignment.Left:    sb.Append("left"   ); break;
				case TextAlignment.Center:  sb.Append("center" ); break;
				case TextAlignment.Right:   sb.Append("right"  ); break;
				case TextAlignment.Justify: sb.Append("justify"); break;
			}
			sb.Append("; ");
		}

		private void RenderLineSpacing(double spacing, StringBuilder sb)
		{
			sb.Append("line-height: ");
			sb.Append(spacing);
			sb.Append("; ");
		}

		private void RenderPadding(Padding padding, StringBuilder sb)
		{
			if(padding == null) return;

			if(padding.Base == null)
			{
				//	Note that we map our design padding to HTML padding, not to HTML
				//	margin, because HTML padding auto-collapses whereas HTML margins
				//	don't, and our design padding doesn't collapse either.

				sb.Append("padding-top: ");
				sb.Append(padding.Top);
				sb.Append("px; ");

				sb.Append("padding-right: ");
				sb.Append(padding.Right);
				sb.Append("px; ");

				sb.Append("padding-bottom: ");
				sb.Append(padding.Bottom);
				sb.Append("px; ");

				sb.Append("padding-left: ");
				sb.Append(padding.Left);
				sb.Append("px; ");
			}
			else
			{
//				RenderPaddingOverride(padding.Base, padding, sb);
			}
		}

		private void RenderBorder(Border border, StringBuilder sb)
		{
			if(border == null) return;

			//	We always want adjacent borders to collapse together
//			sb.Append("border-collapse: collapse; ");

			//	And we only do solid borders
			sb.Append("border-style-top: ");
			if((border.Parts & BorderPart.Top) != BorderPart.None)
				sb.Append("solid");
			else
				sb.Append("none");
			sb.Append("; ");

			sb.Append("border-bottom-style: ");
			if((border.Parts & BorderPart.Bottom) != BorderPart.None)
				sb.Append("solid");
			else
				sb.Append("none");
			sb.Append("; ");

			sb.Append("border-left-style: ");
			if((border.Parts & BorderPart.Left) != BorderPart.None)
				sb.Append("solid");
			else
				sb.Append("none");
			sb.Append("; ");

			sb.Append("border-right-style: ");
			if((border.Parts & BorderPart.Right) != BorderPart.None)
				sb.Append("solid");
			else
				sb.Append("none");
			sb.Append("; ");

			sb.Append("border-width: ");
			sb.Append(border.Thickness);
			sb.Append("px; ");

			RenderColor("border-color", border.Color, sb);
		}

		private void RenderInnerBorder(Border border, StringBuilder sb)
		{
			//	If the style includes inner borders then render a style for that
			if((border != null) && ((border.Parts & BorderPart.Inner) != BorderPart.None))
			{
				Border inner = new Border();
				if((border.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
				{
					inner.Parts |= BorderPart.Top;
					inner.Parts |= BorderPart.Bottom;
				}
				if((border.Parts & BorderPart.InnerVertical) != BorderPart.None)
				{
					inner.Parts |= BorderPart.Left;
					inner.Parts |= BorderPart.Right;
				}
				inner.Thickness = border.Thickness;
				inner.Color = border.Color;

				RenderBorder(inner, sb);
			}
		}

		/// <summary>
		/// This method is only suitable for rendering stand-alone overriding
		/// text styles. It is not suitable for overrides of embedded text styles
		/// such as ListStyle.ItemStyle or PhotoStyle.CaptionStyle.
		/// </summary>
		private void RenderTextStyleOverride(TextStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TextStyle baseStyle = (TextStyle)style.Base;

			RenderFontOverride(baseStyle.Font, style.Font, sb);
			RenderColorOverride("color", baseStyle.Color, style.Color, sb);
			RenderColorOverride("background-color", baseStyle.BackColor, style.BackColor, sb);
			RenderTextAlignmentOverride(baseStyle.Alignment, style.Alignment, sb);
			RenderLineSpacingOverride(baseStyle.LineNumber, style.LineSpacing, sb);
//			RenderParagraphSpacingOverride(baseStyle.ParagraphSpacing, style.ParagraphSpacing, sb);
			RenderPaddingOverride(baseStyle.Padding, style.Padding, sb);
			RenderBorderOverride(baseStyle.Border, style.Border, sb);
		}

		private void RenderListStyleOverride(ListStyle style, StringBuilder sb)
		{
		}

		private void RenderTableStyleOverride(TableStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableStyle baseStyle = (TableStyle)style.Base;

			RenderPaddingOverride(baseStyle.Padding, style.Padding, sb);
			RenderBorderOverride(baseStyle.Border, style.Border, sb);
		}

		private void RenderTableRowStyleOverride(TableRowStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableRowStyle baseStyle = (TableRowStyle)style.Base;

			RenderPaddingOverride(baseStyle.Padding, style.Padding, sb);
			RenderColorOverride("background-color", baseStyle.BackColor, style.BackColor, sb);
		}

		private void RenderTableCellStyleOverride(TableCellStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableCellStyle baseStyle = (TableCellStyle)style.Base;

			RenderPaddingOverride(baseStyle.Padding, style.Padding, sb);

			//	Render inner table borders
//			TableLayout table = (TableLayout)layout.Container.Container;
//			TableStyle tableStyle = (TableStyle)table.Style;
//			TableStyle overrideTableStyle = (TableStyle)tableStyle.Base;
//			RenderCellBorderOverride(tableStyle.Border, overrideTableStyle?.Border, sb);
		}

		private void RenderPhotoStyleOverride(PhotoStyle style, StringBuilder sb)
		{
			if(style == null) return;

			PhotoStyle baseStyle = (PhotoStyle)style.Base;

			//	The core photo style elements - max width and height, resolution and
			//	quality - don't need to be rendered in CSS because they've already
			//	been applied during report generation.

			RenderTextStyleOverride(style.CaptionStyle, sb);
			RenderPaddingOverride(baseStyle.Padding, style.Padding, sb);
			RenderBorderOverride(baseStyle.Border, style.Border, sb);
		}

		private void RenderLineStyleOverride(LineStyle style, StringBuilder sb)
		{
		}

		/// <summary>
		/// Render the overridden parts of a font.
		/// </summary>
		private void RenderFontOverride(Demon.Report.Style.Font styleFont, Demon.Report.Style.Font overrideFont, StringBuilder sb)
		{
			if(overrideFont == null) return; // no override = nothing to do
			
			if(styleFont == null)
			{
				RenderFont(overrideFont, sb);
			}
			else
			{
				if(overrideFont.FamilyName != styleFont.FamilyName)
				{
					sb.Append("font-family: ");
					sb.Append(overrideFont.FamilyName);
					sb.Append("; ");
				}
				if(overrideFont.Size != styleFont.Size)
				{
					sb.Append("font-size: ");
					sb.Append(overrideFont.Size);
					sb.Append("px; ");
				}
				if(overrideFont.Bold != styleFont.Bold)
				{
					if(overrideFont.Bold)
						sb.Append("font-weight: bold; ");
					else
						sb.Append("font-weight: normal; ");
				}
				if(overrideFont.Italic != styleFont.Italic)
				{
					if(overrideFont.Italic)
						sb.Append("font-style: italic; ");
					else
						sb.Append("font-style: normal; ");
				}

				//	Underline and strikethrough both go in the text-decoration style
				//	component, so we have to consider them together. But in either
				//	case an override can add styling but no take it away. That is,
				//	if the style font has underline, then the override can't remove
				//	that underline.
				string textDecoration = "";
				if(overrideFont.Underline && !styleFont.Underline)
					textDecoration += " underline";
				if(overrideFont.Strikeout && !styleFont.Strikeout)
					textDecoration += " line-through";
				if(textDecoration.Length > 0)
				{
					sb.Append("text-decoration:");
					sb.Append(textDecoration);
					sb.Append("; ");
				}
			}
		}

		/// <param name="styleName">color, background-color, border-color etc.</param>
		private void RenderColorOverride(string styleName, Color styleColor, Color overrideColor, StringBuilder sb)
		{
			bool render = false;

			if(overrideColor == null)
				render = false; // no override to render
			
			else if(styleColor == null) // and override colour is not null
				render = true;
			
			else if( // both not null
				overrideColor.Red != styleColor.Red
				||
				overrideColor.Green != styleColor.Green
				||
				overrideColor.Blue != styleColor.Blue)
			{
				render = true; // override colour is different to the style colour
			}

			if(render)
			{
				//	Even though the design allows overriding individual colour components,
				//	that can't be expressed in HTML/CSS. So we just render the color as
				//	we find it.
				int red   = (int)(255 * overrideColor.Red);
				int green = (int)(255 * overrideColor.Green);
				int blue  = (int)(255 * overrideColor.Blue);
			
				sb.Append(styleName);
				sb.Append(": #");
				sb.Append(red  .ToString("x2"));
				sb.Append(green.ToString("x2"));
				sb.Append(blue .ToString("x2"));
				sb.Append("; ");
			}
		}

		private void RenderTextAlignmentOverride(TextAlignment styleAlignment, TextAlignment overrideAlignment, StringBuilder sb)
		{
			if(overrideAlignment != styleAlignment)
			{
				sb.Append("text-align: ");
				switch(overrideAlignment)
				{
					case TextAlignment.Left:    sb.Append("left"   ); break;
					case TextAlignment.Center:  sb.Append("center" ); break;
					case TextAlignment.Right:   sb.Append("right"  ); break;
					case TextAlignment.Justify: sb.Append("justify"); break;
				}
				sb.Append("; ");
			}
		}

		private void RenderLineSpacingOverride(double styleSpacing, double overrideSpacing, StringBuilder sb)
		{
			if(overrideSpacing != styleSpacing)
			{
				sb.Append("line-height: ");
				sb.Append(overrideSpacing);
				sb.Append("; ");
			}
		}

		private void RenderPaddingOverride(Padding stylePadding, Padding overridePadding, StringBuilder sb)
		{
			if(overridePadding == null) return; // no override = nothing to do
			
			if(stylePadding == null)
			{
				RenderPadding(overridePadding, sb);
			}
			else
			{
				//	Note that we map our design padding to HTML padding, not to HTML
				//	margin, because HTML padding auto-collapses whereas HTML margins
				//	don't, and our design padding doesn't collapse either.

				if(overridePadding.Top != stylePadding.Top)
				{
					sb.Append("padding-top: ");
					sb.Append(overridePadding.Top);
					sb.Append("px; ");
				}
				if(overridePadding.Right != stylePadding.Right)
				{
					sb.Append("padding-right: ");
					sb.Append(overridePadding.Right);
					sb.Append("px; ");
				}
				if(overridePadding.Bottom != stylePadding.Bottom)
				{
					sb.Append("padding-bottom: ");
					sb.Append(overridePadding.Bottom);
					sb.Append("px; ");
				}
				if(overridePadding.Left != stylePadding.Left)
				{
					sb.Append("padding-left: ");
					sb.Append(overridePadding.Left);
					sb.Append("px; ");
				}
			}
		}

		private void RenderBorderOverride(Border styleBorder, Border overrideBorder, StringBuilder sb)
		{
			if(overrideBorder == null) return; // no override = nothing to do
			
			if(styleBorder == null)
			{
				RenderBorder(overrideBorder, sb);
			}
			else
			{
				if(overrideBorder.Parts != styleBorder.Parts)
				{
					//	We always want adjacent borders to collapse together
//					sb.Append("border-collapse: collapse; ");

					//	And we only do solid borders
					sb.Append("border-top-style: ");
					if((overrideBorder.Parts & BorderPart.Top) != BorderPart.None)
						sb.Append("solid");
					else
						sb.Append("none");
					sb.Append("; ");

					sb.Append("border-bottom-style: ");
					if((overrideBorder.Parts & BorderPart.Bottom) != BorderPart.None)
						sb.Append("solid");
					else
						sb.Append("none");
					sb.Append("; ");

					sb.Append("border-left-style: ");
					if((overrideBorder.Parts & BorderPart.Left) != BorderPart.None)
						sb.Append("solid");
					else
						sb.Append("none");
					sb.Append("; ");

					sb.Append("border-right-style: ");
					if((overrideBorder.Parts & BorderPart.Right) != BorderPart.None)
						sb.Append("solid");
					else
						sb.Append("none");
					sb.Append("; ");
				}

				if(overrideBorder.Thickness != styleBorder.Thickness)
				{
					sb.Append("border-width: ");
					sb.Append(overrideBorder.Thickness);
					sb.Append("px; ");
				}

				RenderColorOverride("border-color", styleBorder.Color, overrideBorder.Color, sb);
			}
		}

		private void RenderIntegerOverride(string styleName, int styleValue, int overrideValue, StringBuilder sb)
		{
			if(overrideValue != styleValue)
			{
				sb.Append(styleName);
				sb.Append(": ");
				sb.Append(overrideValue);
				sb.Append("; ");
			}
		}

		private void RenderDoubleOverride(string styleName, double styleValue, double overrideValue, StringBuilder sb)
		{
			if(overrideValue != styleValue)
			{
				sb.Append(styleName);
				sb.Append(": ");
				sb.Append(overrideValue);
				sb.Append("; ");
			}
		}

		private string FormatFontOverride(Demon.Report.Style.Font styleFont, Demon.Report.Style.Font overrideFont)
		{
			StringBuilder sb = new StringBuilder();
			RenderFontOverride(styleFont, overrideFont, sb);
			sb.Replace("\r\n", " ");
			return sb.ToString();
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

		/// <summary>
		/// Set standard layout metadata as attributes on a HTML element.
		/// Note that the layout "id" property maps to the HTML "name"
		/// attribute; the HTML "id" has different semantics and must
		/// be set explicitly if you want it. In particular, the HTML "id"
		/// attribute must be globally unique.
		/// </summary>
		private void RenderLayoutMetadata(XElement element, Layout layout)
		{
			element.SetAttributeValue("trackingInfo", layout.TrackingInfo);
			element.SetAttributeValue("name", layout.Id);
		}

		/// <summary>
		/// Append an explicit style fragment to an element's style attribute.
		/// </summary>
		private void AddStyleFragment(XElement element, string style)
		{
			string value = element.Attribute("style")?.Value?.Trim() ?? "";
			if(value.Length > 0)
			{
				if(!value.EndsWith(";"))
					value += ";";
				value += " ";
			}
			value += style;
			element.SetAttributeValue("style", value);
		}

		private void RenderStyle(XElement element, IStyle style)
		{
			//	If it's a library style then set the class attribute,
			//	otherwise render the full style definition
			if(style.Base == null)
			{
				element.SetAttributeValue("class", style.Name);
			}
			else
			{
				StringBuilder sb = new StringBuilder();

				if     (style is TextStyle     ) RenderTextStyleOverride     ((TextStyle     )style, sb);
				else if(style is ListStyle     ) RenderListStyleOverride     ((ListStyle     )style, sb);
				else if(style is TableStyle    ) RenderTableStyleOverride    ((TableStyle    )style, sb);
				else if(style is TableRowStyle ) RenderTableRowStyleOverride ((TableRowStyle )style, sb);
				else if(style is TableCellStyle) RenderTableCellStyleOverride((TableCellStyle)style, sb);
				else if(style is PhotoStyle    ) RenderPhotoStyleOverride    ((PhotoStyle    )style, sb);
				else if(style is LineStyle     ) RenderLineStyleOverride     ((LineStyle     )style, sb);

				element.SetAttributeValue("class", style.Base.Name);
				element.SetAttributeValue("style", sb.ToString());

				RenderStyle(element, style.Base);
			}
		}

		/// <summary>
		/// Set inner table borders around cells.
		/// </summary>
		private void RenderCellBorder(Border border, StringBuilder sb)
		{
			if(border == null) return;

			//	Fake a new border style so that we can reuse the normal border
			//	method
			Border inner = new Border();
			if((border.Parts & BorderPart.InnerVertical) != BorderPart.None)
			{
				inner.Parts |= BorderPart.Left;
				inner.Parts |= BorderPart.Right;
			}
			if((border.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
			{
				inner.Parts |= BorderPart.Top;
				inner.Parts |= BorderPart.Bottom;
			}

			inner.Color = border.Color;
			inner.Thickness = border.Thickness;
			
			RenderBorder(inner, sb);
		}

		private void RenderCellBorderOverride(Border styleBorder, Border overrideBorder, StringBuilder sb)
		{
			if(overrideBorder == null) return; // no override = nothing to do
			
			if(styleBorder == null)
			{
				RenderCellBorder(overrideBorder, sb);
			}
			else
			{
				//	Fake new border styles so that we can reuse the normal border
				//	method
				Border styleInner = new Border();
				if((styleBorder.Parts & BorderPart.InnerVertical) != BorderPart.None)
				{
					styleInner.Parts |= BorderPart.Left;
					styleInner.Parts |= BorderPart.Right;
				}
				if((styleBorder.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
				{
					styleInner.Parts |= BorderPart.Top;
					styleInner.Parts |= BorderPart.Bottom;
				}

				styleInner.Color = styleBorder.Color;
				styleInner.Thickness = styleBorder.Thickness;
			
				Border overrideInner = new Border();
				if((overrideBorder.Parts & BorderPart.InnerVertical) != BorderPart.None)
				{
					overrideInner.Parts |= BorderPart.Left;
					overrideInner.Parts |= BorderPart.Right;
				}
				if((overrideBorder.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
				{
					overrideInner.Parts |= BorderPart.Top;
					overrideInner.Parts |= BorderPart.Bottom;
				}

				overrideInner.Color = overrideBorder.Color;
				overrideInner.Thickness = overrideBorder.Thickness;
			
				RenderBorderOverride(styleInner, overrideInner, sb);
			}
		}

		/// <summary>
		/// Convert from native bottom-left origin to HTML/CSS top-left origin.
		/// </summary>
		private string RectangleToCss(Rectangle rect, Rectangle mediaBox)
		{
			int left = rect.Left;
			int top = mediaBox.Height - rect.Top;
			int width = rect.Width;
			int height = rect.Height;

			return $"left:{left}px; top:{top}px; width:{width}px; height:{height}px;";
		}

#if PAGE_SELECTOR
		private void RenderScript(XElement container)
		{
			string script =
@"
window.onload = function()
{
 selectPage(0);
}
function selectPage(page)
{
 var report = document.getElementById('report');
 for(var i = 0; i != report.children.length; ++i)
  report.children[i].style.display = 'none';
 report.children[page].style.display = 'block';
}
";
			//	Couldn't find a way to encode "i < xxx" that would survive the XML-to-HTML
			//	conversion, so just went for "i != xxx" instead.

			container.Add(new XElement("script", script));
		}

		private void RenderPageSelectors(XElement container, int numPages, int maxWidth)
		{
			XElement div = new XElement("div");
			container.Add(div);
			string style = $"position:absolute; left:{maxWidth+20}px; top:0px;";
			div.SetAttributeValue("style", style);

			for(int x = 0; x < numPages; ++x)
			{
				XElement a = new XElement("a");
				a.SetAttributeValue("href", "#");
				a.SetAttributeValue("onclick", $"selectPage({x})");
				a.Value = (x+1).ToString();
				div.Add(a);
			}
		}
#endif
	}
}
