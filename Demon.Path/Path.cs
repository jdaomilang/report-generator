using System;
using System.Collections.Generic;
using System.Text;
using Demon.Core.Domain;

namespace Demon.Path
{
	public class Path
	{
		private Origin _origin;
		private Step _firstStep;
		private Property _property;

		private int _lineNumber;
		public int LineNumber { get { return _lineNumber; }}
		private int _linePosition;
		public int LinePosition { get { return _linePosition; }}

		public static readonly Path _empty;
		public static Path Empty { get { return _empty; }}

		static Path()
		{
			_empty = new Path();
		}

		private Path()
		{
			//	This constructor is intended only to support the Empty
			//	path, and so it's private
		}

		/// <exception cref="PathException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		public Path(string text, int lineNumber, int linePosition)
		{
			if(text == null)
				throw new ArgumentNullException("text");

			_lineNumber = lineNumber;
			_linePosition = linePosition;

			Parse(text);
		}

		/// <summary>
		/// Construct a one-step path from an existing reference.
		/// </summary>
		public Path(Reference reference, int lineNumber, int linePosition)
		{
			_lineNumber = lineNumber;
			_linePosition = linePosition;

			_origin = Origin.Context;
			_firstStep = new Step(reference);
		}

		public Path(ContentSourceType targetType, string targetId, int lineNumber, int linePosition)
		{
			_lineNumber = lineNumber;
			_linePosition = linePosition;

			string path = $"{targetType}:{targetId}";
			Parse(path);
		}

		public Path(Path other)
		{
			_origin = other._origin;
			_firstStep = other._firstStep;
			_property = new Property(other._property);
		}

		private void Parse(string text)
		{
			try
			{
				int pos = 0;
				ParseOrigin(text, ref pos);

				Step step = null;
				while(true)
				{
					//	If we're at the end of the text then we're finished
					if(pos >= text.Length)
						break;

					//	At each iteration we expect the next character to
					//	be one of these:
					//		/	followed by another step
					//		!	followed by a property
					char c = text[pos];
					if(c == '/') // step separator
					{
						//	Go round the loop again and get the next step
						++pos;
						continue;
					}
					else if(c == '!') // property introducer
					{
						//	Get the property and then finish
						++pos;
						_property = new Property(text, false, ref pos);
						break;
					}
                    else if (c == '$') // extension property introducer
                    {
                        //	Get the extension property and then finish
                        ++pos;
                        _property = new Property(text, true, ref pos);
                        break;
                    }
                    else // must be the start of a step
					{
						if(_firstStep == null) // first step, origin = path origin
						{
							step = new Step(text, ref pos, _origin);
							_firstStep = step;
						}
						else // not first step, origin = previous step's result
						{
							step._next = new Step(text, ref pos, Origin.Context);
							step = step._next;
						}
					}
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

		private void ParseOrigin(string text, ref int pos)
		{
			if(pos >= text.Length)
				throw new PathException($"Invalid path at position {pos}.");

			//	A dot positions the origin at the top of the context stack,
			//	with an optional positive integer offset to move the origin
			//	down the stack. A dollar sign sets the origin at the root
			//	of the document. If the origin is specified explicitly in this
			//	way then it must be followed by a slash before the first step,
			//	but if it's not specified then there must be no initial slash.
			//	The default origin is the top of the context stack.

			char c = text[pos];
			switch(c)
			{
				case '.': // origin = current context
				{
					++pos;
					_origin = Origin.Context;
					break;
				}

				case '$': // origin = document root
				{
					++pos; // advance to the slash
					_origin = Origin.Root;
					break;
				}

				default: // implicit origin = context
				{
					//	Don't increment pos
					_origin = Origin.Context;
					break;
				}
			}
		}

		/// <summary>
		/// Resolve the path into zero or more concrete object references.
		/// Never returns null. The returned list is sorted by ordinal.
		/// </summary>
		public List<Reference> Resolve(Reference context, Resolver resolver)
		{
			try
			{
				resolver.Trace("Path={0} context={1} line {2}:{3}", this, context, _lineNumber, _linePosition);
				if(this == Empty) return new List<Reference>();

				//	Start at the path's origin object. If the origin is the context
				//	but the context is null or empty, then set the origin to the root.
				Origin origin = _origin;
				if(origin == Origin.Context && (context == Reference.Null || context == Reference.Empty))
					origin = Origin.Root;
				Reference start = null;
				switch(origin)
				{
					case Origin.Context:
						start = context;
						break;
					case Origin.Root:
						start = Reference.Create(ContentSourceType.None, resolver.DocumentId, true);
						break;
				}

				//	Follow the first step from the start reference. Each step will
				//	discover a set of zero or more resolved references, and will
				//	then follow its own next step starting at each of those resolved
				//	references. This will build up a set of resolved references, each
				//	of which is a result of following the full path from the single
				//	start position. But if we have no steps at all then resolve to
				//	the start object.
				List<Reference> resolved = null;
				if(_firstStep != null)
				{
					resolved = _firstStep.Follow(start, resolver);
				}
				else
				{
					resolved = new List<Reference>();
					resolved.Add(start);
				}

				resolver.Trace("Resolved {0} objects:", resolved.Count);
				foreach(Reference reference in resolved)
					resolver.Trace("{0}", reference);

				return resolved;
			}
			catch(PathException ex)
			{
				ex.Path = this.ToString();
				ex.Context = context.ToString();
				throw;
				//TODO: Since framework 4.5 (I think) this naked throw statement
				//loses the exception's call stack and shows the exception
				//originating here. There are messy ways to preserve it, but
				//can we find a clean way? 4.5 introduces ExceptionDispatchInfo
				//to handle this kind of thing (although it's really designed
				//for async continuations) but it has some side effects. First,
				//the compiler won't recognise ExceptionDispatchInfo.Throw as
				//being a throw (because it's not) and so may insist on having
				//a return value in the method - no big deal. But what makes this
				//unusable for us is that ExceptionDispatchInfo can't be marshaled
				//across app domain boundaries, and we may want to use app domains
				//to support multiple versions of assemblies for backwards
				//compatibility.
			}
		}

		/// <summary>
		/// Resolve the path into zero or more concrete object references.
		/// Never returns null. The returned list is sorted by ordinal.
		/// </summary>
		public List<Reference> Resolve(
			Reference context,
			DocumentStructure document,
			TracePathDelegate tracer)
		{
			tracer("Path={0} context={1} line {2}:{3}", 2, this, context, _lineNumber, _linePosition);
			if(this == Empty) return new List<Reference>();

			Resolver resolver = new Resolver(document, tracer);
			List<Reference> resolved = Resolve(context, resolver);

			tracer("Resolved {0} objects:", 1, resolved.Count);
			foreach(Reference reference in resolved)
				tracer("{0}", 1, reference);

			return resolved;
		}

		public bool IsResolved
		{
			get
			{
				Step step = _firstStep;
				while(step != null)
					if(!step._reference.IsResolved)
						return false;
				return true;
			}
		}

		public ContentSourceType TargetType
		{
			get
			{
				if(_firstStep == null)
					return ContentSourceType.None;
				Step step = _firstStep;
				while(step._next != null)
					step = step._next;
				return step._reference.Type;
			}
		}

		/// <summary>
		/// A copy of the path's target reference.
		/// </summary>
		public Reference Target
		{
			get
			{
				if(_firstStep == null)
					return Reference.Empty;
				Step step = _firstStep;
				while(step._next != null)
					step = step._next;
				return new Reference(step._reference);
			}
		}

		public Property Property 
		{
			get
			{
				return _property;
			}
		}
		
		public override string ToString()
		{
			if(this == Empty) return "Empty";

			StringBuilder sb = new StringBuilder();
			switch(_origin)
			{
				case Origin.Context: sb.Append("."); break;
				case Origin.Root   : sb.Append("$"); break;
			}

			Step step = _firstStep;
			while(step != null)
			{
				sb.Append("/");
				sb.Append(step);
				step = step._next;
			}
			if(_property != null)
			{
				sb.Append("!");
				sb.Append(_property);
			}
			return sb.ToString();
		}
	}

	internal enum Origin
	{
		Context = 0, // default
		Root    = 1
	}
}
