using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using s = Demon.Report.Style;
using t = Demon.Report.Types;

namespace Demon.Word
{
	public interface ITable : IParagraphContent
	{
		ITableRow AddRow(s.TableRowStyle style, t.TrackingInfo trackingInfo);
	}

	public interface ITableRow
	{
		ITableCell AddCell(int columnSpan, s.TableCellStyle style, t.TrackingInfo trackingInfo);
	}

	public interface ITableCell : IContentContainer
	{
	}

	internal class Table : ITable, IBlockContent
	{
		private int _width;
		private int[] _columnWidths;
		private s.TableStyle _style;
		private List<TableRow> _rows;
		private t.TrackingInfo _trackingInfo;

		public Table(int width, int[] columnWidths, s.TableStyle style, t.TrackingInfo trackingInfo)
		{
			_width = width;
			_columnWidths = columnWidths;
			_style = style;
			_rows = new List<TableRow>();
			_trackingInfo = trackingInfo;
		}

		public ITableRow AddRow(s.TableRowStyle style, t.TrackingInfo trackingInfo)
		{
			TableRow row = new TableRow(style, trackingInfo);
			_rows.Add(row);
			return row;
		}

		int GetNumbering(s.BulletStyle style)
		{
			throw new NotImplementedException(); //TODO: refactor so we don't have to do this
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement table = new XElement(ns + "tbl");
			parent.Add(table);

			XElement props = new XElement(ns + "tblPr");
			table.Add(props);

			XElement algorithm = new XElement(
				ns + "tblLayout",
				new XAttribute(ns + "type", "fixed"));
			props.Add(algorithm);

			XElement width = StyleHelpers.CreateWidth(ns, "tblW", _width);
			props.Add(width);

			XElement grid = new XElement(ns + "tblGrid");
			props.Add(grid);

			foreach(int columnWidth in _columnWidths)
			{
				XElement column = new XElement(
					ns + "gridCol",
					new XAttribute(ns + "w", columnWidth));
				grid.Add(column);
			}

			XElement border = StyleHelpers.CreateBorder(ns, "tblBorders", _style?.Border, _style?.Padding);
			if(border != null)
				props.Add(border);

			foreach(TableRow row in _rows)
				row.Write(table);
		}
	}

	internal class TableRow : ITableRow
	{
		private List<TableCell> _cells;
		private t.TrackingInfo _trackingInfo;

		public TableRow(s.TableRowStyle style, t.TrackingInfo trackingInfo)
		{
			_cells = new List<TableCell>();
			_trackingInfo = trackingInfo;
		}

		public ITableCell AddCell(int columnSpan, s.TableCellStyle style, t.TrackingInfo trackingInfo)
		{
			TableCell cell = new TableCell(columnSpan, style, trackingInfo);
			_cells.Add(cell);
			return cell;
		}

		public void Write(XElement table)
		{
			XNamespace ns = table.Name.Namespace;

			XElement row = new XElement(ns + "tr");
			table.Add(row);

			XElement props = new XElement(ns + "trPr");
			row.Add(props);

			foreach(TableCell cell in _cells)
				cell.Write(row);
		}
	}

	internal class TableCell : ITableCell
	{
		private int _columnSpan;
		private s.TableCellStyle _style;
		private List<IBlockContent> _content;
		private t.TrackingInfo _trackingInfo;

		public TableCell(int columnSpan, s.TableCellStyle style, t.TrackingInfo trackingInfo)
		{
			_columnSpan = columnSpan;
			_style = style;
			_trackingInfo = trackingInfo;
			_content = new List<IBlockContent>();
		}

		public IParagraph AddParagraph(s.TextStyle style, t.TrackingInfo trackingInfo)
		{
			Paragraph paragraph = new Paragraph(style, trackingInfo);
			_content.Add(paragraph);
			return paragraph;
		}

		public ITable AddTable(int width, int[] columnWidths, s.TableStyle style, t.TrackingInfo trackingInfo)
		{
			Table table = new Table(width, columnWidths, style, trackingInfo);
			_content.Add(table);
			return table;
		}

		public int AddNumberingStyle(s.BulletStyle style, int level, string bulletText)
		{
			throw new NotImplementedException(); //TODO: refactor so we don't have to do this
		}

		public int AddNumberingInstance(int abstractNumberingDefinitionId)
		{
			throw new NotImplementedException(); //TODO: refactor so we don't have to do this
		}

		public void Write(XElement parent)
		{
			XNamespace ns = parent.Name.Namespace;

			XElement cell = new XElement(ns + "tc");
			parent.Add(cell);

			XElement props = new XElement(ns + "tcPr");
			cell.Add(props);

			XElement span = new XElement(
				ns + "gridSpan",
				new XAttribute(ns + "val", _columnSpan));
			props.Add(span);

			XElement margin = StyleHelpers.CreateMargin(ns, "tcMar", _style?.Padding);
			if(margin != null)
				props.Add(margin);

			//	The last child of a cell must be a paragraph. ECMA-376 Part 1, section
			//	17.4.65 on page 457, says that
			//
			//		"a table cell can contain any block-level content, which allows for
			//		the nesting of paragraphs and tables within table cells. If a table
			//		cell does not include at least one block-level element, then this
			//		document shall be considered corrupt."
			//
			//	This doesn't seem to say that the last child must be a paragraph, but if
			//	it's not then Word reports this error:
			//
			//		"Ambiguous cell mapping encountered. Possible missing paragraph
			//		element. <p> elements are required before very </tc>."
			if((_content.Count == 0) || !(_content[_content.Count - 1] is Paragraph))
			{
//				Paragraph p = new Paragraph(new Report.Style.TextStyle());
//				p.AddRun("hello", new Report.Style.Font { FamilyName="courier new", Size=10, Bold=true}, new Report.Style.Color{Red=1, Green=0,Blue=0});
//				_content.Add(p);
				AddParagraph(new s.TextStyle(), t.TrackingInfo.None);
			}

			foreach(IBlockContent content in _content)
				content.Write(cell);
		}
	}
}
