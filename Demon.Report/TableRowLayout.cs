using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Path;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class TableRowLayout : Layout
	{
		private List<TableCellLayout> _cells;
		private int[] _cellWidths;
		private TableRowStyle _style;

		public IEnumerable<TableCellLayout> Cells { get { return _cells; }}
		public override LayoutType LayoutType { get {return LayoutType.TableRow;} }
		public override IStyle Style { get { return _style; }}
		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}
		public override Color BackgroundColor { get { return _style?.BackColor; }}


		public TableRowLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public TableRowLayout(TableRowLayout src)
			:base(src)
		{
			_style = src._style;
		}

		/// <summary>
		/// Constructor used for rows in photo tables.
		/// </summary>
		public TableRowLayout(Generator generator, TableRowStyle style, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			//	A row layout created as part of a photo table doesn't have its
			//	own conditions because it's not defined in the design file. Any
			//	conditions governing the inclusion of a photo are evaluated in
			//	the photo table layout when it loads its content. So static
			//	conditions are implicitly satisfied here.
			_staticConditionsSatisfied = true;

			_style = style;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_style = _generator.ReportDesign.LoadStyle<TableRowStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultTableRowStyle;
		}

		protected override List<Reference> ResolveMany(Reference context)
		{
			//	If our source reference is to a list then return all of its elements
			//	to cause us to be duplicated for each element. Otherwise call the
			//	base class implementation for the normal processing, which is to
			//	repeat ourself for all instances of our source reference.
			List<Reference> resolvedReferences = null;
			switch(_sourcePath.TargetType)
			{
				case ContentSourceType.MultiSelect:
				case ContentSourceType.SingleSelect:
				case ContentSourceType.CalculationList:
					//	Resolve our source path into a concrete list object and then
					//	get the items in that list
					resolvedReferences = new List<Reference>();
					List<Reference> lists = base.ResolveMany(context);
					foreach(Reference list in lists)
					{
						List<Reference> items = _generator.Resolver.GetChildren(list);
						resolvedReferences.AddRange(items);
					}
					break;

				default:
					resolvedReferences = base.ResolveMany(context);
					break;
			}
			return resolvedReferences;
		}

		private bool SourceIsChecked()
		{
			//	If we have a checkable source type and it's checked then
			//	include ourself. Otherwise delete ourself. We can only
			//	determine whether our source is checked after resolving it,
			//	so resolve it now if necessary.
			switch(_sourcePath.TargetType)
			{
				case ContentSourceType.Checkbox:
				{
					//	Get the checkbox. There should be zero or one.
					List<Checkbox> checkboxes = ResolveMany<Checkbox>();
					switch(checkboxes.Count)
					{
						case 0: return false;
						case 1: return checkboxes[0].State != 0;
						default:
							throw new PathException(
								$"{_sourcePath.Target} appears {checkboxes.Count} times in this context.",
								_sourcePath.ToString(), ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
					}
				}
				
				case ContentSourceType.RadioButton:
				{
					//	Get the radio button. There should be zero or one.
					List<RadioButton> radios = ResolveMany<RadioButton>();
					switch(radios.Count)
					{
						case 0: return false;
						case 1: return radios[0].State != 0;
						default:
							throw new PathException(
								$"{_sourcePath.Target} appears {radios.Count} times in this context.",
								_sourcePath.ToString(), ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
					}
				}

				case ContentSourceType.Calculation:
				{
					//	Get the radio button. There should be zero or one.
					List<Calculation> calculations = ResolveMany<Calculation>();
					switch(calculations.Count)
					{
						case 0: return false;
						case 1: return calculations[0].State != 0;
						default:
							throw new PathException(
								$"{_sourcePath.Target} appears {calculations.Count} times in this context.",
								_sourcePath.ToString(), ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
					}
				}
			}
			return false;
		}

		public override void RemoveEmptyLayouts()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start");

				//	Recurse down to the bottom and then remove empty
				//	layouts on the way back up
				foreach(Layout layout in _subLayouts)
					layout.RemoveEmptyLayouts();

				//	We don't actually remove empty cells because that would alter
				//	the table structure. If all cells are empty then IsEmpty will
				//	return true, and so the row can be removed by the table.

				Trace("end");
			}
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				//	Remember the original bounds
				_bounds = new Rectangle(bounds);

				//	Apply row padding
				//TODO: get rid of table row padding because it can interfere with
				//the uniformity of table columns?
				bounds.Left  += _style?.Padding?.Left  ?? 0;
				bounds.Right -= _style?.Padding?.Right ?? 0;
				bounds.Top   -= _style?.Padding?.Top   ?? 0;

				GetCells();

				int columnLeft = bounds.Left;
				int columnRight = columnLeft;
				int greatestCellHeight = 0;
				foreach(TableCellLayout cell in _cells)
				{
					//	Get the cell bounds
					columnRight += cell.CalculatedWidth;
					Rectangle cellBounds = new Rectangle(columnLeft, bounds.Top, columnRight, bounds.Top);

					//	Write the cell. It will apply its own padding.
					Position pos = cell.Draft(cellBounds);

					//	Advance to the next column
					columnLeft = cellBounds.Right;

					//	If this is the tallest cell so far, set its height as our own
					int height = cellBounds.Top - pos.Y;
					if(height > greatestCellHeight)
						greatestCellHeight = height;
				}

				greatestCellHeight += (_style?.Padding?.Top ?? 0) + (_style?.Padding?.Bottom ?? 0);
				_bounds.Bottom = _bounds.Top - greatestCellHeight;

				HandleEmpty();
				Trace("after first draft _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);

				//	Ensure that our _cells property is set up. When redrafting the lower
				//	half of a split layout, _cells will be null when Redraft is called.
				GetCells();

				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//	Reposition ourself and all of our cells
				_bounds.Top = top;
				int cellTop = _bounds.Top - PaddingTop;
				int lowestBottom = _bounds.Top;
				foreach(TableCellLayout cell in _cells)
				{
					cell.Redraft(cellTop);
					if(cell.Bounds.Bottom < lowestBottom)
						lowestBottom = cell.Bounds.Bottom;
				}
				_bounds.Bottom = lowestBottom - PaddingBottom;

				Trace("end _bounds={0}", _bounds);
			}
		}

		public override PageDisposition AssessPageBreak(Rectangle bodyBox)
		{
			//	We override this method here because a table row's sublayouts
			//	are arranged horizontally, not vertically, and so their
			//	individual dispositions interact with each other. For example,
			//	if one cell wants to split but another refuses, then the row
			//	cannot split. Or if any one cell wants to go on the next page
			//	then the entire row goes on the next page.
			//
			//	Keep-with-next is meaningless in a table row because the next
			//	sublayout is another cell on the same row, and by definition
			//	all cells start on the same page.

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("_bounds={0} bodyBox={1}", _bounds, bodyBox);
				PageDisposition disposition = PageDisposition.ThisPage;

				//	If we've got a new-page rule then put ourself on the next page
				if(_pageBreakRules?.NewPage ?? false)
				{
					Trace("page break rules say new-page, so set new-page disposition");
					disposition = PageDisposition.NewPage;
				}

				if(disposition == PageDisposition.ThisPage)
				{
					//	If we've got a max-position rule then check whether we're past
					//	that position. And if we don't have a max-position rule then
					//	assume a max position of 1.0 so that we don't position ourself
					//	off the bottom of the page.
					float maxPosition = _pageBreakRules?.MaximumPosition ?? 1.0f;
					if(maxPosition > 1.0f) maxPosition = 1.0f;

					//	Express the distance from the top of the body box to the top
					//	of ourself, as a percentage of the body box height
					float position = ((float)(bodyBox.Top - _bounds.Top)) / (float)bodyBox.Height;
					if(position > maxPosition)
					{
						Trace(
							"max-position {0} rule is violated by bounds {1}, so set overflow disposition",
							maxPosition, _bounds);
						disposition = PageDisposition.Overflow;
					}
				}

				//	If it looks like we can start on this page then ask our sublayouts
				//	what they want to do. Even if we think that we can fit entirely on
				//	this page, a sublayout could have rules that cause it to move to the
				//	next page and that would cause us to split.
				//	Min-lines is evaluated on splitting.
				if(disposition == PageDisposition.ThisPage)
				{
					List<PageDisposition> sublayoutDispositions = new List<PageDisposition>();
					foreach(Layout sublayout in _subLayouts)
					{
						PageDisposition sublayoutDisposition = sublayout.AssessPageBreak(bodyBox);
						sublayoutDispositions.Add(sublayoutDisposition);
					}

					//	If any one cell said next-page then put the entire row on the next page
					if(sublayoutDispositions.Contains(PageDisposition.NewPage))
					{
						Trace("at least one cell says next-page, so set new-page disposition");
						disposition = PageDisposition.NewPage;
					}
					else if(sublayoutDispositions.Contains(PageDisposition.Split))
					{
						//	If no cell said next-page, and any one said split, then split.
						//	(If any cell couldn't fit on this page but wasn't prepared to
						//	split then it would have said next-page. So we know that all
						//	cells are content to split.)
						Trace("at least one cell says split, so set split disposition");
						bool canSplit = CanSplit(bodyBox);
						if(canSplit)
						{
							disposition = PageDisposition.Split;
						}
						else
						{
							Trace("need to split but can't, so set overflow disposition");
							_pageSplitIndex = 0;
							disposition = PageDisposition.Overflow;
						}
					}
				}

				Trace("return {0}, split index {1}", disposition, _pageSplitIndex);
				return disposition;
			}
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	Create a copy of ourself and then split each of our cells and move
				//	the lower halves of the cells into our copy. If no cell opts to split
				//	we'll then discard our (empty) copy.
				bool empty = true;
				Layout copy = ShallowCopy();
				foreach(TableCellLayout cell in _cells)
				{
					Layout lower = cell.DoPageBreak();
				
					//	If the cell didn't need to split then create an empty copy
					if(lower == null)
						lower = cell.ShallowCopy();
					else
						empty = false;
			
					copy.AddSubLayout(lower);
				}

				//	If no cell split then return null (to indicate that we're staying
				//	on the page) and discard the empty copy row
				return empty ? null : copy;
			}
		}

		/// <summary>
		/// Bump the page split index from zero to one to avoid infinite
		/// overflow when the first sublayout is too large to fit on the
		/// page.
		/// </summary>
		public override Bump BumpPageSplitIndex()
		{
			if(_subLayouts.Count == 0) return Bump.Impossible;
			//	Note that a table row's own page split index is always zero

			//	A table row's sublayouts are arranged horizontally, not vertically,
			//	and so bumping is different here. We bump every cell.
			List<Bump> bumps = new List<Bump>();
			foreach(Layout cell in _subLayouts)
			{
				Bump bump = cell.BumpPageSplitIndex();
				bumps.Add(bump);
			}

			//	Return the most serious of the individual cell bump results. For
			//	example, if two cells bumped ok but one was impossible, then
			//	treat that as it being impossible to bump this row.
			if(bumps.Contains(Bump.Impossible )) return Bump.Impossible;
			if(bumps.Contains(Bump.Bumped     )) return Bump.Bumped;
			if(bumps.Contains(Bump.Unnecessary)) return Bump.Unnecessary;

			return Bump.Unnecessary;
		}

		public void SetColumnWidths(int[] widths)
		{
			_cellWidths = widths;

			GetCells();
			int columnIndex = 0;
			for(int cellIndex = 0; cellIndex < _cells.Count; ++cellIndex)
			{
				TableCellLayout cell = _cells[cellIndex];
				cell.CalculatedWidth = 0;
				for(int span = 0; span < cell.ColumnSpan; ++span)
				{
					if(columnIndex >= _cellWidths.Length)
						throw new ReportDesignException($"Too many table cells. Table defines {_cellWidths.Length} columns.", this);
					cell.CalculatedWidth += _cellWidths[columnIndex++];
				}
			}
		}

		/// <summary>
		/// Get our sublayouts as TableCellLayouts.
		/// </summary>
		private void GetCells()
		{
			if(_cells != null) return;

			_cells = new List<TableCellLayout>(_subLayouts.Count);
			foreach(Layout layout in _subLayouts)
				_cells.Add((TableCellLayout)layout);
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			//	If we have no rules then anything goes
			if(_pageBreakRules == null)
				return true;

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
			//	4.	Min-lines rule. In a row layout the only values that
			//	make sense here are zero and one. Zero means that we're
			//	happy to split (provided our cells are also happy to do
			//	so) and one means that we are not happy to split (that
			//	is, we must fit this entire one row on the page.)

			if(_pageBreakRules.MinimumLines == 1)
			{
				Trace("cannot split because of min-lines {0} rule", _pageBreakRules.MinimumLines);
				return false;
			}


			//--------------------------------------------------------
			//	No rule was unsatisfied, so we're OK
			return true;
		}

		protected override void Clear()
		{
			base.Clear();
			_cells?.Clear();
		}
	}
}
