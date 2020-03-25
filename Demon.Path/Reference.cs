using System.Linq;
using System.Collections.Generic;
using Demon.Core.Domain;

namespace Demon.Path
{
	/// <summary>
	/// A reference is a source type and template id.
	/// </summary>
	public class Reference
	{
		private ContentSourceType _type;
		private string _id;
		private bool _isResolved;

		private static readonly Reference _null;
		private static readonly Reference _empty;
		/// <summary>
		/// An unresolved reference to nothing.
		/// </summary>
		public static Reference Null { get { return _null; }}
		/// <summary>
		/// A resolved reference to nothing.
		/// </summary>
		public static Reference Empty { get { return _empty; }}

		public ContentSourceType Type { get { return _type; }}
		public string Id { get { return _id; }}
		public bool IsResolved { get { return _isResolved; }}

		static Reference()
		{
			_null  = new Reference(ContentSourceType.None, "00000000000000000000000000000000", false);
			_empty = new Reference(ContentSourceType.None, "00000000000000000000000000000000", true);
		}

		private Reference(ContentSourceType type, string id, bool resolved)
		{
			_type = type;
			_id = id;
			_isResolved = resolved;
		}

		/// <exception cref="PathException"></exception>
		internal static Reference Parse(string text, ref int pos)
		{
			ContentSourceType type = ParseTypeName(text, ref pos);
			
			if(text[pos] != ':')
				throw new PathException($"Expected ':' at position {pos}.");
			++pos;

			string id = ParseObjectId(text, ref pos);

			return Create(type, id, false);
		}

		public static Reference Create(ContentSourceType type, string id, bool resolved)
		{
			//	If the type:id matches any of our const values then return
			//	that const, otherwise return a new reference. It's because
			//	we want to do this const-matching that we don't expose a
			//	public constructor.
			if(type == ContentSourceType.None)
			{
				if(resolved && id == _empty.Id)
					return _empty;
				else if(!resolved && id == _null.Id)
					return _null;
			}

			return new Reference(type, id, resolved);
		}

		public Reference(Reference other)
		{
			_type = other._type;
			_id = other._id;
			_isResolved = other._isResolved;
		}

		/// <exception cref="PathException"></exception>
		private static ContentSourceType ParseTypeName(string text, ref int pos)
		{
			ContentSourceType type = ContentSourceType.None;

			int start = pos;

			//	Find the colon separator between the type name and the object id
			int end = start;
			while((end < text.Length) && (text[end] != ':'))
				++end;

			string typeName = text.Substring(start, end - start);
			bool ok = System.Enum.TryParse<ContentSourceType>(typeName, out type);
			if(!ok)
				throw new PathException($"Invalid type name at position {start}.");

			pos = end;
			return type;
		}

		/// <exception cref="PathException"></exception>
		private static string ParseObjectId(string text, ref int pos)
		{
			//	An object id is always 32 hex characters
			const string idChars = "abcdefABCDEF0123456789";
			const int idLen = 32;

			int start = pos;
			for(int x = 0; x < idLen; ++x)
			{
				char c = text[pos];
				if(!idChars.Contains(c))
					throw new PathException($"Invalid template id at position {pos}.");
				++pos;
			}
			string id = text.Substring(start, idLen);
			return id;
		}

		public void Resolve(string id)
		{
			_id = id;
			_isResolved = true;
		}

		public override string ToString()
		{
			string s = $"{_type}:{_id}";
			if(!_isResolved)
				s += "?";
			return s;
		}
	}

	public class ReferenceComparer : IEqualityComparer<Reference>
	{
		public bool Equals(Reference ref1, Reference ref2)
		{
			if(ref1 == null && ref2 == null) return true;
			if(ref1 == null || ref2 == null) return false;
			if(ref1.Type != ref2.Type) return false;
			if(ref1.Id != ref2.Id) return false;
			if(ref1.IsResolved != ref2.IsResolved) return false;
			return true;
		}

		public int GetHashCode(Reference reference)
		{
			return reference.ToString().GetHashCode();
		}
	}
}
