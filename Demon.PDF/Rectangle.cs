namespace Demon.PDF
{
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
			Left   = left;
			Right  = right;
			Bottom = bottom;
			Top    = top;
		}

		public Rectangle(Rectangle rect)
		{
			Left   = rect.Left;
			Right  = rect.Right;
			Top    = rect.Top;
			Bottom = rect.Bottom;
		}

		public Rectangle(Demon.Font.BoundingBox bbox)
		{
			Left   = bbox.Left;
			Right  = bbox.Right;
			Top    = bbox.Top;
			Bottom = bbox.Bottom;
		}

		/// <summary>
		/// Returns a string of the form "[ left bottom right top ]".
		/// </summary>
		public override string ToString()
		{
			//	The rectangle's Y position is the bottom
			return $"[ {Left} {Bottom} {Right} {Top} ]";
		}

		/// <summary>
		/// Returns a string of the form "[ left bottom right top ]".
		/// </summary>
		public string LongString
		{
			get
			{
				//	The rectangle's Y position is the bottom
				return string.Format(
					"[ {0,6:#####0} {1,6:#####0} {2,6:#####0} {3,6:#####0} ]",
					Left,Bottom,Right,Top);
			}
		}

		//	This is why we need a PDF-specific rectangle class.
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
