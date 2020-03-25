using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Demon.Core.Domain;
using Demon.Report.Style;
using Demon.Path;
using Demon.Report.Types;

namespace Demon.Report
{
	internal abstract class Layout : IOrdered
	{
		//	Optional id used when the layout is the controller of a condition
		protected string _id;
		public string Id { get { return _id; }}

		//	For runtime debugging, when the only tool you have is
		//	the log file. These things are never made visible to
		//	the user, except perhaps in error messages.
		protected TrackingInfo _trackingInfo;
		public TrackingInfo TrackingInfo { get { return _trackingInfo; }}

		//	The source reference path from the design, and the object
		//	to which that path resolved. The path can be shared, for
		//	example when the layout is duplicated or split over a page
		//	break.
		protected Path.Path _sourcePath = Path.Path.Empty;
		protected Reference _sourceObject = Reference.Null;

		public Path.Path SourcePath { get { return _sourcePath; }}
		public Reference SourceObject { get { return _sourceObject; }}

		public virtual IStyle Style { get { return null; }}

		protected int _ordinal;

		protected TermDictionary _termDictionary;

		protected Generator _generator;
		public Generator Generator { get { return _generator; }}
		protected TraceContext _traceContext;

		protected Rectangle _bounds;
		public Rectangle Bounds { get { return _bounds; }}

		protected Layout _container;
		public Layout Container { get { return _container; }}
		protected List<Layout> _subLayouts;
		public IEnumerable<Layout> SubLayouts { get { return _subLayouts; }}
		public int NumSubLayouts { get { return _subLayouts.Count; }}

		public virtual PageLayout Page { get { return _container?.Page; }}
		public virtual ReportLayout Report { get { return _container?.Report; }}

		protected PageBreakRules _pageBreakRules;
		protected int _pageSplitIndex;

		protected ConditionSet _conditions;
		protected bool _staticConditionsSatisfied;
		//	Avoid infinite recursion if conditions have circular references
		protected bool _dynamicConditionsApplied;

		public virtual int PaddingTop { get { return 0; }}
		public virtual int PaddingBottom { get { return 0; }}
		public virtual Color BackgroundColor { get { return null; }}

		public int Ordinal { get {return _ordinal;} set {_ordinal = value;} }

		public abstract LayoutType LayoutType { get; }
		public PageBreakRules PageBreakRules { get { return _pageBreakRules; }}

		protected HashSet<string> _referencedFontNames = new HashSet<string>();


		public Layout(Generator generator, int lineNUmber, int linePosition)
		{
			_generator = generator;

			_subLayouts = new List<Layout>();
			_termDictionary = new TermDictionary();
			_conditions = new ConditionSet(this);

			_trackingInfo = new TrackingInfo(null, lineNUmber, linePosition, _generator.NextTrackingId, 0);
			_traceContext = new TraceContext
			{
				OwnerTrackingId = _trackingInfo.TrackingId
			};

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace();
			}
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public Layout(Layout src)
		{
			_id = src._id;

			//	Create a copy of the source reference, rather than just a reference
			//	to it, because it may need to be updated independently during layout
			//	expansion and page break handling.
			//TODO: is this comment still relevant?
			_sourcePath = src._sourcePath;
			_sourceObject = src._sourceObject;

			_container = src._container;
			_bounds = src._bounds;
			_ordinal = src._ordinal;

			//	Layouts will be copied by the caller as appropriate
			_subLayouts = new List<Layout>();
			_termDictionary = src._termDictionary;

			//	Page break rules can be updated at runtime - for example, a NewPage
			//	rule gets switched off after it has been honoured, so that a very
			//	long layout won't repeatedly demand new pages but never fill them.
			//	So take a copy of the source's rules, rather than a reference to them.
			if(src._pageBreakRules != null)
			{
				_pageBreakRules = new PageBreakRules
				{
					NewPage         = src._pageBreakRules.NewPage,
					KeepWithNext    = src._pageBreakRules.KeepWithNext,
					MaximumPosition = src._pageBreakRules.MaximumPosition,
					MinimumLines    = src._pageBreakRules.MinimumLines
				};
			}

			_conditions = new ConditionSet(src._conditions);
			_generator = src._generator;

			string name = src.TrackingInfo.Name;
			int trackingId    = _generator.NextTrackingId;
			int srcTrackingId = src.TrackingInfo.TrackingId;
			int lineNumber    = src.TrackingInfo.LineNumber;
			int linePosition  = src.TrackingInfo.LinePosition;
			_trackingInfo = new TrackingInfo(name, lineNumber, linePosition, trackingId, srcTrackingId);
			_traceContext = new TraceContext
			{
				OwnerTrackingId = _trackingInfo.TrackingId,
				TraceLayout     = src._traceContext.TraceLayout,
				TraceText       = src._traceContext.TraceText,
				TraceOutline    = src._traceContext.TraceOutline,
				TracePath       = src._traceContext.TracePath
			};

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace();
			}
		}

		public virtual void Load(XElement root)
		{
			XNamespace ns = root.GetDefaultNamespace();
			_id = _generator.ReportDesign.LoadString(root.Attribute("id"));

			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;

			//	Load the source reference if there is one
			string path = _generator.ReportDesign.LoadString(root.Attribute("source"));
			if(!string.IsNullOrWhiteSpace(path))
				_sourcePath = new Path.Path(path, lineNumber, linePosition);

			string name = _generator.ReportDesign.LoadString(root.Attribute("name"));
			_pageBreakRules = _generator.ReportDesign.LoadPageBreakRules(root.Element(ns + "PageBreakRules"));

			_trackingInfo = new TrackingInfo(
				name, lineNumber, linePosition,
				_trackingInfo.TrackingId, _trackingInfo.SourceTrackingId);

			_traceContext.TraceLayout  = _generator.ReportDesign.LoadBoolean(root.Attribute("traceLayout" )) ?? false;
			_traceContext.TraceText    = _generator.ReportDesign.LoadBoolean(root.Attribute("traceText"   )) ?? false;
			_traceContext.TraceOutline = _generator.ReportDesign.LoadBoolean(root.Attribute("traceOutline")) ?? false;
			_traceContext.TracePath    = _generator.ReportDesign.LoadBoolean(root.Attribute("tracePath"   )) ?? false;

			//	This base class implementation can't load any type-specific
			//	properties, but it can load sublayouts
			LoadSubLayouts(root.Element(ns + "Layouts"));

			_termDictionary.Load(root.Element(ns + "TermDictionary"), _generator);

			_conditions.Load(root.Element(ns + "Conditions"));
		}

		protected void LoadSubLayouts(XElement root)
		{
			if(root == null) return;

			foreach(XElement subLayoutNode in root.Elements())
			{
				Layout subLayout = null;
				int lineNumber = ((IXmlLineInfo)subLayoutNode).LineNumber;
				int linePosition = ((IXmlLineInfo)subLayoutNode).LinePosition;
				switch(subLayoutNode.Name.LocalName)
				{
					case "ChapterLayout":
						subLayout = new PageLayout(_generator, lineNumber, linePosition);
						break;
					case "TextLayout":
						subLayout = new TextLayout(_generator, lineNumber, linePosition);
						break;
					case "TableLayout":
						subLayout = new TableLayout(_generator, lineNumber, linePosition);
						break;
					case "TableRowLayout":
						subLayout = new TableRowLayout(_generator, lineNumber, linePosition);
						break;
					case "TableCellLayout":
						subLayout = new TableCellLayout(_generator, lineNumber, linePosition);
						break;
					case "ListLayout":
						subLayout = new ListLayout(_generator, lineNumber, linePosition);
						break;
					case "PhotoTableLayout":
						subLayout = new PhotoTableLayout(_generator, lineNumber, linePosition);
						break;
					case "PictureLayout":
						subLayout = new PictureLayout(_generator, lineNumber, linePosition);
						break;
					case "GroupLayout":
						subLayout = new GroupLayout(_generator, lineNumber, linePosition);
						break;
					case "SpaceLayout":
						subLayout = new SpaceLayout(_generator, lineNumber, linePosition);
						break;
					case "LineLayout":
						subLayout = new LineLayout(_generator, lineNumber, linePosition);
						break;
				}

				if(subLayout != null)
				{
					subLayout.Load(subLayoutNode);
					AddSubLayout(subLayout);
				}
			}
		}

		/// <summary>
		/// At design time every layout's source path points to a template object.
		/// Resolve those references to point to zero, one or more concrete objects.
		/// </summary>
		public virtual void ResolveSublayoutReferences()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace();

				//	A list of sublayouts whose source references have been resolved.
				//	Also includes sublayouts that don't have a source reference,
				//	because they're effectively resolved too.
				List<Layout> resolved = new List<Layout>();

				//	Resolve every sublayout's source reference, duplicating as necessary
				foreach(Layout layout in _subLayouts)
				{
					Trace("Sublayout {0} {1}", layout, layout._sourcePath);

					if(layout._sourcePath == Path.Path.Empty)
					{
						//	The sublayout has no source reference, so it's effectively already
						//	resolved. Just add it to the list. Don't waste time duplicating
						//	it because there's no point.
						resolved.Add(layout);
					}
					else
					{
						//	Resolve the sublayout's source reference. This can yield zero, one or
						//	more resolved references. For each one, duplicate the layout and add
						//	the duplicate to the list.
						List<Reference> resolvedRefs = layout.ResolveMany(this.ReferenceContext);
						Trace("Sublayout {0} resolves to {1} objects", layout, resolvedRefs.Count);
						if(resolvedRefs.Count > 0)
						{
							//	Apply the first resolved reference to the existing sublayout
							layout._sourceObject = resolvedRefs[0];
							resolved.Add(layout);
							Trace("Sublayout {0} original {1}", layout, resolvedRefs[0]);

							//	Duplicate the layout for the remaining resolved references
							for(int x = 1; x < resolvedRefs.Count; ++x)
							{
								Layout duplicate = layout.DeepCopy();
								duplicate._sourceObject = resolvedRefs[x];
								resolved.Add(duplicate);
								Trace("Sublayout {0} copy {1} {2}", duplicate, x, resolvedRefs[x]);
							}
						}
					}
				}

				//	Clear out our original designed sublayout list
				_subLayouts.Clear();
//				while(_subLayouts.Count > 0)
//					RemoveSubLayout(_subLayouts[0]); // clears the sublayout's container reference
				//	Add all resolved layouts to list of sublayouts, and then recursively
				//	resolve their own sublayouts' references
				foreach(Layout layout in resolved)
				{
					AddSubLayout(layout);
					// not _subLayouts.Add because we want to set ourself as the layout's container

					//	Recurse into the duplicated layout's own sublayouts
					layout.ResolveSublayoutReferences();
				}
			}
		}

		public virtual void LoadContent()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start");

				foreach(Layout layout in _subLayouts)
					layout.LoadContent();

				Trace("end");
			}
		}

		public virtual Layout MergeContent(Layout other)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start");

				//	If a layout type wants to support merging then it should
				//	override this method, do its own merging, and then call
				//	this base class implementation to recurse into its
				//	sublayouts to let them merge too if they want to. But
				//	currently merging is only supported on PhotoTableLayout,
				//	and that restriction is enforced in the design schema.

				//	Merge consecutive layouts that want to be merged together.
				//	To support cumulative merging, start at the end of list
				//	and work backwards.
				List<Layout> layouts = new List<Layout>(_subLayouts);
				layouts.Reverse();
				Layout merged = null;
				foreach(Layout layout in layouts)
					merged = layout.MergeContent(merged);

				Trace("end");
				return this;
			}
		}

		public virtual void RemoveEmptyLayouts()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start");

				//	Recurse down to the bottom and then remove empty
				//	layouts on the way back up
				foreach(Layout layout in _subLayouts)
					layout.RemoveEmptyLayouts();
				List<Layout> empties = new List<Layout>();
				foreach(Layout layout in _subLayouts)
					if(layout.IsEmpty())
						empties.Add(layout);
				foreach(Layout empty in empties)
				{
					Trace("Remove empty sublayout {0}", empty);
					_subLayouts.Remove(empty);
				}

				Trace("end");
			}
		}

		/// <summary>
		/// Position sublayouts (containers and content) on a page. Start laying out at
		/// the top of the page, but ignore the bottom - keep laying out as if the page
		/// were of infinite length, until all sublayouts have been drafted.
		/// </summary>
		public virtual Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);
				_bounds = new Rectangle(bounds);

				Position pos = bounds.TopLeft;
				foreach(Layout layout in _subLayouts)
				{
					bounds.Top = pos.Y;
					bounds.Bottom = bounds.Top;
					pos = layout.Draft(bounds);
				}
				_bounds.Bottom = pos.Y;
				HandleEmpty();

				Trace("end _bounds={0}", _bounds);
				return pos;
			}
		}

		/// <summary>
		/// Adjust the layout's top and bottom positions based on a new
		/// page layout.
		/// </summary>
		public virtual void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);

				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//	Reposition ourself and all of our sublayouts
				_bounds.Top = top;
				int pos = _bounds.Top - PaddingTop;
				foreach(Layout layout in _subLayouts)
				{
					layout.Redraft(pos);

					//	Some layouts with no content have bounds of [0 0 0 0]. Don't
					//	let those bounds screw up our positioning. Check the bounds
					//	against Rectangle.Zero rather than check layout.IsEmpty
					//	because space layouts report that they're empty (so that they
					//	can be removed) but they still have size that contributes to
					//	redrafting.
					if(layout.Bounds != Rectangle.Zero)
						pos = layout.Bounds.Bottom;
				}
				_bounds.Bottom = pos - PaddingBottom;
				Trace("end _bounds={0}", _bounds);
			}
		}

		public virtual void DraftUnusedPhotos()
		{
			//	This method is implemented only by PhotoTableLayout
			foreach(Layout layout in _subLayouts)
				layout.DraftUnusedPhotos();
		}

		/// <summary>
		/// Assess how to handle a page break.
		/// </summary>
		public virtual PageDisposition AssessPageBreak(Rectangle bodyBox)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("_bounds={0} bodyBox={1}", _bounds, bodyBox);
				
				//	If we have no height then we don't care where we go. (Not
				//	all zero-height layouts get removed. For example, table
				//	cells are never removed because that would break the table
				//	structure.)
				if(_bounds == Rectangle.Zero) return PageDisposition.ThisPage;

				PageDisposition disposition = PageDisposition.ThisPage;
				bool honourKeepWithNext = false;

				//	If we've got a new-page rule then put ourself on the next page
				if(_pageBreakRules?.NewPage ?? false)
				{
					Trace("page break rules say new-page, so set new-page disposition");
					disposition = PageDisposition.NewPage;
					//	Switch off the new-page rule now that it's been honoured. If we don't
					//	do this then it can cause us to create infinite new pages.
					_pageBreakRules.NewPage = false;
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

				//	If it looks like we can start on this page then check our min-lines
				//	rule. And even if we think that we can fit entirely on this page, a
				//	sublayout could have other rules such as keep-with-next that cause it
				//	to move to the next page and that would cause us to split.
				if(disposition == PageDisposition.ThisPage)
				{
					disposition = SetPageSplitIndex(bodyBox, ref honourKeepWithNext);

					//	If we're to honour keep-with-next rules then work backwards from
					//	the sublayout before the split index, squeezing in as many earlier
					//	layouts with keep-with-next rules as possible
					if(honourKeepWithNext)
					{
						Trace("honour keep-with-next rules");
						Layout splitLayout = _subLayouts[_pageSplitIndex];

						//	We'll insert earlier keep-with-next layouts before the split layout,
						//	one at a time, redraft the split layout accordingly, and then check
						//	whether that works. We don't need to redraft the keep-with-next
						//	layouts to do this - we can just move the split layout's top position
						//	down by the heights of the keep-with-next layouts. This shortcut works
						//	as long as we have a policy of applying top padding even at the top
						//	of the page, but if we change that policy then we'll have to redraft
						//	the keep-with-next layouts in case their padding would be removed when
						//	they appear at the top of the page.
						int testTop = bodyBox.Top;

						for(int x = _pageSplitIndex - 1; x >= 0; --x)
						{
							Layout prev = _subLayouts[x];
							Layout next = _subLayouts[x + 1];

							//	If the sublayout does not have a keep-with-next rule then finish.
							if(!(prev.PageBreakRules?.KeepWithNext ?? false))
							{
								Trace(
									"sublayout[{0}] {1} happy to be separated from next {2} so break at split index {3}",
									x, prev, next, _pageSplitIndex);
								break;
							}

							//	If the sublayout is split then finish. Splitting implicitly honours
							//	its own keep-with-next because the overflow part of the layout
							//	will be on the same page as the next layout.
							if(prev.IsSplit)
							{
								Trace(
									"sublayout[{0}] {1} is split, honouring its own keep-with-next, so break at split index {2}",
									x, prev, _pageSplitIndex);
								break;
							}

							//	Move the split layout down to allow room for this keep-with-next
							//	layout before it, and then reassess the page break on the split layout
							Trace("test keep-with-next feasibility on sublayout[{0}] {1}", x, prev);
							testTop -= prev._bounds.Height;
							splitLayout.Redraft(testTop);
							PageDisposition testDisposition = splitLayout.AssessPageBreak(bodyBox);

							//	If it says new-page or overflow then this sublayout's keep-with-next
							//	rule cannot be honoured. Set the split index to the sublayout after
							//	this one. Finish.
							if(testDisposition == PageDisposition.NewPage || testDisposition == PageDisposition.Overflow)
							{
								Trace("cannot honour keep-with-next rule on sublayout[{0}] {1}", x, prev);

								honourKeepWithNext = false;
								_pageSplitIndex = x + 1;
								break;
							}

							//	Otherwise: It says this-page or split, meaning that all sublayouts
							//	from this one to the split index can fit together on one page.
							//	Carry on backwards through the loop.
							Trace(
								"split index {0} puts sublayout[{1}] {2} on next page, and previous sublayout {3} has keep-with-next rule, so adjust split index to {4} to put them both on the next page",
								_pageSplitIndex, x, next, prev, x);
							_pageSplitIndex = x;
							
							//	Reset the previous sublayout's page split index to zero, so that it
							//	will move itself onto the next page during DoPageBreak. The previous
							//	layout will only be moved by keep-with-next if it's not split.
							prev.ResetPageSplitIndex();
						}
					}
				}

				Trace("return {0}, split index {1}", disposition, _pageSplitIndex);
				return disposition;
			}
		}

		protected virtual PageDisposition SetPageSplitIndex(Rectangle bodyBox, ref bool honourKeepWithNext)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				PageDisposition disposition = PageDisposition.ThisPage;

				//	If our first sublayout says next-page then move to the next page.
				//
				//	If any sublayout other than the first says next-page, or if any
				//	sublayout (including the first) says split, then check our min-lines
				//	rule. If we can split at that sublayout then do so, otherwise move
				//	to the next page.

				//	Iterate over our sublayouts using _pageSplitIndex as the
				//	counter. When we terminate the loop, _pageSplitIndex will
				//	indicate where we want to split. That is, it will be the
				//	index of the first sublayout that goes on the next page.
				for(_pageSplitIndex = 0; _pageSplitIndex < _subLayouts.Count; ++_pageSplitIndex)
				{
					Layout sublayout = _subLayouts[_pageSplitIndex];
					//	Ask each sublayout how it wants to handle the page break.
					//	If any sublayout wants to split then skip the rest because
					//	they'll certainly go on the next page.
					disposition = sublayout.AssessPageBreak(bodyBox);

					//	If the first sublayout says new-page then set new-page disposition.
					//	Finish. Do not honour the keep-with-next chain (because new-page trumps
					//	keep-with-next.)
					if((disposition == PageDisposition.NewPage) && (_pageSplitIndex == 0))
					{
						Trace(
							"first sublayout {0} says new-page, so set new-page disposition",
							sublayout);
						disposition = PageDisposition.NewPage;
						break;
					}

					//	If any other sublayout says new-page then set split disposition and
					//	set split index to that sublayout's index. Finish. Do not honour the
					//	keep-with-next chain, because new-page trumps keep-with-next. But
					//	if we can't split here then set new-page disposition.
					if((disposition == PageDisposition.NewPage) && (_pageSplitIndex > 0))
					{
						Trace(
							"non-first sublayout[{0}] {1} says new-page, so set split disposition",
							_pageSplitIndex, sublayout);
						disposition = PageDisposition.Split;
					}

					//	If any sublayout (including the first) says split then
					//	set split disposition and set split index to that sublayout's index. Finish.
					//	The previous sublayout's keep-with-next rule (if it has one) is implicitly
					//	honoured because the previous sublayout is on the same page as the first
					//	part of this sublayout, even though this sublayout will be split.
					if(disposition == PageDisposition.Split)
					{
						Trace(
							"sublayout[{0}] {1} says split, so set split disposition",
							_pageSplitIndex, sublayout);
						disposition = PageDisposition.Split;
					}

					//	If any sublayout (including the first) says overflow then set split
					//	disposition and set split index to that sublayout's index. Break. Honour
					//	the keep-with-next chain of earlier sublayouts as much as possible.
					if(disposition == PageDisposition.Overflow)
					{
						Trace(
							"sublayout[{0}] {1} says overflow, so set split disposition",
							_pageSplitIndex, sublayout);
						disposition = PageDisposition.Split;
						honourKeepWithNext = true;
					}

					//	Only split if we can
					if(disposition == PageDisposition.Split)
					{
						bool canSplit = CanSplit(bodyBox);
						if(!canSplit)
						{
							Trace("cannot split, so set overflow disposition");
							_pageSplitIndex = 0;
							disposition = PageDisposition.Overflow;
						}
						break;
					}
				}
				return disposition;
			}
		}

		/// <summary>
		/// Split this layout at the position determined during an earlier call to
		/// AssessPageBreak. Moves sublayouts below the split into a new layout
		/// and returns that new layout.
		/// </summary>
		public virtual Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	If we have no sublayouts, or if our split index is greater
				//	than our number of sublayouts, then we don't need to split
				//	at all. Return a valid empty layout of the same type as
				//	ourself.
				if((_subLayouts.Count == 0) || (_pageSplitIndex >= _subLayouts.Count))
					return ShallowCopy();

				//	Otherwise we do want to split. Make another layout with the same
				//	metadata as ourself, then split our sublayout that straddles the
				//	page break, and move the lower half of the split into the copy.
				//	Then move all our subsequent sublayouts out of ourself and into
				//	the copy.
				Layout lower = ShallowCopy();
				Layout subOriginal = _subLayouts[_pageSplitIndex];

				Layout subcopy = subOriginal.DoPageBreak();
				if(subcopy != null)
					lower.AddSubLayout(subcopy);

				while(_subLayouts.Count > _pageSplitIndex + 1)
				{
					Layout sublayout = _subLayouts[_pageSplitIndex + 1];
					this.RemoveSubLayout(sublayout);
					lower.AddSubLayout(sublayout);
				}

				//	If the sublayout that we split on is now empty (because it opted
				//	to move all of its content to the next page) then remove it
				if(subOriginal.IsEmpty())
					this.RemoveSubLayout(subOriginal);

				return lower;
			}
		}

		/// <summary>
		/// Does this layout contain any content? If it contains only empty
		/// sublayouts then it is empty. Some layout types override this
		/// method with their own definitions of emptiness.
		/// </summary>
		public virtual bool IsEmpty()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				foreach(Layout layout in _subLayouts)
					if(!layout.IsEmpty())
						return false;
				return true;
			}
		}

		public bool IsSplit
		{
			get
			{
				//	If the layout wasn't split, then its page split index will have
				//	advanced beyond its last sublayout during SetPageSplitIndex
				return _pageSplitIndex < _subLayouts.Count;
			}
		}

		/// <summary>
		/// Throws InvalidDesignException if this layout's id is already in
		/// use by another layout.
		/// </summary>
		public virtual void AssertIdUnique(HashSet<string> inUseIds)
		{
			if(!string.IsNullOrWhiteSpace(_id))
			{
				if(inUseIds.Contains(_id))
					throw new InvalidDesignException($"ID '{_id}' is already in use.", this);
				inUseIds.Add(_id);
			}

			foreach(Layout layout in _subLayouts)
				layout.AssertIdUnique(inUseIds);
		}

		/// <summary>
		/// Throws an InvalidConditionException if any condition is invalid.
		/// </summary>
		public virtual void ValidateConditions()
		{
			using(new TraceContextPusher(_generator, _traceContext))
			{
				Trace();
				_conditions.Validate(_sourceObject, ReferenceContext);
				foreach(Layout layout in _subLayouts)
					layout.ValidateConditions();
			}
		}

		/// <summary>
		/// Apply static layout conditions. Does not apply static content conditions
		/// because their sources are not known until content load time.
		/// </summary>
		public virtual void ApplyStaticLayoutConditions()
		{
			using(new TraceContextPusher(_generator, _traceContext))
			{
				Trace();
				_staticConditionsSatisfied = _conditions.AreStaticLayoutConditionsSatisfied(_sourceObject, ReferenceContext);
				if(_staticConditionsSatisfied)
				{
					//	If we're satisfied then recurse into our sublayouts. Otherwise clear
					//	ourself.
					foreach(Layout layout in _subLayouts)
						layout.ApplyStaticLayoutConditions();
				}
				else
				{
					Trace("Clear layout because static conditions are not satisfied");
					Clear();
				}
			}
		}

		public virtual void ApplyDynamicConditions()
		{
			//	Dynamic conditions don't fit neatly into the ConditionSet
			//	scheme because they need some internal layout stuff such
			//	as recursing into sublayouts and finding related layouts,
			//	so we do condition stuff in ConditionSet (evaluating condition
			//	terms with respect to data retrieved here in the layout) and
			//	do the layout stuff here (recursing and finding other layouts.)
			//
			//	And that means that we have to expose the ContentSet's dynamic
			//	layout lists in properties so that we can iterate over them here.
			//	We could avoid that by passing ourself to ConditionSet and having
			//	it evaluate the conditions, but that would need to call back here
			//	to apply them (clearing sublayouts, for example) and it would just
			//	be a big incomprehensible knot. So let's expose the conditions
			//	that we need.

			using (new TraceContextPusher(_generator, _traceContext))
			{
				if(_dynamicConditionsApplied) return;
				Trace();

				//	Apply conditions to sublayouts before applying our own, in case
				//	the sublayouts' conditions change things that might affect our
				//	conditions.
				foreach(Layout layout in _subLayouts)
					layout.ApplyDynamicConditions();

				foreach(EmptyLayoutCondition condition in _conditions.EmptyLayoutConditions)
					ApplyEmptyLayoutCondition(condition);
				foreach(PhotoCountCondition condition in _conditions.PhotoCountConditions)
					ApplyPhotoCountCondition(condition);
				foreach(ItemCountCondition condition in _conditions.ItemCountConditions)
					ApplyItemCountCondition(condition);

				_dynamicConditionsApplied = true;
			}
		}

		protected virtual void ApplyEmptyLayoutCondition(EmptyLayoutCondition condition)
		{
			if(!_conditions.IsConditionSatisfied(condition, this))
			{
				Trace("Clear layout because empty-layout condition [{0}] is not satisfied", condition);
				Clear();
			}
		}

		/// <summary>
		/// Show or hide the layout based on the number of photos in a specified photo list.
		/// </summary>
		protected virtual void ApplyPhotoCountCondition(PhotoCountCondition condition)
		{
			if(!_conditions.IsConditionSatisfied(condition, this))
			{
				Trace("Clear layout because photo-count condition [{0}] is not satisfied", condition);
				Clear();
			}
		}

		/// <summary>
		/// Show or hide the layout based on the number of sublayouts in a specified layout.
		/// This is similar to EmptyLayoutCondition, but the layout types can define their
		/// own semantics for the term "empty" that doesn't necessarily mean that the number
		/// of sublayouts is zero.
		/// </summary>
		protected virtual void ApplyItemCountCondition(ItemCountCondition condition)
		{
			if(!_conditions.IsConditionSatisfied(condition, this))
			{
				Trace("Clear layout because item-count condition [{0}] is not satisfied", condition);
				Clear();
			}
		}

		public void AddConditions(ConditionSet conditions)
		{
			_conditions.Add(conditions);
		}

		public virtual void MapFontCharacters()
		{
			foreach(Layout layout in _subLayouts)
				layout.MapFontCharacters();
		}

		protected virtual void Clear()
		{
			_sourceObject = Reference.Empty;
			_sourcePath = Path.Path.Empty;
			_subLayouts.Clear();
		}

		/// <summary>
		/// Reset the layout's page split index to zero. Recurse into all of its
		/// sublayouts.
		/// </summary>
		protected virtual void ResetPageSplitIndex()
		{
			_pageSplitIndex = 0;
			foreach(Layout sublayout in _subLayouts)
				sublayout.ResetPageSplitIndex();
		}

		/// <summary>
		/// Bump the page split index from zero to one to avoid infinite
		/// overflow when the first sublayout is too large to fit on the
		/// page.
		/// </summary>
		public virtual Bump BumpPageSplitIndex()
		{
			if(_subLayouts.Count == 0) return Bump.Impossible;
			if(_pageSplitIndex > 0) return Bump.Unnecessary;

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

		/// <summary>
		/// Find a layout of the given type and id anywhere within the given
		/// context. First work up to the context, and then down to the
		/// layout being sought. If the target layout type is LayoutType.None
		/// then just return the context layout.
		/// </summary>
		public Layout FindLayout(int context, LayoutType type, string id)
		{
			//	Find the root of the context
			Layout root = this;
			switch(context)
			{
				case Condition.LocalContext:
				{
					root = this;
					break;
				}

				case Condition.DocumentContext:
				{
					//	Go as high as we can, even if that doesn't take us all
					//	the way to the report layout
					while(root.LayoutType != LayoutType.Report)
					{
						if(root._container == null)
							break;
						root = root._container;
					}
					break;
				}

				case Condition.ChapterContext:
				{
					//	Go as high as we can, even if that doesn't take us all
					//	the way to the page layout
					while(root.LayoutType != LayoutType.Page)
					{
						if(root._container == null)
							break;
						root = root._container;
					}
					break;
				}

				default:
				{
					//	Go as high as we can, even if that doesn't take us all
					//	the way to the specified context
					for(int x = 0; x < context; ++x)
					{
						if(root._container == null)
							break;
						root = root._container;
					}
					break;
				}
			}

			//	If the layout reference is empty then return the root
			if(type == LayoutType.None) return root;

			//	Find the given layout within the root
			return root.FindLayout(type,id);
		}

		protected Layout FindLayout(LayoutType type, string id)
		{
			if((this.LayoutType == type) && (this._id == id))
				return this;

			foreach(Layout sublayout in _subLayouts)
			{
				Layout found = sublayout.FindLayout(type,id);
				if(found != null)
					return found;
			}

			return null;
		}

		/// <summary>
		///	If we have no content then set our height to zero so that we won't be drawn
		/// </summary>
		protected Position HandleEmpty()
		{
			if(IsEmpty())
			{
				Trace("layout is empty");
				_bounds.Bottom = _bounds.Top;
			}
			return _bounds.BottomLeft;
		}

		public virtual bool IsAtTopOfPage()
		{
			//	To be at the top of the page doesn't necessarily mean that the
			//	layout's top is exactly at the page top, because the layout or
			//	any of its outer containers could have padding. To be at the top
			//	means that there is no other content layout along the chain of
			//	layouts from the page layout to this layout.
			if(!_container.IsFirstSubLayout(this))
				return false;
			return _container.IsAtTopOfPage();
		}

		protected bool IsFirstSubLayout(Layout layout)
		{
			bool zero = layout._ordinal == 0;
			bool first = (_subLayouts.Count > 0) && (_subLayouts[0] == layout);
			if(zero != first) throw new Exception("is first layout wonky");
			return first;
		}

		/// <summary>
		/// Returns true if all top space is known to have been collapsed
		/// and the operation is complete. Returns false to continue
		/// collapsing later layouts.
		/// </summary>
		public virtual bool CollapseTopSpace()
		{
			foreach(Layout layout in _subLayouts)
				if(layout.CollapseTopSpace())
					return true; // stop recursing at the first non-space layout
			return false;
		}

		public Layout GetSubLayoutAtIndex(int index)
		{
			return _subLayouts[index];
		}

		public virtual bool CanSplit(Rectangle bodyBox)
		{
			return true;
		}

		public void PushTraceContext()
		{
			_generator.PushTraceContext(_traceContext);
		}

		public void PopTraceContext()
		{
			_generator.PopTraceContext();
		}

		/// <summary>
		/// At design time a layout's source reference points to a template object.
		/// Resolve that reference to point to a concrete object. This method resolves
		/// only a single reference - for repeating objects such as table rows, you
		/// must duplicate the template row layout yourself and then call this method
		/// on each duplicate, passing a context appropriate to the specific row.
		/// </summary>
		protected virtual void ResolveThisReference()
		{
			//	Make one attempt to resolve the reference. If that attempt returns
			//	null then assign the empty reference, which is resolved but
			//	points nowhere.
			if(!_sourceObject.IsResolved)
			{
				Reference source = _generator.ResolveOne(_sourcePath, ReferenceContext);
				_sourceObject = source ?? Reference.Empty;
			}
		}

		protected T ResolveOne<T>() where T : class, IContentSource
		{
			return _generator.ResolveOne<T>(_sourcePath, ReferenceContext);
		}

		protected List<T> ResolveMany<T>() where T : class, IContentSource
		{
			return _generator.ResolveMany<T>(_sourcePath, ReferenceContext);
		}

		protected virtual List<Reference> ResolveMany(Reference context)
		{
			List<Reference> resolvedRefs = _generator.ResolveMany(_sourcePath, context);
			return resolvedRefs;
		}

		/// <summary>
		///	Make a copy of this layout and its metadata, but do not copy
		///	any sublayouts.
		/// </summary>
		internal virtual Layout ShallowCopy()
		{
			Layout copy = null;
			switch(LayoutType)
			{
				case LayoutType.Report:
					copy = new ReportLayout((ReportLayout)this);
					break;
				case LayoutType.Page:
					copy = new PageLayout((PageLayout)this);
					break;
				case LayoutType.Text:
					copy = new TextLayout((TextLayout)this);
					break;
				case LayoutType.List:
					copy = new ListLayout((ListLayout)this);
					break;
				case LayoutType.ListItem:
					copy = new ListItemLayout((ListItemLayout)this);
					break;
				case LayoutType.PhotoTable:
					copy = new PhotoTableLayout((PhotoTableLayout)this);
					break;
				case LayoutType.PhotoRow:
					copy = new PhotoRowLayout((PhotoRowLayout)this);
					break;
				case LayoutType.Photo:
					copy = new PhotoLayout((PhotoLayout)this);
					break;
				case LayoutType.Picture:
					copy = new PictureLayout((PictureLayout)this);
					break;
				case LayoutType.Table:
					copy = new TableLayout((TableLayout)this);
					break;
				case LayoutType.TableRow:
					copy = new TableRowLayout((TableRowLayout)this);
					break;
				case LayoutType.TableCell:
					copy = new TableCellLayout((TableCellLayout)this);
					break;
				case LayoutType.Group:
					copy = new GroupLayout((GroupLayout)this);
					break;
				case LayoutType.Space:
					copy = new SpaceLayout((SpaceLayout)this);
					break;
				case LayoutType.Line:
					copy = new LineLayout((LineLayout)this);
					break;
			}
			return copy;
		}

		/// <summary>
		///	Make a copy of this layout and all its sublayouts.
		/// </summary>
		internal virtual Layout DeepCopy()
		{
			Layout thisCopy = ShallowCopy();

			//	Copy our sublayouts
			foreach(Layout sub in _subLayouts)
			{
				Layout subCopy = sub.DeepCopy();
				thisCopy.AddSubLayout(subCopy);
			}

			return thisCopy;
		}

		/// <summary>
		/// Add a sublayout, and set its container to this. A layout can actually
		/// be contained in more than one container layout, but it can only recognize
		/// one as its container.
		/// </summary>
		public void AddSubLayout(Layout layout)
		{
			_subLayouts.Add(layout);
			layout._container = this;

			//	Reset the layout's ordinal within ourself
			layout.Ordinal = _subLayouts.Count - 1;
		}

		public void InsertSubLayout(int index, Layout layout)
		{
			_subLayouts.Insert(index,layout);
			layout._container = this;

			//	Reset all layouts' ordinals to accommodate the new one
			for(int x = 0; x < _subLayouts.Count; ++x)
				_subLayouts[x].Ordinal = x;
		}

		/// <summary>
		/// Remove a sublayout, and set its container to null. If you're moving a sublayout
		/// to a different container then be sure to call this method on the current
		/// container before calling AddSubLayout on the new container, and not the
		/// other way round. Otherwise this method will undo the new container assignment
		/// made by AddSubLayout.
		/// </summary>
		public void RemoveSubLayout(Layout layout)
		{
			_subLayouts.Remove(layout);
			layout._container = null;
		}

//		internal Layout FindSubLayout(Reference source)
//		{
//			if((_source != null) && (_source.Type == source.Type) && (_source.Id == source.Id))
//				return this;
//			
//			foreach(Layout sub in _subLayouts)
//			{
//				Layout found = sub.FindSubLayout(source);
//				if(found != null)
//					return found;
//			}
//			return null;
//		}

//		internal bool Contains(IContentSource source)
//		{
//			if((_source != null) && (_source.Type == source.Type) && (_source.Id == source.Id))
//				return true;
//
//			foreach(Layout sub in _subLayouts)
//				if(sub.Contains(source))
//					return true;
//
//			return false;
//		}

		public void SetAsHeaderOf(PageLayout page)
		{
			_container = page;
		}

		public void SetAsFooterOf(PageLayout page)
		{
			_container = page;
		}

		public Reference ReferenceContext
		{
			get
			{
				if(_sourceObject != Reference.Null && _sourceObject != Reference.Empty)
					return _sourceObject;
				else if(_container != null)
					return _container.ReferenceContext;
				else
					return Reference.Empty;
			}
		}

		/// <summary>
		///	Get a copy of the layout's source object reference.
		/// </summary>
		public Reference Source
		{
			get
			{
				//	Return a copy of the source reference, not a reference to it, to prevent
				//	the caller changing it
				return new Reference(_sourceObject);
			}
		}

		protected string GetTermDefinition(Reference source)
		{
			string term = null;
			bool ok = _termDictionary.TryGetTerm(source, out term);

			//	If not found in our dictionary, try our container
			if(!ok)
				term = _container?.GetTermDefinition(source);

			return term;
		}

		protected string GetTermDefinition(ContentSourceType type, string id)
		{
			Reference source = Reference.Create(type, id, true);
			return GetTermDefinition(source);
		}

		public virtual void Dump(int indent)
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append($"{_bounds.LongString}\t");

			for(int x = 0; x < indent; ++x)
				sb.Append("\t");
			sb.Append($"{GetType().Name} '{_trackingInfo.Name}'");
			if(_sourcePath.TargetType != ContentSourceType.None)
			{
				if(_sourceObject.IsResolved)
					sb.Append($" : {_sourceObject}");
				else
					sb.Append($" : {_sourcePath}");
			}
			sb.Append("\r\n");
			System.Diagnostics.Debugger.Log(0,null,sb.ToString());

			foreach(Layout child in _subLayouts)
				child.Dump(indent+1);
		}

		/// <summary>
		/// Logs trace information. Automatically includes information about the
		/// calling method and the current layout, so you don't need to include
		/// that stuff in the text argument.
		/// </summary>
		/// <param name="format">Will be formatted with the args only if the
		/// TraceLayout flag is set.
		/// </param>
		/// <param name="args">Will be evaluated only if the TraceLayout flag is set.
		/// </param>
		/// To minimize the performance cost of tracing, we use
		/// string.Format(text,args) to construct the trace message inside Trace
		/// but only if the trace flag is set, rather than always construct the
		/// message in the caller. That is, we call Trace like this:
		///		Trace("values = {0} {1}", 0, value1, value2);
		///	rather than
		///		Trace($"values = {value1} {value2}", 0);
		public void Trace(string format = null, params object[] args)
		{
			if(!_generator.TraceLayout) return;

			string info = GetTraceInfo();
			if(format != null)
				info += " " + format;
			_generator.Trace(info, 1, args);
		}

		public string GetTraceInfo()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(this.GetType().Name);
			if(_id != null)
			{
				sb.Append(" &[");
				sb.Append(_id);
				sb.Append("]");
			}
			sb.Append(" ");
			sb.Append(_trackingInfo);
			return sb.ToString();
		}

		public static int Compare(IOrdered item1, IOrdered item2)
		{
			if((item1 == null) && (item2 == null)) return 0;
			if(item1 == null) return -1;
			if(item2 == null) return +1;

			if(item1.Ordinal < item2.Ordinal) return -1;
			if(item1.Ordinal > item2.Ordinal) return +1;
			return 0;
		}

		public override string ToString()
		{
			return $"{LayoutType} {_trackingInfo}";
		}

        /// <summary>
        /// Bump the layout's page split index from zero to one to prevent
        /// infinite page overlow.
        /// </summary>
        public enum Bump
        {
            /// <summary>
            /// The page split index is already greater than zero.
            /// </summary>
            Unnecessary,

            /// <summary>
            /// The page split index has been bumped from zero to one.
            /// </summary>
            Bumped,

            /// <summary>
            /// The page split index cannot be bumped from zero to one because
            /// the layout has only zero or one sublayout.
            /// </summary>
            Impossible
        }

        public string DebugPreview()
		{
			using(StringWriter sw = new StringWriter())
			using(JsonWriter jw = new JsonTextWriter(sw))
			{
				DebugPreview(jw);
				return sw.ToString();
			}
		}

		public virtual void DebugPreview(JsonWriter writer)
		{
			writer.WriteStartObject();

			writer.WritePropertyName("$type");
			writer.WriteValue(LayoutType.ToString());

			writer.WritePropertyName("_id");
			writer.WriteValue(_id);

			writer.WritePropertyName("SourceTrackingId");
			writer.WriteValue(_trackingInfo.SourceTrackingId);

			writer.WritePropertyName("_trackingInfo");
			writer.WriteStartObject();
			writer.WritePropertyName("Name");
			writer.WriteValue(_trackingInfo.Name);
			writer.WritePropertyName("LineNumber");
			writer.WriteValue(_trackingInfo.LineNumber);
			writer.WritePropertyName("LinePosition");
			writer.WriteValue(_trackingInfo.LinePosition);
			writer.WritePropertyName("TrackingId");
			writer.WriteValue(_trackingInfo.TrackingId);
			writer.WritePropertyName("SourceTrackingId");
			writer.WriteValue(_trackingInfo.SourceTrackingId);
			writer.WriteEndObject();
			
			writer.WritePropertyName("_sourcePath");
			if(_sourcePath != null)
				writer.WriteValue(_sourcePath.ToString());
//				_sourcePath.DebugPreview(writer);
			else
				writer.WriteNull();

			writer.WritePropertyName("_sourceObject");
			if(_sourceObject != null)
				writer.WriteValue(_sourceObject.ToString());
//				_sourceObject.DebugPreview(writer);
			else
				writer.WriteNull();

			writer.WritePropertyName("_ordinal");
			writer.WriteValue(_ordinal);

			writer.WritePropertyName("_bounds");
			writer.WriteValue(_bounds.Specification);

			writer.WritePropertyName("_pageBreakRules");
			if(_pageBreakRules != null)
				writer.WriteValue(_pageBreakRules.ToString());
//				_pageBreakRules.DebugPreview(writer);
			else
				writer.WriteNull();

			writer.WritePropertyName("_pageSplitIndex");
			writer.WriteValue(_pageSplitIndex);

			writer.WritePropertyName("_conditions");
			if(_conditions != null)
				writer.WriteValue(_conditions.ToString());
//				_conditions.DebugPreview(writer);
			else
				writer.WriteNull();

			writer.WritePropertyName("_staticConditionsSatisfied");
			writer.WriteValue(_staticConditionsSatisfied);

			writer.WritePropertyName("_dynamicConditionsApplied");
			writer.WriteValue(_dynamicConditionsApplied);

			DebugPreviewCustomProperties(writer);

			writer.WritePropertyName("_subLayouts");
			writer.WriteStartArray();
			foreach(Layout layout in _subLayouts)
				layout.DebugPreview(writer);
			writer.WriteEndArray();

			writer.WriteEndObject();
		}

		protected virtual void DebugPreviewCustomProperties(JsonWriter writer)
		{
		}
	}

	internal class PageMetrics
	{
		public int MediaBoxLeft { get; set; }
		public int MediaBoxBottom { get; set; }
		public int MediaBoxRight { get; set; }
		public int MediaBoxTop { get; set; }
		public int BodyBoxLeft { get; set; }
		public int BodyBoxBottom { get; set; }
		public int BodyBoxRight { get; set; }
		public int BodyBoxTop { get; set; }
		public int HeaderBoxLeft { get; set; }
		public int HeaderBoxBottom { get; set; }
		public int HeaderBoxRight { get; set; }
		public int HeaderBoxTop { get; set; }
		public int FooterBoxLeft { get; set; }
		public int FooterBoxBottom { get; set; }
		public int FooterBoxRight { get; set; }
		public int FooterBoxTop { get; set; }
	}

	internal enum LayoutType
	{
		None				=  0,
		Page				=  1,
		List				=  5,
		TableCell		=  6,
		Text				=  7, // for in-text formatting
		Report			=  8,
		Table				=  9,
		TableColumn	= 10,
		Photo				= 11,
		ListItem		= 12,
		Picture			= 13,
		TableRow		= 14,
		PhotoTable	= 16,
		PhotoRow		= 17,
		Group				= 18,
		Space				= 19,
		Line        = 20
	}

	internal enum LayoutPlacement
	{
		Simple,
		Complex
	}

	public enum ControlType
	{
		None         =  0,
		RadioButton  =  1,
		Checkbox     =  2,
		TextEntry    =  3,
		PhotoList    =  4,
		MultiSelect  =  5,
		SingleSelect =  6,
		StaticText   =  7,
		Photo        =  8,
		Form         =  9,
		Section      = 10,
		Calculation   = 11,
		CalculationList     = 12,
		CalculationVariable = 13
	}
}
