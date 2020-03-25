using System.Collections.Generic;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class PhotoRowLayout : Layout
	{
		private int _numCells; // some cells could be empty
		private int _numPhotos;
		private TableRowLayout _photoRow;
		private TableRowLayout _captionRow;
		private int _cellWidth;

		public int NumPhotos { get { return _numPhotos; }}
		public TableRowLayout PhotoRow { get { return _photoRow; }}
		public TableRowLayout CaptionRow { get { return _captionRow; }}
		public override LayoutType LayoutType { get {return LayoutType.PhotoRow;} }


		public PhotoRowLayout(
			int numCells,
			Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			_numCells = numCells;
			_photoRow = new TableRowLayout(generator, null, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			_captionRow = new TableRowLayout(generator, null, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			AddSubLayout(_photoRow);
			AddSubLayout(_captionRow);

			//	A photo row layout doesn't have its own conditions because it's not
			//	defined in the design file. Any conditions governing the inclusion
			//	of a photo are evaluated in the photo table layout when it loads
			//	its content. So static conditions are implicitly satisfied here.
			_staticConditionsSatisfied = true;
		}

		/// <summary>
		/// Copy constructor used during page break handling.
		/// </summary>
		public PhotoRowLayout(PhotoRowLayout src)
			:base(src)
		{
			_numCells = src._numCells;
			_numPhotos = src._numPhotos;
			_cellWidth = src._cellWidth;
		}

		public void AddPhoto(PhotoLayout photoLayout, TextLayout captionLayout)
		{
			TableCellLayout outer = new TableCellLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition, null);
			_photoRow.AddSubLayout(outer);
			outer.AddSubLayout(photoLayout);

			outer = new TableCellLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition, null);
			_captionRow.AddSubLayout(outer);
			outer.AddSubLayout(captionLayout);

			++_numPhotos;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				//	Pad with blank cells if necessary
				while(_photoRow.NumSubLayouts < _numCells)
				{
					_photoRow.AddSubLayout(new TableCellLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition, null));
					_captionRow.AddSubLayout(new TableCellLayout(_generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition, null));
				}

				//	Remember the original bounds
				_bounds = new Rectangle(bounds);

				//	We don't apply padding to the table or to the row, because the table
				//	is just structural and isn't part of the designed layout. When we
				//	get down to the photo layout we'll apply padding there.

				Position pos = _photoRow.Draft(bounds);
				bounds.Bottom = pos.Y;
				bounds.Top = bounds.Bottom;
				pos = _captionRow.Draft(bounds);

				//	We can't call the base class implementation of Draft because that assumes
				//	the sublayouts are laid out vertically, but in the case of a photo row
				//	they're laid out horizontally
				_bounds.Bottom = pos.Y;
				HandleEmpty();
				Trace("end _bounds={0}", _bounds);
				return _bounds.BottomLeft;
			}
		}

		public void SetColumnWidth(int width)
		{
			_cellWidth = width;

			//	All columns in a photo table have the same width. Duplicate that single
			//	width into an array and then pass it to the photo and caption rows.
			int[] widths = new int[_numCells];
			for(int x = 0; x < widths.Length; ++x)
				widths[x] = _cellWidth;
			
			_photoRow.SetColumnWidths(widths);
			_captionRow.SetColumnWidths(widths);
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			//	We never split a photo row
			return false;
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				//	We never split a photo row, so on a page break we always move all
				//	of our content into a copy of ourself, and return that copy. This
				//	leaves ourself empty, and the base class implementation will then
				//	remove us from our container.
				PhotoRowLayout copy = new PhotoRowLayout(this);

				copy._photoRow = this._photoRow;
				copy._captionRow = this._captionRow;
				this.RemoveSubLayout(_photoRow);
				this.RemoveSubLayout(_captionRow);
				copy.AddSubLayout(_photoRow);
				copy.AddSubLayout(_captionRow);

				//	Empty ourself
				_subLayouts.Clear();
				_photoRow = null;
				_captionRow = null;

				return copy;
			}
		}

		public override Bump BumpPageSplitIndex()
		{
			//	We never split a photo row
			return Bump.Impossible;
		}

		protected override void Clear()
		{
			base.Clear();
			_photoRow = null;
			_captionRow = null;
		}
	}
}
