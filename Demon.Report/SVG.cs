//#define DEV_STANDALONE
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
	internal class SVG : ReportRenderer
	{
		private Generator _generator;

		private Font.Font _rulesFont;
		private int _rulesFontSize;

		private string _photoUri;
		private string _resourceUri;
		private int _pageHeight;

		private List<TextStyle>      _textStyles;
		private List<TableStyle>     _tableStyles;
		private List<TableRowStyle>  _tableRowStyles;
		private List<TableCellStyle> _tableCellStyles;
		private List<PhotoStyle>     _photoStyles;
		private List<ListStyle>      _listStyles;

		public SVG()
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
			//	names to them when we write them in CSS.
			_textStyles      = new List<TextStyle>     (_generator.ReportDesign.TextStyles .Values);
			_tableStyles     = new List<TableStyle>    (_generator.ReportDesign.TableStyles.Values);
			_tableRowStyles  = new List<TableRowStyle> (_generator.ReportDesign.RowStyles  .Values);
			_tableCellStyles = new List<TableCellStyle>(_generator.ReportDesign.CellStyles .Values);
			_photoStyles     = new List<PhotoStyle>    (_generator.ReportDesign.PhotoStyles.Values);
			_listStyles      = new List<ListStyle>     (_generator.ReportDesign.ListStyles .Values);
			
			//	Assume that we're going to draw rules on at least one page, and make
			//	sure that we've got a font prepared for that
			_rulesFont = _generator.GetFont("Helvetica", 400, false, false, false, false);
//			string ruleChars = "0123456789";
//			foreach(char c in ruleChars)
//				_rulesFont.MapCharacter(c);
			_rulesFontSize = 8;

			//	Measure and cut content
			_generator.TraceLayoutActivity("Measure and cut content");
			List<PageLayout> pages = report.LayOut();

			tracer.TraceLayoutActivity("Generate document");
			XDocument document = new XDocument();
			document.Declaration = null;

			XElement html = new XElement("html");
			document.Add(html);

			XElement head = new XElement("head");
			html.Add(head);

			XElement style = new XElement("style");
			style.SetAttributeValue("type", "text/css");
			WriteLibraryStyles(style);
			head.Add(style);

#if DEV_STANDALONE
			//	For dev only: render buttons to select the pages. In production this
			//	would be handled by something that Ramon and Jab do in the app.
			WriteScript(head);
#endif

			XElement body = new XElement("body");
			html.Add(body);

			XElement reportDiv = new XElement("div");
			reportDiv.SetAttributeValue("id", "report");
			body.Add(reportDiv);

			foreach(PageLayout page in pages)
				RenderPageLayout(page, reportDiv, drawRules, drawPageBoxes);

#if DEV_STANDALONE
			//	For dev only: render buttons to select the pages. In production this
			//	would be handled by something that Ramon and Jab do in the app.
			if(pages.Count > 0)
			{
				int maxWidth = pages.Max(l => ((PageLayout)l).MediaBox.Width);
				WritePageSelectors(body, pages.Count, maxWidth);
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
//					case LayoutType.Report:     RenderReportLayout    ((ReportLayout    )layout, container); break;
//					case LayoutType.Page:       RenderPageLayout      ((PageLayout      )layout, container); break;
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

		private void RenderPageLayout(PageLayout pageLayout, XElement container, bool drawRules, bool drawPageBoxes)
		{
			//	Make this page's metrics globally available
			_pageHeight = pageLayout.MediaBox.Height;

			XElement page = new XElement("div");
			page.SetAttributeValue("id", $"page-{pageLayout.PageNumber}");
			RenderLayoutMetadata(page, pageLayout);
			page.SetAttributeValue("style", null); // remove the positioning, just on the page layout
			container.Add(page);

			XElement svg = new XElement("svg");
			page.Add(svg);
//			svg.SetAttributeValue("xmlns", "http://www.w3.org/2000/svg");
			svg.SetAttributeValue("version", "1.1");
			svg.SetAttributeValue("width", pageLayout.MediaBox.Width);
			svg.SetAttributeValue("height", pageLayout.MediaBox.Height);

			if(drawRules || pageLayout.DrawRules)
			{
				DrawBleedMarks(pageLayout.BodyBox, svg);
				DrawRules(pageLayout.MediaBox, pageLayout.BodyBox, svg);
			}

			//	Background before content
			foreach(FixedPicture picture in pageLayout.Background)
				RenderFixedPicture(picture, svg);

			//	Content
			foreach(Layout sublayout in pageLayout.SubLayouts)
				Render(sublayout, svg);

			if(pageLayout.Header != null)
				Render(pageLayout.Header, svg);
			if(pageLayout.Footer != null)
				Render(pageLayout.Footer, svg);

			//	Overlay after content
			foreach(FixedPicture picture in pageLayout.Overlays)
				RenderFixedPicture(picture, svg);

			//	Draw the page boxes
			if(drawPageBoxes)
			{
				DrawPath(pageLayout.MediaBox .Points, "page-metric", true, svg);
				DrawPath(pageLayout.BodyBox  .Points, "page-metric", true, svg);
				DrawPath(pageLayout.HeaderBox.Points, "page-metric", true, svg);
				DrawPath(pageLayout.FooterBox.Points, "page-metric", true, svg);
			}
		}

		private void RenderGroupLayout(GroupLayout layout, XElement container)
		{
			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, container);
		}

		private void RenderTextLayout(TextLayout layout, XElement container)
		{
			if(layout == null) return;

			TextStyle style = (TextStyle)layout.Style;
			Fill(layout.Bounds, style.BackColor, container);

			//	Draw the border after the background so that the background doesn't
			//	overwrite the border. But draw the border before the content so that
			//	if for any reason the text exceeds the bounds (which should be impossible)
			//	then at least the text will still be visible.
			RenderBorder(layout.Bounds, style.Border, container);

			foreach(LineDraft line in layout.Lines)
			{
				foreach(StrokeDraft stroke in line.Strokes)
				{
					string text = stroke.Stroke.Text;
					int x = stroke.Position.X;
					int y = stroke.Position.Y;

					//	Make a style for the stroke so that we can use the style
					//	and override rendering methods. We're only interested in
					//	the font and the colour, because all of the positioning
					//	considerations - alignment, border, spacing - have already
					//	been taken into account during layout when we calculated
					//	the text position.
					TextStyle strokeStyle = new TextStyle();
					strokeStyle.Base = layout.Style;
					strokeStyle.Font = stroke.Stroke.Format.Font;
					strokeStyle.Color = stroke.Stroke.Format.Color;
					DrawText(text, x, y, stroke.Stroke.Width, strokeStyle, container);
				}
			}
		}

		private void RenderPhotoTableLayout(PhotoTableLayout table, XElement container)
		{
			foreach(PhotoRowLayout row in table.SubLayouts)
				RenderPhotoRowLayout(row, container);

			//	Draw borders around the photos (including their captions)
			PhotoStyle style = (PhotoStyle)table.Style;
			Border border = style.Border;
			if(border.Thickness > 0)
			{
				List<List<Position>> lines = new List<List<Position>>();

				foreach(PhotoRowLayout row in table.SubLayouts)
				{
					//	The row's bounds includes both photo and caption. Note that
					//	there's no padding possible on a photo table or photo row,
					//	so we don't have to allow for that.
					int top    = row.Bounds.Top;
					int bottom = row.Bounds.Bottom;

					for(int col = 0; col < row.NumPhotos; ++col)
					{
						int left  = row.Bounds.Left + (table.ColumnWdith * col);
						int right = left + table.ColumnWdith;

						if((border.Parts & BorderPart.Left) != BorderPart.None)
						{
							List<Position> line = new List<Position>();
							lines.Add(line);
							line.Add(new Position(left, top   ));
							line.Add(new Position(left, bottom));
						}

						if((border.Parts & BorderPart.Right) != BorderPart.None)
						{
							List<Position> line = new List<Position>();
							lines.Add(line);
							line.Add(new Position(right, top   ));
							line.Add(new Position(right, bottom));
						}

						if((border.Parts & BorderPart.Top) != BorderPart.None)
						{
							List<Position> line = new List<Position>();
							lines.Add(line);
							line.Add(new Position(left,  top));
							line.Add(new Position(right, top));
						}

						if((border.Parts & BorderPart.Bottom) != BorderPart.None)
						{
							List<Position> line = new List<Position>();
							lines.Add(line);
							line.Add(new Position(left,  bottom));
							line.Add(new Position(right, bottom));
						}
					}
				}
				DrawPath(lines, border.Thickness, border.Color, null, container);
			}
		}

		private void RenderPhotoRowLayout(PhotoRowLayout layout, XElement container)
		{
			for(int x = 0; x < layout.NumPhotos; ++x)
			{
				//	There's always a photo...
				PhotoLayout photoCell = (PhotoLayout)layout.PhotoRow.GetSubLayoutAtIndex(x).GetSubLayoutAtIndex(0);
				RenderPhotoLayout(photoCell, container);

				//	but not always a caption
				TableCellLayout captionCell = (TableCellLayout)layout.CaptionRow.GetSubLayoutAtIndex(x);
				if(captionCell.NumSubLayouts > 0)
				{
					TextLayout caption = (TextLayout)captionCell.GetSubLayoutAtIndex(0);
					RenderTextLayout(caption, container);
				}
			}
		}

		private void RenderPhotoLayout(PhotoLayout layout, XElement container)
		{
			Rectangle bounds = layout.PhotoBounds; // bounds of the photo within the layout
			int x      = bounds.Left;
			int y      = Normalise(bounds.Top);
			int width  = bounds.Width;
			int height = bounds.Height;
			string uri = $"{_photoUri}/{layout.PhotoId}";
			DrawImage(x, y, width, height, uri, container);
		}

		private void RenderListLayout(ListLayout layout, XElement container)
		{
			//	Draw the border before the content so that
			//	if for any reason the text exceeds the bounds (which should be impossible)
			//	then at least the text will still be visible.
			ListStyle style = (ListStyle)layout.Style;
			RenderBorder(layout.Bounds, style.Border, container);

			foreach(ListItemLayout item in layout.SubLayouts)
				RenderListItemLayout(item, container);
		}

		private void RenderListItemLayout(ListItemLayout layout, XElement container)
		{
			Render(layout.BulletLayout, container);
			Render(layout.ContentLayout, container);
		}

		private void RenderTableLayout(TableLayout table, XElement container)
		{
			//	Draw the table content first, before the borders, so that if any
			//	row or cell has a background color then that color won't overwrite
			//	the border. We don't do header rows in SVG because the
			//	generator has already taken care of them and explicitly repeated rows
			//	as necessary.
			foreach(TableRowLayout row in table.SubLayouts)
				Render(row, container);

			//	Draw the table's borders
			TableStyle style = (TableStyle)table.Style;
			Border border = style.Border;
			if(border.Thickness > 0)
			{
				//	Build a single path covering all borders, including the inner
				//	cell borders
				List<List<Position>> lines = new List<List<Position>>();

				//	Draw the external border
				if((border.Parts & BorderPart.Left) != BorderPart.None)
				{
					List<Position> line = new List<Position>();
					lines.Add(line);
					line.Add(new Position(table.Bounds.Left, table.Bounds.Top   ));
					line.Add(new Position(table.Bounds.Left, table.Bounds.Bottom));
				}
				if((border.Parts & BorderPart.Bottom) != BorderPart.None)
				{
					List<Position> line = new List<Position>();
					lines.Add(line);
					line.Add(new Position(table.Bounds.Left,  table.Bounds.Bottom));
					line.Add(new Position(table.Bounds.Right, table.Bounds.Bottom));
				}
				if((border.Parts & BorderPart.Right) != BorderPart.None)
				{
					List<Position> line = new List<Position>();
					lines.Add(line);
					line.Add(new Position(table.Bounds.Right, table.Bounds.Bottom));
					line.Add(new Position(table.Bounds.Right, table.Bounds.Top   ));
				}
				if((border.Parts & BorderPart.Top) != BorderPart.None)
				{
					List<Position> line = new List<Position>();
					lines.Add(line);
					line.Add(new Position(table.Bounds.Right, table.Bounds.Top));
					line.Add(new Position(table.Bounds.Left,  table.Bounds.Top));
				}

				//	If the border style includes inner horizontal then draw
				//	a top border on all rows except the first
				if((border.Parts & BorderPart.InnerHorizontal) != BorderPart.None)
				{
					bool firstRow = true;
					foreach(TableRowLayout row in table.SubLayouts)
					{
						if(firstRow)
						{
							firstRow = false;
							continue;
						}
						List<Position> line = new List<Position>();
						lines.Add(line);
						line.Add(new Position(table.Bounds.Left,  row.Bounds.Top));
						line.Add(new Position(table.Bounds.Right, row.Bounds.Top));
					}
				}

				//	If the border style includes inner vertical then on every row -
				//	including the first - draw a left border on every cell except the
				//	first. We can't draw full column borders because a cell may span
				//	more than one column, so we do a bit of fancy footwork.
				if((border.Parts & BorderPart.InnerVertical) != BorderPart.None)
				{
					//	Build a matrix representing the row/column grid, and then fill
					//	it with bools indicating whether whether each cell of the grid
					//	needs a left border. Where a table cell spans more than one
					//	grid cell, the first spanned grid cell will have "true" and
					//	the remaning grid cells in the span will have "false".
					bool[,] grid = new bool[table.NumSubLayouts, table.ColumnCount];
					int rowIndex = 0;
					foreach(TableRowLayout row in table.SubLayouts)
					{
						int colIndex = 0;
						foreach(TableCellLayout cell in row.SubLayouts)
						{
							grid[rowIndex, colIndex] = true;
							colIndex += cell.ColumnSpan;
						}
						++rowIndex;
					}

					//	Plot the top-left corners of all grid cells. (Grid cells, not
					//	table cells.) We'll also want the the bottom-left corners of
					//	the cells on the bottom row.
					Position[,] points = new Position[table.NumSubLayouts + 1, table.ColumnCount];
					rowIndex = 0;
					int firstLeft = table.Bounds.Left;
					int x = 0;
					int y = 0;
					foreach(TableRowLayout row in table.SubLayouts)
					{
						x = firstLeft;
						y = row.Bounds.Top;
						for(int colIndex = 0; colIndex < table.ColumnCount; ++colIndex)
						{
							points[rowIndex, colIndex] = new Position(x, y);
							x += table.ColumnWidths[colIndex];
						}
						++rowIndex;
					}
					//	Add the bottom of the last row. In fact we want the bottom of the
					//	table, because if the table has bottom padding then the bottom of
					//	the last row will be higher than the bottom of the table.
					x = firstLeft;
					y = table.Bounds.Bottom;
					rowIndex = table.NumSubLayouts;
					for(int colIndex = 0; colIndex < table.ColumnCount; ++colIndex)
					{
						points[rowIndex, colIndex] = new Position(x, y);
						x += table.ColumnWidths[colIndex];
					}
					//	Correct the top row's points to allow for any top padding on
					//	the table, which would have put the top of the first row lower
					//	than the top of the table
					x = firstLeft;
					y = table.Bounds.Top;
					rowIndex = 0;
					for(int colIndex = 0; colIndex < table.ColumnCount; ++colIndex)
					{
						points[rowIndex, colIndex] = new Position(x, y);
						x += table.ColumnWidths[colIndex];
					}

					//	Draw the left-hand borders, skipping the first column because
					//	that's been covered by the table's own external border. For
					//	each column move down the rows: start at points[row,col] and
					//	set a destination of points[row+1,col]. If the flag at the
					//	corresponding grid[row,col] is set then we want a border there
					//	so draw a line from the start to the destination, otherwise just
					//	move to the destination without drawing.
					for(int colIndex = 1; colIndex < table.ColumnCount; ++colIndex)
					{
						for(rowIndex = 0; rowIndex < table.NumSubLayouts; ++rowIndex)
						{
							Position start = points[rowIndex,     colIndex];
							Position end   = points[rowIndex + 1, colIndex];
							bool flag = grid[rowIndex, colIndex];
							if(flag)
							{
								List<Position> line = new List<Position>();
								lines.Add(line);
								line.Add(start);
								line.Add(end);
							}
							start = end;
						}
					}
				}
					
				DrawPath(lines, border.Thickness, border.Color, null, container);
			}
		}

		private void RenderTableRowLayout(TableRowLayout layout, XElement container)
		{
			TableRowStyle style = (TableRowStyle)layout.Style;
			Fill(layout.Bounds, style.BackColor, container);

			foreach(TableCellLayout cell in layout.Cells)
				RenderTableCellLayout(cell, container);
		}

		private void RenderTableCellLayout(TableCellLayout layout, XElement container)
		{
			TableCellStyle style = (TableCellStyle)layout.Style;
//			Fill(layout.Bounds, style., container); TODO: why doesn't TableCellStyle have BackColor?

			foreach(Layout child in layout.SubLayouts)
				Render(child, container);
		}

		private void RenderPictureLayout(PictureLayout layout, XElement container)
		{
			string filename = layout.Filename;
			if(filename == null)
				filename = _generator.ReportDesign.GetResourceFilename(layout.ResourceId);
			string uri = $"{_resourceUri}/{filename}";

			Rectangle bounds = layout.Bounds;
			int x      = bounds.Left;
			int y      = Normalise(bounds.Top);
			int width  = bounds.Width;
			int height = bounds.Height;

			DrawImage(x, y, width, height, uri, container);
		}

		private void RenderFixedPicture(FixedPicture picture, XElement container)
		{
			string filename = picture.Filename;
			if(filename == null)
				filename = _generator.ReportDesign.GetResourceFilename(picture.ResourceId);
			string uri = $"{_resourceUri}/{filename}";

			Rectangle bounds = picture.Bounds;
			int x      = bounds.Left;
			int y      = Normalise(bounds.Top);
			int width  = bounds.Width;
			int height = bounds.Height;

			DrawImage(x, y, width, height, uri, container);
		}

		/// <summary>
		/// Draw a simple outer border. Ignore any inner parts.
		/// </summary>
		private void RenderBorder(Rectangle rect, Border border, XElement container)
		{
            if (border == null) return;
			if(border.Thickness <= 0) return;

			//	Build a single path covering all outer border edges
			List<List<Position>> lines = new List<List<Position>>();

			if((border.Parts & BorderPart.Left) != BorderPart.None)
			{
				List<Position> line = new List<Position>();
				lines.Add(line);
				line.Add(new Position(rect.Left, rect.Top   ));
				line.Add(new Position(rect.Left, rect.Bottom));
			}
			if((border.Parts & BorderPart.Bottom) != BorderPart.None)
			{
				List<Position> line = new List<Position>();
				lines.Add(line);
				line.Add(new Position(rect.Left,  rect.Bottom));
				line.Add(new Position(rect.Right, rect.Bottom));
			}
			if((border.Parts & BorderPart.Right) != BorderPart.None)
			{
				List<Position> line = new List<Position>();
				lines.Add(line);
				line.Add(new Position(rect.Right, rect.Bottom));
				line.Add(new Position(rect.Right, rect.Top   ));
			}
			if((border.Parts & BorderPart.Top) != BorderPart.None)
			{
				List<Position> line = new List<Position>();
				lines.Add(line);
				line.Add(new Position(rect.Right, rect.Top));
				line.Add(new Position(rect.Left,  rect.Top));
			}

			DrawPath(lines, border.Thickness, border.Color, null, container);
		}

		private void WriteLibraryStyles(XElement container)
		{
			StringBuilder sb = new StringBuilder();

			WritePageStyles(sb);
			WriteLibraryTextStyles(sb);
			WriteLibraryTableStyles(sb);
			WriteLibraryPhotoStyles(sb);

			//TODO: write only the styles that are actually used

			sb.Replace("; ", ";\r\n");
			container.Add(sb.ToString());
		}

		private void WritePageStyles(StringBuilder sb)
		{
			sb.Append("\r\n");

#if DEV_STANDALONE
			sb.Append("body\r\n");
			sb.Append("{\r\n");
			sb.Append("background-color: #f8f8f8;\r\n");
			sb.Append("}\r\n");

			sb.Append("svg\r\n");
			sb.Append("{\r\n");
			sb.Append("background-color: #ffffff;\r\n");
			sb.Append("}\r\n");
#endif

			sb.Append(".page-metric\r\n");
			sb.Append("{\r\n");
			sb.Append("stroke: #bfbfbf;\r\n");
			sb.Append("stroke-width: 1;\r\n");
			sb.Append("stroke-dasharray: 4 4;\r\n");
			sb.Append("fill: none;\r\n");
			sb.Append("}\r\n");

			sb.Append(".rules\r\n");
			sb.Append("{\r\n");
			sb.Append("stroke: #bfbfbf;\r\n");
			sb.Append("stroke-width: 1;\r\n");
			sb.Append("fill: none;\r\n");
			sb.Append("}\r\n");

			sb.Append(".rules-text\r\n");
			sb.Append("{\r\n");
			sb.Append($"font-family: {_rulesFont.FamilyName};\r\n");
			sb.Append($"font-size: {_rulesFontSize}px;\r\n");
			sb.Append("font-weight: 100;\r\n");
			sb.Append("fill: #bfbfbf;\r\n");
			sb.Append("}\r\n");
		}

		private void WriteLibraryTextStyles(StringBuilder sb)
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
				WriteLibraryTextStyle(style, sb);
				sb.Append("}\r\n");
			}
		}

		private void WriteLibraryTableStyles(StringBuilder sb)
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
				WriteLibraryTableStyle(style, sb);
				sb.Append("}\r\n");

				//	If the style includes inner borders then write a style for that
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
					WriteLibraryTableStyle(inner, sb);
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
				WriteLibraryTableRowStyle(style, sb);
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
				WriteLibraryTableCellStyle(style, sb);
				sb.Append("}\r\n");
			}
		}

		private void WriteLibraryPhotoStyles(StringBuilder sb)
		{
			for(int x = 0; x < _photoStyles.Count; ++x)
			{
				//	A photo style is a table style, with a corresponding text style
				//	for the captions
				PhotoStyle style = _photoStyles[x];
				style.Name = $"photo-style-{x}";

				//	The caption style needs its own CSS identity so that
				//	it can be applied to the caption div and have different
				//	border to the photo div.
				style.CaptionStyle.Name = style.Name + "-caption-style";
				sb.Append(".");
				sb.Append(style.CaptionStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				WriteLibraryTextStyle(style.CaptionStyle, sb);
				sb.Append("}\r\n");
			}
		}

		private void WriteLibraryListStyles(StringBuilder sb)
		{
			for(int x = 0; x < _listStyles.Count; ++x)
			{
				ListStyle style = _listStyles[x];
				style.Name = $"list-style-{x}";
				sb.Append(".");
				sb.Append(style.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				WriteLibraryListStyle(style, sb);
				sb.Append("}\r\n");

				//	The item and bullet styles need their own CSS identities so that
				//	they can be applied to the item and bullet divs and have different
				//	border to the list div.

				style.ItemStyle.Name = style.Name + "-item-style";
				sb.Append(".");
				sb.Append(style.ItemStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
				WriteLibraryTextStyle(style.ItemStyle, sb);
				sb.Append("}\r\n");

				style.BulletStyle.Name = style.Name + "-bullet-style";
				sb.Append(".");
				sb.Append(style.BulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				WriteLibraryBulletStyle(style.BulletStyle, sb);
				sb.Append("}\r\n");

				style.SelectedBulletStyle.Name = style.Name + "-selected-bullet-style";
				sb.Append(".");
				sb.Append(style.SelectedBulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				WriteLibraryBulletStyle(style.SelectedBulletStyle, sb);
				sb.Append("}\r\n");

				style.UnselectedBulletStyle.Name = style.Name + "-unselected-bullet-style";
				sb.Append(".");
				sb.Append(style.UnselectedBulletStyle.Name);
				sb.Append("\r\n");
				sb.Append("{\r\n");
//				WriteLibraryBulletStyle(style.UnselectedBulletStyle, sb);
				sb.Append("}\r\n");
			}
		}

		private void WriteLibraryTextStyle(TextStyle style, StringBuilder sb)
		{
			if(style == null) return;

			WriteFont(style.Font, sb);
			WriteColor("fill", style.Color, sb);
//			WriteColor("background-color", style.BackColor, sb);
		}

		private void WriteLibraryListStyle(ListStyle style, StringBuilder sb)
		{
			if(style == null) return;

//			WriteBorderStyle(style.Border);
//			WriteBulletStyle(style.BulletStyle);
//			WriteBulletStyle(style.SelectedBulletStyle);
//			WriteBulletStyle(style.UnelectedBulletStyle);
//			WriteTextStyle(style.ItemStyle);
		}

		private void WriteLibraryTableStyle(TableStyle style, StringBuilder sb)
		{
			if(style == null) return;
		}

		private void WriteLibraryTableRowStyle(TableRowStyle style, StringBuilder sb)
		{
			if(style == null) return;

//			WriteColor("background-color", style.BackColor, sb);
		}

		private void WriteLibraryTableCellStyle(TableCellStyle style, StringBuilder sb)
		{
			if(style == null) return;
		}

		/// <summary>
		/// Write a font. If the font is an override then only writes the overridden
		/// parts, otherwise writes the whole font.
		/// </summary>
		private void WriteFont(Demon.Report.Style.Font font, StringBuilder sb)
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
				WriteFontOverride((Style.Font)font.Base, font, sb);
			}
		}

		/// <param name="styleName">color, background-color, border-color etc.</param>
		private void WriteColor(string styleName, Color color, StringBuilder sb)
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

		/// <summary>
		/// This method is only suitable for writing stand-alone overriding
		/// text styles. It is not suitable for overrides of embedded text styles
		/// such as ListStyle.ItemStyle or PhotoStyle.CaptionStyle.
		/// </summary>
		private void WriteTextStyleOverride(TextStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TextStyle baseStyle = (TextStyle)style.Base;

			WriteFontOverride(baseStyle.Font, style.Font, sb);
			WriteColorOverride("fill", baseStyle.Color, style.Color, sb);
//			WriteColorOverride("background-color", baseStyle.BackColor, style.BackColor, sb);
		}

		private void WriteListStyleOverride(ListStyle style, StringBuilder sb)
		{
		}

		private void WriteTableStyleOverride(TableStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableStyle baseStyle = (TableStyle)style.Base;
		}

		private void WriteTableRowStyleOverride(TableRowStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableRowStyle baseStyle = (TableRowStyle)style.Base;

//			WriteColorOverride("background-color", baseStyle.BackColor, style.BackColor, sb);
		}

		private void WriteTableCellStyleOverride(TableCellStyle style, StringBuilder sb)
		{
			if(style == null) return;

			TableCellStyle baseStyle = (TableCellStyle)style.Base;

			//	Write inner table borders
//			TableLayout table = (TableLayout)layout.Container.Container;
//			TableStyle tableStyle = (TableStyle)table.Style;
//			TableStyle overrideTableStyle = (TableStyle)tableStyle.Base;
//			WriteCellBorderOverride(tableStyle.Border, overrideTableStyle?.Border, sb);
		}

		private void WritePhotoStyleOverride(PhotoStyle style, StringBuilder sb)
		{
			if(style == null) return;

			PhotoStyle baseStyle = (PhotoStyle)style.Base;

			//	The core photo style elements - max width and height, resolution and
			//	quality - don't need to be written in CSS because they've already
			//	been applied during report generation.

			WriteTextStyleOverride(style.CaptionStyle, sb);
		}

		/// <summary>
		/// Write the overridden parts of a font.
		/// </summary>
		private void WriteFontOverride(Demon.Report.Style.Font styleFont, Demon.Report.Style.Font overrideFont, StringBuilder sb)
		{
			if(overrideFont == null) return; // no override = nothing to do
			
			if(styleFont == null)
			{
				WriteFont(overrideFont, sb);
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
		private void WriteColorOverride(string styleName, Color styleColor, Color overrideColor, StringBuilder sb)
		{
			bool write = false;

			if(overrideColor == null)
				write = false; // no override to write
			
			else if(styleColor == null) // and override colour is not null
				write = true;
			
			else if( // both not null
				overrideColor.Red != styleColor.Red
				||
				overrideColor.Green != styleColor.Green
				||
				overrideColor.Blue != styleColor.Blue)
			{
				write = true; // override colour is different to the style colour
			}

			if(write)
			{
				//	Even though the design allows overriding individual colour components,
				//	that can't be expressed in HTML/CSS. So we just write the color as
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

		private string FormatFontOverride(Demon.Report.Style.Font styleFont, Demon.Report.Style.Font overrideFont)
		{
			StringBuilder sb = new StringBuilder();
			WriteFontOverride(styleFont, overrideFont, sb);
			sb.Replace("\r\n", " ");
			return sb.ToString();
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

			if(string.IsNullOrWhiteSpace(value))
				value = null;
			element.SetAttributeValue("style", value);
		}

		private void WriteStyle(XElement element, IStyle style)
		{
			//	If it's a library style then set the class attribute,
			//	otherwise write the full style definition
			if(style.Base == null)
			{
				element.SetAttributeValue("class", style.Name);
			}
			else
			{
				StringBuilder sb = new StringBuilder();

				if     (style is TextStyle     ) WriteTextStyleOverride     ((TextStyle     )style, sb);
				else if(style is ListStyle     ) WriteListStyleOverride     ((ListStyle     )style, sb);
				else if(style is TableStyle    ) WriteTableStyleOverride    ((TableStyle    )style, sb);
				else if(style is TableRowStyle ) WriteTableRowStyleOverride ((TableRowStyle )style, sb);
				else if(style is TableCellStyle) WriteTableCellStyleOverride((TableCellStyle)style, sb);
				else if(style is PhotoStyle    ) WritePhotoStyleOverride    ((PhotoStyle    )style, sb);

				element.SetAttributeValue("class", style.Base.Name);

//				element.SetAttributeValue("style", sb.ToString());
				AddStyleFragment(element, sb.ToString());

//				WriteStyle(element, style.Base);
			}
		}

		private void DrawText(string text, int x, int y, int width, TextStyle style, XElement container)
		{
			XElement element = new XElement("text");
			container.Add(element);
			
			element.SetAttributeValue("x", x);
			element.SetAttributeValue("y", Normalise(y));
			element.SetAttributeValue("textLength", width);
//			element.SetAttributeValue("lengthAdjust", "spacingAndGlyphs");
			WriteStyle(element, style);
			element.SetAttributeValue(XNamespace.Xml + "space", "preserve");
			element.Value = text;
		}

		private void DrawText(string text, int x, int y, int width, string styleName, XElement container)
		{
			XElement element = new XElement("text");
			container.Add(element);
			
			element.SetAttributeValue("x", x);
			element.SetAttributeValue("y", Normalise(y));
			element.SetAttributeValue("textLength", width);
//			element.SetAttributeValue("lengthAdjust", "spacingAndGlyphs");
			element.SetAttributeValue("class", styleName);
			element.SetAttributeValue(XNamespace.Xml + "space", "preserve");
			element.Value = text;
		}

		/// <summary>
		/// Draw a series of lines in a single SVG path with a single style.
		/// </summary>
		private void DrawPath(IEnumerable<IEnumerable<Position>> lines, string styleName, XElement container)
		{
			XElement path = new XElement("path");
			container.Add(path);

			string instructions = "";
			foreach(IEnumerable<Position> line in lines)
				instructions += ConstructPath(line, false) + " ";

			path.SetAttributeValue("d", instructions);
			path.SetAttributeValue("class", styleName);
		}

		/// <summary>
		/// Draw a series of lines in a single SVG path with a single style.
		/// </summary>
		private void DrawPath(
			IEnumerable<IEnumerable<Position>> lines, float lineWidth, Color strokeColor, Color fillColor,
			XElement container)
		{
			XElement path = new XElement("path");
			container.Add(path);

			string instructions = "";
			foreach(IEnumerable<Position> line in lines)
				instructions += ConstructPath(line, false) + " ";

			path.SetAttributeValue("d", instructions);

			if(strokeColor != null)
				path.SetAttributeValue("stroke", ColorSpec(strokeColor));
			path.SetAttributeValue("stroke-width", lineWidth);

			string fill = fillColor != null ? ColorSpec(fillColor) : "none";
			path.SetAttributeValue("fill", fill);
		}

		private void DrawPath(
			IEnumerable<Position> points, float lineWidth, Color strokeColor, Color fillColor,
			bool close,
			XElement container)
		{
//			if(points.Count < 2)
//				throw new InvalidOperationException("Cannot draw a path with fewer than two points.");

			XElement path = new XElement("path");
			container.Add(path);
			path.SetAttributeValue("d", ConstructPath(points, close));

			if(strokeColor != null)
				path.SetAttributeValue("stroke", ColorSpec(strokeColor));
			path.SetAttributeValue("stroke-width", lineWidth);

			string fill = fillColor != null ? ColorSpec(fillColor) : "none";
			path.SetAttributeValue("fill", fill);
		}

		private void DrawPath(IEnumerable<Position> points, string styleName, bool close, XElement container)
		{
//			if(points.Count < 2)
//				throw new InvalidOperationException("Cannot draw a path with fewer than two points.");

			XElement path = new XElement("path");
			container.Add(path);
			path.SetAttributeValue("d", ConstructPath(points, close));
			path.SetAttributeValue("class", styleName);
		}

		private void DrawImage(int x, int y, int width, int height, string uri, XElement container)
		{
			XElement image = new XElement("image");
			container.Add(image);

			image.SetAttributeValue("x", x);
			image.SetAttributeValue("y", y);
			image.SetAttributeValue("width", width);
			image.SetAttributeValue("height", height);
			image.SetAttributeValue("preserveAspectRatio", "xMinYMin"); // position relative to the origin
			image.SetAttributeValue("href", uri);
		}

		private void Fill(Rectangle rect, Color color, XElement container)
		{
			if(color == null) return;

			DrawPath(rect.Points, 0, null, color, true, container);
		}

		private string ConstructPath(IEnumerable<Position> points, bool close)
		{
//			if(points.Count < 2)
//				throw new InvalidOperationException("Cannot draw a path with fewer than two points.");

			string path = null;
			bool first = true;
			foreach(Position pos in points)
			{
				int x = pos.X;
				int y = Normalise(pos.Y);
				
				if(first)
				{
					path = "M";
					first = false;
				}
				else
					path += " L";
				
				path += $" {x} {y}";
			}

			if(close)
				path += " z";

			return path;
		}

		private void DrawBleedMarks(Rectangle bodyBox, XElement container)
		{
			List<Position> path = new List<Position>();
			Position p1 = new Position(bodyBox.Left - 20, bodyBox.Top +  1);
			Position p2 = new Position(bodyBox.Left -  1, bodyBox.Top +  1);
			Position p3 = new Position(bodyBox.Left -  1, bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			DrawPath(path, "rules", false, container);

			path.Clear();
			p1 = new Position(bodyBox.Right + 20, bodyBox.Top  + 1);
			p2 = new Position(bodyBox.Right +  1, bodyBox.Top +  1);
			p3 = new Position(bodyBox.Right +  1, bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			DrawPath(path, "rules", false, container);

			path.Clear();
			p1 = new Position(bodyBox.Left - 20, bodyBox.Bottom -  1);
			p2 = new Position(bodyBox.Left -  1, bodyBox.Bottom -  1);
			p3 = new Position(bodyBox.Left -  1, bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			DrawPath(path, "rules", false, container);

			path.Clear();
			p1 = new Position(bodyBox.Right + 20, bodyBox.Bottom -  1);
			p2 = new Position(bodyBox.Right +  1, bodyBox.Bottom -  1);
			p3 = new Position(bodyBox.Right +  1, bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			DrawPath(path, "rules", false, container);
		}

		private void DrawRules(Rectangle mediaBox, Rectangle bodyBox, XElement container)
		{
			int fontHeight = _rulesFont.GetCapHeight(_rulesFontSize);

			List<List<Position>> lines = new List<List<Position>>();
			List<Position> line;
			Position p1, p2;

			//	Side media box rule (left)
			for(int x = 0; x < mediaBox.Height; x += 10)
			{
				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left + 12, mediaBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, _rulesFontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X + 2;
					int ty = p2.Y - (fontHeight / 2);
					DrawText(text, tx, ty, textLen, "rules-text", container);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left + 12, mediaBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left +  5, mediaBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
			}

			//	Bottom media box rule (bottom)
			for(int x = 0; x < mediaBox.Width; x += 10)
			{
				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom + 12);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, _rulesFontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y + 2;
					DrawText(text, tx, ty, textLen, "rules-text", container);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom + 12);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom +  5);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
			}

			//	Side body box rule (right)
			int start = mediaBox.Bottom - bodyBox.Bottom; // negative start to put zero at the body box bottom
			start = (start / 10) * 10;
			for(int x = start; x < mediaBox.Height; x += 10)
			{
				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right - 12, bodyBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, _rulesFontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - 2 - textLen;
					int ty = p2.Y - (fontHeight / 2);
					DrawText(text, tx, ty, textLen, "rules-text", container);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right - 12, bodyBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right -  5, bodyBox.Bottom + x);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
			}

			//	Bottom body box rule (top)
			start = mediaBox.Left - bodyBox.Left; // negative start to put zero at the body box left
			start = (start / 10) * 10;
			for(int x = start; x < mediaBox.Width; x += 10)
			{
				if(x % 100 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top - 12);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, _rulesFontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y - 2 - fontHeight;
					DrawText(text, tx, ty, textLen, "rules-text", container);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top - 12);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top -  5);
					line = new List<Position>();
					line.Add(p1);
					line.Add(p2);
					lines.Add(line);
				}
			}

			DrawPath(lines, "rules", container);
		}

		private int Normalise(int y)
		{
			return _pageHeight - y;
		}

		private string ColorSpec(Color color)
		{
			if(color == null) return "";

			int red   = (int)(255 * color.Red);
			int green = (int)(255 * color.Green);
			int blue  = (int)(255 * color.Blue);
			
			return $"#{red:x2}{green:x2}{blue:x2}";
		}

#if DEV_STANDALONE
		private void WriteScript(XElement container)
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

		private void WritePageSelectors(XElement container, int numPages, int maxWidth)
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
#endif // DEV_STANDALONE
	}
}
