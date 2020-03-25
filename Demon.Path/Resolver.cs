using System.Collections.Generic;
using Demon.Core.Domain;

namespace Demon.Path
{
	public delegate void TracePathDelegate(string text, int skipFrames, params object[] args);

	public class Resolver
	{
		public string DocumentId { get { return _document.DocumentId; }}

		private DocumentStructure _document;
		private DocTaggedObjects _docTaggedObjects;
		private TracePathDelegate _tracer;

		public Resolver(DocumentStructure document, TracePathDelegate tracer)
		{
			_document = document;
			_tracer = tracer;
		}

		public Resolver(DocumentStructure document, DocTaggedObjects docTaggedObjects, TracePathDelegate tracer)
		{
			_document = document;
			_docTaggedObjects = docTaggedObjects;
			_tracer = tracer;
		}

		/// <summary>
		/// Get the immediate parent or container of the given child object. The child object
		/// reference must already be resolved to a concrete object reference. See
		/// the document file specification for the rules governing what types can
		/// be parents of what other types.
		/// </summary>
		public Reference GetParent(Reference child)
		{
			_tracer("{0}", 1, child);

			DocumentItem parentItem = null;
			try
			{
				//	Every concrete object must have exactly one parent. The only
				//	possible exception is the document root, and that should
				//	never be passed in here.
				parentItem = _document.GetParent(child.Id);
			}
			catch(NotFoundException ex)
			{
				ex.Reference = child.ToString();
				throw; //TODO: See the comment in Path.Resolve relating to throw
			}

			ContentSourceType sourceType = (ContentSourceType)parentItem.ObjectType;
			Reference parentRef = Reference.Create(sourceType, parentItem.ObjectId, true);

			_tracer("Found parent {0}", 1, parentRef);
			return parentRef;
		}

		/// <summary>
		/// Get all immediate children of the given parent. See
		/// the document file specification for the rules governing what types can
		/// be children of what other types. The returned list is sorted by ordinal.
		/// </summary>
		public List<Reference> GetChildren(Reference parent)
		{
			_tracer("{0}", 1, parent);
			List<Reference> children = new List<Reference>();

			List<DocumentItem> childItems = null;
			try
			{
				childItems = _document.GetChildren(parent.Id);
			}
			catch(NotFoundException ex)
			{
				ex.Reference = parent.ToString();
				throw; //TODO: See the comment in Path.Resolve relating to throw
			}
			foreach(DocumentItem item in childItems)
			{
				ContentSourceType sourceType = (ContentSourceType)item.ObjectType;
				Reference child = Reference.Create(sourceType, item.ObjectId, true);
				children.Add(child);
			}

			_tracer("Found {0} children:", 1, children.Count);
			foreach(Reference reference in children)
				_tracer("{0}", 1, reference);

			return children;
		}
		
		public bool IsInstanceOf(Reference instance, Reference template)
		{
			List<DocumentItem> instances = null;
			try
			{
				instances = _document.GetInstances(template.Id);
			}
			catch(NotFoundException ex)
			{
				ex.Reference = template.ToString();
				throw; //TODO: See the comment in Path.Resolve relating to throw
			}
			foreach(DocumentItem item in instances)
				if(item.ObjectId == instance.Id)
					return true;
			return false;
		}

		public bool IsObjectTagged(Reference obj, string docTag)
		{
			return _docTaggedObjects.IsTagged(obj, docTag);
		}

		internal void Trace(string msg, params object[] args)
		{
			_tracer(msg, 2, args);
		}
	}
}
