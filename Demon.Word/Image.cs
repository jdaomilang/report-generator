using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Xml.Linq;
using System.IO;

namespace Demon.Word
{
	internal class Image : Part
	{
		//	Our photos and image resources are always JPEG or PNG
// image/png ISO/IEC 15948:2003, http://www.libpng.org/pub/png/spec/
// image/jpeg, http://www.w3.org/Graphics/JPEG

		private byte[] _data;

		public override string ContentType => "image/jpeg"; //TODO: PNG

		public Image(string name, byte[] data)
			:base(name)
		{
			_data = data;
		}

		public override void Write(ZipArchive archive)
		{
			//	Write the image data to a new part file
			ZipArchiveEntry entry = archive.CreateEntry(Name, CompressionLevel.NoCompression);
			using(Stream stream = entry.Open())
			{
				stream.Write(_data, 0, _data.Length);
			}
		}
	}
}
