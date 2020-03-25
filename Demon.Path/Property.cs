using Demon.Core.Domain;
using Demon.Core.Enums;

namespace Demon.Path
{
	/// <summary>
	/// A property is a name and a display format.
	/// </summary>
	public class Property
	{
		private string _name;
		private string _format;
        private bool _isExtension;
        private ExtensionType _extensionType;

        public string Name { get { return _name; }}
		public string Format { get { return _format; }}
        public bool IsExtension { get { return _isExtension; } }
        public ExtensionType ExtensionType { get { return _extensionType; } }

        internal Property()
		{
			_name = null;
		}

		internal Property(Property other)
		{
			_name = other._name;
			_format = other._format;
            _isExtension = other._isExtension;
		}

		/// <exception cref="PathException"></exception>
		internal Property(string text, bool isExtension, ref int pos)
		{
            _isExtension = isExtension;

            //	If no format is specified then the property name ends at the
            //	end of the text, otherwise it ends at the colon separator
            int start = pos;

            _extensionType = ExtensionType.None;
            if (isExtension)
            {
                while ((pos < text.Length) && (text[pos] != '!'))
                    ++pos;

                string extensionTypeName = text.Substring(start, pos - start);

                bool ok = System.Enum.TryParse<ExtensionType>(extensionTypeName, out _extensionType);
                if (!ok)
                    throw new PathException($"Invalid extension type name at position {start}.");

                ++pos;
            }

            start = pos;
			while((pos < text.Length) && (text[pos] != ':'))
				++pos;
			_name = text.Substring(start, pos - start);

            if (pos >= text.Length) return; // no format specifier

			if(text[pos] != ':')
				throw new PathException($"Invalid format specifier at position {pos}.");
			++pos;

			//	Whatever is left in the text is the format specifier
			_format = text.Substring(pos);
		}

		public override string ToString()
		{
			string s = _name.ToString();
			if(_format != null)
			{
				s += ":";
				s += _format.ToString();
			}
			return s;
		}
	}
}
