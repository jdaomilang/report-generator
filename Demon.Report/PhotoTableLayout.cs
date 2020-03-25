using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Report.Style;
using Demon.Path;

namespace Demon.Report
{
	internal class PhotoTableLayout : Layout
	{
		public override LayoutType LayoutType { get {return LayoutType.PhotoTable;} }
		private int _columns;
		private int _columnWidth; // all columns have the same width
		private Size _maxPhotoSize;
		private bool _merge;
		private PhotoStyle _style;
		
		public int NumColumns { get { return _columns; }}
		public int ColumnWdith { get { return _columnWidth; }}
		public override IStyle Style { get { return _style; }}

		/// <summary>
		/// Photo objects found in the inspection, with their captions.
		/// </summary>
		private List<CompositePhotoLayout> _photos = new List<CompositePhotoLayout>();
		public int NumPhotos { get { return _photos.Count; }}

		
		public PhotoTableLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}
																
		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public PhotoTableLayout(PhotoTableLayout src)
			:base(src)
		{
			_columns = src._columns;
			_columnWidth = src._columnWidth;
			_maxPhotoSize = src._maxPhotoSize;
			_merge = src._merge;
			_style = src._style;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_style = _generator.ReportDesign.LoadStyle<PhotoStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultPhotoStyle;

			//	Default to a single column, overrideable by the designer. But if the
			//	design explicitly specifies zero or negative columns then that's an error.
			_columns = _generator.ReportDesign.LoadInt(root.Element(ns + "Columns")) ?? 1;
			if(_columns <= 0)
				throw new Exception("Photo table must have at least one column.");

//			if(_style.MaxWidth <= 0 || _style.MaxHeight <= 0)
//				throw new Exception("Max photo size cannot be zero or negative.");
			_maxPhotoSize = new Size(_style.MaxWidth, _style.MaxHeight);

			_merge = _generator.ReportDesign.LoadBoolean(root.Element(ns + "Merge")) ?? false;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				//	A photo table is implemented like this: Create one or more photo
				//	row layouts and add them all as normal sequential sublayouts of
				//	the table. Assign photos to the rows, where the number of photos
				//	in each row is defined by the table's _columns property. The last
				//	row will have fewer photos if the number of photos is not an even
				//	multiple of the number of columns.
				//
				//	A photo row is implemented as a normal table with two normal
				//	rows, one for the photos and the other for the captions. A photo
				//	row cannot be split across a page break, but the photo table
				//	can be split in the normal way by moving some photo rows onto
				//	the next page.

				//	If we have no photo content then don't draw ourself at all. We know
				//	that _columns is greater than zero because we checked it during load.
				if(_photos.Count == 0) return bounds.BottomLeft;

				//	Work out the column width. All photo cells will have the same width.
				//	We don't apply padding to the table or to the row, because the table
				//	is just structural and isn't part of the designed layout. When we
				//	get down to the photo layout we'll apply padding there.
				_columnWidth = bounds.Width / _columns;
				
				//	Work out the photo size from the column width
				if(_columnWidth < _maxPhotoSize.Width)
				{
					double scale = (double)_columnWidth / (double)_maxPhotoSize.Width;
					int scaledWidth = (int)(_maxPhotoSize.Width * scale);
					int scaledHeight = (int)(_maxPhotoSize.Height * scale);
					_maxPhotoSize = new Size(scaledWidth,scaledHeight);
				}

				for(int photoIndex = 0; photoIndex < _photos.Count; ) // will be incremented inside the inner loop
				{
					PhotoRowLayout row = new PhotoRowLayout(
						_columns, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
					AddSubLayout(row);
					while(row.NumPhotos < _columns)
					{
						if(photoIndex >= _photos.Count)
							break;

						CompositePhotoLayout photo = _photos[photoIndex++];
						row.AddPhoto(photo.PhotoLayout, photo.CaptionLayout);
					}
				}
			
				//	Pass the column width on to the rows
				foreach(PhotoRowLayout row in _subLayouts)
					row.SetColumnWidth(_columnWidth);
				
				Trace("end _bounds={0}", _bounds);
				return base.Draft(bounds);
			}
		}

		public override void LoadContent()
		{
			List<Photo> photos = new List<Photo>();
			switch(_sourcePath.TargetType)
			{
				case ContentSourceType.PhotoList:
					photos = LoadPhotoList();
					break;
				case ContentSourceType.TextEntry:
				case ContentSourceType.Checkbox:
				case ContentSourceType.RadioButton:
				case ContentSourceType.Section:
					photos = LoadAssociatedPhotos();
					break;
				case ContentSourceType.None:
					//	Meaningless, but acceptable. This allows the user to create a photo table
					//	layout in the report designer, leave its source unspecified, and carry
					//	on working on the design and previewing/generating reports.
					break;
				default:
					throw new ReportDesignException($"Invalid source type {_sourceObject.Type}", this);
			}

			//	For each photo, evaluate our content conditions with respect
			//	to any objects associated with the photo
			List<Photo> remove = new List<Photo>();
			foreach(Photo photo in photos)
			{
				IEnumerable<PhotoAssoc> assocs = _generator.UnitOfWork.Repository<PhotoAssoc>()
					.Query(a => a.DocumentId == _generator.DocumentId
											&&
											a.PhotoId == photo.Id)
					.Get(false);
				
				List<Reference> targets = new List<Reference>();
				foreach(PhotoAssoc assoc in assocs)
				{
					//	If the photo has no assoc then don't check conditions
					if((ContentSourceType)assoc.SourceType == ContentSourceType.None)
						continue;

					//	If the photo is associated with the form then don't check conditions
					if((ContentSourceType)assoc.SourceType == ContentSourceType.Form)
						continue;

					Reference target = Reference.Create((ContentSourceType)assoc.SourceType, assoc.SourceId, true);
					targets.Add(target);
				}
				foreach(Reference target in targets)
				{
					bool satisfies = _conditions.SatisfiesContentConditions(target);
					if(!satisfies)
					{
						//	One broken rule is all it takes to exclude the photo
						remove.Add(photo);
						break;
					}
				}
			}
			foreach(Photo photo in remove)
				photos.Remove(photo);

			//	Load the captions and create the photo and caption layouts
			foreach(Photo photo in photos)
			{
				string caption = GetPhotoCaption(photo, _generator);
				AddPhoto(photo, caption);
			}
		}

		private List<Photo> LoadPhotoList()
		{
			//	This method is called as part of LoadContent, which is part of the
			//	drafting stage, so our source reference has already been resolved
			//	because the generator resolves all references before drafting
			if(_sourceObject == null)
				throw new InvalidOperationException("Photo table source object is null");

			if(_sourceObject.Type != ContentSourceType.PhotoList)
				throw new Exception($"Expected source type PhotoList but found {_sourceObject.Type}");

			List<Photo> all = new List<Photo>();
			List<PhotoList> lists = ResolveMany<PhotoList>();
			foreach(PhotoList list in lists)
			{
				List<Photo> photos = _generator.UnitOfWork.Repository<Photo>()
					.Query(p => p.DocumentId == _generator.DocumentId
											&&
											p.ListId == list.Id)
					.Get(false)
					.ToList();

				photos.Sort(Compare);
				all.AddRange(photos);
			}
			return all;
		}

		private List<Photo> LoadAssociatedPhotos()
		{
			//	This method is called as part of LoadContent, which is part of the
			//	drafting stage, so our source reference has already been resolved
			//	because the generator resolves all references before drafting
			if(_sourceObject == null)
				throw new InvalidOperationException("Photo table source object is null");

			List<Photo> all = new List<Photo>();

			//	There's no good way to sort the associated objects, because they
			//	don't necessarily belong together in any kind of list. But we can
			//	sort the photos associated with any one object.
			IEnumerable<PhotoAssoc> assocs = _generator.UnitOfWork.Repository<PhotoAssoc>()
				.Query(a => a.DocumentId == _generator.DocumentId
										&&
										a.SourceType == (int)_sourceObject.Type
										&&
										a.SourceId == _sourceObject.Id)
				.Get(false);
			foreach(PhotoAssoc assoc in assocs)
			{
				List<Photo> photos = _generator.UnitOfWork.Repository<Photo>()
					.Query(p => p.Id == assoc.PhotoId)
					.Get(false)
					.ToList();

				photos.Sort(Compare);
				all.AddRange(photos);
			}
			return all;
		}

		/// <summary>
		/// Get the caption for a photo.
		/// </summary>
		public static string GetPhotoCaption(Photo photo, Generator generator)
		{
			//	Technically there could be more than one PhotoAssoc for a single photo
			//	but in practice the mobile app only supports assigning one.
			//TODO: support multiple assocs, even if the current version of the
			//app can't generate them?
			PhotoAssoc assoc = generator.UnitOfWork.Repository<PhotoAssoc>()
				.Query(a => a.PhotoId == photo.Id)
				.GetSingle();
			if(assoc == null) return null;

			string caption = null;
			switch((ContentSourceType)assoc.SourceType)
			{
				case ContentSourceType.TextEntry:
				{
					caption = generator.UnitOfWork.Repository<TextEntry>()
						.Query(t => t.DocumentId == photo.DocumentId
												&&
												t.Id == assoc.SourceId)
						.GetSingle()
						?.Text;
					break;
				}

				case ContentSourceType.Checkbox:
				{
					caption = generator.UnitOfWork.Repository<Checkbox>()
						.Query(b => b.DocumentId == photo.DocumentId
												&&
												b.Id == assoc.SourceId)
						.GetSingle()
						?.Caption;
					break;
				}

				case ContentSourceType.RadioButton:
				{
					caption = generator.UnitOfWork.Repository<RadioButton>()
						.Query(b => b.DocumentId == photo.DocumentId
												&&
												b.Id == assoc.SourceId)
						.GetSingle()
						?.Caption;
					break;
				}
			}
			return caption;
		}

		private void AddPhoto(Photo photo, string caption)
		{
			PhotoLayout photoLayout = new PhotoLayout(
				photo, _maxPhotoSize, _style, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);

			TextLayout captionLayout = new TextLayout(caption, _style.CaptionStyle, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);

			_photos.Add(new CompositePhotoLayout(photoLayout, captionLayout));
		}

		public override void MapFontCharacters()
		{
			//	This method gets called before drafting, when our photos are
			//	stored in a list of composite photo/caption layouts and not
			//	in our sublayouts
			foreach(CompositePhotoLayout layout in _photos)
				layout.CaptionLayout.MapFontCharacters();

//			foreach(Layout layout in _subLayouts)
//				layout.MapFontCharacters();
		}

		public override Layout MergeContent(Layout other)
		{
			if(_merge)
			{
				PhotoTableLayout src = other as PhotoTableLayout;
				if(src != null)
				{
					//	Move the source's photos into us
					_photos.AddRange(src._photos);
					src._photos.Clear();
				}
			}
			return base.MergeContent(this);
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
			int y = position.Y - pageLayout.BodyBox.Bottom;

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

			//	Treat each row as a line
			int minLines = _subLayouts.Count > _pageBreakRules.MinimumLines ? _pageBreakRules.MinimumLines : _subLayouts.Count;
			if(_pageSplitIndex < minLines)
			{
				Trace("split index {0} breaks min-lines {1} rule", _pageSplitIndex, minLines);
				return false;
			}


			//--------------------------------------------------------
			//	No rule was unsatisfied, so we're OK
			return true;
		}

		/// <summary>
		/// A paired photo layout and caption layout.
		/// </summary>
		/// Loaded at content load time, and will be separated onto the
		/// photo row's photo and caption rows at drafting time, after
		/// we've worked out which row the photo is to go on.
		private class CompositePhotoLayout
		{
			public PhotoLayout PhotoLayout;
			public TextLayout CaptionLayout;

			public CompositePhotoLayout(PhotoLayout photoLayout, TextLayout captionLayout)
			{
				PhotoLayout = photoLayout;
				CaptionLayout = captionLayout;
			}

			public CompositePhotoLayout(PhotoLayout photoLayout)
			{
				PhotoLayout = photoLayout;
			}
		}
	}
}
