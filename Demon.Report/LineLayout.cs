using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class LineLayout : Layout
	{
		public override LayoutType LayoutType { get {return LayoutType.Line;} }
		private LineStyle _style;
//		public LineStyle Style { get { return _style; }}

		public override IStyle Style { get { return _style; }}
		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}


		public LineLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public LineLayout(LineLayout src)
			:base(src)
		{
			_style = src._style;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_style = _generator.ReportDesign.LoadStyle<LineStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultLineStyle;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				if(_style == null)
					throw new InvalidOperationException($"LineLayout style is null at {_trackingInfo}.");

				//	Remember the original bounds, before we apply our own padding
				_bounds = new Rectangle(bounds);

				//	Apply our own padding within the given bounds
				bounds.Left  += _style.Padding?.Left  ?? 0;
				bounds.Right -= _style.Padding?.Right ?? 0;
				bounds.Top   -= _style.Padding?.Top   ?? 0;

				//	Create our own drawing position
				Position pos = new Position(bounds.Left,bounds.Top);
				pos.Y -= _style.Thickness;

				//	Advance by the padding and record our bounds bottom
				pos.Y -= _style.Padding?.Bottom ?? 0;
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
				_bounds.Top = top;
				_bounds.Bottom = _bounds.Top - (_style.Padding?.Top ?? 0) - _style.Thickness - (_style.Padding?.Bottom ?? 0);
				Trace("end _bounds={0}", _bounds);
			}
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				LineLayout lower = new LineLayout(this);
				return lower;
			}
		}

		public override bool IsEmpty()
		{
			return false;
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			return false;
		}

		public override bool CollapseTopSpace()
		{
			return true; // stop collapsing
		}
	}
}
