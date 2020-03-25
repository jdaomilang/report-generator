using System.Xml.Linq;

namespace Demon.Report
{
	internal class GroupLayout : Layout
	{
		public override LayoutType LayoutType { get {return LayoutType.Group;} }

		public GroupLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public GroupLayout(GroupLayout src)
			:base(src)
		{
		}

		public override void Load(XElement root)
		{
			base.Load(root);
		}
	}
}
