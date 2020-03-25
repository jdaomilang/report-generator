using System;
using System.Text;
using Demon.Report.Types;
using Demon.Report.Style;
using Demon.Core.Domain;
using Demon.Path;

namespace Demon.Report
{
	//	A ListItemLayout is a wrapper for a normal layout such as a TextLayout.
	//	The ListItemLayout treats the wrapped layout as a single item in the
	//	list, and applies a bullet as appropriate based on the list style.
	//	A ListItemLayout can only be created implicitly by the list layout, and
	//	cannot appear in a design file. That is, the design file specification
	//	does not define a ListItemLayout type.
	internal class ListItemLayout : Layout
	{
		private Layout _contentLayout;
		private Layout _bulletLayout;
		private ListStyle _listStyle;
		private int _number;

		public override LayoutType LayoutType { get {return LayoutType.ListItem;} }
		public Layout ContentLayout { get { return _contentLayout; }}
		public Layout BulletLayout { get { return _bulletLayout; }}
		public override IStyle Style { get { return _listStyle; }}

		/// <summary>
		///	The design syntax for bullet numbering is the same as for
		///	document properties, but parsing it is a simple text
		///	replacement because there's only one property name supported:
		///	"bullet!number".
		/// </summary>
		public static string BulletNumberProperty = $"{Marker.DocumentProperty.Start}Bullet!Number{Marker.DocumentProperty.End}";


		public ListItemLayout(
			Layout content, ListStyle style, int number,
			Generator generator, int lineNumber, int linePosition, TraceContext traceContext)
			:base(generator, lineNumber, linePosition)
		{
			_listStyle = style;

			//	Set these data before doing the interesting stuff, in case we
			//	need to log errors
			_traceContext = traceContext;
			_trackingInfo = content.TrackingInfo;

			_contentLayout = content;
			AddSubLayout(content);

			SetNumber(number);
		}

		public ListItemLayout(ListItemLayout src)
			:base(src)
		{
			_number = src._number;
			_listStyle = src._listStyle;
		}

		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied) return bounds.BottomLeft;

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);
				//	Remember the original bounds
				_bounds = new Rectangle(bounds);

				//	The list item doesn't have any style of its own, including
				//	padding. The bullet and content layouts occupy the item
				//	layout fully without padding, but they can define their
				//	own internal padding. Position the bullet and content
				//	layouts offset from our bounds left by the indents defined
				//	by the list style.
				Rectangle bulletBounds = new Rectangle();
				bulletBounds.Left   = _bounds.Left + _listStyle.BulletIndent;
				bulletBounds.Bottom = _bounds.Bottom;
				bulletBounds.Right  = _bounds.Left + _listStyle.ItemIndent;
				bulletBounds.Top    = _bounds.Top;
				Rectangle contentBounds = new Rectangle();
				contentBounds.Left   = _bounds.Left + _listStyle.ItemIndent;
				contentBounds.Bottom = _bounds.Bottom;
				contentBounds.Right  = _bounds.Right;
				contentBounds.Top    = _bounds.Top;

				//	Lay out the bullet and the contents
				_bulletLayout.Draft(bulletBounds);
				_contentLayout.Draft(contentBounds);

				//	Set our bounds bottom to whichever is lower, bullet or content.
				_bounds.Bottom =
					HasBullet
					? Math.Min(_bulletLayout.Bounds.Bottom, _contentLayout.Bounds.Bottom)
					: _contentLayout.Bounds.Bottom;

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

				//	If we have no content then there's nothing to do
				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//	Redraft the bullet and the contents
				_contentLayout.Redraft(top);
				_bulletLayout.Redraft(top);

				//	Set our bounds bottom to whichever is lower, bullet or content
				int bottom = Math.Min(_bulletLayout.Bounds.Bottom, _contentLayout.Bounds.Bottom);

				//	Reposition ourself
				_bounds.Top = top;
				_bounds.Bottom = bottom;
				Trace("end _bounds={0}", _bounds);
			}
		}

		/// <summary>
		/// Assess how to handle a page break.
		/// </summary>
		public override PageDisposition AssessPageBreak(Rectangle bodyBox)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("_bounds={0} bodyBox={1}", _bounds, bodyBox);
				PageDisposition disposition = PageDisposition.ThisPage;

				//	We never split the bullet, so if the bullet doesn't fit on the
				//	page then we need to move entirely to the next page
				disposition = _bulletLayout.AssessPageBreak(bodyBox);
				if(disposition != PageDisposition.ThisPage)
					return PageDisposition.NewPage;

				//	We're happy to split our content and duplicate the bullet
				disposition = _contentLayout.AssessPageBreak(bodyBox);
				return disposition;
			}
			//	ListItemLayout doesn't have to worry about keep-with-next because
			//	keep-with-next is implemented by the container layout, and the
			//	list item layout doesn't really have sublayouts. The bullet and
			//	content layouts are technically sublayouts but they don't function
			//	as such.
		}

		/// <summary>
		/// Split this layout at the position determined during an earlier call to
		/// AssessPageBreak. Moves sublayouts below the split into a new layout
		/// and returns that new layout.
		/// </summary>
		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	If we come here then we're either splitting or moving entirely to
				//	the next page. This situation could be caused by either the bullet
				//	or the content being too long to fit on the current page. We never
				//	split the bullet, and the bullet and content are always top-aligned,
				//	so if it's the bullet that's too long then we want to move entirely
				//	to the next page, regardless of the length of the content. In either
				//	case we want to split the content (possibly leaving the first half
				//	of the split empty) and duplicate the bullet (leaving either the
				//	original or the copy empty).

				//	Create a copy of ourself
				ListItemLayout lower = (ListItemLayout)ShallowCopy();

				//	Split our content and move the lower half to the copy
				Layout contentCopy = _contentLayout.DoPageBreak();
				lower.AddSubLayout(contentCopy);
				lower._contentLayout = contentCopy;

				//	If the content was moved entirely to the next page, leaving the
				//	original content layout empty, then the copy needs a valid and fully
				//	functional bullet. If the content was split then the copy doesn't
				//	need a visible bullet, and shouldn't be capable of being renumbered,
				//	but everything's a lot easier if we give it an empty bullet layout
				//	anyway.
				Layout bulletCopy = null;
				if(!_contentLayout.IsEmpty()) // content was split
					bulletCopy = new TextLayout("", null, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				else
					bulletCopy = _bulletLayout.DeepCopy();
				lower.AddSubLayout(bulletCopy);
				lower._bulletLayout = bulletCopy;

				return lower;
			}
		}

		public override bool IsEmpty()
		{
			if(base.IsEmpty()) return true;

			//	If we have no content then we're empty, regardless of whether
			//	we have a bullet layout
			return _contentLayout.IsEmpty();
		}

		protected override void Clear()
		{
			base.Clear();
			_contentLayout = null;
			_bulletLayout = null;
		}

		public bool HasBullet { get { return !_bulletLayout.IsEmpty();}}

		private void SetNumber(int number)
		{
			//	The bullet layout must become _subLayouts[1]
			if(_subLayouts.Count == 0)
				throw new InvalidOperationException("Must set content before setting the bullet number.");
			if(_subLayouts.Count != 1)
				throw new InvalidOperationException("Bullet number is already set.");

			_number = number;

			//	Get the bullet style, which can vary based on whether the source control
			//	is selected, and then construct a corresponding text style
			BulletStyle bulletStyle = GetBulletStyle();
			TextStyle style = new TextStyle();
			style.Base         = _listStyle.ItemStyle;
			style.Font         = bulletStyle.Font;
			style.Color        = bulletStyle.Color;
			style.Padding      = bulletStyle.Padding;
			style.Name         = bulletStyle.Name;
			style.LineNumber   = bulletStyle.LineNumber;
			style.LinePosition = bulletStyle.LinePosition;

			//	Get the bullet text, which is defined by the bullet style
			string text = GetBulletText(bulletStyle);

			//	Create the bullet layout
			_bulletLayout = new	TextLayout(text, style, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			AddSubLayout(_bulletLayout); // must become _subLayouts[1]
		}

		public void Renumber(int number)
		{
			//	Remebmber the bounds of the current bullet point, remove it, set the
			//	new number, and then redraft the bullet point in the same bounds.
			Rectangle bounds = _bulletLayout.Bounds;
			RemoveSubLayout(_bulletLayout);
			SetNumber(number);
			_bulletLayout.Draft(bounds);
		}

		private string GetBulletText(BulletStyle style)
		{
			string number = null;
			switch(style.NumberStyle)
			{
				case ListNumberStyle.Bullet:
					number = style.BulletText;
					break;
				case ListNumberStyle.Number:
					number = _number.ToString();
					break;
				case ListNumberStyle.AlphaLower:
					number = Alpha(_number);
					break;
				case ListNumberStyle.AlphaUpper:
					number = Alpha(_number).ToUpperInvariant();
					break;
				case ListNumberStyle.RomanLower:
					number = Roman(_number);
					break;
				case ListNumberStyle.RomanUpper:
					number = Roman(_number).ToUpperInvariant();
					break;
				case ListNumberStyle.GreekLower:
					number = Greek(_number);
					break;
				case ListNumberStyle.GreekUpper:
					number = Greek(_number).ToUpperInvariant();
					break;
				default:
					number = _number.ToString();
					break;
			}

			return style.BulletText.Replace(BulletNumberProperty, number);
		}

		private BulletStyle GetBulletStyle()
		{
			SelectionState selected = IsSourceSelected();

			BulletStyle style = _listStyle.BulletStyle;
			switch(selected)
			{
				case SelectionState.Selected:
					style = _listStyle.SelectedBulletStyle;
					break;
				case SelectionState.NotSelected:
					style = _listStyle.UnselectedBulletStyle;
					break;
				case SelectionState.NotSelectable:
					style = _listStyle.BulletStyle;
					break;
			}

			//	If either the selected or unselected bullet style was chosen, but is null, then
			//	use the regular bullet style
			if(style == null) style = _listStyle.BulletStyle;
			return style;
		}

		private SelectionState IsSourceSelected()
		{
			//	The notion of being selected makes sense only for checkboxes, radio buttons
			//	and calculations
			Reference source = _contentLayout.Source;
			switch(source.Type)
			{
				case ContentSourceType.Checkbox:
					Checkbox checkbox = _generator.ResolveOne<Checkbox>(source);
					return checkbox.State != 0 ? SelectionState.Selected : SelectionState.NotSelected;

				case ContentSourceType.RadioButton:
					RadioButton radio = _generator.ResolveOne<RadioButton>(source);
					return radio.State != 0 ? SelectionState.Selected : SelectionState.NotSelected;

				case ContentSourceType.Calculation:
					Calculation calc = _generator.ResolveOne<Calculation>(source);
					return calc.State != 0 ? SelectionState.Selected : SelectionState.NotSelected;

				default:
					return SelectionState.NotSelectable;
			}
		}

		private string Alpha(int number)
		{
			if(number <= 0) return number.ToString();

			StringBuilder sb = new StringBuilder();
			while(number > 0)
			{
				int over = number % 26;
				if(over == 0) over = 26;
				char c = (char)('a' - 1 + over); 
				sb.Insert(0,c);
				number -= 26;
			}
			return sb.ToString();
		}

		/// <summary>
		/// Only goes up to 3,999.
		/// </summary>
		private string Roman(int number)
		{
			if(number <= 0) return number.ToString();
			if(number >= 4000) return number.ToString();

			StringBuilder sb = new StringBuilder();
			while(number >= 1000)
			{
				sb.Append("m");
				number -= 1000;
			}
			while(number >= 900)
			{
				sb.Append("cm");
				number -= 900;
			}
			while(number >= 500)
			{
				sb.Append("d");
				number -= 500;
			}
			while(number >= 400)
			{
				sb.Append("cd");
				number -= 400;
			}
			while(number >= 100)
			{
				sb.Append("c");
				number -= 100;
			}
			while(number >= 90)
			{
				sb.Append("xc");
				number -= 90;
			}
			while(number >= 50)
			{
				sb.Append("l");
				number -= 50;
			}
			while(number >= 40)
			{
				sb.Append("xl");
				number -= 40;
			}
			while(number >= 10)
			{
				sb.Append("x");
				number -= 10;
			}
			while(number >= 9)
			{
				sb.Append("ix");
				number -= 9;
			}
			while(number >= 5)
			{
				sb.Append("v");
				number -= 5;
			}
			while(number >= 4)
			{
				sb.Append("iv");
				number -= 4;
			}
			while(number >= 1)
			{
				sb.Append("i");
				number -= 1;
			}

			return sb.ToString();
		}

		private string Greek(int number)
		{
			if(number <= 0) return number.ToString();

			StringBuilder sb = new StringBuilder();
			while (number > 0)
			{
				int over = number % 24;
				if(over == 0) over = 24;
				char c = '?';
				switch(over)
				{
					case  1: c = 'α'; break;
					case  2: c = 'β'; break;
					case  3: c = 'γ'; break;
					case  4: c = 'δ'; break;
					case  5: c = 'ε'; break;
					case  6: c = 'ζ'; break;
					case  7: c = 'η'; break;
					case  8: c = 'θ'; break;
					case  9: c = 'ι'; break;
					case 10: c = 'κ'; break;
					case 11: c = 'λ'; break;
					case 12: c = 'μ'; break;
					case 13: c = 'ν'; break;
					case 14: c = 'ξ'; break;
					case 15: c = 'ο'; break;
					case 16: c = 'π'; break;
					case 17: c = 'ρ'; break;
					case 18: c = 'σ'; break;
					case 19: c = 'τ'; break;
					case 20: c = 'υ'; break;
					case 21: c = 'φ'; break;
					case 22: c = 'χ'; break;
					case 23: c = 'ψ'; break;
					case 24: c = 'ω'; break;
				}
				sb.Insert(0,c);
				number -= 24;
			}
			return sb.ToString();
		}

		private enum SelectionState
		{
			NotSelectable,
			Selected,
			NotSelected
		}
	}
}
