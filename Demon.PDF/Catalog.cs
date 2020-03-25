using System.IO;
using System.Text;

namespace Demon.PDF
{
	internal class Catalog : IIndirectObject
	{
		public void Write(Stream file, ObjectReference reference, Document doc)
		{
			reference.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(reference.Id);
			sb.Append(" obj\r\n<<\r\n/Type /Catalog\r\n");

			ObjectReference pages = doc.GetReference(doc.Pages);
			sb.Append("/Pages ");
			sb.Append(pages.Reference);
			sb.Append("\r\n");

//			Dictionary names = new Dictionary(this);
//			_catalog.AddEntry("/Names",names);

//			Dictionary metadata = new Dictionary(this);
//			_catalog.AddEntry("/Metadata",metadata);

			sb.Append(">>\r\nendobj\r\n");
			Document.WriteData(file, sb.ToString());
		}
	}
}
