using System.Text;

namespace Demon.Report.Types
{
	public class TrackingInfo
	{
		private static TrackingInfo _none = new TrackingInfo(null, 0, 0, 0, 0);
		public static TrackingInfo None { get { return _none; }}

		public string Name { get; private set; }
		public int LineNumber { get; private set; }
		public int LinePosition { get; private set; }
		public int TrackingId { get; private set; }
		public int SourceTrackingId { get; private set; }

		public TrackingInfo(
			string name,
			int lineNumber,
			int linePosition,
			int trackingId,
			int sourceTrackingId)
		{
			Name = name;
			LineNumber = lineNumber;
			LinePosition = linePosition;
			TrackingId = trackingId;
			SourceTrackingId = sourceTrackingId;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("#");
			sb.Append(TrackingId);
			sb.Append("@");
			sb.Append(LineNumber);
			sb.Append(":");
			sb.Append(LinePosition);
			if(Name != null)
			{
				sb.Append(" $[");
				sb.Append(Name);
				sb.Append("]");
			}
			return sb.ToString();
		}
	}
}
