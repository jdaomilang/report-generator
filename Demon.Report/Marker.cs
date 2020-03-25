namespace Demon.Report
{
	internal static class Marker
	{
		//	Embedded references and document properties are bounded by characters
		//	from the Private Use Area in the Unicode BMP. These characters won't
		//	appear in any user text, and are legal in XML 1.0. We don't use any
		//	of the Unicode Information Separator characters because they're not
		//	legal in XML 1.0. They are legal in XML 1.1 but hardly any parser in
		//	the world supports that version.

		public static class EmbeddedReference
		{
			public const char Start = '\xe000';
			public const char End   = '\xe001';
		}

		public static class DocumentProperty
		{
			public const char Start = '\xe002';
			public const char End   = '\xe003';
		}
	}
}
