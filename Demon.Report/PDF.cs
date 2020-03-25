using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Demon.PDF;
using Demon.Report.Style;
using Demon.Report.Types;

namespace Demon.Report
{
	internal class PDF : ReportRenderer
	{
		private Generator _generator;

		private Font.Font _rulesFont;

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

			//	In case we're going to draw rules on at least one page, make
			//	sure that we've got a font prepared for that
			_rulesFont = _generator.GetFont("Helvetica", 400, false, false, false, false);
			string ruleChars = "0123456789";
			foreach(char c in ruleChars)
				_rulesFont.MapCharacter(c);

			//	Lay out the content and apply page breaks
			_generator.TraceLayoutActivity("Measure and cut content");
			List<PageLayout> pageLayouts = report.LayOut();

			//	Subset the fonts. First map characters actually used by the
			//	layouts into the fonts' glyph character maps, and then create
			//	the subsets using only those glyphs. Do this after laying out
			//	because header and footer content isn't loaded until the layout
			//	phase. See the note in TextLayout.LoadContent before the call
			//	to ExpandProperties.
			//TODO: find a better way to handle PageNumber and PageCount so that we
			//can include them in the page body, and so that we can lay out the
			//header and footer at the same time as the body.
			report.MapFontCharacters();
			_generator.FontCache.Subset();

			Demon.PDF.Document doc = new Demon.PDF.Document(docInfo, _generator.FontCache);

			//	Write the content to the PDF
			tracer.TraceLayoutActivity("Generate document");
			foreach(PageLayout pageLayout in pageLayouts)
				RenderPageLayout(pageLayout, doc, drawRules);

			Stream file = doc.Write();
			return file;
		}

		private void Render(Layout layout, Page page)
		{
			try
			{
				layout.PushTraceContext();

				switch(layout.LayoutType)
				{
//					case LayoutType.Report:     RenderReportLayout    ((ReportLayout    )layout, page); break;
//					case LayoutType.Page:       RenderPageLayout      ((PageLayout      )layout, page); break;
					case LayoutType.Group:      RenderGroupLayout     ((GroupLayout     )layout, page); break;
					case LayoutType.Text:       RenderTextLayout      ((TextLayout      )layout, page); break;
					case LayoutType.PhotoTable: RenderPhotoTableLayout((PhotoTableLayout)layout, page); break;
					case LayoutType.PhotoRow:   RenderPhotoRowLayout  ((PhotoRowLayout  )layout, page); break;
					case LayoutType.Photo:      RenderPhotoLayout     ((PhotoLayout     )layout, page); break;
					case LayoutType.List:       RenderListLayout      ((ListLayout      )layout, page); break;
					case LayoutType.ListItem:   RenderListItemLayout  ((ListItemLayout  )layout, page); break;
					case LayoutType.Table:      RenderTableLayout     ((TableLayout     )layout, page); break;
					case LayoutType.TableRow:   RenderTableRowLayout  ((TableRowLayout  )layout, page); break;
					case LayoutType.TableCell:  RenderTableCellLayout ((TableCellLayout )layout, page); break;
					case LayoutType.Picture:    RenderPictureLayout   ((PictureLayout   )layout, page); break;
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

		private void RenderPageLayout(PageLayout pageLayout, Document document, bool drawRules)
		{
			Demon.PDF.Rectangle mediaBox  = Convert.Rectangle(pageLayout.MediaBox);
			Demon.PDF.Rectangle bodyBox   = Convert.Rectangle(pageLayout.BodyBox);
			Demon.PDF.Rectangle headerBox = Convert.Rectangle(pageLayout.HeaderBox);
			Demon.PDF.Rectangle footerBox = Convert.Rectangle(pageLayout.FooterBox);

			Page page = new Page(pageLayout.PageNumber, mediaBox, bodyBox, headerBox, footerBox, document);

			if(drawRules || pageLayout.DrawRules)
			{
				DrawBleedMarks(mediaBox, bodyBox, headerBox, footerBox, page);
				DrawRules(mediaBox, bodyBox, headerBox, footerBox, page);
			}
			
			//	Background before content
			foreach(FixedPicture picture in pageLayout.Background)
				RenderFixedPicture(picture, page);

			//	Content
			foreach(Layout sublayout in pageLayout.SubLayouts)
				Render(sublayout, page);

			if(pageLayout.Header != null)
				Render(pageLayout.Header, page);
			if(pageLayout.Footer != null)
				Render(pageLayout.Footer, page);

			//	Overlay after content
			foreach(FixedPicture picture in pageLayout.Overlays)
				RenderFixedPicture(picture, page);
		}

		private void RenderGroupLayout(GroupLayout layout, Page page)
		{
			foreach(Layout sublayout in layout.SubLayouts)
				Render(sublayout, page);
		}

		private void RenderTextLayout(TextLayout layout, Page page)
		{
			if(layout == null) return;

			TextStyle style = (TextStyle)layout.Style;
			if(style.BackColor != null)
				page.AddPath(layout.Bounds.Points, 0, null, Convert.Color(style.BackColor));

			//	Draw the border after the background so that the background doesn't
			//	overwrite the border. But draw the border before the content so that
			//	if for any reason the text exceeds the bounds (which should be impossible)
			//	then at least the text will still be visible.
			RenderBorder(layout.Bounds, style.Border, page);

			foreach(LineDraft line in layout.Lines)
			{
				foreach(StrokeDraft stroke in line.Strokes)
				{
					string text = stroke.Stroke.Text;
					int x = stroke.Position.X;
					int y = stroke.Position.Y;
					Demon.Report.Style.Font font = stroke.Stroke.Format.Font;
					Demon.PDF.Color color = Convert.Color(stroke.Stroke.Format.Color);
					page.AddText(text, x, y, font.FamilyName, font.Bold, font.Italic, font.Size, color);

					if(font.Underline)
					{
						Demon.Font.Font strokeFont = _generator.GetFont(font);
						int lineY = y + strokeFont.GetUnderlinePosition(font.Size);
						float thickness = strokeFont.GetUnderlineThickness(font.Size);

						List<Position> points = new List<Position>();
						points.Add(new Position(x, lineY));
						points.Add(new Position(x + stroke.Stroke.Width, lineY));
						
						page.AddPath(points, thickness, color, null);
					}
				}
			}
		}

		private void RenderPhotoTableLayout(PhotoTableLayout table, Page page)
		{
			foreach(PhotoRowLayout row in table.SubLayouts)
				RenderPhotoRowLayout(row, page);

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

				Demon.PDF.Color color = Convert.Color(border.Color);
				foreach(List<Position> line in lines)
					page.AddPath(line, border.Thickness, color, null);
				//TODO: draw all lines in a single PDF path?
			}
		}

		private void RenderPhotoRowLayout(PhotoRowLayout layout, Page page)
		{
			for(int x = 0; x < layout.NumPhotos; ++x)
			{
				//	There's always a photo...
				PhotoLayout photoCell = (PhotoLayout)layout.PhotoRow.GetSubLayoutAtIndex(x).GetSubLayoutAtIndex(0);
				RenderPhotoLayout(photoCell, page);

				//	but not always a caption
				TableCellLayout captionCell = (TableCellLayout)layout.CaptionRow.GetSubLayoutAtIndex(x);
				if(captionCell.NumSubLayouts > 0)
				{
					TextLayout caption = (TextLayout)captionCell.GetSubLayoutAtIndex(0);
					RenderTextLayout(caption, page);
				}
			}
		}

		private void RenderPhotoLayout(PhotoLayout layout, Page page)
		{
			Demon.PDF.Rectangle bounds = Convert.Rectangle(layout.PhotoBounds); // bounds of the photo within the layout
			page.AddImage(layout.PhotoData.Size, layout.PhotoData.RawData, 8, bounds);
		}

		private void RenderListLayout(ListLayout layout, Page page)
		{
			//	Draw the border before the content so that
			//	if for any reason the text exceeds the bounds (which should be impossible)
			//	then at least the text will still be visible.
			ListStyle style = (ListStyle)layout.Style;
			RenderBorder(layout.Bounds, style.Border, page);

			foreach(ListItemLayout item in layout.SubLayouts)
				RenderListItemLayout(item, page);
		}

		private void RenderListItemLayout(ListItemLayout layout, Page page)
		{
			Render(layout.BulletLayout, page);
			Render(layout.ContentLayout, page);
		}

		private void RenderTableLayout(TableLayout table, Page page)
		{
			//	Draw the table content first, before the borders, so that if any
			//	row or cell has a background color then that color won't overwrite
			//	the border. We don't do header rows in PDF because the
			//	generator has already taken care of them and explicitly repeated rows
			//	as necessary.
			foreach(TableRowLayout row in table.SubLayouts)
				Render(row, page);

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
					
				Demon.PDF.Color color = Convert.Color(border.Color);
				foreach(List<Position> line in lines)
					page.AddPath(line, border.Thickness, color, null);
				//TODO: draw all lines in a single PDF path?
			}
		}

		private void RenderTableRowLayout(TableRowLayout layout, Page page)
		{
			TableRowStyle style = (TableRowStyle)layout.Style;
			if(style.BackColor != null)
				page.AddPath(layout.Bounds.Points, 0, null, Convert.Color(style.BackColor));

			foreach(TableCellLayout cell in layout.Cells)
				RenderTableCellLayout(cell, page);
		}

		private void RenderTableCellLayout(TableCellLayout layout, Page page)
		{
			TableCellStyle style = (TableCellStyle)layout.Style;
//			if(style.BackColor != null)
//				Fill(layout.Bounds, style., page); TODO: why doesn't TableCellStyle have BackColor?

			foreach(Layout child in layout.SubLayouts)
				Render(child, page);
		}

		private void RenderPictureLayout(PictureLayout layout, Page page)
		{
			Demon.PDF.Rectangle bounds = Convert.Rectangle(layout.ImageBounds); // bounds of the image within the layout
			page.AddImage(layout.Image.Size, layout.Image.RawData, 8, bounds);
		}

		private void RenderFixedPicture(FixedPicture picture, Page page)
		{
			Demon.PDF.Rectangle bounds = Convert.Rectangle(picture.Bounds);
			page.AddImage(picture.Image.Size, picture.Image.RawData, 8, bounds);
		}

		/// <summary>
		/// Draw a simple outer border. Ignore any inner parts.
		/// </summary>
		private void RenderBorder(Demon.Report.Types.Rectangle rect, Border style, Page page)
		{
			if(style == null) return;

			BorderPart parts = style.Parts;
			Demon.PDF.Color color = Convert.Color(style.Color);

			if((parts & BorderPart.Left) != 0)
			{
				List<Position> path = new List<Position>();
				path.Add(new Position(rect.Left,rect.Top));
				path.Add(new Position(rect.Left,rect.Bottom));
				page.AddPath(path, style.Thickness, color, null);
			}
			if((parts & BorderPart.Bottom) != 0)
			{
				List<Position> path = new List<Position>();
				path.Add(new Position(rect.Left,rect.Bottom));
				path.Add(new Position(rect.Right,rect.Bottom));
				page.AddPath(path, style.Thickness, color, null);
			}
			if((parts & BorderPart.Right) != 0)
			{
				List<Position> path = new List<Position>();
				path.Add(new Position(rect.Right,rect.Top));
				path.Add(new Position(rect.Right,rect.Bottom));
				page.AddPath(path, style.Thickness, color, null);
			}
			if((parts & BorderPart.Top) != 0)
			{
				List<Position> path = new List<Position>();
				path.Add(new Position(rect.Left,rect.Top));
				path.Add(new Position(rect.Right,rect.Top));
				page.AddPath(path, style.Thickness, color, null);
			}
		}

		private void DrawBleedMarks(
			Demon.PDF.Rectangle mediaBox,
			Demon.PDF.Rectangle bodyBox,
			Demon.PDF.Rectangle headerBox,
			Demon.PDF.Rectangle footerBox,
			Page page)
		{
			Demon.PDF.Color color = new Demon.PDF.Color { Red = 0.75f, Green = 0.75f, Blue = 0.75f };

			List<Position> path = new List<Position>();
			Position p1 = new Position(bodyBox.Left - 20, bodyBox.Top +  1);
			Position p2 = new Position(bodyBox.Left -  1, bodyBox.Top +  1);
			Position p3 = new Position(bodyBox.Left -  1, bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			page.AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(bodyBox.Right + 20, bodyBox.Top  + 1);
			p2 = new Position(bodyBox.Right +  1, bodyBox.Top +  1);
			p3 = new Position(bodyBox.Right +  1, bodyBox.Top + 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			page.AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(bodyBox.Left - 20, bodyBox.Bottom -  1);
			p2 = new Position(bodyBox.Left -  1, bodyBox.Bottom -  1);
			p3 = new Position(bodyBox.Left -  1, bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			page.AddPath(path, 1, color, null);

			path.Clear();
			p1 = new Position(bodyBox.Right + 20, bodyBox.Bottom -  1);
			p2 = new Position(bodyBox.Right +  1, bodyBox.Bottom -  1);
			p3 = new Position(bodyBox.Right +  1, bodyBox.Bottom - 20);
			path.Add(p1);
			path.Add(p2);
			path.Add(p3);
			page.AddPath(path, 1, color, null);
		}

		private void DrawRules(
			Demon.PDF.Rectangle mediaBox,
			Demon.PDF.Rectangle bodyBox,
			Demon.PDF.Rectangle headerBox,
			Demon.PDF.Rectangle footerBox,
			Page page)
		{
			int fontSize = 8;
			int fontHeight = _rulesFont.GetCapHeight(fontSize);

			Demon.PDF.Color color = new Demon.PDF.Color { Red = 0.75f, Green = 0.75f, Blue = 0.75f };

			List<Position> path = new List<Position>();
			Position p1;
			Position p2;

			//	Side media box rule (left)
			for(int x = 0; x < mediaBox.Height; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left + 12, mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);

					string text = x.ToString();
					int tx = p2.X + 2;
					int ty = p2.Y - (fontHeight / 2);
					page.AddText(text, tx, ty, _rulesFont.FamilyName, false, false, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left + 12, mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Left,      mediaBox.Bottom + x);
					p2 = new Position(mediaBox.Left +  5, mediaBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
			}

			//	Bottom media box rule (bottom)
			for(int x = 0; x < mediaBox.Width; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom + 12);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, fontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y + 2;
					page.AddText(text, tx, ty, _rulesFont.FamilyName, false, false, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom + 12);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Left + x, mediaBox.Bottom     );
					p2 = new Position(mediaBox.Left + x, mediaBox.Bottom +  5);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
			}

			//	Side body box rule (right)
			int start = mediaBox.Bottom - bodyBox.Bottom; // negative start to put zero at the body box bottom
			start = (start / 10) * 10;
			for(int x = start; x < mediaBox.Height; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right - 12, bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, fontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - 2 - textLen;
					int ty = p2.Y - (fontHeight / 2);
					page.AddText(text, tx, ty, _rulesFont.FamilyName, false, false, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right - 12, bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(mediaBox.Right,      bodyBox.Bottom + x);
					p2 = new Position(mediaBox.Right -  5, bodyBox.Bottom + x);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
			}

			//	Bottom body box rule (top)
			start = mediaBox.Left - bodyBox.Left; // negative start to put zero at the body box left
			start = (start / 10) * 10;
			for(int x = start; x < mediaBox.Width; x += 10)
			{
				path.Clear();

				if(x % 100 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top - 12);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);

					string text = x.ToString();
					int textLen = _rulesFont.GetTextLength(text, 0, text.Length, fontSize) / _rulesFont.UnitsPerEm;
					int tx = p2.X - (textLen / 2);
					int ty = p2.Y - 2 - fontHeight;
					page.AddText(text, tx, ty, _rulesFont.FamilyName, false, false, fontSize, color);
				}
				else if(x % 50 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top - 12);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
				else if(x % 10 == 0)
				{
					p1 = new Position(bodyBox.Left + x, mediaBox.Top     );
					p2 = new Position(bodyBox.Left + x, mediaBox.Top -  5);
					path.Add(p1);
					path.Add(p2);
					page.AddPath(path, 1, color, null);
				}
			}
		}

		private static class Convert
		{
			public static Demon.PDF.Rectangle Rectangle(Demon.Report.Types.Rectangle rect)
			{
				return new Demon.PDF.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top);
			}

			public static Demon.PDF.Color Color(Demon.Report.Style.Color color)
			{
				if(color == null) return null;

				return new Demon.PDF.Color(color.Red, color.Green, color.Blue);
			}
		}
	}
}
