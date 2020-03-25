using System;
using System.Collections.Generic;
using Demon.Core.Domain;

namespace Demon.Path
{
	/// <summary>
	/// A step is a navigation instruction, an object reference, and a property.
	/// </summary>
	internal class Step
	{
		private Navigation _navigation;
		internal Reference _reference; // internal, not private, so that Path can read it
		internal Step _next; // internal, not private, so that Path can set it

		/// <exception cref="PathException"></exception>
		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		internal Step(string text, ref int pos, Origin origin)
		{
			if(text == null)
				throw new ArgumentNullException("text");
			if(pos < 0)
				throw new ArgumentOutOfRangeException("start");
			if(pos >= text.Length)
				throw new PathException($"Empty path at position {pos}.");
			
			_navigation = new Navigation(text, ref pos, origin);
			_reference = Reference.Parse(text, ref pos);
		}

		internal Step(Reference reference)
		{
			_navigation = new Navigation();
			_reference = new Reference(reference);
		}

		internal Step(Step other)
		{
			_navigation = new Navigation(other._navigation);
			_reference = new Reference(other._reference);
			_next = new Step(other._next);
		}

		/// <summary>
		/// Find the concrete objects, based on the target template object, with this
		/// step's relationship to the start object. Throws ArgumentException if the
		/// start reference is not resolved, or if the step's target is already resolved.
		/// The returned list is sorted by ordinal.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		internal List<Reference> Follow(Reference start, Resolver resolver)
		{
			if(!start.IsResolved)
				throw new ArgumentException($"Start object {start} is not resolved.");
			if(_reference.IsResolved)
				throw new ArgumentException($"Target reference {_reference} is already resolved.");

			List<Reference> found = new List<Reference>();

			//	If the start reference is an instance of the path reference then
			//	return it directly and don't look any further up/down
			if(resolver.IsInstanceOf(start, _reference))
			{
				found.Add(start);
				return found;
			}

			switch(_navigation.Direction)
			{
				case Direction.Up:
					Reference ancestor = FollowUp(start, resolver);
					if(ancestor != null)
						found.Add(ancestor);
					break;
				case Direction.Down:
					List<Reference>	descendants = FollowDown(start, resolver);
					foreach(Reference descendant in descendants)
						if(descendant != null)
							found.Add(descendant);
					break;
			}

			//	If we're the last step in the path then our result set is the
			//	full result set for this fork of the path.
			if(_next == null)
				return found;

			//	If we're not the last step then use each of our resolved references
			//	as the starting point for a fork at the next step.
			List<Reference> result = new List<Reference>();
			foreach(Reference reference in found)
			{
				List<Reference> forks = _next.Follow(reference, resolver);
				result.AddRange(forks);
			}
			return result;
		}

		private Reference FollowUp(Reference start, Resolver resolver)
		{
			int limit = _navigation.Limit > 0 ? _navigation.Limit : int.MaxValue;
			Reference parent = start;
			for(int distance = 0; distance < limit; ++distance)
			{
				parent = resolver.GetParent(parent);
				if(parent.Type == ContentSourceType.None)
					return null; // reached the root without finding a match
				if(resolver.IsInstanceOf(parent, _reference))
					return parent;
			}
			return null; // didn't find the target within the limit
		}

		/// <summary>
		/// The returned list is sorted by ordinal.
		/// </summary>
		private List<Reference> FollowDown(Reference start, Resolver resolver)
		{
			int limit = _navigation.Limit > 0 ? _navigation.Limit : int.MaxValue;
			List<Reference> children = FollowDown(start, limit, resolver);
			return children;
		}

		/// <summary>
		/// FollowDown requires recursion because there can be any number
		/// of children at each distance unit. The returned list is sorted
		/// by ordinal.
		/// </summary>
		private List<Reference> FollowDown(Reference start, int limit, Resolver resolver)
		{
			List<Reference> found = new List<Reference>();
			List<Reference> children = resolver.GetChildren(start);

			//	Breadth-first, so that if there are several matching descendants then
			//	we return the closest one.
			foreach(Reference child in children)
			{
				if(resolver.IsInstanceOf(child, _reference))
				{
					found.Add(child);
				}
			}
			if(found.Count > 0)
				return found;

			if(limit > 0)
			{
				foreach(Reference child in children)
				{
					List<Reference> nextLevel = FollowDown(child, limit - 1, resolver);
					found.AddRange(nextLevel);
				}
			}
			return found; // could be empty
		}

		public override string ToString()
		{
			return $"{_navigation}:{_reference}";
		}
	}
}
