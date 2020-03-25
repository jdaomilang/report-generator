using System;
using System.IO;
using System.Collections.Generic;
using Demon.Core.Subscription;
using Demon.Core.Domain;
using Demon.Core.Interface.Services;
using Demon.Core.Interface.Data;

namespace Demon.Report.Types
{
	public interface IGenerator
	{
		void Initialize(
			User user, Client client,
			string documentId, int documentVersion, DateTimeOffset timestamp,
			string fontDirectory,
			IPhotoService photoService, IResourceService pictureService,
			IUnitOfWork unitOfWork, ILog logger,
			bool traceLayout, bool drawRules, bool traceText, bool tracePath);
		
		byte[] Save(Stream designFile);
		Dictionary<string, List<string>> MissingPhotos { get; }
		List<string> MissingResources { get; }
	}
}
