using System;
using Demon.Core.Domain;

namespace Demon.Path
{
	public class DocumentProperty
	{
		private PropertyOwnerType _scope;
		private Property _property;
		private int _lineNumber;
		private int _linePosition;

		public PropertyOwnerType Scope { get { return _scope; }}
		public string Name { get { return _property?.Name; }}
		public string Format { get { return _property?.Format; }}

		/// <exception cref="PathException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		public DocumentProperty(string text, int lineNumber, int linePosition)
		{
			if(text == null)
				throw new ArgumentNullException("text");

			_lineNumber = lineNumber;
			_linePosition = linePosition;

			Parse(text);
		}

		private void Parse(string text)
		{
			//	A doc prop follows this pattern:
			//
			//		scope!property:format
			//
			//	Scope is optional, and defaults to "system", but the ! separator
			//	is required in any case. Property is required. Format is optional,
			//	and if it's omitted then the : separator can be omitted too.

			try
			{
				int pos = 0;
				while(true)
				{
					//	If we're at the end of the text then we're finished
					if(pos >= text.Length)
						break;

					char c = text[pos];
					if(c == '!') // property introducer
					{
						//	Read the optional scope name up to the !
						if(pos > 0)
						{
							string sourceName = text.Substring(0, pos);
							bool ok = System.Enum.TryParse<PropertyOwnerType>(sourceName, out _scope);
							if(!ok)
								throw new PathException("Invalid document property scope.");
						}

						//	Read the property, including its format, and then finish
						++pos;
						_property = new Property(text, false, ref pos);
						break;
					}
					++pos;
				}
			}
			catch(PathException ex)
			{
				ex.Path = this.ToString();
				ex.LineNumber = _lineNumber;
				ex.LinePosition = _linePosition;
				throw; //TODO: See the comment in Path.Resolve relating to throw
			}
		}

		public override string ToString()
		{
			return $"{_scope}!{_property}";
		}
	}
}
