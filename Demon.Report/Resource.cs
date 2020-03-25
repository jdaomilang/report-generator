using System.Threading.Tasks;
using Demon.Core.Interface.Services;

namespace Demon.Report
{
	internal class Resource
	{
		private string _id;
		private string _filename;
		private byte[] _bits;

		public Resource(string id, string filename)
		{
			_id = id;
			_filename = filename;
		}

		public async Task<byte[]> GetFile(IResourceService service)
		{
			//	Load the resource if we haven't already done so
			if(_bits == null)
				_bits = await service.GetResourceAsync(_filename);
			return _bits;
		}

		public string Id { get { return _id; } }
		public string Filename { get { return _filename; } }
	}
}
