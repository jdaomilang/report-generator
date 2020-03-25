using System;
using System.Reflection;
using Demon.Path;

namespace Demon.Report
{
	internal abstract class Condition
	{
		//	For runtime debugging, when the only tool you have is
		//	the log file. These things are never made visible to
		//	the user.
		protected int _lineNumber;
		protected int _linePosition;

		//	For development-time debugging, to help identify conditions
		//	created and copied during page layout.
		protected int _trackingId;
		public int TrackingId { get { return _trackingId; } set { _trackingId = value; } }

		public const int LocalContext = 0;
		public const int ChapterContext = -1;
		public const int DocumentContext = -2;

		public Condition(int lineNumber, int linePosition)
		{
			_lineNumber = lineNumber;
			_linePosition = linePosition;
		}

		public string ConditionName
		{
			get
			{
				Type type = this.GetType();
				ConditionNameAttribute attr = (ConditionNameAttribute)type.GetCustomAttribute(typeof(ConditionNameAttribute));
				return attr?.Name;
			}
		}
	}

	//TODO: some way to combine conditions, AND and OR

	[ConditionName("empty-layout")]
	internal class EmptyLayoutCondition : Condition
	{
		public int Context;
		public LayoutType RefType;
		public string RefId;
		public bool Require;
		public bool Prohibit;

		public EmptyLayoutCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public EmptyLayoutCondition(EmptyLayoutCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			Context = src.Context;
			RefType = src.RefType;
			RefId = src.RefId;
			Require = src.Require;
			Prohibit = src.Prohibit;
		}

		public override string ToString()
		{
			string show = "";
			if(!Require && !Prohibit)
				show = "always";
			else if(Require && Prohibit)
				show = "require+prohibit";
			else if(Require)
				show = "require";
			else if(Prohibit)
				show = "prohibit";

			return $"{ConditionName} {show} @{_lineNumber} #{_trackingId}";
	}
	}

	[ConditionName("option-selected")]
	internal class OptionSelectedCondition : Condition
	{
		public Path.Path Source = Path.Path.Empty;
		public bool Require;
		public bool Prohibit;
		public bool IsImplicit = false;

		public OptionSelectedCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public OptionSelectedCondition(OptionSelectedCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			Source = src.Source; // can share paths, but not resolved references
			Require = src.Require;
			Prohibit = src.Prohibit;
		}

		public override string ToString()
		{
			string isImplicit = IsImplicit ? "implicit " : "";

			string show = "";
			if(!Require && !Prohibit)
				show = "always";
			else if(Require && Prohibit)
				show = "require+prohibit";
			else if(Require)
				show = "require";
			else if(Prohibit)
				show = "prohibit";

			return $"{ConditionName} {isImplicit}{show} @{_lineNumber} #{_trackingId}";
		}
	}

	[ConditionName("photo-count")]
	internal class PhotoCountCondition : Condition
	{
		public Path.Path Source = Path.Path.Empty;
		public int Context;
		public LayoutType RefType;
		public string RefId;
		public int Minimum;
		public int Maximum;

		public PhotoCountCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public PhotoCountCondition(PhotoCountCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			Source = src.Source; // can share paths, but not resolved references
			Context = src.Context;
			RefType = src.RefType;
			RefId = src.RefId;
			Minimum = src.Minimum;
			Maximum = src.Maximum;
		}

		public override string ToString()
		{
			return $"{ConditionName} {Minimum}-{Maximum} @{_lineNumber} #{_trackingId}";
		}
	}

	[ConditionName("item-count")]
	internal class ItemCountCondition : Condition
	{
		public int Context;
		public LayoutType RefType;
		public string RefId;
		public int Minimum;
		public int Maximum;

		public ItemCountCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public ItemCountCondition(ItemCountCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			Context = src.Context;
			RefType = src.RefType;
			RefId = src.RefId;
			Minimum = src.Minimum;
			Maximum = src.Maximum;
		}

		public override string ToString()
		{
			return $"{ConditionName} {Minimum}-{Maximum} @{_lineNumber} #{_trackingId}";
		}
	}

	[ConditionName("doctag")]
	internal class DocTagCondition : Condition
	{
		public Path.Path Source = Path.Path.Empty;
		public string DocTag;
		public bool Require;
		public bool Prohibit;

		public DocTagCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public DocTagCondition(DocTagCondition src)
			:base(src._lineNumber, src._lineNumber)
		{
			Source = src.Source; // can share paths, but not resolved references
			DocTag = src.DocTag;
			Require = src.Require;
			Prohibit = src.Prohibit;
		}

		public override string ToString()
		{
			string show = "";
			if(!Require && !Prohibit)
				show = "always";
			else if(Require && Prohibit)
				show = "require+prohibit";
			else if(Require)
				show = "require";
			else if(Prohibit)
				show = "prohibit";

			return $"{ConditionName} {show} @{_lineNumber} #{_trackingId}";
		}
	}

	[ConditionName("content-selected")]
	internal class ContentSelectedCondition : Condition
	{
		public bool Require;
		public bool Prohibit;
		public bool IsImplicit = false;

		public ContentSelectedCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public ContentSelectedCondition(ContentSelectedCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			Require = src.Require;
			Prohibit = src.Prohibit;
		}

		public override string ToString()
		{
			string isImplicit = IsImplicit ? "implicit " : "";

			string show = "";
			if(!Require && !Prohibit)
				show = "always";
			else if(Require && Prohibit)
				show = "require+prohibit";
			else if(Require)
				show = "require";
			else if(Prohibit)
				show = "prohibit";

			return $"{ConditionName} {isImplicit}{show} @{_lineNumber} #{_trackingId}";
		}
	}

	[ConditionName("content-doctag")]
	internal class ContentDocTagCondition : Condition
	{
		public string DocTag;
		public bool Require;
		public bool Prohibit;

		public ContentDocTagCondition(int lineNumber, int linePosition)
			:base(lineNumber, linePosition)
		{
		}

		public ContentDocTagCondition(ContentDocTagCondition src)
			:base(src._lineNumber, src._linePosition)
		{
			DocTag = src.DocTag;
			Require = src.Require;
			Prohibit = src.Prohibit;
		}

		public override string ToString()
		{
			string show = "";
			if(!Require && !Prohibit)
				show = "always";
			else if(Require && Prohibit)
				show = "require+prohibit";
			else if(Require)
				show = "require";
			else if(Prohibit)
				show = "prohibit";

			return $"{ConditionName} {show} @{_lineNumber} #{_trackingId}";
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	internal class ConditionNameAttribute : Attribute
	{
		public string Name { get; private set; }
		public ConditionNameAttribute(string name)
		{
			Name = name;
		}
	}
}
