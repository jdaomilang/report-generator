namespace Demon.Report.Types
{
	public class PageBreakRules
	{
		public bool NewPage { get; set; }
		public bool KeepWithNext { get; set; }
		public int MinimumLines { get; set; }
		public float MaximumPosition { get; set; }

		public override string ToString()
		{
			string str = "";
			if(NewPage)               str += "new-page ";
			if(KeepWithNext)          str += "keep-with-next ";
			if(MinimumLines > 0)      str += $"min-lines={MinimumLines} ";
			if(MaximumPosition < 1.0) str += $"max-position={MaximumPosition}";
			return str.Trim();
		}
	}
}
