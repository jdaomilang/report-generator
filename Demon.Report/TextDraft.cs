using System.Collections.Generic;
using Demon.Report.Types;
using Demon.Report.Style;

namespace Demon.Report
{
	/// <summary>
	/// A line of text with draft positioning.
	/// </summary>
	internal class LineDraft
	{
		private List<StrokeDraft> _strokes;
		private List<UnderlineDraft> _paths;
		private Rectangle _bounds;
		private string _roughText;
		private bool _isParagraph;

		public List<StrokeDraft> Strokes { get { return _strokes; }}
		public List<UnderlineDraft> Paths { get { return _paths; }}
		public Rectangle Bounds { get { return _bounds; } set { _bounds = value; } }
		public string RoughText { get { return _roughText; }}
		public bool IsParagraph { get { return _isParagraph; }}

		public LineDraft(TextLine line, Rectangle bounds)
		{
			_strokes = new List<StrokeDraft>();
			_paths = new List<UnderlineDraft>();
			_bounds = new Rectangle(bounds);;
			_roughText = line.GetRoughText();
			_isParagraph = line is ParagraphLine;
		}

		public LineDraft(LineDraft other)
		{
			_strokes = new List<StrokeDraft>(other._strokes.Count);
			foreach(StrokeDraft stroke in other._strokes)
				_strokes.Add(new StrokeDraft(stroke));

			_paths = new List<UnderlineDraft>(other._paths.Count);
			foreach(UnderlineDraft path in other._paths)
				_paths.Add(new UnderlineDraft(path));

			_bounds = new Rectangle(other._bounds);
			_roughText = other._roughText;
			_isParagraph = other._isParagraph;
		}

		public void Add(StrokeDraft stroke)
		{
			_strokes.Add(stroke);
		}

		public void Add(UnderlineDraft path)
		{
			_paths.Add(path);
		}

		public void Redraft(int top)
		{
			int offset = top - _bounds.Top;
			_bounds.Top    += offset;
			_bounds.Bottom += offset;
			_strokes.ForEach(s => s.Offset(offset));
			_paths.  ForEach(p => p.Offset(offset));
		}

		public override string ToString()
		{
			return $"{_bounds.LongString} : {_roughText}";
		}
	}

	/// <summary>
	/// A text stroke with draft positioning.
	/// </summary>
	internal class StrokeDraft
	{
		private Stroke _stroke;
		private Position _position;

		public Stroke Stroke { get { return _stroke; }}
		public Position Position { get { return _position; }}

		public StrokeDraft(Stroke stroke, Position position)
		{
			_stroke = stroke;
			_position = position;
		}

		public StrokeDraft(StrokeDraft other)
		{
			//	We can just take a reference to the other draft's stroke
			//	because strokes are never updated after creation
			_stroke = other._stroke;

			//	But we have to take a copy of the position because draft
			//	positions are updated on redrafting
			_position = new Position(other.Position.X,other.Position.Y);
		}

		public void Offset(int offset)
		{
			_position.Y += offset;
		}

		public override string ToString()
		{
			return $"{_position} : {_stroke.Text}";
		}
	}

	/// <summary>
	/// A text underline with draft positioning.
	/// </summary>
	internal class UnderlineDraft
	{
		private List<Position> _points;
		private float _thickness;
		private Color _color;

		public List<Position> Path { get { return _points; }}
		public float Thickness { get { return _thickness; }}
		public Color Color { get { return _color; }}

		public UnderlineDraft(List<Position> points, float thickness, Color color)
		{
			_points = new List<Position>();
			foreach(Position point in points)
				_points.Add(new Position(point));
			_thickness = thickness;
			_color = color;
		}

		public UnderlineDraft(UnderlineDraft other)
		{
			_points = new List<Position>();
			foreach(Position point in other._points)
				_points.Add(new Position(point));

			_thickness = other._thickness;
			_color = other._color;
		}

		public void Offset(int offset)
		{
			foreach(Position pos in _points)
				pos.Y += offset;
		}

		public override string ToString()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			sb.Append(_thickness);
			sb.Append(" ");
			sb.Append(_color);
			sb.Append(" : ");
			foreach(Position point in _points)
			{
				sb.Append(point);
				sb.Append(" ");
			}
			return sb.ToString();
		}
	}
}
