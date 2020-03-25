using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Demon.Report
{
	public class ReportDesignException : Exception
	{
		internal ReportDesignException(string message)
			:base(message)
		{
			SetSafeMessage(message);
		}

		internal ReportDesignException(string message, Layout layout)
			:base(message)
		{
			SetSafeMessage(message);
			SetLineNumber(layout.TrackingInfo.LineNumber, layout.TrackingInfo.LinePosition);
		}

		internal ReportDesignException(Exception exception)
			:base(exception.Message, exception)
		{
			//	In general we can't trust the message in System.Exception to be
			//	safe, so don't add it to the dictionary
		}

		/// <summary>
		/// Set an item in the exception's dictionary with the "Message" key. That
		/// item may be exposed to clients, so it should not contain any sensitive
		/// information.
		/// </summary>
		protected void SetSafeMessage(string message)
		{
			Data.Add("Message", message);
		}

		protected void SetLineNumber(int lineNumber, int linePosition)
		{
			Data.Add("LineNumber", lineNumber);
			Data.Add("LinePosition", linePosition);
		}

		public int LineNumber
		{
			get
			{
				int number = 0;
				object value = Data["LineNumber"];
				if(value != null)
					int.TryParse(value.ToString(), out number);
				return number;
			}
		}

		public int LinePosition
		{
			get
			{
				int position = 0;
				object value = Data["LinePosition"];
				if(value != null)
					int.TryParse(value.ToString(), out position);
				return position;
			}
		}
	}

	public class InvalidConditionException : ReportDesignException
	{
		internal InvalidConditionException(string message, Layout layout)
			:base(message, layout)
		{
		}

		internal InvalidConditionException(string message, XElement element)
			:base(message)
		{
			int lineNumber = ((IXmlLineInfo)element).LineNumber;
			int linePos = ((IXmlLineInfo)element).LinePosition;
			SetLineNumber(lineNumber, linePos);
		}
	}

	public class InvalidDesignException : ReportDesignException
	{
		internal InvalidDesignException(XmlSchemaException exception)
			:base(exception)
		{
			SetSafeMessage(exception.Message);
			SetLineNumber(exception.LineNumber, exception.LinePosition);
		}

		internal InvalidDesignException(IList<string> jsonErrors)
			:base("Design file is invalid.")
		{
			for(int x = 0; x < jsonErrors.Count; ++x)
				Data.Add($"Error {(x+1)}", jsonErrors[x]);
		}

		internal InvalidDesignException(Newtonsoft.Json.JsonReaderException exception)
			:base(exception)
		{
			SetSafeMessage(exception.Message);
			SetLineNumber(exception.LineNumber, exception.LinePosition);
			Data.Add("Path", exception.Path);
		}

		internal InvalidDesignException(string message, Layout layout)
			:base(message, layout)
		{
		}

#if JSON_SCHEMA
		internal InvalidDesignException(Newtonsoft.Json.Schema.ValidationError error)
			:base(error.Message)
		{
			SetLineNumber(error.LineNumber, error.LinePosition);
			Data.Add("Path", error.Path);
			if(error.Value != null)
				Data.Add("Value", error.Value);
			if(error.SchemaId != null)
				Data.Add("SchemaId", error.SchemaId);
			Data.Add("ErrorType", error.ErrorType.ToString());

			if(error.ChildErrors != null)
				Data.Add("ChildErrors", error.ChildErrors);
		}

		internal InvalidDesignException(Newtonsoft.Json.JsonSerializationException exception)
			:base(exception)
		{
			SetSafeMessage(exception.Message);
			SetLineNumber(exception.LineNumber, exception.LinePosition);
			Data.Add("Path", exception.Path);
		}

		internal InvalidDesignException(Newtonsoft.Json.Schema.JSchemaException exception)
			:base(exception)
		{
			SetSafeMessage(exception.Message);
		}

		internal InvalidDesignException(Newtonsoft.Json.Schema.JSchemaReaderException exception)
			:base(exception)
		{
			SetSafeMessage(exception.Message);
			SetLineNumber(exception.LineNumber, exception.LinePosition);
			Data.Add("Path", exception.Path);
		}
#endif // JSON_SCHEMA
	}
}
