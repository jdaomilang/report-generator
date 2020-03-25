using System.Collections.Generic;

namespace Demon.Report.Types
{
	public struct Size
	{
		public int Width;
		public int Height;

		public Size(int width, int height)
		{
			Width = width;
			Height = height;
		}

		public Size(Size src)
		{
			Width = src.Width;
			Height = src.Height;
		}

		public Size(System.Drawing.Size src)
		{
			Width = src.Width;
			Height = src.Height;
		}

		public override string ToString()
		{
			return $"{Width},{Height}";
		}

		public static bool operator==(Size r1, Size r2)
		{
			if(r1.Width  != r2.Width  ) return false;
			if(r1.Height != r2.Height) return false;
			return true;
		}

		public static bool operator!=(Size r1, Size r2)
		{
			if(r1.Width  != r2.Width  ) return true;
			if(r1.Height != r2.Height) return true;
			return false;
		}

		public override bool Equals(object obj)
		{
			if(!(obj is Size)) return false;
			return this == (Size)obj;
		}

		public override int GetHashCode()
		{
			//	In C#, by default, integer arithmetic is done in an unchecked
			//	context, which means that on overflow the excess significant
			//	bits are discarded rather than throwing an exception. That's
			//	exactly what we want.
			return Width + Height;
		}
	}

	public struct Rectangle
	{
		public int Left;
		public int Right;
		public int Top;
		public int Bottom;

		public int Width { get {return Right - Left;}}
		public int Height { get {return Top - Bottom;}}

		public Rectangle(int left, int bottom, int right, int top)
		{
			Left = left;
			Right = right;
			Bottom = bottom;
			Top = top;
		}

		public Rectangle(Rectangle rect)
		{
			Left = rect.Left;
			Right = rect.Right;
			Top = rect.Top;
			Bottom = rect.Bottom;
		}

//		public Position Origin { get { return BottomLeft; }}
		public Position TopLeft { get { return new Position(Left, Top); }}
		public Position BottomLeft { get { return new Position(Left, Bottom); }}
		public Position BottomRight{ get { return new Position(Right, Bottom); }}
		public Position TopRight { get { return new Position(Right, Top); }}

		public IList<Position> Points
		{
			get
			{
				List<Position> points = new List<Position>();
				points.Add(BottomLeft);
				points.Add(BottomRight);
				points.Add(TopRight);
				points.Add(TopLeft);
				return points;
			}
		}

		public bool Contains(Position pos)
		{
			if(pos.X < Left) return false;
			if(pos.X > Right) return false;
			if(pos.Y < Bottom) return false;
			if(pos.Y > Top) return false;
			return true;
		}

		/// <summary>
		/// Returns a string of the form "[left bottom right top]"
		/// </summary>
		public override string ToString()
		{
			//	The rectangle's Y position is the bottom
			return $"[{Left} {Bottom} {Right} {Top}]";
		}

		/// <summary>
		/// Returns a string of the form "[left bottom right top]"
		/// </summary>
		public string LongString
		{
			get
			{
				//	The rectangle's Y position is the bottom
				return string.Format(
					"[{0,6:#####0} {1,6:#####0} {2,6:#####0} {3,6:#####0}]",
					Left,Bottom,Right,Top);
			}
		}

		public string Specification { get {return ToString();}}

		public static bool operator==(Rectangle r1, Rectangle r2)
		{
			if(r1.Left   != r2.Left  ) return false;
			if(r1.Bottom != r2.Bottom) return false;
			if(r1.Right  != r2.Right ) return false;
			if(r1.Top    != r2.Top   ) return false;
			return true;
		}

		public static bool operator!=(Rectangle r1, Rectangle r2)
		{
			if(r1.Left   != r2.Left  ) return true;
			if(r1.Bottom != r2.Bottom) return true;
			if(r1.Right  != r2.Right ) return true;
			if(r1.Top    != r2.Top   ) return true;
			return false;
		}

		public static readonly Rectangle Zero = new Rectangle(0,0,0,0);

		public override bool Equals(object obj)
		{
			if(!(obj is Rectangle)) return false;
			return this == (Rectangle)obj;
		}

		public override int GetHashCode()
		{
			//	In C#, by default, integer arithmetic is done in an unchecked
			//	context, which means that on overflow the excess significant
			//	bits are discarded rather than throwing an exception. That's
			//	exactly what we want.
			return Left + Bottom + Right + Top;
		}
	}
}
