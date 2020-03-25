using System.Collections.Generic;
using Demon.Core.Interface.Data;
using System.Linq;

namespace Demon.Path
{
	public class DocumentStructure
	{
		private string _documentId;
		private List<DocumentItem> items;
		private Dictionary<string, DocumentItem> _indexById;
		private Dictionary<string, List<DocumentItem>> _indexByTemplate;
		private Dictionary<string, List<DocumentItem>> _indexByParent;

		public DocumentStructure(string documentId, IUnitOfWork unitOfWork)
		{
			_documentId = documentId;

			string sql = "select TemplateId from Document where Id = @p0";
			string docTemplateId = unitOfWork.SQLQuery2<string>(sql, _documentId).FirstOrDefault();

			//	Load the document and its template
			sql =
				@"select ObjectId, ObjectType, TemplateId, ParentId, ParentType, Ordinal
				from DocumentStructure
				where DocumentId = @p0
				or DocumentId = @p1";
			items = unitOfWork.SQLQuery2<DocumentItem>(sql, _documentId, docTemplateId).ToList();
			//	It seems that Entity Framwork sorts the results by the query
			//	columns, in order: by objid, objtype etc. Presumably it does
			//	this so that it can do paging in the absence of a primary key.
			//	It seems to do this even if we use an "order by" clause - EF
			//	orders by whatever we say first and then by its own specification.
			//	With this data set that means that items are ordered by object id.

			_indexById = new Dictionary<string, DocumentItem>();
			foreach(DocumentItem item in items)
				_indexById.Add(item.ObjectId, item);

			//	Index by template. For every template object, get all concrete
			//	objects based on it. Add these items to a dictionary - the
			//	template as the key, and the list of derived objects as
			//	the value. Ensure that every template is in the dictionary,
			//	even if it has no derived objects.
			_indexByTemplate = new Dictionary<string, List<DocumentItem>>();
			//	First create all the template entries
			foreach(DocumentItem item in items)
				if(item.TemplateId == null)
					_indexByTemplate.Add(item.ObjectId, new List<DocumentItem>());
			//	Then fill in the derived object lists
			foreach(DocumentItem item in items)
				if(item.TemplateId != null)
					_indexByTemplate[item.TemplateId].Add(item);

			//	Index by parent. Similar to index by template.
			//	1.	Insert every object as a key, even if it has no children
			//			(and even if it can't have children because of its type,
			//			such as TextEntry and Photo.)
			//	2.	Include the document itself because it's the parent of the
			//			top-level sections.
			//	3.	Include the document template because it holds the template
			//			objects referenced by concrete objects.
			_indexByParent = new Dictionary<string, List<DocumentItem>>();
			_indexByParent.Add(_documentId, new List<DocumentItem>());
			_indexByParent.Add(docTemplateId, new List<DocumentItem>());
			foreach(DocumentItem item in items)
				_indexByParent.Add(item.ObjectId, new List<DocumentItem>());
			foreach(DocumentItem item in items)
				if(item.ParentId != null)
					_indexByParent[item.ParentId].Add(item);

			//	Sort the lists by ordinal so that we'll always return
			//	sorted lists
			foreach(List<DocumentItem> list in _indexByTemplate.Values)
				list.Sort(Compare);
			foreach(List<DocumentItem> list in _indexByParent.Values)
				list.Sort(Compare);
		}

		/// <exception cref="NotFoundException"></exception>
		internal DocumentItem GetParent(string objectId)
		{
			DocumentItem obj = null;
			try
			{
				obj = _indexById[objectId];
			}
			catch(KeyNotFoundException)
			{
				throw new NotFoundException("Object not found.");
			}
			
			string parentId = obj.ParentId;
			if(parentId == null) return null;
			
			DocumentItem parent = null;
			try
			{
				parent = _indexById[parentId];
			}
			catch(KeyNotFoundException)
			{
				throw new NotFoundException("Parent not found.");
			}
			return parent;
		}

		/// <summary>
		/// The returned list is sorted by ordinal.
		/// </summary>
		/// <exception cref="NotFoundException"></exception>
		internal List<DocumentItem> GetInstances(string templateId)
		{
			try
			{
				List<DocumentItem> instances = _indexByTemplate[templateId];
				return instances;
			}
			catch(KeyNotFoundException)
			{
				throw new NotFoundException("Template not found.");
			}
		}

		/// <summary>
		/// The returned list is sorted by ordinal.
		/// </summary>
		/// <exception cref="NotFoundException"></exception>
		internal List<DocumentItem> GetChildren(string parentId)
		{
			try
			{
				List<DocumentItem> children = _indexByParent[parentId];
				return children;
			}
			catch(KeyNotFoundException)
			{
				throw new NotFoundException("Parent not found.");
			}
		}

		public string DocumentId { get { return _documentId; }}

		private static int Compare(DocumentItem item1, DocumentItem item2)
		{
			if((item1 == null) && (item2 == null)) return 0;
			if(item1 == null) return -1;
			if(item2 == null) return +1;

			if(item1.Ordinal < item2.Ordinal) return -1;
			if(item1.Ordinal > item2.Ordinal) return +1;
			return 0;
		}
	}

	internal class DocumentItem
	{
		public string ObjectId { get; set; }
		public int ObjectType { get; set; }
		public string TemplateId { get; set; }
		public string ParentId { get; set; }
		public int ParentType { get; set; }
		public int Ordinal { get; set; }

		public override string ToString()
		{
			return $"o:{ObjectType:D2}:{ObjectId} t:{TemplateId} p:{ParentType:D2}:{ParentId} @{Ordinal:D2}";
		}
	}
}
