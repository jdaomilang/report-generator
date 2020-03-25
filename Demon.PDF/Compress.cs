using System.IO;
using System.IO.Compression;

namespace Demon.PDF
{
	internal static class Compression
	{
		public static MemoryStream Compress(Stream payload)
		{
			//	The compression won't complete until the compressor stream
			//	has been disposed. And closing it explicitly won't do the
			//	trick either - it has to be disposed. The implementation
			//	uses disposal as the trigger to finalise the compression
			//	processing, which needs to know when it has received the
			//	last of its input. I'm not sure of the details, but the upshot
			//	is that the "using" block is the best way to go, and we can't
			//	read the compressed data until after the "using" block.
			MemoryStream compressedStream = new MemoryStream();
			using(DeflateStream compressor = new DeflateStream(compressedStream, CompressionLevel.Optimal, true))
			{
				payload.CopyTo(compressor);
			}
			compressedStream.Capacity = (int)compressedStream.Length;

			//	The Microsoft implementation of Deflate doesn't write a two-byte
			//	header that PDF seems to expect. As far as I can make out, this
			//	header is not part of Deflate but is part of zlib or something
			//	like that. This page gives a clue:
			//	https://bytes.com/topic/python/answers/579903-removing-header-gzipd-string
			//
			//		The first 2 bytes (or more if using a preset dictionary) are
			//		header information. The last 4 bytes are for checksum. In-between
			//		lies the encoded bit stream.
			//		Using the default options ("deflate", default compression level, no
			//		custom dictionary) will make those first two bytes 0x78 0x9c.
			//
			//	And this StackOverflow question gives a bit more info:
			//	https://stackoverflow.com/questions/9050260/what-does-a-zlib-header-look-like
			//	In particular:
			//
			//		78 01 - No Compression/low
			//		78 9C - Default Compression
			//		78 DA - Best Compression 
			//
			//	And a bit more clarification at
			//	https://stackoverflow.com/questions/6282575/zlib-compressing-byte-array
			//
			//	The PNG spec, in section 10 (https://www.w3.org/TR/PNG/#10Compression)
			//	say:
			//
			//		Deflate-compressed datastreams within PNG are stored in the "zlib" format.
			//
			//	But the /FlateDecode filter in PDF can be applied to any stream, not only PDFs,
			//	and so it seems that the error is in PDF's assumption that any Deflate
			//	stream is also a zlib stream.
			//
			//	Anyway, I've found that sticking any of those pairs in front of the compressed
			//	data does indeed make PDF happy.

			byte[] compressedBuffer = compressedStream.GetBuffer();
			int len = compressedBuffer.Length;
			byte[] zlibBuffer = new byte[len + 2];
			System.Buffer.BlockCopy(compressedBuffer, 0, zlibBuffer, 2, len);
			zlibBuffer[0] = 0x78;
			zlibBuffer[1] = 0x9c;
			return new MemoryStream(zlibBuffer, 0, zlibBuffer.Length, false, true);
		}
	}
}
