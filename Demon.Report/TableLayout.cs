using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class TableLayout : Layout
	{
		protected List<TableRowLayout> _rows;
		protected List<ColumnDefinition> _columnDefinitions;
		private int[] _columnWidths;
		protected int _headerRows;
		protected TableStyle _style;

		public IEnumerable<TableRowLayout> Rows { get { return _rows; }}
		public int NumHeaderRows { get { return _headerRows; }}
		public override LayoutType LayoutType { get {return LayoutType.Table;} }
		public override IStyle Style { get { return _style; }}
		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}


		public TableLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public TableLayout(TableLayout src)
			:base(src)
		{
			_style = src._style;

			if(src._columnDefinitions != null)
			{
				_columnDefinitions = new List<ColumnDefinition>(src._columnDefinitions.Count);
				foreach(ColumnDefinition column in src._columnDefinitions)
					_columnDefinitions.Add(new ColumnDefinition(column));
			}
			_columnWidths = src._columnWidths;
			
			_headerRows = src._headerRows;
		}

		public TableLayout(Generator generator, TableStyle style, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			_style = style;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			LoadColumnDefinitions(root.Element(ns + "ColumnDefinitions"), ns);
			_headerRows = _generator.ReportDesign.LoadInt(root.Attribute("headerRows")) ?? 0;
			_style = _generator.ReportDesign.LoadStyle<TableStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultTableStyle;
		}

		private void LoadColumnDefinitions(XElement collection, XNamespace ns)
		{
			_columnDefinitions = new List<ColumnDefinition>();
			if(collection == null) return;

			foreach(XElement elem in collection.Elements(ns + "ColumnDefinition"))
			{
				float? width = _generator.ReportDesign.LoadFloat(elem.Element(ns + "Width"));
				if(width != null)
					_columnDefinitions.Add(new ColumnDefinition(width.Value));
			}
		}

		public int ColumnCount
		{
			get
			{
				return _columnDefinitions.Count;
			}
		}

		public int[] ColumnWidths
		{
			get
			{
				int[] widths = new int[_columnWidths.Length];
				for(int x = 0; x < widths.Length; ++x)
					widths[x] = _columnWidths[x];
				return widths;
			}
		}

		private void GetColumnWidths()
		{
			//	Work out the column widths. A column's width is specified as a real
			//	number and is interpreted like this:
			//
			//		= 0	Flexible width. Fill any space remaining after other
			//				columns have been sized. If more than one column has
			//				a zero width then the remaining space is shared equally
			//				among them.
			//
			//		> 0	Explicit width. Size the column to exactly this number of
			//				user-space units.
			//
			//		< 0	Relative width. Size the column to this percentage of the
			//				full table width. (Multiply by -1, obviously.)


			//	Convert all relative (negative) widths to user-space units
			//	(and convert to positive while we're at it)
			for(int col = 0; col < _columnDefinitions.Count; ++col)
				if(_columnDefinitions[col].Width < 0.0)
					_columnDefinitions[col].Width *= -_bounds.Width;

			//	Sum all explicit and relative widths
			float sumWidths = 0.0f;
			for(int col = 0; col < _columnDefinitions.Count; ++col)
				sumWidths += _columnDefinitions[col].Width;

			//	Share any left-over space among the flexible-width columns.
			//	Note that there might not be any left-over space, in which
			//	case the flexible columns will be zero-width.
			float leftOver = _bounds.Width - sumWidths;
			if(leftOver > 0.0f)
			{
				float numFlexible = 0.0f;
				for(int col = 0; col < _columnDefinitions.Count; ++col)
					if(_columnDefinitions[col].Width == 0.0f)
						++numFlexible;
				if(numFlexible > 0.0f)
				{
					float flexible = leftOver / numFlexible;
					for(int col = 0; col < _columnDefinitions.Count; ++col)
						if(_columnDefinitions[col].Width == 0.0f)
							_columnDefinitions[col].Width = flexible;
				}
			}

			//	Now that we've converted all widths to explicit widths,
			//	scale them all again if the sum exceeds our bounds.
			sumWidths = 0.0f;
			for(int col = 0; col < _columnDefinitions.Count; ++col)
				sumWidths += _columnDefinitions[col].Width;
			if(sumWidths > _bounds.Width)
			{
				float scale = _bounds.Width / sumWidths;
				for(int col = 0; col < _columnDefinitions.Count; ++col)
					_columnDefinitions[col].Width *= scale;
			}

			//	Convert from float to int
			_columnWidths = new int[_columnDefinitions.Count];
			for(int x = 0; x < _columnDefinitions.Count; ++x)
				_columnWidths[x] = (int)System.Math.Round(_columnDefinitions[x].Width);
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				//	Get our sublayouts as rows
				GetRows();

				//	Remember the original bounds
				_bounds = new Rectangle(bounds);

				GetColumnWidths();
				foreach(TableRowLayout row in _rows)
					row.SetColumnWidths(_columnWidths);

				//	Apply table padding
				bounds.Left  += _style?.Padding?.Left  ?? 0;
				bounds.Right -= _style?.Padding?.Right ?? 0;
				bounds.Top   -= _style?.Padding?.Top   ?? 0;

				Position pos = bounds.TopLeft;

				//	Draft the rows
				foreach(TableRowLayout row in _rows)
				{
					pos = row.Draft(bounds);
					bounds.Bottom = pos.Y;
					bounds.Top = bounds.Bottom;
				}

				pos.Y -= _style?.Padding?.Bottom ?? 0;
				_bounds.Bottom = pos.Y;

				HandleEmpty();
				Trace("end _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);

				if (IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				base.Redraft(top);

				//	Set our bottom to match our last row
				if(_subLayouts.Count > 0)
					_bounds.Bottom = _subLayouts.Last().Bounds.Bottom - PaddingBottom;
				else
					_bounds.Bottom = _bounds.Top;
				Trace("end _bounds={0}", _bounds);
			}
		}

		protected override PageDisposition SetPageSplitIndex(Rectangle bodyBox, ref bool honourKeepWithNext)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				PageDisposition disposition = base.SetPageSplitIndex(bodyBox, ref honourKeepWithNext);

				//	Don't split the header rows
				if(_pageSplitIndex < _headerRows)
					_pageSplitIndex = _headerRows;

				return disposition;
			}
		}

		/// <summary>
		/// Bump the page split index from zero to one to avoid infinite
		/// overflow when the first line is too large to fit on the
		/// page.
		/// </summary>
		public override Bump BumpPageSplitIndex()
		{
			//	For most layouts the index to bump from is zero, but in a table
			//	the zero-th row is the first one after any header rows
			int bumpFrom = _headerRows;

			if(_subLayouts.Count <= bumpFrom) return Bump.Impossible;
			if(_pageSplitIndex > bumpFrom) return Bump.Unnecessary;

			Layout splitLayout = _subLayouts[_pageSplitIndex];
			Bump bump = splitLayout.BumpPageSplitIndex();
			if(bump == Bump.Impossible)
			{
				//	Not able to bump the sublayout, but maybe we can bump ourself
				if(_subLayouts.Count > 1)
				{
					++_pageSplitIndex;
					bump = Bump.Bumped;
				}
			}
			return bump;
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	Do the normal page break
				TableLayout overflow = (TableLayout)base.DoPageBreak();

				//	The normal page break handling will have copied or moved
				//	zero or more rows to the overflow layout. Header rows must
				//	be repeated in the overflow, so ensure that it's got them.
				if(overflow != null)
				{
					//	CanSplit ensures that the page break doesn't separate the
					//	header rows - the overflow either has them all or it has
					//	none. If it has them all then we no longer have them. If
					//	it has none then copy all of our header rows into the start
					//	of the overflow.
					if(_subLayouts.Count > 0)
					{
						List<Layout> headers = _subLayouts.GetRange(0,_headerRows);
						for(int x = 0; x < headers.Count; ++x)
						{
							Layout header = headers[x];
							Layout copy = header.DeepCopy();
							overflow.InsertSubLayout(x,copy);
						}
					}
				}

				return overflow;
			}
		}

		/// <summary>
		/// Get our sublayouts as rows.
		/// </summary>
		protected void GetRows()
		{
			//	If we already have a _rows collection, throw it away and rebuild it.
			//	Our sublayouts collection may have been truncated by a page break.
			_rows = new List<TableRowLayout>(_subLayouts.Count);
			foreach(Layout layout in _subLayouts)
				_rows.Add((TableRowLayout)layout);
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
//				//	If we have no rules then anything goes
//				if(_pageBreakRules == null)
//					return true;

	#if false // new-page rule not applicable when splitting
				//--------------------------------------------------------
				//	1.	New-page rule. If we're at the top of the page then we're OK.
				if(rules.NewPage != 0)
				{
					if(_bounds.Top < pageLayout.BodyBox.Top)
						return false;
				}
	#endif


				//--------------------------------------------------------
				//	2.	Keep-with-next rule


	#if false // max-position rule not applicable when splitting
				//--------------------------------------------------------
				//	3.	Max-position rule

				//	Our current position is an offset from the bottom of the page, not
				//	from the bottom of the body box, so subtract the body box bottom
				//	to recalibrate the calculations to start at zero.
				int y = _bounds.Top - pageLayout.BodyBox.Bottom;

				//	Our position is measured from the bottom, but the rule is expressed
				//	in terms where zero is at the top, so invert position.
				y = pageLayout.BodyBox.Height - y;

				//	Convert the position to a percentage
				float percent = (float)y / (float)pageLayout.BodyBox.Height;

				//	Now check the rule
				if(percent > rules.MaximumPosition)
					return false;
	#endif


				//--------------------------------------------------------
				//	4.	Min-lines rule
			
				int minLines = _pageBreakRules?.MinimumLines ?? 0;

				//	If the rule specifies fewer rows than our header rows plus one,
				//	then use that larger number. We want to keep our header rows
				//	together, and there's no point in headers without at least
				//	one data row.
				if(minLines < _headerRows + 1)
					minLines = _headerRows + 1;

				//	If we don't have that many rows then just take what we've got
				GetRows();
				if(minLines > _rows.Count)
					minLines = _rows.Count;

				if(_pageSplitIndex < minLines)
				{
					Trace("split index {0} breaks min-lines {1} rule", _pageSplitIndex, minLines);
					return false;
				}


				//--------------------------------------------------------
				//	No rule was unsatisfied, so we're OK
				return true;
			}
		}

		/// <summary>
		/// If the table contains only header rows then it's empty.
		/// </summary>
		public override bool IsEmpty()
		{
			return _subLayouts.Count <= _headerRows;
		}

		protected override void Clear()
		{
			base.Clear();
			_rows.Clear();
		}

		public class ColumnDefinition
		{
			public float Width;

			public ColumnDefinition(float width)
			{
				Width = width;
			}

			public ColumnDefinition(ColumnDefinition src)
			{
				Width = src.Width;
			}
		}
	}
}
