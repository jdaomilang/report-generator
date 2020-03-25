using System;

namespace Demon.Word
{
	internal abstract class Part : File
	{
		private string _name;

		public abstract string ContentType { get; }
		public override string Name { get { return _name; }}

		public Part(string name)
		{
			_name = name;
		}
	}
}
