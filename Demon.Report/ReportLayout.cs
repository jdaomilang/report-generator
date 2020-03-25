using System.Collections.Generic;
using System.Xml.Linq;
using Newtonsoft.Json;
using Demon.Report.Style;

namespace Demon.Report
{
	internal class ReportLayout : Layout
	{
		private string _designId;
		private string _inspectionTemplateId;
		public override LayoutType LayoutType { get {return LayoutType.Report;} }
		public override IStyle Style { get { return null; }}

		public override ReportLayout Report { get { return this; }}


		public ReportLayout(Generator generator)
			:base(generator, 0, 0)
		{
		}

		public ReportLayout(ReportLayout src)
			:base(src)
		{
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			_designId = _generator.ReportDesign.LoadString(root.Attribute("id"));
			_inspectionTemplateId = _generator.ReportDesign.LoadString(root.Attribute("inspectionTemplateId"));
		}

		public List<PageLayout> LayOut()
		{
			List<PageLayout> pages = new List<PageLayout>();
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace();

				//	Lay out our content on as many pages as needed. As we draft each
				//	page layout, it can expand to any number of layouts.
				foreach(PageLayout page in _subLayouts)
				{
					List<PageLayout> laidOut = page.LayOut();
					pages.AddRange(laidOut);
				}

				_generator.TraceLayoutActivity("Remove empty pages");
				pages.RemoveAll(p => p.IsEmpty());

				//	Now that the pages are laid out, lay out their headers and footers.
				//	We do this last so that we can include page numbers.
				_generator.TraceLayoutActivity("Write header and footer");
				for(int pageIndex = 0; pageIndex < pages.Count; ++pageIndex)
					pages[pageIndex].LayOutHeaderAndFooter(pageIndex + 1, pages.Count);
			}
			return pages;
		}

		public void Redraft()
		{
			foreach(Layout layout in _subLayouts)
			{
				PageLayout page = (PageLayout)layout;
				page.Redraft(page.Bounds.Top);
			}
		}

		public void Subset(int[] chapters)
		{
			if(chapters == null || chapters.Length == 0) return;

			List<Layout> subset = new List<Layout>();
			for(int x = 0; x < chapters.Length; ++x)
				subset.Add(_subLayouts[chapters[x]]);
			_subLayouts = subset;
		}

		public void Validate()
		{
			HashSet<string> ids = new HashSet<string>();
			AssertIdUnique(ids);
		}

		protected override void DebugPreviewCustomProperties(JsonWriter writer)
		{
			writer.WritePropertyName("_designId");
			writer.WriteValue(_designId);

			writer.WritePropertyName("_inspectionTemplateId");
			writer.WriteValue(_inspectionTemplateId);
		}
	}
}
