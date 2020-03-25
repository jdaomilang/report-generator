using System.Collections.Generic;
using System.Xml.Linq;
using Demon.Report.Types;
using Demon.Report.Style;
using Newtonsoft.Json;

namespace Demon.Report
{
	internal class PageLayout : Layout
	{
		private Rectangle _mediaBox;
		private Rectangle _bodyBox;
		private Rectangle _headerBox;
		private Rectangle _footerBox;
		private GroupLayout _headerLayout;
		private GroupLayout _footerLayout;

		private List<FixedPicture> _background;
		private List<FixedPicture> _overlays;
		private int _pageNumber;
		private int _pageCount;

		//	For Word reports we need to identify the first page in every
		//	chapter, because of the way section breaks are inserted. So
		//	we set this flag to true when the page layout is loaded from
		//	the design file. For pages created during page break handl
		private bool _isChapterFirstPage = false;
		public bool IsChapterFirstPage => _isChapterFirstPage;

		private bool _renderEmpty;

		protected bool _drawRules;

		/// <summary>
		/// Draw bleed marks and page rules on the page.
		/// </summary>
		public bool DrawRules { get { return _drawRules; }}

		public override LayoutType LayoutType { get {return LayoutType.Page;} }
		public override PageLayout Page { get { return this; }}
		public int PageNumber { get { return _pageNumber; }}
		public int PageCount { get { return _pageCount; }}
		public List<FixedPicture> Background { get { return _background; } }
		public List<FixedPicture> Overlays { get { return _overlays; } }


		public PageLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public PageLayout(PageLayout src)
			:base(src)
		{
			_mediaBox = src._mediaBox;
			_bodyBox = src._bodyBox;
			_headerBox = src._headerBox;
			_footerBox = src._footerBox;

			_headerLayout = (GroupLayout)src._headerLayout?.DeepCopy();
			_footerLayout = (GroupLayout)src._footerLayout?.DeepCopy();
			_headerLayout?.SetAsHeaderOf(this);
			_footerLayout?.SetAsFooterOf(this);
			
			_background = src._background;
			_overlays = src._overlays;
			_drawRules = src._drawRules;
			_renderEmpty = src._renderEmpty;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			XElement header = root.Element(ns + "Header")?.Element(ns + "GroupLayout");
			if(header != null)
			{
				_headerLayout = new GroupLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				_headerLayout.Load(header);
				_headerLayout.SetAsHeaderOf(this);
			}
			XElement footer = root.Element(ns + "Footer")?.Element(ns + "GroupLayout");
			if(footer != null)
			{
				_footerLayout = new GroupLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				_footerLayout.Load(footer);
				_footerLayout.SetAsFooterOf(this);
			}

			PageMetrics metrics = _generator.ReportDesign.LoadPageMetrics(root.Element(ns + "PageMetrics"));
			if(metrics == null)
				throw new System.InvalidOperationException($"ChapterLayout page metrics is null at {_trackingInfo}.");
			_mediaBox = new Rectangle
			{
				Left   = metrics.MediaBoxLeft,
				Bottom = metrics.MediaBoxBottom,
				Right  = metrics.MediaBoxRight,
				Top    = metrics.MediaBoxTop
			};
			_bodyBox = new Rectangle
			{
				Left   = metrics.BodyBoxLeft,
				Bottom = metrics.BodyBoxBottom,
				Right  = metrics.BodyBoxRight,
				Top    = metrics.BodyBoxTop
			};
			_headerBox = new Rectangle
			{
				Left   = metrics.HeaderBoxLeft,
				Bottom = metrics.HeaderBoxBottom,
				Right  = metrics.HeaderBoxRight,
				Top    = metrics.HeaderBoxTop
			};
			_footerBox = new Rectangle
			{
				Left   = metrics.FooterBoxLeft,
				Bottom = metrics.FooterBoxBottom,
				Right  = metrics.FooterBoxRight,
				Top    = metrics.FooterBoxTop
			};

			_bounds = _bodyBox;

			//	Note that the header and footer tables are structured as part of the
			//	page layout, not as content. That is, the page layout has explicit
			//	pointers to the header and footer, and the header and footer layouts
			//	nominate no container. This allows a header or footer
			//	layout to be shared by any number of page layouts.
//			if(src.HeaderLayout != null)
//				_headerLayout = new TableLayout(src.HeaderLayout,generator);
//			if(src.FooterLayout != null)
//				_footerLayout = new TableLayout(src.FooterLayout,generator);

			//	A page layout's page break rules is always null because a page layout always
			//	starts on a new page

			//	Load any background images
			_background = new List<FixedPicture>();
			XElement background = root.Element(ns + "Background");
			if(background != null)
			{
				foreach(XElement pictureElement in background.Elements(ns + "Picture"))
				{
					//	The picture can be specified either with an explicit filename or with
					//	a reference to a resource
					string filename = _generator.ReportDesign.LoadString(pictureElement.Attribute("filename"));
					string resourceId = _generator.ReportDesign.LoadString(pictureElement.Attribute("ref"));
				
					int left   = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Left"  )) ?? 0;
					int bottom = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Bottom")) ?? 0;
					int right  = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Right" )) ?? 0;
					int top    = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Top"   )) ?? 0;
				
					PictureAlignment alignment = _generator.ReportDesign.LoadEnum<PictureAlignment>(pictureElement.Element(ns + "Alignment"));
					PictureScaleMode scalemode = _generator.ReportDesign.LoadEnum<PictureScaleMode>(pictureElement.Element(ns + "ScaleMode"));
					int quality = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Quality")) ?? 0;

					FixedPicture picture = new FixedPicture(resourceId, filename, left, bottom, right, top, alignment, scalemode, quality);
					_background.Add(picture);
				}
			}

			//	Load any overlay images
			_overlays = new List<FixedPicture>();
			XElement overlay = root.Element(ns + "Overlay");
			if(overlay != null)
			{
				foreach(XElement pictureElement in overlay.Elements(ns + "Picture"))
				{
					//	The picture can be specified either with an explicit filename or with
					//	a reference to a resource
					string filename = _generator.ReportDesign.LoadString(pictureElement.Attribute("filename"));
					string resourceId = _generator.ReportDesign.LoadString(pictureElement.Attribute("ref"));
				
					int left   = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Left"  )) ?? 0;
					int bottom = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Bottom")) ?? 0;
					int right  = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Right" )) ?? 0;
					int top    = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Top"   )) ?? 0;
				
					PictureAlignment alignment = _generator.ReportDesign.LoadEnum<PictureAlignment>(pictureElement.Element(ns + "Alignment"));
					PictureScaleMode scalemode = _generator.ReportDesign.LoadEnum<PictureScaleMode>(pictureElement.Element(ns + "ScaleMode"));
					int quality = _generator.ReportDesign.LoadInt(pictureElement.Element(ns + "Quality")) ?? 0;

					FixedPicture picture = new FixedPicture(resourceId, filename, left, bottom, right, top, alignment, scalemode, quality);
					_overlays.Add(picture);
				}
			}

			_drawRules = _generator.ReportDesign.LoadBoolean(root.Attribute("drawRules")) ?? false;
			_renderEmpty = _generator.ReportDesign.LoadBoolean(root.Attribute("renderEmpty")) ?? false;
		}

		public override void LoadContent()
		{
			foreach(FixedPicture picture in _background)
				picture.LoadContent(_generator);
			foreach(FixedPicture overlay in _overlays)
				overlay.LoadContent(_generator);

			base.LoadContent();
		}

		/// <summary>
		/// Lay out this page's content. The result can be more than a single page
		/// if the content is longer than a page. Returns a list of page layouts
		/// that represent the full content. The first page in the list is always
		/// this page, possibly with its contents truncated at the page break.
		/// </summary>
		public List<PageLayout> LayOut()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace();

				//	Mark this page as the first in the chapter. We need this information for
				//	the Word reports. We set the flag here because layout duplication has
				//	already been done, but page break handling hasn't yet. This means that if
				//	a chapter layout has been duplicated to several chapters then this method
				//	will be called on each duplicate, and so each one will be marked as the
				//	first page in a new chapter; but this method will not be called on overflow
				//	page layouts created during page break handling, and so the flag will not
				//	be set on second and later pages in each chapter. And we don't copy the
				//	flag in the copy constructor, so it won't get passed on to the overflow
				//	page layouts.
				//
				//	Note that this relies on the fact that when we apply page breaks we keep
				//	this original page. If we replaced it with a copy of itself then the copy
				//	wouldn't have the flag, and so we'd lose this information.
				_isChapterFirstPage = true;

				//	Lay out all our sublayouts on a single page. The content will almost
				//	certainly extend beyond the bottom of the page - we'll fix that up
				//	later by inserting page breaks.

				//	Create a bounds rectangle to be adjusted for each sublayout. At each
				//	call this rectangle indicates the sides and top, but the bottom is
				//	always the same as the top.
				Rectangle subBounds = new Rectangle(_bounds);
				foreach(Layout layout in _subLayouts)
				{
					Position pos = layout.Draft(subBounds);
					subBounds.Top = pos.Y;
					subBounds.Bottom = subBounds.Top;
				}

				_generator.TraceLayoutActivity("Remove empty layouts");
				RemoveEmptyLayouts();

				//	Split ourself into pages
				List<PageLayout> pages = ApplyPageBreaks();
				return pages;
			}
		}

		public void LayOutHeaderAndFooter(int pageNumber, int pageCount)
		{
			//	Header and footer content isn't loaded during normal page content
			//	loading, because it might have references to document properties
			//	such as page number or page count, which can't be evaluated until
			//	the page breaks have been done. So load the content now, and then
			//	draft. See the note in TextLayout.LoadContent before the call
			//	to ExpandProperties.
			//TODO: find a better way to handle PageNumber and PageCount so that we
			//can include them in the page body, and so that we can lay out the
			//header and footer at the same time as the body.
			_pageNumber = pageNumber;
			_pageCount  = pageCount;

			if(_headerLayout != null)
			{
				_generator.TraceLayoutActivity("Resolve header references for page {0}", 0, pageNumber);
				_headerLayout.ResolveSublayoutReferences();

				_generator.TraceLayoutActivity("Apply static conditions for page {0}", 0, pageNumber);
				_headerLayout.ValidateConditions();
				_headerLayout.ApplyStaticLayoutConditions();

				_generator.TraceLayoutActivity("Load header content for page {0}", 0, pageNumber);
				_headerLayout.LoadContent();

				_generator.TraceLayoutActivity("Draft header for page {0}", 0, pageNumber);
				_headerLayout.Draft(_headerBox);
			}
			if(_footerLayout != null)
			{
				_generator.TraceLayoutActivity("Resolve footer references for page {0}", 0, pageNumber);
				_footerLayout.ResolveSublayoutReferences();

				_generator.TraceLayoutActivity("Apply static conditions for page {0}", 0, pageNumber);
				_footerLayout.ValidateConditions();
				_footerLayout.ApplyStaticLayoutConditions();

				_generator.TraceLayoutActivity("Load footer content for page {0}", 0, pageNumber);
				_footerLayout.LoadContent();

				_generator.TraceLayoutActivity("Draft footer for page {0}", 0, pageNumber);
				_footerLayout.Draft(_footerBox);
			}
		}

		private List<PageLayout> ApplyPageBreaks()
		{
			//	Break ourself into pages. At each iteration we split off any excess
			//	content from ourself into an overflow page, and then we apply the same
			//	processing to that page until we run out of content.
			List<PageLayout> pages = new List<PageLayout>();
			PageLayout page = this;
			while(!page.IsEmpty())
			{
				pages.Add(page);
				PageLayout overflow = page.ApplyPageBreak();
				page = overflow;
				//	The overflow will never be null, but will be empty when there's no
				//	excess content left to overflow
			};
			return pages;
		}

		private PageLayout ApplyPageBreak()
		{
			//	Collapse any space layouts at the top of the page. Space layouts are
			//	intended to provide space _between_ layouts and thus are not appropriate
			//	at the top of a page. We collapse top space after drafting so that we know
			//	where the space layouts fall on this page, but before applying the next
			//	page break because lower layouts might now fit on this page.
			CollapseTopSpace();

			PageDisposition disposition = AssessPageBreak(_bodyBox);

			//	Avoid infinite overflow. If any layout starts at the top of the page and
			//	is longer than the page, then it will return disposition overflow or split
			//	and its page split index will be zero. Splitting at index zero is the same
			//	thing as overflowing. Overflowing a full page will cause this page to move
			//	all of its content onto a new page, leaving itself empty, and this will lead
			//	to infinite overflow. So just stop the overflow right now. This will give us
			//	an oversized page, but that's better than making a report with an infinite
			//	number of empty pages. (In the current implementation, AssessPageBreak here on
			//	a page layout will never return overflow because SetPageSplitIndex always
			//	converts overflow to split, and then because a page can split it doesn't convert
			//	it back to overflow. But for semantic clarity we still check for overflow here.)
			if((disposition == PageDisposition.Overflow)
				||
				(disposition == PageDisposition.Split && _pageSplitIndex == 0))
			{
				//	If we can split as requested then do so. Otherwise bump the page split
				//	index from zero to one - this will leave the oversized layout on this
				//	page but overflow subsequent layouts to the next. If it's not possible
				//	to bump the index then return an empty page to stop the overflow.
				Bump bump = BumpPageSplitIndex();
				switch(bump)
				{
					case Bump.Bumped:
					case Bump.Unnecessary:
						//	The split index has been moved beyond zero (or was already there)
						//	and so we can now do the normal page break. This may still leave
						//	the oversized layout on this page, but later layouts are moved
						//	to the next page.
						break;
					case Bump.Impossible:
						//	Couldn't fix the situation by splitting further down, so
						//	just accept the oversized layout
						return (PageLayout)ShallowCopy();
				}
			}

			PageLayout overflow = (PageLayout)DoPageBreak();
			//	This page now contains only the content that can fit in it. Content
			//	that must be moved to the next page, or a later page, is now contained
			//	in the overflow page. If the overflow page is empty then this is the last
			//	page.
			
			//	Redraft this page to set the layouts' bottoms so that they can
			//	draw borders correctly, and redraft the new page so that its contents
			//	start at the top of the page.
			Redraft(_bodyBox.Top);
			overflow?.Redraft(overflow._bodyBox.Top);
			return overflow;
		}

		public override bool IsEmpty()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				if(!base.IsEmpty()) return false;
				return !_renderEmpty;
			}
		}

		protected override void Clear()
		{
			base.Clear();
			_headerLayout = null;
			_footerLayout = null;
		}

		public override bool IsAtTopOfPage()
		{
			return true;
		}

		public override void MapFontCharacters()
		{
			base.MapFontCharacters();
			_headerLayout?.MapFontCharacters();
			_footerLayout?.MapFontCharacters();
		}

		public Rectangle MediaBox { get { return _mediaBox; }}
		public Rectangle BodyBox { get { return _bodyBox; }}
		public Rectangle HeaderBox { get { return _headerBox; }}
		public Rectangle FooterBox { get { return _footerBox; }}
		public GroupLayout Header { get { return _headerLayout; }}
		public GroupLayout Footer { get { return _footerLayout; }}

		protected override void DebugPreviewCustomProperties(JsonWriter writer)
		{
			writer.WritePropertyName("_pageNumber");
			writer.WriteValue(_pageNumber);
			writer.WritePropertyName("_pageCount");
			writer.WriteValue(_pageCount);

			writer.WritePropertyName("_mediaBox");
			writer.WriteValue(_mediaBox.Specification);
			writer.WritePropertyName("_bodyBox");
			writer.WriteValue(_bodyBox.Specification);
			writer.WritePropertyName("_headerBox");
			writer.WriteValue(_headerBox.Specification);
			writer.WritePropertyName("_footerBox");
			writer.WriteValue(_footerBox.Specification);

			if(_headerLayout != null)
			{
				writer.WritePropertyName("_headerLayout");
				_headerLayout.DebugPreview(writer);
			}
			if(_footerLayout != null)
			{
				writer.WritePropertyName("_footerLayout");
				_footerLayout.DebugPreview(writer);
			}
		}
	}

	internal enum PageDisposition
	{
		/// <summary>
		/// Can stay on this page.
		/// </summary>
		ThisPage = 0,

		/// <summary>
		/// Must go on a new page because of a new-page rule.
		/// </summary>
		NewPage = 1,

		/// <summary>
		/// Must go on the next page because of a max-position or
		/// min-lines rule. Cannot fit on this page.
		/// </summary>
		Overflow = 2,

		/// <summary>
		/// A sublayout must go on a new page. Reason unspecified.
		/// </summary>
		Split = 3
	}
}
