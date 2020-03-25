namespace Demon.Report.Types
{
	public class TraceContext
	{
		public int OwnerTrackingId; // the tracking id of the layout that set this context
		public bool TraceLayout;
		public bool TraceText;
		public bool TracePath;
		public bool TraceOutline;
	}

	public interface ITrace
	{
		/// <summary>
		/// Enable tracing of layout actions such as drafting and page break handling on all layouts.
		/// </summary>
		bool TraceLayout { get; }

		/// <summary>
		/// Enable tracing of text processing actions such as working out line breaks on all layouts.
		/// </summary>
		bool TraceText { get; }

		/// <summary>
		/// Enable tracing of resolution of template references to object references.
		/// </summary>
		bool TracePath { get; }

		/// <summary>
		/// Enable tracing of layout outlines.
		/// </summary>
		bool TraceOutline { get; }

		/// <summary>
		/// Trace only if the TraceLayout flag is set.
		/// </summary>
		void TraceLayoutActivity(string format, int skipFrames = 0, params object[] args);

		/// <summary>
		/// Trace only if the TracePath flag is set.
		/// </summary>
		void TracePathResolution(string text, int skipFrames = 0, params object[] args);

		/// <summary>
		/// Trace unconditionally.
		/// </summary>
		void Trace(string text, int skipFrames = 0, params object[] args);

		void PushTraceContext(TraceContext context);

		void PopTraceContext();
	}
}
