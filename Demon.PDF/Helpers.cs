using System.Text;

namespace Demon.PDF
{
	internal static class Helpers
	{
		/// <summary>
		/// Encode an integer in four hex characters.
		/// </summary>
		public static string Hex(ushort i)
		{
			int hi1 = (i >> 12) & 0x0f;
			int lo1 = (i >>  8) & 0x0f;
			int hi2 = (i >>  4) & 0x0f;
			int lo2 = (i >>  0) & 0x0f;

			char[] c = new char[4];
			c[0] = (char)((hi1 < 0x0a ? '0' : 'a' - 0x0a) + hi1);
			c[1] = (char)((lo1 < 0x0a ? '0' : 'a' - 0x0a) + lo1);
			c[2] = (char)((hi2 < 0x0a ? '0' : 'a' - 0x0a) + hi2);
			c[3] = (char)((lo2 < 0x0a ? '0' : 'a' - 0x0a) + lo2);

			return new string(c);
		}
	}
}
