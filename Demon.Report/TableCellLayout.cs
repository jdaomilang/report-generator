using System.Collections.Generic;
using System.Xml.Linq;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class TableCellLayout : Layout
	{
		/// <summary>
		/// The actual cell width calculated from the designed width. Specified in user units. 
		/// </summary>
		public int CalculatedWidth { get; set; }

		private int _colSpan = 1;
		public int ColumnSpan { get { return _colSpan; }}

		protected TableCellStyle _style;
		public override IStyle Style { get { return _style; }}

		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}

		public override LayoutType LayoutType { get {return LayoutType.TableCell;} }


		public TableCellLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public TableCellLayout(TableCellLayout src)
			:base(src)
		{
			_style = src._style;
			_colSpan = src._colSpan;
			CalculatedWidth = src.CalculatedWidth;
		}

		/// <summary>
		/// Constructor used for creating cells in photo tables.
		/// </summary>
		public TableCellLayout(Generator generator, int lineNumber, int linePosition, object staticConditionsSatisfied)
			:base(generator, lineNumber, linePosition)
		{
			//	A cell layout created as part of a photo table doesn't have its
			//	own conditions because it's not defined in the design file. Any
			//	conditions governing the inclusion of a photo are evaluated in
			//	the photo table layout when it loads its content. So static
			//	conditions are implicitly satisfied here.
			_staticConditionsSatisfied = true;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_colSpan = _generator.ReportDesign.LoadInt(root.Attribute("colSpan")) ?? 1;
			_style = _generator.ReportDesign.LoadStyle<TableCellStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultTableCellStyle;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);
				_bounds = new Rectangle(bounds);

				//	Apply cell padding
				bounds.Left  += _style?.Padding?.Left  ?? 0;
				bounds.Right -= _style?.Padding?.Right ?? 0;
				bounds.Top   -= _style?.Padding?.Top   ?? 0;
				bounds.Bottom = bounds.Top;

				Position pos = bounds.TopLeft;
				foreach(Layout layout in _subLayouts)
				{
					bounds.Top = pos.Y;
					bounds.Bottom = bounds.Top;
					pos = layout.Draft(bounds);
					Trace("after laying out sublayout [{0}] pos={1}", layout.GetTraceInfo(), pos);
				}
				pos.Y -= _style?.Padding?.Bottom ?? 0;
				_bounds.Bottom = pos.Y;

				HandleEmpty();
				Trace("after first draft _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}
	}
}
