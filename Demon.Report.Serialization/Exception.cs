using System;
using System.Xml;
using System.Xml.Linq;

namespace Demon.Report.Serialization
{
	public class DesignException : Exception
	{
		public DesignException(string message)
			:base(message)
		{
		}

		public DesignException(string message, ILineNumber source)
			:base(message)
		{
			SetLineNumber(source.LineNumber, source.LinePosition);
		}

		public DesignException(Exception exception)
			:base(exception.Message, exception)
		{
		}

		public DesignException(string message, XElement element)
			:base(message)
		{
			IXmlLineInfo info = (IXmlLineInfo)element;
			SetLineNumber(info.LineNumber, info.LinePosition);
		}

		protected void SetLineNumber(int lineNumber, int linePosition)
		{
			Data.Add("LineNumber",   lineNumber  );
			Data.Add("LinePosition", linePosition);
		}
	}
}
