using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Demon.PDF
{
	internal class ContentStream : IIndirectObject
	{
		public void Write(Stream file, ObjectReference streamRef, List<ContentFragment> fragments)
		{
			streamRef.ByteOffset = file.Position;

			//	We need to know the full length of the stream so that we can
			//	write it into the object header
			byte[] fullStream = new byte[0];
			foreach(ContentFragment fragment in fragments)
			{
				int pos = fullStream.Length;
				byte[] fragStream = fragment.GetStream();
				int length = fullStream.Length + fragStream.Length;
				System.Array.Resize(ref fullStream, length);
				fragStream.CopyTo(fullStream, pos);
			}


			StringBuilder sb = new StringBuilder();
			sb.Append(streamRef.Id);
			sb.Append(" obj\r\n<</Length ");
			sb.Append(fullStream.Length);
			sb.Append(">>\r\nstream\r\n");
			Document.WriteData(file, sb.ToString());

			file.Write(fullStream, 0, fullStream.Length);
			Document.WriteData(file, "\r\nendstream\r\nendobj\r\n");
		}
	}

	internal abstract class ContentFragment
	{
		public abstract byte[] GetStream();
		public abstract string Dump(int indentLevel);
		public virtual void ExpandDocumentProperties(Document doc, Page page) {}
	}
}
