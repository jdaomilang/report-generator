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
	//	A nested list is introduced by a group layout, which is a single
	//	item in the current list and which contains its own list.
	//
	//	We only support text content in list items - no photos.
	//
	//	So a list item layout's content sublayout is always either
	//	a text layout or a group layout.
	internal class ListLayout : Layout
	{
		private string _emptyText;
		private Layout _whenEmpty;
		private bool _merge;
		private ListStyle _style;
		private ListStyle _emptyStyle;

		public override LayoutType LayoutType { get {return LayoutType.List;} }

		public override IStyle Style { get { return _style; }}

		private static PageBreakRules _defaultListPageBreakRules = new PageBreakRules
		{
			NewPage = false,
			KeepWithNext = false,
			MaximumPosition = 1.0f,
			MinimumLines = 1
		};

		private static PageBreakRules _defaultItemPageBreakRules = new PageBreakRules
		{
			NewPage = false,
			KeepWithNext = false,
			MaximumPosition = 1.0f,
			MinimumLines = 1
		};

		public ListLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public ListLayout(ListLayout src)
			:base(src)
		{
			_emptyText = src._emptyText;
			_whenEmpty = src._whenEmpty?.DeepCopy();
			_merge = src._merge;
			_style = src._style;
			_emptyStyle = src._emptyStyle;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_merge = _generator.ReportDesign.LoadBoolean(root.Attribute("merge")) ?? false;
			_style = _generator.ReportDesign.LoadStyle<ListStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultListStyle;
			_emptyStyle = _generator.ReportDesign.LoadStyle<ListStyle>(root.Element(ns + "EmptyStyle"));
			if (_emptyStyle == null) _emptyStyle = _style;
			_emptyText = _generator.ReportDesign.LoadString(root.Element(ns + "EmptyText"));
		}

		public override void ResolveSublayoutReferences()
		{
			//	Call the base class implementation to resolve any sublayouts
			//	that were declared statically in the design file
			base.ResolveSublayoutReferences();

			//	Create dynamic sublayouts based on our source reference. These
			//	new sublayouts will be resolved as soon as we create them
			//	because we'll be passing resolved references to them.
			List<Reference> items = new List<Reference>();
			switch(_sourcePath.TargetType)
			{
				case ContentSourceType.Checkbox:
				case ContentSourceType.RadioButton:
				case ContentSourceType.TextEntry:
				case ContentSourceType.Calculation:
				case ContentSourceType.Section:
					items.Add(_sourceObject);
					break;

				case ContentSourceType.MultiSelect:
				case ContentSourceType.SingleSelect:
				case ContentSourceType.CalculationList:
					//	Include all the items in the source list
					List<Reference> children = _generator.Resolver.GetChildren(_sourceObject);
					items.AddRange(children);
					break;
			}
			//	We want the dynamic sublayouts based on our source reference to appear
			//	first in the list, before static layouts defined in the design file.
			//	The static layouts have already been added by the call to the base
			//	class implementation of this method (which must be called before we
			//	do our own stuff here, because it clears _subLayouts) and so we want
			//	to start inserting the dynamic layouts at index zero. To preserve the
			//	internal order of those dynamic layouts, we increment the insert
			//	index for each one.
			int insertIndex = 0;
			foreach(Reference item in items)
			{
				bool satisfies = _conditions.SatisfiesContentConditions(item);
				if(!satisfies) continue;

				TextLayout layout = new TextLayout(item, _style.ItemStyle, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				InsertSubLayout(insertIndex++, layout);
			}

			//TODO: For calculations, which have complex structure, a complex rendition
			//comprised of any of the calculation's parts - caption, value and unit of
			//measure - all in a single list item. For example, "price : $10.00" or
			//"size = 30sq.m.". I think this is easy enough to do in a text layout,
			//and we could embed a text layout in the list, but then how would we repeat
			//the text layout for all calculations that the source reference resolves to?
			//Do we need to add a "pattern" attribute to ListLayout? Something like
			//	<ListLayout source="CalculationList:1234[...]" pattern="!Caption = !Value!UnitOfMeasure">
			//See Jira ticket DEMON-268.

			//	Merge sublists into this list
			MergeSubLists();

			//	Pass on our static conditions to our items so that they can
			//	apply them when they load their content
			foreach(Layout layout in _subLayouts)
				layout.AddConditions(_conditions);

			//	Wrap our sublayouts in list items
			List<Layout> wrapped = WrapSublayouts(_subLayouts);

			//	Set ourself as the container on each of the wrappers. Creating the
			//	wrappers has automatically made each wrapper the container of its
			//	content layout, replacing us. Making ourself the container of the
			//	wrappers keeps the container hierarchy intact. The hierarchy must
			//	be maintained so that reference resolution can have correct context.
			_subLayouts.Clear();
			foreach(Layout sublayout in wrapped)
				AddSubLayout(sublayout);

			//	Prepare our empty text in case we need to use it later. Even if we have
			//	some items now, after applying conditions we may be left with none,
			//	in which case we'll need our empty text.
			if(!string.IsNullOrWhiteSpace(_emptyText))
			{
				ListStyle style = _emptyStyle ?? _style;
				_whenEmpty = new TextLayout(_emptyText, style.ItemStyle, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				List<Layout> unwrapped = new List<Layout>();
				unwrapped.Add(_whenEmpty);
				wrapped = WrapSublayouts(unwrapped);
				_whenEmpty = wrapped[0];
			}

			//	Use our empty layout right away if we're empty
			if((_subLayouts.Count == 0) && (_whenEmpty != null))
				AddSubLayout(_whenEmpty);
		}

		/// <summary>
		/// If any sublayout is a list layout with its "merge" property set, then
		/// replace that layout in this list with its individual elements.
		/// </summary>
		private void MergeSubLists()
		{
			List<Layout> afterMerge = new List<Layout>();

			foreach(Layout layout in _subLayouts)
			{
				ListLayout sublist = layout as ListLayout;
				if(sublist != null && sublist._merge)
				{
					//	Recursively merge any sublists of the sublist
					sublist.MergeSubLists();

					//	Add the sublist's items to our new list
					afterMerge.AddRange(sublist._subLayouts);
				}
				else
				{
					//	Add the layout to our new list, unchanged
					afterMerge.Add(layout);
				}
			}

			_subLayouts.Clear();
			foreach(Layout layout in afterMerge)
				AddSubLayout(layout);
		}

		/// <summary>
		/// Wrap all our sublayouts in ListItemLayouts.
		/// </summary>
		private List<Layout> WrapSublayouts(List<Layout> unwrapped)
		{
			List<Layout> wrapped = new List<Layout>();
			int number = _style.BulletStyle.StartAt;
			foreach(Layout layout in unwrapped)
			{
				ListItemLayout item = null;
				if(layout is ListItemLayout)
					item = (ListItemLayout)layout; // already wrapped
				else
					item = new ListItemLayout(layout, _style, number++,
						_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition, _traceContext);
				wrapped.Add(item);
			}
			return wrapped;
		}
			
		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", _bounds);

				//	A list layout must have a style, with embedded bullet style and
				//	item style complete with fonts for those two styles. All other
				//	style elements, such as the selected/unselected bullet styles,
				//	colors and borders and paddings, can be null.
				if(_style == null)
					throw new InvalidOperationException($"ListLayout style is null at {_trackingInfo}.");
				if(_style.BulletStyle == null)
					throw new InvalidOperationException($"ListLayout bullet style is null at {_trackingInfo}.");
				if(_style.BulletStyle.Font == null)
					throw new InvalidOperationException($"ListLayout bullet font is null at {_trackingInfo}.");
				if(_style.ItemStyle == null)
					throw new InvalidOperationException($"ListLayout item style is null at {_trackingInfo}.");
				if(_style.ItemStyle.Font == null)
					throw new InvalidOperationException($"ListLayout item font is null at {_trackingInfo}.");

				//	Remember the original bounds, before we apply our own padding
				_bounds = new Rectangle(bounds);

				//	Apply our own padding within the given bounds
				bounds.Left  += _style.Padding?.Left  ?? 0;
				bounds.Right -= _style.Padding?.Right ?? 0;
				bounds.Top   -= _style.Padding?.Top   ?? 0;

				//	Create our own drawing position
				Rectangle itemBounds = new Rectangle(bounds);
				Position itemPos = itemBounds.TopLeft;
//			itemPos.Y -= _style.ItemStyle.Padding?.Top ?? 0;

				//	Lay out the items
				foreach(Layout item in _subLayouts)
				{
					itemBounds.Top = itemPos.Y;
					itemBounds.Bottom = itemBounds.Top;
					itemPos = item.Draft(itemBounds);
				}

				//	Advance by the list padding
				itemPos.Y -= _style.Padding?.Bottom ?? 0;
				_bounds.Bottom = itemPos.Y;

				//	Draft our when-empty layout in a copy of the bounds that we drafted our
				//	normal content in
				if(_whenEmpty != null)
				{
					Rectangle whenEmptyBounds = new Rectangle(bounds);
					_whenEmpty.Draft(whenEmptyBounds);
				}

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

				//	If we're empty and we have a special when-empty layout then use it
				if(IsEmpty() && (_whenEmpty != null))
				{
					_subLayouts.Clear();
					AddSubLayout(_whenEmpty);
				}

				//	If we have no content then there's nothing to do
				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//TODO: handle sublayouts

				//	Reposition all of our items to fit with our new position,
				//	maintaining the inter-item spacing.
				//	Insert our top padding before the first item, and our
				//	bottom padding after the last.
				int pos = top - (_style.Padding?.Top ?? 0);
				foreach(Layout layout in _subLayouts)
				{
					layout.Redraft(pos);
					pos = layout.Bounds.Bottom;
				}
				pos -= _style.Padding?.Bottom ?? 0;

				//	Reposition ourself
				_bounds.Top = top;
				_bounds.Bottom = pos;
				Trace("end _bounds={0}", _bounds);
			}
		}

		public override void RemoveEmptyLayouts()
		{
			base.RemoveEmptyLayouts();

			//	Renumber our list items to fill in any gaps left by removed items
			int number = _style.BulletStyle.StartAt;
			foreach(Layout layout in _subLayouts)
			{
				if(!layout.IsEmpty())
				{
					ListItemLayout item = layout as ListItemLayout;
					//	If a list item has been split across a page break then we
					//	only want to renumber its upper half
					if(item.HasBullet)
						item.Renumber(number++);
				}
			}
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			using (new TraceContextPusher(_generator, _traceContext))
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
					return true;
	#endif


				//--------------------------------------------------------
				//	4.	Min-lines rule
				//	In the context of the list as a whole, "minimum lines" means "minimum items"
			
				int minItems = _subLayouts.Count > _pageBreakRules.MinimumLines ? _pageBreakRules.MinimumLines : _subLayouts.Count;
				if(_pageSplitIndex < minItems)
				{
					Trace("split index {0} breaks min-lines {1} rule", _pageSplitIndex, minItems);
					return false;
				}


				//--------------------------------------------------------
				//	No rule was unsatisfied, so we're OK
				return true;
			}
		}

		/// <summary>
		/// Evalute page break rules for the list as a whole
		/// </summary>
		private bool EvaluateListPageBreakRules(Position position, PageLayout pageLayout)
		{
			//	If we're outside the page's body box then we're not body text, and
			//	so we don't apply page break rules
			if(!pageLayout.BodyBox.Contains(position)) return false;

			//	If we're already on a new page then there's nothing to do. Even if
			//	our rules said that we don't have enough room, we can't fix that
			//	by creating another page.
			if(position.Y >= pageLayout.BodyBox.Top) return false;

			//	If we have our own rules then use them, otherwise if the current
			//	page has rules the use them, otherwise use none
			PageBreakRules rules = _pageBreakRules;
			if(rules == null)
				rules = pageLayout.PageBreakRules;
			if(rules == null)
				rules = _defaultListPageBreakRules;


			//--------------------------------------------------------
			//	1.	New-page rule
			if(rules.NewPage)
				return true;


			//--------------------------------------------------------
			//	2.	Keep-with-next rule


			//--------------------------------------------------------
			//	3.	Max-position rule

			//	Our current position is an offset from the bottom of the page, not
			//	from the bottom of the body box, so subtract the body box bottom
			//	to recalibrate the calculations to start at zero.
			int y = position.Y - pageLayout.BodyBox.Bottom;

			//	Our position is measured from the bottom, but the rule is expressed
			//	in terms where zero is at the top, so invert position.
			y = pageLayout.BodyBox.Height - y;

			//	Convert the position to a percentage
			float percent = (float)y / (float)pageLayout.BodyBox.Height;

			//	Now check the rule
			if(percent > rules.MaximumPosition)
				return true;


			//--------------------------------------------------------
			//	4.	Min-lines rule
			//	In the context of the list as a whole, "minimum lines" means "minimum items"
			
			//	Work out the height we need to draw our minimum start lines.
			//	If it's greater than the space available on the page then
			//	start a new page.
//			int minItems = items.Count > rules.MinimumLines ? rules.MinimumLines : items.Count;
//			int minStartHeight = 0;
//			for(int x = 0; x < minItems; ++x)
//				minStartHeight += items[x].ItemHeight;
//			if(position.Y < pageLayout.BodyBox.Bottom + (_style.Padding?.Bottom ?? 0) + (_style.ItemStyle.Padding?.Bottom ?? 0) + minStartHeight)
//				return true;


			//--------------------------------------------------------
			//	No rule said that we need a break, so stay on the current page
			return false;
		}

		/// <summary>
		/// Evalute page break rules for within a single item in the list.
		/// </summary>
		private bool EvaluateItemPageBreakRules(Position position, ListItemLayout item, PageLayout pageLayout)
		{
			//	If we're outside the page's body box then we're not body text, and
			//	so we don't apply page break rules
			if(!pageLayout.BodyBox.Contains(position))
				return false;

			//	If we're already on a new page then there's nothing to do. Even if
			//	our rules said that we don't have enough room, we can't fix that
			//	by creating another page.
			if(position.Y >= pageLayout.BodyBox.Top)
				return false;

			//	If we have our own rules then use them, otherwise if the current
			//	page has rules the use them, otherwise use none
			PageBreakRules rules = item.PageBreakRules;
			if(rules == null)
				rules = pageLayout.PageBreakRules;
			if(rules == null)
				rules = _defaultItemPageBreakRules;


			//--------------------------------------------------------
			//	1.	New-page rule
			if(rules.NewPage)
				return true;


			//--------------------------------------------------------
			//	2.	Keep-with-next rule


			//--------------------------------------------------------
			//	3.	Max-position rule

			//	Our current position is an offset from the bottom of the page, not
			//	from the bottom of the body box, so subtract the body box bottom
			//	to recalibrate the calculations to start at zero.
			int y = position.Y - pageLayout.BodyBox.Bottom;

			//	Our position is measured from the bottom, but the rule is expressed
			//	in terms where zero is at the top, so invert position.
			y = pageLayout.BodyBox.Height - y;

			//	Convert the position to a percentage
			float percent = (float)y / (float)pageLayout.BodyBox.Height;

			//	Now check the rule
			if(percent > rules.MaximumPosition)
				return true;


			//--------------------------------------------------------
			//	4.	Min-lines rule

			//	Work out the height we need to draw our minimum start lines.
			//	If it's greater than the space available on the page then
			//	start a new page.
//			int minLines = item.Lines.Count > rules.MinimumLines ? rules.MinimumLines : item.Lines.Count;
//			int minStartHeight = 0;
//			for(int x = 0; x < minLines; ++x)
//			{
//				int lineHeight = item.Lines[x].Height;
//				minStartHeight += lineHeight;
//			}
//			if(position.Y < pageLayout.BodyBox.Bottom + (_style.Padding?.Bottom ?? 0) + (_style.ItemStyle.Padding?.Bottom ?? 0) + minStartHeight)
//				return true;


			//--------------------------------------------------------
			//	No rule said that we need a break, so stay on the current page
			return false;
		}
	}
}
