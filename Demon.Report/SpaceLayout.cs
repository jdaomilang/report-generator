using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Demon.Core.Domain;
using Demon.Report.Types;

namespace Demon.Report
{
	internal class SpaceLayout : Layout
	{
		public override LayoutType LayoutType { get {return LayoutType.Space;} }
		private int _height;


		public SpaceLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public SpaceLayout(SpaceLayout src)
			:base(src)
		{
			_height = src._height;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_height = _generator.ReportDesign.LoadInt(root.Element(ns + "Height")) ?? 0;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);
				_bounds = new Rectangle(bounds);
				_bounds.Bottom = _bounds.Top - _height;
				Trace("end _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);
				_bounds.Top = top;
				_bounds.Bottom = _bounds.Top - _height;
				Trace("end _bounds={0}", _bounds);
			}
		}

		public override bool IsEmpty()
		{
			//	Even though we have no content, we're not empty. This way
			//	we don't get removed from the report, and can continue to
			//	create space as designed.
			return false;
		}

		public override bool CollapseTopSpace()
		{
			//	Redraft to zero height. It's safe to override our designed
			//	height with zero as long as we're guaranteed not to be
			//	redrafted afterwards. The page layout makes this guarantee
			//	by collapsing top space one page at a time, and not
			//	collapsing in the overlow page until it has become the
			//	current page. See PageLayout.ApplyPageBreaks.
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("starting _bounds={0}", _bounds);
				_height = 0;
				_bounds.Bottom = _bounds.Top;
				Trace("ending _bounds={0}", _bounds);
				return false;
			}
		}

		public override PageDisposition AssessPageBreak(Rectangle bodyBox)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("_bounds={0} bodyBox={1}", _bounds, bodyBox);
				PageDisposition disposition = PageDisposition.ThisPage;

				//	If we don't fit on this page then split
				if(_bounds.Bottom < bodyBox.Bottom)
					disposition = PageDisposition.Split;

				Trace("return {0}, split index {1}", disposition, _pageSplitIndex);
				return disposition;
			}
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	Actually we don't really want to split at all - we just want to
				//	truncate ourself and let the next layout start at the top of the
				//	next page. We don't want to start a page with a space. So always
				//	return a valid empty layout of the same type as ourself, with
				//	zero height.
				SpaceLayout copy = new SpaceLayout(this);
				copy._height = 0;
				return copy;
			}
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			return false;
		}
	}
}
