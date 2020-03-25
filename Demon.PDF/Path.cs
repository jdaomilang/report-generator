using System;
using System.Collections.Generic;
using System.Text;
using Demon.Report.Types;

namespace Demon.PDF
{
	internal class Path : ContentFragment
	{
		private List<Position> _vertices;
		private Color _strokeColor;
		private Color _fillColor;
		private float _lineWidth;


		public Path(Rectangle rect, float lineWidth, Color strokeColor, Color fillColor)
		{
			_lineWidth = lineWidth;
			_strokeColor = strokeColor;
			_fillColor = fillColor;

			_vertices = new List<Position>();
			_vertices.Add(new Position(rect.Left,  rect.Top   ));
			_vertices.Add(new Position(rect.Right, rect.Top   ));
			_vertices.Add(new Position(rect.Right, rect.Bottom));
			_vertices.Add(new Position(rect.Left,  rect.Bottom));
			_vertices.Add(new Position(rect.Left,  rect.Top   ));
		}

		public Path(IList<Position> points, float lineWidth, Color strokeColor, Color fillColor)
		{
			_lineWidth = lineWidth;
			_strokeColor = strokeColor;
			_fillColor = fillColor;

			_vertices = new List<Position>(points);
		}

		public override byte[] GetStream()
		{
			if(_vertices.Count < 2)
				throw new Exception("Path must have at least two points.");
			if((_strokeColor == null) && (_fillColor == null))
				throw new Exception("Stroke color and fill color must not both be null.");

			StringBuilder sb = new StringBuilder();
			sb.Append(_lineWidth);
			sb.Append(" w ");

			if(_strokeColor != null)
			{
				sb.Append(string.Format(
					"{0} {1} {2} RG ",
					(double)_strokeColor.Red,
					(double)_strokeColor.Green,
					(double)_strokeColor.Blue));
			}
			if(_fillColor != null)
			{
				sb.Append(string.Format(
					"{0} {1} {2} rg ",
					(double)_fillColor.Red,
					(double)_fillColor.Green,
					(double)_fillColor.Blue));
			}
			sb.Append($"{_vertices[0].X} {_vertices[0].Y} m ");
			for(int index = 1; index < _vertices.Count; ++index)
				sb.Append($"{_vertices[index].X} {_vertices[index].Y} l ");
			
//			sb.Append("h");
			if((_strokeColor != null) && (_fillColor != null))
				sb.Append(" B"); // fill and then stroke
			else if(_strokeColor != null)
				sb.Append(" S"); // stroke
			else if(_fillColor != null)
				sb.Append(" f"); // fill

			sb.Append("\r\n");
			return Encoding.UTF8.GetBytes(sb.ToString());
		}

		public override string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(Document.Space(indentLevel));
			sb.Append("Path start=");
			sb.Append(_vertices[0].X);
			sb.Append(",");
			sb.Append(_vertices[0].Y);
			sb.Append(" end=");
			sb.Append(_vertices[_vertices.Count-1].X);
			sb.Append(",");
			sb.Append(_vertices[_vertices.Count-1].Y);
			sb.Append("\r\n");
			return sb.ToString();
		}
	}
}
