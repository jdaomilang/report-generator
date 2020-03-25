namespace Demon.PDF
{
	public class Color
	{
		public double Red { get; set; }
		public double Green { get; set; }
		public double Blue { get; set; }

		public Color()
		{
		}

		public Color(double red, double green, double blue)
		{
			Red   = red;
			Green = green;
			Blue  = blue;
		}
	}
}
