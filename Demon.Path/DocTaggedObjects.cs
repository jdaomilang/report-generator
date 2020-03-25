using System.Collections.Generic;
using Demon.Core.Domain;
using Demon.Core.Interface.Data;
using System.Linq;

namespace Demon.Path
{
	public class DocTaggedObjects
	{
		private string _documentId;

		//	<doctagtemplateid:fullinfo>
		private Dictionary<string, List<DocTaggedObject>> _taggedObjects;

		public DocTaggedObjects(string documentId, IUnitOfWork unitOfWork)
		{
			_documentId = documentId;

			_taggedObjects = new Dictionary<string, List<DocTaggedObject>>();

			//	Get all tags defined in the template so that the dictionary
			//	will be complete even if some tags aren't used
			IEnumerable<string> tagTemplateIds = unitOfWork.Repository<DocTag>()
				.Query(t => t.DocumentId == _documentId)
				.Get()
				.Select(t => t.TemplateId);
			foreach(string tagTemplateId in tagTemplateIds)
				_taggedObjects.Add(tagTemplateId, new List<DocTaggedObject>());

			//	Get info about all taggings
			string sql =
$@"select DocumentId, DocTagId, DocTagTemplateId, Name,
	DocTagListId, SourceId, SourceType, Ordinal, Caption
from DocTaggedObject where DocumentId = @p0";
			IEnumerable<DocTaggedObject> instances = unitOfWork.SQLQuery2<DocTaggedObject>(sql, _documentId);
			foreach(DocTaggedObject instance in instances)
				_taggedObjects[instance.DocTagTemplateId].Add(instance);
		}

		public bool IsTagged(Reference obj, string tagTemplateId)
		{
			return _taggedObjects[tagTemplateId]
				.Any(i => i.SourceType == obj.Type && i.SourceId == obj.Id);
		}
	}

	internal class DocTaggedObject
	{
		public string DocumentId { get; set; }
		public string DocTagId { get; set; }
		public string DocTagTemplateId { get; set; }
		public string Name { get; set; }
		public string DocTagListId { get; set; }
		public string SourceId { get; set; }
		public ContentSourceType SourceType { get; set; }
		public int Ordinal { get; set; }
		public string Caption { get; set; }

		public override string ToString()
		{
			return $"{Name} {SourceType}:{SourceId} @{Ordinal:D2}";
		}
	}
}
