using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Demon.PDF
{
	public class ProcSet
	{
		public List<string> _names;

		public ProcSet()
		{
			_names = new List<string>();
		}

		public void Add(string name)
		{
			if(!_names.Contains(name))
				_names.Add(name);
		}

		public void Write(Stream file)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("/ProcSet [ ");
			foreach(string name in _names)
			{
				sb.Append("/");
				sb.Append(name);
				sb.Append(" ");
			}
			sb.Append("]\r\n");

			Document.WriteData(file,sb.ToString());
		}

		internal string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append("[ ");
			foreach(string name in _names)
			{
				sb.Append("/");
				sb.Append(name);
				sb.Append(" ");
			}
			sb.Append("]");

			return sb.ToString();
		}
	}
}
