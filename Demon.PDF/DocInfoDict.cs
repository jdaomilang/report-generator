using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Demon.PDF
{
	public class DocumentInformationDictionary : IIndirectObject
	{
		private Dictionary<string, string> _info;

		public DocumentInformationDictionary(Dictionary<string, string> info)
		{
			_info = new Dictionary<string, string>(info);
		}

		public void Write(Stream file, ObjectReference dictRef)
		{
			dictRef.ByteOffset = file.Position;

			StringBuilder sb = new StringBuilder();
			sb.Append(dictRef.Id);
			sb.Append(" obj\r\n<<");

			foreach(KeyValuePair<string, string> item in _info)
			{
				sb.Append("/");
				sb.Append(item.Key);
				sb.Append(" (");
				sb.Append(item.Value);
				sb.Append(")\r\n");
			}

			sb.Append(">>\r\n");
			sb.Append("endobj\r\n");
			Document.WriteData(file,sb.ToString());
		}
	}
}
