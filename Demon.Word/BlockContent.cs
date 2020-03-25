using System;
using System.Xml.Linq;

namespace Demon.Word
{
	public interface IBlockContent
	{
		void Write(XElement parent);
	}
}
