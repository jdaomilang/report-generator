using System;

namespace Demon.Path
{
	public class PathException : Exception
	{
		public PathException()
		{
		}

		public PathException(string message)
			:base(message)
		{
		}

		public PathException(string message, string path, string context, int lineNumber, int linePosition)
			:base(message)
		{
			Data.Add("Path", path);
			Data.Add("Context", context);
			Data.Add("LineNumber", lineNumber);
			Data.Add("LinePosition", linePosition);
		}

		public string Path
		{
			get
			{
				string value = null;
				if(Data.Contains("Path"))
					 value = (string)Data["Path"];
				return value;
			}
			set
			{
				if(!Data.Contains("Path"))
					Data.Add("Path", value);
				else
					Data["Path"] = value;
			}
		}

		public string Context
		{
			get
			{
				string value = null;
				if(Data.Contains("Context"))
					 value = (string)Data["Context"];
				return value;
			}
			set
			{
				if(!Data.Contains("Context"))
					Data.Add("Context", value);
				else
					Data["Context"] = value;
			}
		}

		public int LineNumber
		{
			get
			{
				int value = 0;
				if(Data.Contains("LineNumber"))
					 value = (int)Data["LineNumber"];
				return value;
			}
			set
			{
				if(!Data.Contains("LineNumber"))
					Data.Add("LineNumber", value);
				else
					Data["LineNumber"] = value;
			}
		}

		public int LinePosition
		{
			get
			{
				int value = 0;
				if(Data.Contains("LinePosition"))
					 value = (int)Data["LinePosition"];
				return value;
			}
			set
			{
				if(!Data.Contains("LinePosition"))
					Data.Add("LinePosition", value);
				else
					Data["LinePosition"] = value;
			}
		}
	}

	public class AmbiguousPathException : PathException
	{
		public AmbiguousPathException()
		{
		}

		public AmbiguousPathException(string message)
			:base(message)
		{
		}

		public AmbiguousPathException(string message, string path, string context, int lineNumber, int linePosition)
			:base(message, path, context, lineNumber, linePosition)
		{
		}
	}

	public class NotFoundException : PathException
	{
		public NotFoundException()
		{
		}

		public NotFoundException(string message)
			:base(message)
		{
		}

		public NotFoundException(string message, string path, string context, int lineNumber, int linePosition)
			:base(message, path, context, lineNumber, linePosition)
		{
		}

		public string Reference
		{
			get
			{
				string value = null;
				if(Data.Contains("Reference"))
					 value = (string)Data["Reference"];
				return value;
			}
			set
			{
				if(!Data.Contains("Reference"))
					Data.Add("Reference", value);
				else
					Data["Reference"] = value;
			}
		}
	}

	public class ContextException : PathException
	{
		public ContextException()
		{
		}

		public ContextException(string message)
			:base(message)
		{
		}

		public ContextException(string message, string path, string context, int lineNumber, int linePosition)
			:base(message, path, context, lineNumber, linePosition)
		{
		}
	}
}
