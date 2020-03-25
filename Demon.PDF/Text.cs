using System.Collections.Generic;
using System.Text;
using Demon.Font;

namespace Demon.PDF
{
	internal class Text : ContentFragment
	{
		private Font _font;
		private int _fontSize;
		private int _x;
		private int _y;
		private Color _color;
		private string _text;

		public Text(string text, int x, int y, Font font, int fontSize, Color color)
		{
			_font = font;
			_fontSize = fontSize;
			_x = x;
			_y = y;
			_color = color;
			_text = text;
		}

		public override byte[] GetStream()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("q\r\nBT\r\n/");
			sb.Append(_font.Alias);
			sb.Append(" ");
			sb.Append(_fontSize);
			sb.Append(" Tf\r\n");
			sb.Append(_color.Red);
			sb.Append(" ");
			sb.Append(_color.Green);
			sb.Append(" ");
			sb.Append(_color.Blue);
			sb.Append(" rg\r\n");
			sb.Append(_x);
			sb.Append(" ");
			sb.Append(_y);
			sb.Append(" Td\r\n[");

			//	Kern the string. This breaks it into substrings that need no kerning
			//	within them, but do need kerning between them.
			List<KernFragment> fragments = _font.Kern(_text,0,_text.Length);
			foreach(KernFragment fragment in fragments)
			{
				sb.Append(-fragment.Adjust);
				sb.Append(" ");
				string encoded = _font.Encode(fragment.Text);
				sb.Append(encoded);
				sb.Append(" ");
			}
			
			sb.Append("] TJ\r\nET\r\nQ\r\n");
			return Encoding.UTF8.GetBytes(sb.ToString());

			//	Strings in content streams are limited to 32,767 bytes. That's not
			//	likely to be a problem for us because I think we'll be drawing strings
			//	one line at a time.
		}

		public override string Dump(int indentLevel)
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(Document.Space(indentLevel));
			sb.Append("Text font=");
			sb.Append(_font.Alias);
			sb.Append(" ");
			sb.Append(_fontSize);
			sb.Append("pt pos=");
			sb.Append(_x);
			sb.Append(",");
			sb.Append(_y);
			sb.Append(" ");

			string preview = _text.Length < 40 ? _text : _text.Substring(0,40);
			sb.Append(preview);
			
			sb.Append("\r\n");

			return sb.ToString();
		}
	}
}
