namespace Demon.Report.Types
{
	public class Position
	{
		public int X;
		public int Y;

		public Position(int x, int y)
		{
			X = x;
			Y = y;
		}

		public Position(Position src)
		{
			X = src.X;
			Y = src.Y;
		}

		public override System.String ToString()
		{
			return string.Format("{0},{1}",X,Y);
		}
	}
}
