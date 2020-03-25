using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using Demon.Core;
using Demon.Report.Types;

namespace Demon.Report
{
	internal class Image
	{
		public byte[] RawData;
		public Size Size;

		private const int DefaultQuality = 90;

		public Image(byte[] bits)
		{
			RawData = bits;

			//	Get the size
			Stream stream = new MemoryStream(bits);
			System.Drawing.Image img = System.Drawing.Image.FromStream(stream);
			Size = new Size(img.Size);
		}

		/// <summary>
		/// Load an image from the report design's resource cache.
		/// </summary>
		public static async Task<Image> LoadCachedPicture(Generator generator, string resourceId)
		{
			return await LoadPictureResource(resourceId,null,generator);
		}

		/// <summary>
		/// Load an image directly from storage, without caching.
		/// </summary>
		public static async Task<Image> LoadPictureDirect(Generator generator, string filename)
		{
			return await LoadPictureResource(null,filename,generator);
		}

		/// <summary>
		/// Load an image resource, either from the design's resource cache or
		/// directly from storage. Specify resourceId or filename, but not both.
		/// </summary>
		/// <param name="resourceId">The id of a cached resource. Specify resourceId or filename, but not both.</param>
		/// <param name="filename">The filename of an uncached resource. Specify resourceId or filename, but not both.</param>
		private static async Task<Image> LoadPictureResource(string resourceId, string filename, Generator generator)
		{
			byte[] bits = null;
			try
			{
				if(resourceId != null)
					bits = await generator.ReportDesign.GetResource(resourceId, generator.ResourceService);
				else if(filename != null)
					bits = await generator.ResourceService.GetResourceAsync(filename);
			}
			catch(Exception ex)
			{
				generator.RecordMissingResource(resourceId);

				if(resourceId != null)
					ex.Data.Add("resourceId",resourceId);
				if(filename != null)
					ex.Data.Add("filename",filename);
				generator.Logger.LogException(ex);
			}

			//	If the image resource wasn't found then load the placeholder from the assembly
			if(bits == null)
			{
				Stream missing = Assembly.GetExecutingAssembly().GetManifestResourceStream("Demon.Report.picture-missing.jpg");
				bits = new byte[missing.Length];
				await missing.ReadAsync(bits,0,bits.Length);

				//	We could cache and reuse the placeholder, but it's a bit tricky to
				//	do that here inside of the image loading routine. Probably best just
				//	to pay the extra performance price than to complicate the procedure.
				//	The main performance cost is the fetching of the data from storage,
				//	but we don't do that here because we fetch from the assembly. And
				//	the second performance cost is the increased size in the PDF, but
				//	if the image isn't found then the PDF will have to be regenerated
				//	anyway.
			}

			return new Image(bits);
		}

		public static async Task<Image> LoadPhoto(Generator generator, string id, PhotoType type, string photoLayoutName)
		{
			//	Load the photo from storage. If it doesn't exist then load the placeholder
			//	image. Unlike pictures, photos are not cacheable because it's unlikely
			//	that we'd want to reuse many photos, and even if we did it's unlikely that
			//	the report designer would know which ones were likely to be reused.
			byte[] bits = null;
			try
			{
				bits = await generator.PhotoService.GetPhotoAsync(id,PhotoType.Full);
			}
			catch(Exception ex)
			{
				generator.RecordMissingPhoto(id);

				ex.Data.Add("PhotoTableLayout",photoLayoutName);
				ex.Data.Add("Photo",id);
				generator.Logger.LogException(ex);
			}

			//	If the photo wasn't found then load the placeholder from the assembly
			if(bits == null)
			{
				Stream missing = Assembly.GetExecutingAssembly().GetManifestResourceStream("Demon.Report.photo-missing.jpg");
				bits = new byte[missing.Length];
				await missing.ReadAsync(bits,0,bits.Length);

				//	We could cache and reuse the placeholder, but it's a bit tricky to
				//	do that here inside of the image loading routine. Probably best just
				//	to pay the extra performance price than to complicate the procedure.
				//	The main performance cost is the fetching of the data from storage,
				//	but we don't do that here because we fetch from the assembly. And
				//	the second performance cost is the increased size in the PDF, but
				//	if the image isn't found then the PDF will have to be regenerated
				//	anyway.
			}

			return new Image(bits);
		}

		public Image Resize(Size size, int quality)
		{
			//	Don't resize if we're already at the requested size, because
			//	JPEG can deteriorate dramatically when you do that
			if(size == this.Size) return this;

			System.Drawing.Image original = null;
			System.Drawing.Bitmap bmp = null;
			try
			{
				//	Load the original pixel data into an image
				MemoryStream stream = new MemoryStream(RawData);
				original = System.Drawing.Image.FromStream(stream);

				//	Copy the original to a new bitmap at the new size
				bmp = new System.Drawing.Bitmap(original, new System.Drawing.Size(size.Width, size.Height));

				//	Get the codec and set its quality parameter to 100 = max.
				//	This is intended for JPEG; PNG doesn't have a notion of
				//	quality and so the PNG codec just ignores the parameter.
				ImageCodecInfo encoder = GetCodec(original.RawFormat);
				EncoderParameters parameters = new EncoderParameters(1);
				if(quality < 1 || quality > 100) quality = DefaultQuality;
				parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

				//	Write the resized bitmap to a new JPEG or PNG stream with the
				//	quality parameter. (Memory streams don't need to be disposed
				//	so it's ok just to create a new one.)
				stream = new MemoryStream();
				bmp.Save(stream, encoder, parameters);

				//	Load the resized and encoded image from the saved stream
				stream.Capacity = (int)stream.Length;
				Image resized = new Image(stream.GetBuffer());
				return resized;
			}
			finally
			{
				if(original != null) original.Dispose();
				if(bmp != null) bmp.Dispose();
			}
		}

		private static ImageCodecInfo GetCodec(ImageFormat format)
		{
			ImageCodecInfo[] encoders = ImageCodecInfo.GetImageDecoders();
			foreach(ImageCodecInfo encoder in encoders)
				if(encoder.FormatID == format.Guid)
					return encoder;
			return null;
		}
	}
}
