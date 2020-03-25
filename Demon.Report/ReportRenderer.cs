using System;
using System.Collections.Generic;
using System.IO;
using Demon.Report.Types;

namespace Demon.Report
{
	internal abstract class ReportRenderer
	{
		public abstract Stream Render(
			ReportLayout report,
			string documentId, string designId, string title,
			int version, DateTimeOffset timestamp,
			string photoUri, string resourceUri, bool drawRules, bool drawPageBoxes,
			Generator generator, ITrace tracer);
	}
}
