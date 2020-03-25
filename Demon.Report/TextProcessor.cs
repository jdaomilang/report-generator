using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Demon.Report.Style;

namespace Demon.Report
{
	/// <summary>
	/// Text and formatting from a single source, such as a text layout,
	/// automatically compiled into verses ready for handling.
	/// </summary>
	internal class TextBlock
	{
		private Stack<TextFormat> _formats;
		private TextFormat _rootFormat;
		private string _htmlStream;
		private int _textIndex;
		private List<Verse> _verses;
		private Generator _generator;
		private int _trackingId;

		public static char Hyphen = '-';

		public TextBlock(string htmlStream, TextFormat initialFormat, Generator generator)
		{
			_generator = generator;
			_trackingId = _generator.NextTrackingId;

			_htmlStream = htmlStream;
			//	Replace <p> with CRLF, and remove </p>
			//_htmlStream = _htmlStream.Replace("<p>","\r\n").Replace("</p>","");

			_textIndex = 0;
			_formats = new Stack<TextFormat>();

			_rootFormat = initialFormat;
			_formats.Push(_rootFormat);
			Trace("Root format: {0}", _rootFormat);

			//	Break the text into verses
			GetVerses();

			Trace(_htmlStream);
		}

		/// <summary>
		/// Break the verses into lines based on the given line width. This operation
		/// does not change the state of the text block - you can call it again with
		/// different arguments and get fresh results.
		/// </summary>
		public List<TextLine> BreakIntoLines(int width, int firstLineStartPosition, int softBreakLimit)
		{
			//	Lay out the verses in lines whose widths are bounded and whose
			//	heights are zero. We'll handle the heights when we do the
			//	actual layout.
			List<TextLine> lines = new List<TextLine>();
			TextLine line = new TextLine();
			lines.Add(line);
			int textPos = firstLineStartPosition;
			foreach(Verse verse in _verses)
			{
				if(verse is ParagraphVerse)
				{
					Trace("Paragraph");

					//	Insert a paragraph line and then start a new line
					lines.Add(new ParagraphLine());

					line = new TextLine();
					lines.Add(line);
					textPos = 0;

					continue;
				}
				if(verse is LineBreakVerse)
				{
					Trace("LineBreak");

					//	Start a new line
					line = new TextLine();
					lines.Add(line);
					textPos = 0;

					continue;
				}
				
				Font.Font font = _generator.GetFont(verse.Format.Font);

				int startIndex = 0;
				while(true) //startIndex < verse.Text.Length)
				{
					//	Skip any whitespace at the start of the physical line
					if(textPos == 0)
					{
						while((startIndex < verse.Text.Length) && (char.IsWhiteSpace(verse.Text[startIndex])))
							++startIndex;
						if(startIndex >= verse.Text.Length)
							break; // nothing left after leading white space
					}

					//	Get as much of this verse as can fit on the current line.
					//	Express our soft break limit in terms relative to the
					//	position where this verse starts.
					int availableWidth = width - textPos;
					int verseSoftBreakLimit = softBreakLimit - textPos;
					if(verseSoftBreakLimit < 0)
						verseSoftBreakLimit = 0;
					Trace(
						"width={0} availableWidth={1} textPos={2} verseSoftBreakLimit={3}",
						width, availableWidth, textPos, verseSoftBreakLimit);
					System.Drawing.Size textSize;
					bool hardBreakMade;
					int endIndex = GetLineBreakPosition(
						verse.Text, startIndex,
						font, verse.Format.Font.Size, availableWidth,
						verseSoftBreakLimit, out textSize, out hardBreakMade);

					//	GetLineBreakPosition returns these values:
					//
					//	-	endIndex > startIndex: The text should break at the
					//		given location. Add the indicated portion of the text
					//		to the current line and append a hyphen, then continue
					//		with the rest of the verse. The soft break limit
					//		should be small enough to ensure that there's room
					//		for the hyphen.
					//
					//	-	endIndex == startIndex: There isn't enough space
					//		left on this line for any more text. In that case
					//		start a new line and continue processing at the
					//		same location in the text.
					//
					//	-	endIndex == -1: The text fits on the current line
					//		without breaking. In that case add the text to the
					//		current line and move on to the next verse. Do not
					//		start a new line, because the next verse might fit
					//		on the current line.
					if(endIndex > startIndex)
					{
						int len = endIndex - startIndex;
						string part = verse.Text.Substring(startIndex, len);
						if(hardBreakMade)
							part += Hyphen;

						Stroke stroke = new Stroke(part, verse.Format, textSize.Width);
						line.Add(stroke);
						
						Trace("Stroke at {0:000} : {1}", textPos, stroke);

						//	Start a new line
						line = new TextLine();
						lines.Add(line);
						textPos = 0;
					}
					else if(endIndex >= 0)
					{
						Trace("New line");

						//	There is not enough room to take even one character from the
						//	text, so we need to start a new line. If the current line
						//	already has some text then that's OK - we just move on to the
						//	next line. But if the current line is empty then that means
						//	that we couldn't fit even one character on the whole line,
						//	and so if we just add a new line here and try again we'll end
						//	up adding an infinite number of empty lines. So in that case
						//	add just one character to the line - it will exceed the line
						//	bounds, and could give us a very long, thin layout, but that's
						//	better than an infinite loop.
						if(line.Strokes.Count == 0)
						{
							endIndex = startIndex + 1;
							string part = verse.Text.Substring(startIndex,1);
							
							//	Measure this one character
							textSize.Width = font.GetTextLength(part, 0, 1, verse.Format.Font.Size);
							textSize.Width /= font.UnitsPerEm;

							Stroke stroke = new Stroke(part, verse.Format, textSize.Width);
							line.Add(stroke);
						}
						line = new TextLine();
						lines.Add(line);
						textPos = 0;
					}
					else if(endIndex == -1)
					{
						//	Get the remainder of the verse text
						string part = verse.Text.Substring(startIndex);

						Stroke stroke = new Stroke(part, verse.Format, textSize.Width);
						line.Add(stroke);
						
						Trace("Stroke at {0:000} : {0}", textPos, stroke);
						Trace("End of verse");

						textPos += textSize.Width;

						//	Finished with this verse, so move on to the next
						break;
					}

					//	Move on to the next part of the text
					startIndex = endIndex;
				}
			}

			//	Remove empty strokes from all lines, and then remove lines
			//	with no strokes, except for paragraph lines. An empty stroke
			//	can be troublesome because it can influence the line height
			//	without contributing any content. For example, if the empty
			//	stroke pops a format that transitions us from a small font
			//	to a larger font, then that larger font will increase the
			//	line height.
			List<TextLine> cleaned = new List<TextLine>();
			foreach(TextLine candidate in lines)
			{
				List<Stroke> strokes = new List<Stroke>(candidate.Strokes);
				candidate.Strokes.Clear();
				foreach(Stroke stroke in strokes)
					if(!string.IsNullOrWhiteSpace(stroke.Text))
						candidate.Strokes.Add(stroke);

				if((candidate.Strokes.Count > 0) || (candidate is ParagraphLine))
					cleaned.Add(candidate);
			}

			//	Measure each line to get its height
			cleaned.ForEach(l => l.Measure(_generator));

			return cleaned;
		}

		/// <summary>
		/// </summary>
		/// <param name="text"></param>
		/// <param name="fontSize"></param>
		/// <param name="boundsWidth">The maximum length of a line, in user space units.</param>
		/// <param name="softBreakLimit"></param>
		/// <param name="textlength">Returns the lengths, in font units, of the wrapped lines.</param>
		private int GetLineBreakPosition(
			string text, int startIndex,
			Font.Font font, int fontSize, int boundsWidth,
			int softBreakLimit, out System.Drawing.Size textSize, out bool hardBreakMade)
		{
			//	We want to find the character position at which to wrap the
			//	string onto a new line. We define three (position, length)
			//	pairs:
			//
			//		low			:	A position that falls within the bounds width.
			//							If we accept this position then the text will
			//							not exceed the bounds, but if we accept it too
			//							soon then we may cut the line shorter than we
			//							need to. Our objective is to raise this position
			//							as high as possible without exceeding the
			//							bounds width.
			//
			//		high		:	A position that falls outside the bounds width.
			//							We can't accept this position under any
			//							circumstance. (Except of course if the conditions
			//							imposed on us are impossible.) Our objective
			//							is to move this position down just low enough
			//							to come within the bounds width.
			//
			//		current	:	The current position that we're testing. It
			//							will always be within the closed range
			//							[low, high].
			//
			//	To set the initial positions:
			//
			//		low			:	Assume that all characters are the maximum width
			//							within the font. Set the initial low position at
			//							the number of characters of this width that will
			//							still fall within the bounds width. This will
			//							never put us outside the bounds (unless the bounds
			//							are impossibly tight.)
			//
			//		high		:	Assume that all characters are of the average
			//							font width. Set the initial high position at
			//							the smallest number of characters of this width
			//							that will exceed the bounds width. Note that this
			//							could still put us within the bounds when we apply
			//							the actual character widths. If that happens then
			//							we'll jump straight to the final fine-tuning.
			//
			//		current	:	Set the current position to the high position.
			//							It seems likely that the average-based high
			//							will be closer to the truth than the max-based
			//							low.
			//
			//							If the current position is greater than the string
			//							length then set it to the string length.
			//
			//	The algorithm:
			//
			//		1.	Measure the string, using actual character widths, up
			//				to the current position. Store this in current.length.
			//
			//		2.	If current.length is less than or equal to the bounds width
			//				then set low = current (copy both position and length).
			//
			//		3.	If the current.length is greater than or equal to the bounds
			//				width then set high = current (copy both position and
			//				length.)
			//
			//				This doubling up of <= and >= in steps 2 and 3 allows low
			//				and high to coincide. Not likely to happen, but it could do.
			//
			//		4.	If low.length is greater than or equal to the bounds width then
			//				go to the fine-tuning stage.
			//
			//		5.	If high.length is less than or equal to the bounds width then go
			//				to the fine-tuning stage.
			//
			//		6.	Set current.position to half the difference between low.position
			//				and high.position. Go back to step 1.
			//
			//	Fine tuning:
			//
			//		7.	If current.length is less than the bounds width then
			//				increase current.position by 1 and re-measure. Repeat
			//				until current.length exceeds the bounds width. (That
			//				is, until we've gone too far.)
			//
			//		8.	If current.length is greater than the bounds width then
			//				decrease current.position by 1 and re-measure. Repeat
			//				until current.length is less than or equal to the
			//				bounds width.
			//
			//	This gives us a current position that's just within the bounds. This is
			//	the "hard break position" - if we don't find a suitable place for a
			//	soft break then we'll break here at the hard break position. Next
			//	we want to find a sensible position to break at - this is a soft break.
			//	A soft break breaks the line on a space or other suitable character.
			//	We'd prefer not to break the line in the middle of a word, but we'll
			//	do so if we have to - that's a hard break.
			//
			//	Apply the break:
			//
			//		9.	If the character at the current position is a soft-break
			//				character then break here. End.
			//
			//		10.	If the current length is at or before the soft-break
			//				limit then we didn't find a suitable soft break position
			//				and so we must do a hard break. Break at the hard break
			//				position discovered at the end of the fine-tuning stage.
			//				End.
			//
			//		11.	Decrease the current position by 1 and re-measure. Go
			//				to step 9.

			//	Convert bounds width to font units
			boundsWidth    *= font.UnitsPerEm;
			softBreakLimit *= font.UnitsPerEm;

			Trace("boundsWidth={0} softBreakLimit={1} startIndex={2} text={3}", boundsWidth, softBreakLimit, startIndex, text);

			int endIndex = text.Length; // one past the end
			int height = font.GetDefaultLineSpacing(fontSize); // in text/user units

			hardBreakMade = false;

			//	If the text is short enough not to need wrapping then do nothing
			int textLength = font.GetTextLength(text,startIndex,endIndex,fontSize);
			if(textLength <= boundsWidth)
			{
				//	Convert the text width back to user/text units
				textLength /= font.UnitsPerEm;

				textSize = new System.Drawing.Size(textLength,height);

				//	Return -1 to indicate that the text fits in without breaking
				return -1;
			}

			int maxCharWidth = font.GetMaxWidth(fontSize);
			int avgCharWidth = font.GetAverageWidth(fontSize);

			textLength = 0;
			int loPos = startIndex + (boundsWidth / maxCharWidth);
			if(loPos >= text.Length)
				loPos = text.Length - 1;
			if(loPos < startIndex)
			{
				//	Bounds too small for even one character
				textSize = new System.Drawing.Size();

				//	Return the given start index to indicate that we've
				//	consumed no text and we need a new line
				return startIndex;
			}
			int loLen = font.GetTextLength(text,startIndex,loPos,fontSize);
			
			int hiPos = startIndex + (boundsWidth / avgCharWidth);
			if(hiPos >= text.Length)
				hiPos = text.Length - 1;
			if(hiPos < startIndex)
			{
				//	Bounds too small for even one character
				textSize = new System.Drawing.Size();

				//	Return the given start index to indicate that we've
				//	consumed no text and we need a new line
				return startIndex;
			}
			int hiLen = font.GetTextLength(text,startIndex,hiPos,fontSize);
			int curPos = hiPos;
			int curLen = hiLen;
			Trace(
				"Start: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
				loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
	
			while(true)
			{
				//	2.	If current.length is less than or equal to the bounds width
				//			then set low = current (copy both position and length).
				if(curLen <= boundsWidth)
				{
					loPos = curPos;
					loLen = curLen;
					Trace(
						"2: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
						loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
				}

				//	3.	If the current.length is greater than or equal to the bounds
				//			width then set high = current (copy both position and
				//			length.)
				if(curLen >= boundsWidth)
				{
					hiPos = curPos;
					hiLen = curLen;
					Trace(
						"3: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
						loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
				}

				//	4.	If low.length is greater than or equal to the bounds width then
				//			go to the fine-tuning stage.
				if(loLen >= boundsWidth)
					break;

				//	5.	If high.length is less than or equal to the bounds width then go
				//			to the fine-tuning stage.
				if(hiLen <= boundsWidth)
					break;
			
				//	6.	Set current.position to half the difference between low.position
				//			and high.position. Go back to step 1.
				//
				//	If this calculation doesn't change the current position then stop
				int newPos = loPos + ((hiPos - loPos) / 2);
				if(newPos == curPos) break;
				curPos = newPos;
				Trace(
					"6: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
					loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);

				//	1.	Measure the string, using actual character widths, up
				//			to the current position. Store this in current.length.
				curLen = font.GetTextLength(text,startIndex,curPos,fontSize);
				Trace(
					"1: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
					loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
			}

			//----------------------------------------------------------------------
			//	Fine tuning
			//
			//	7.	If current.length is less than the bounds width then
			//			increase current.position by 1 and re-measure. Repeat
			//			until current.length exceeds the bounds width. (That
			//			is, until we've gone too far.)
			while(curLen < boundsWidth)
			{
				//	But don't go past the end of the string
				if(curPos >= text.Length - 1) break;

				++curPos;
				curLen = font.GetTextLength(text,startIndex,curPos,fontSize);
				Trace(
					"7: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
					loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
			}

			//	8.	If current.length is greater than the bounds width then
			//			decrease current.position by 1 and re-measure. Repeat
			//			until current.length is less than or equal to the
			//			bounds width.
			while(curLen > boundsWidth)
			{
				if(curPos == 0) break;

				--curPos;
				curLen = font.GetTextLength(text,startIndex,curPos,fontSize);
				Trace(
					"8: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
					loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
			}

			textLength = curLen;
			int hardBreakPos = curPos > 0 ? curPos - 1 : 0;
			Trace("hardBreakPos={0} (allowing for hyphen if possible)", hardBreakPos);

			//-------------------------------------------------------------------
			//	Apply the break:

			while(curPos > 0)
			{
				char prevChar = text[curPos - 1];

				//	9.	If the character at the current position is a soft-break
				//				character then break here. End.
				if(CanBreakAt(text[curPos], prevChar))
					break;
				
				//	If the previous character is punctuation then break after it
//				if(CanBreakAfter(prevChar))
//					break;
//Not sure that we want this. Normally punctuation is followed by a
//space, in which case the previous check at step 9 would kick in.
//If the punctuation isn't followed by space then it's probably
//not really punctuation at all, but rather part of a code of
//some kind.

				//	10.	If the current length is at or before the soft-break
				//			limit then break at the hard break position. End.
				if(curLen <= softBreakLimit)
				{
					//	If we could break at the character before this position
					//	then there's no need for a hyphen. For example if the
					//	current position is the start of a word, and it's
					//	preceded by space, we don't need a hyphen after the space.
					if(!CanBreakAt(text[curPos-1], (char)0))
						hardBreakMade = true;

					break;
				}

				//	11.	Decrease the current position by 1 and re-measure. Go
				//			to step 10.
				--curPos;
				curLen = font.GetTextLength(text,startIndex,curPos,fontSize);
				Trace(
					"11: loPos={0} hiPos={1} curPos={2} loLen={3} hiLen={4} curLen={5} startIndex={6} text={7}",
					loPos, hiPos, curPos, loLen, hiLen, curLen, startIndex, text);
				textLength = curLen;
			}

			//	If we have to make a hard break then do it as far to the right as possible,
			//	at the hard break position discovered at the end of the fine-tuning stage
			if(hardBreakMade)
			{
				curPos = hardBreakPos;
				curLen = font.GetTextLength(text,startIndex,curPos,fontSize);
				textLength = curLen;
				Trace("Applied hard break: pos={0} len={1}", curPos, curLen);
			}
			else if(curPos < text.Length)
				Trace("Applied soft break: pos={0} len={1}", curPos, curLen);
			else
				Trace("No break required");

			//	Convert the text width back to user/text units
			textLength /= font.UnitsPerEm;
			textSize = new System.Drawing.Size(textLength,height);
			return curPos;
		}

		private bool CanBreakAt(char c, char before)
		{
			//	IsWhiteSpace includes CR, LF, tab, paragraph mark etc.
			if(char.IsWhiteSpace(c)) return true;

			//	We can break at an opening double quote. We guess it's
			//	opening if it's preceded by white space.
			if((c == '"') && char.IsWhiteSpace(before)) return true;

			return false;
		}

		private bool CanBreakAfter(char c)
		{
			if(c == '.') return true;
			if(c == ',') return true;
			if(c == ';') return true;
			if(c == ':') return true;
			if(c == '-') return true;
//			if(c == '"') return true;
			if(c == '?') return true;
			if(c == '!') return true;
			return false;
		}

		private void GetVerses()
		{
			_verses = new List<Verse>();
			if(_htmlStream == null) return;

			//	If the text is HTML then process it into verses, otherwise just create
			//	a single verse. We use the TinyMCE editor for designing layout text,
			//	and that always starts the text with <p>.
			if(_htmlStream.StartsWith("<p>"))
			{
				//	Break the stream into verses at each change of format. We only
				//	support these HTML format tags: <p> <b> <i> <span style=font-family>
				//	<span style=font-size> <span style=color>
				int verseStart = _textIndex;
				while(_textIndex < _htmlStream.Length)
				{
					//	"while" because we could have two format tags together like <b><i>
					while(_htmlStream[_textIndex] == '<')
					{
						if(_textIndex >= _htmlStream.Length - 1)
							throw new InvalidOperationException($"Unclosed '<' at position {_textIndex}.");
					
						//	We start a new verse at every start or end tag. But if the verse
						//	is empty (two tags together with no intervening text) then we
						//	discard it.
						int verseEnd = _textIndex - 1;
						int len = verseEnd - verseStart + 1;
						if(len > 0)
						{
							Verse verse = new Verse(_htmlStream.Substring(verseStart,len), _formats.Peek());
							verse.DecodeEntities();
							_verses.Add(verse);
						
							Trace("Verse: {0}", verse);
						}

						if(_htmlStream[_textIndex + 1] == '/')
						{
							HandleEndTag();
						}
						else
						{
							HtmlTag tag = HandleStartTag();

							//	If it's a <p> tag then insert a special paragraph verse
							if(tag is ParagraphTag)
								_verses.Add(new ParagraphVerse());

							//	If it's a <br> tag then insert a special line verse
							if(tag is LineBreakTag)
								_verses.Add(new LineBreakVerse());
						}

						verseStart = _textIndex; // start of next verse
					
						//	HandleXxxTag can push _textIndex past the end of the stream
						if(_textIndex >= _htmlStream.Length) break;
					}
					++_textIndex;
				}
			}
			else
			{
				Verse verse = new Verse(_htmlStream, _formats.Peek());
				_verses.Add(verse);
			}
		}

		private HtmlTag HandleStartTag()
		{
			HtmlTag tag = ReadTag();

			if     (tag is BoldTag)      PushFormat(tag as BoldTag);
			else if(tag is ItalicTag)    PushFormat(tag as ItalicTag);
			else if(tag is UnderlineTag) PushFormat(tag as UnderlineTag);
#if false
			else if(tag is FontTag)      PushFormat(tag as FontTag);
			else if(tag is ColorTag)     PushFormat(tag as ColorTag);
#endif
			else if(tag is SpanTag)      PushFormat(tag as SpanTag);
			else if(tag is ParagraphTag) PushFormat(tag as ParagraphTag);
			else if(tag is LineBreakTag) {} // do nothing

			else throw new InvalidOperationException($"Found unexpected start tag {tag}");
			return tag;
		}

		private void HandleEndTag()
		{
			HtmlTag tag = ReadTag();
//			if(tag != null)
				PopFormat();
		}

		private void PushFormat(BoldTag tag)
		{
			//	Take a copy of the current format and modify it
			TextFormat format = new TextFormat(_formats.Peek());
			format.Font.Bold = true;
			_formats.Push(format);

			Trace("Pushed {0}", format);
		}

		private void PushFormat(ItalicTag tag)
		{
			//	Take a copy of the current format and modify it
			TextFormat format = new TextFormat(_formats.Peek());
			format.Font.Italic = true;
			_formats.Push(format);

			Trace("Pushed {0}", format);
		}

		private void PushFormat(UnderlineTag tag)
		{
			//	Take a copy of the current format and modify it
			TextFormat format = new TextFormat(_formats.Peek());
			format.Font.Underline = true;
			_formats.Push(format);

			Trace("Pushed {0}", format);
		}

#if false
		private void PushFormat(FontTag tag)
		{
			//	Take a copy of the current format and modify it
			TextFormat current = new TextFormat(_formats.Peek());
			Font font = new Font
			{
				FaceName = tag.FaceName ?? current.Font.FaceName,
				Size = tag.Size ?? current.Font.Size,
				Bold = current.Font.Bold,
				Italic = current.Font.Italic
			};
			TextFormat format = new TextFormat(font,current.Color);
			_formats.Push(format);

			Debug("Pushed {0}", format);
		}

		private void PushFormat(ColorTag tag)
		{
			//	Take a copy of the current format and modify it
			TextFormat current = _formats.Peek();
			TextFormat format = new TextFormat(current.Font,tag.Color);
			_formats.Push(format);

			Debug("Pushed {0}", format);
		}
#endif

		private void PushFormat(SpanTag tag)
		{
			//	A <span> tag can contain font and colour

			//	Take a copy of the current format and modify it
			TextFormat current = _formats.Peek();
			Style.Font font = new Style.Font
			{
				FamilyName = current.Font.FamilyName,
				Size = current.Font.Size,
				Bold = current.Font.Bold,
				Italic = current.Font.Italic,
				Underline = current.Font.Underline
			};
			if(!string.IsNullOrWhiteSpace(tag.FaceName))
				font.FamilyName = tag.FaceName;
			if(tag.Size != null)
				font.Size = tag.Size.Value;

			Color color = tag.Color ?? current.Color;
			
			TextFormat format = new TextFormat(font,color);
			_formats.Push(format);

			Trace("Pushed {0}", format);
		}

		private void PushFormat(ParagraphTag tag)
		{
			//	The format doesn't change with a <p> tag, but it makes
			//	the stack management easier if we push here. So just
			//	push the top format again.
			TextFormat format = _formats.Peek();
			_formats.Push(format);

			Trace("Pushed {0}", format);
		}

		private void PopFormat()
		{
//			//	Check that the closing tag matches the current format
//			TextFormat format = _formats.Peek();
//			HtmlTag tag = ReadTag();
//			if(!format.Matches(tag))
//				throw new InvalidOperationException();

			TextFormat format = _formats.Pop();

			Trace("Popped {0}", format);
		}

		/// <summary>
		/// Advances _textIndex to the next character after the closing angle bracket.
		/// </summary>
		private HtmlTag ReadTag()
		{
			if(_htmlStream[_textIndex] != '<')
				throw new InvalidOperationException();

			//	Find the end of the tag
			bool closed = false;
			int start = _textIndex;
			while(_textIndex < _htmlStream.Length)
			{
				if(_htmlStream[_textIndex++] == '>')
				{
					closed = true;
					break;
				}
			}
			if(!closed)
				throw new InvalidOperationException($"Unclosed '<' character at position {start}.");
			
			int len = _textIndex - start;
			string spec = _htmlStream.Substring(start,len).ToLower();
			HtmlTag tag = HtmlTag.Parse(spec);

			if(tag != null)
				Trace("Read tag {0}", tag);
			else
				Trace("Skipped tag {0}", spec);

			return tag;
		}

		public List<Verse> Verses { get { return _verses; }}

		public void MapFontCharacters()
		{
			foreach(Verse verse in _verses)
				verse.MapFontCharacters(_generator);
		}

		protected void Trace(string format, params object[] args)
		{
			if(_generator.TraceText)
			{
				//	Prepend our tracking id to the message
				format = $"~{{{args.Length}}} {format}";
				object[] newargs = new object[args.Length + 1];
				for(int x = 0; x < args.Length; ++x)
					newargs[x] = args[x];
				newargs[newargs.Length - 1] = _trackingId;

				_generator.TraceTextProcessing(format, 1, newargs);
			}
		}
	}

	internal abstract class HtmlTag
	{
		public static HtmlTag Parse(string spec)
		{
			if(BoldTag     .Matches(spec))	return new BoldTag();
			if(ItalicTag   .Matches(spec))	return new ItalicTag();
			if(UnderlineTag.Matches(spec))	return new UnderlineTag();
			if(SpanTag     .Matches(spec))	return new SpanTag(spec);
#if false
			if(FontTag     .Matches(spec))	return new FontTag(spec);
			if(ColorTag    .Matches(spec))	return new ColorTag(spec);
#endif
			if(ParagraphTag.Matches(spec))	return new ParagraphTag();
			if(LineBreakTag.Matches(spec))	return new LineBreakTag();

			return null;				
		}
		
	}

	internal class BoldTag : HtmlTag
	{
		public static bool Matches(string spec)
		{
			string pattern = "<b>";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			return "Bold";
		}
	}

	internal class ItalicTag : HtmlTag
	{
		public static bool Matches(string spec)
		{
			string pattern = "<i>";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			return "Italic";
		}
	}

	internal class UnderlineTag : HtmlTag
	{
		public static bool Matches(string spec)
		{
			string pattern = "<u>";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			return "Underline";
		}
	}

#if false
	internal class FontTag : HtmlTag
	{
		// We only support explicit face names, no families
		private static string _reFamily = "font-family: *([^;]+);";

		//	We only support size defined in points
		private static string _reSize   = "font-size: *([0-9]+)pt;";

		private string _facename;
		private int? _size;

		public FontTag(string spec)
		{
			//	<span style="font-family: helvetica;">
			//	<span style="font-size: 24pt;">
			//	<span style="font-family: helvetica; font-size: 24pt;">
			
			MatchCollection mc = Regex.Matches(spec,_reFamily);
			if(mc.Count == 1)
				_facename = mc[0].Groups[1].Captures[0].Value;

			mc = Regex.Matches(spec,_reSize);
			if(mc.Count == 1)
			{
				string sizeSpec = mc[0].Groups[1].Captures[0].Value;
				int size;
				bool ok = int.TryParse(sizeSpec, out size);
				if(ok)
					_size = size;
			}
		}

		public string FaceName { get { return _facename; }}
		public int? Size { get { return _size; }}

		public static bool Matches(string spec)
		{
			string pattern = "<span style=\"font-";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			string s = "Font";
			if(_facename != null)
				s += " " + _facename;
			if(_size != null)
				s += $" {_size}pt";
			return s;
		}
	}

	internal class ColorTag : HtmlTag
	{
		// We only support hex colours
		private static string _reColor = "color: *#([a-f0-9]{2})([a-f0-9]{2})([a-f0-9]{2});";

		private Color _color;

		public ColorTag(string spec)
		{
			//	<span style="color: #ff0000;">
			
			MatchCollection mc = Regex.Matches(spec,_reColor);
			if(mc.Count == 1)
			{
				string component = mc[0].Groups[1].Captures[0].Value;
				int red;
				bool ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out red);

				component = mc[0].Groups[2].Captures[0].Value;
				int green;
				ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out green);

				component = mc[0].Groups[3].Captures[0].Value;
				int blue;
				ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out blue);

				_color = new Color
				{
					Red   = red   > 0 ? (double)red   / (double)255 : 0,
					Green = green > 0 ? (double)green / (double)255 : 0,
					Blue  = blue  > 0 ? (double)blue  / (double)255 : 0
				};
			}
		}

		public Color Color { get { return _color; }}

		public static bool Matches(string spec)
		{
			string pattern = "<span style=\"color: ";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}

		public override String ToString()
		{
			string s = "Color";
			if(_color != null)
				s += $" r{_color.Red} g{_color.Green} b{_color.Blue}";
			return s;
		}
	}
#endif

	internal class SpanTag : HtmlTag
	{
		//	A <span> tag can contain font name, font size and colour

		// We only support explicit face names, no families
		private static string _reFamily = "font-family: *([^;]+);";

		//	We only support size defined in points
		private static string _reSize   = "font-size: *([0-9]+)pt;";

		// We only support hex colours
		private static string _reColor = "color: *#([a-f0-9]{2})([a-f0-9]{2})([a-f0-9]{2});";

		private string _facename;
		private int? _size;
		private Color _color;


		public SpanTag(string spec)
		{
			//	<span style="font-family: helvetica;">
			//	<span style="font-size: 24pt;">
			//	<span style="font-family: helvetica; font-size: 24pt;">
			//	<span style="color: #ff0000;">
			
			MatchCollection mc = Regex.Matches(spec,_reFamily);
			if(mc.Count == 1)
				_facename = mc[0].Groups[1].Captures[0].Value;

			mc = Regex.Matches(spec,_reSize);
			if(mc.Count == 1)
			{
				string sizeSpec = mc[0].Groups[1].Captures[0].Value;
				int size;
				bool ok = int.TryParse(sizeSpec, out size);
				if(ok)
					_size = size;
			}

			mc = Regex.Matches(spec,_reColor);
			if(mc.Count == 1)
			{
				string component = mc[0].Groups[1].Captures[0].Value;
				int red;
				bool ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out red);

				component = mc[0].Groups[2].Captures[0].Value;
				int green;
				ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out green);

				component = mc[0].Groups[3].Captures[0].Value;
				int blue;
				ok = int.TryParse(component, System.Globalization.NumberStyles.HexNumber, null, out blue);

				_color = new Color
				{
					Red   = red   > 0 ? (double)red   / (double)255 : 0,
					Green = green > 0 ? (double)green / (double)255 : 0,
					Blue  = blue  > 0 ? (double)blue  / (double)255 : 0
				};
			}
		}

		public string FaceName { get { return _facename; }}
		public int? Size { get { return _size; }}
		public Color Color { get { return _color; }}

		public static bool Matches(string spec)
		{
			string pattern = "<span style=\"font-";
			if((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern))
				return true;

			pattern = "<span style=\"color: ";
			if((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern))
				return true;

			return false;
		}
		public override String ToString()
		{
			string s = "";
			if(_facename != null)
				s += " " + _facename;
			if(_size != null)
				s += $" {_size}pt";
			if(s != "")
				s = $"Font {s}";

			if(_color != null)
				s += $" Color r{_color.Red} g{_color.Green} b{_color.Blue}";
			return s;
		}
	}

	internal class ParagraphTag : HtmlTag
	{
		public static bool Matches(string spec)
		{
			string pattern = "<p>";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			return "Paragraph";
		}
	}

	internal class LineBreakTag : HtmlTag
	{
		public static bool Matches(string spec)
		{
			string pattern = "<br>";
			return ((spec.Length >= pattern.Length) && (spec.Substring(0,pattern.Length).ToLower() == pattern));
		}
		public override String ToString()
		{
			return "LineBreak";
		}
	}

	internal class TextLine
	{
		private List<Stroke> _strokes;
		private int _width;
		private int _height;
		private int _ascender;
		private int _descender;
		/// <summary>
		/// Offset from the top of the line bounds to the baseline.
		/// </summary>
		private int _baseline;
		
		public TextLine()
		{
			_strokes = new List<Stroke>();
			_width = 0;
			_height = 0;
			_ascender = 0;
			_descender = 0;
			_baseline = 0;
		}

		public void Add(Stroke stroke)
		{
			_strokes.Add(stroke);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="generator"></param>
		/// <param name="baseline">Distance from the top of the line bounds to the baseline.</param>
		/// <param name="bottom">Distance from the top of the line bounds to the bottom.</param>
		public void Measure(Generator generator)
		{
			//	Measure these things:
			//
			//	1.	We want all strokes on the same baseline.
			//
			//	2.	After positioning every stroke on the baseline,
			//		set our height from the max ascender and the
			//		max descender.
			//
			//	3.	Line width = sum of stroke widths

			_baseline = 0;
			_ascender = 0;
			_height = 0;
			_width = 0;
			foreach(Stroke stroke in _strokes)
			{
				//	Find the greatest ascender, and that gives us our baseline
				TextFormat format = stroke.Format;
				Font.Font font = generator.GetFont(format.Font);
				int asc = font.GetAscender(stroke.Format.Font.Size);
				if(asc > _ascender)
					_ascender = asc;
				_baseline = _ascender;
				
				//	Find the greatest descender, and that gives us our bottom bound.
				//	Note that the descender is given as a negative figure.
				int desc = font.GetDescender(stroke.Format.Font.Size);
				desc *= -1;
				if(desc > _descender)
					_descender = desc;
				_height = _baseline + _descender;

				//	Width
				_width += stroke.Width;
			}
		}

		public int Width { get { return _width; }}
		public int Height { get { return _height; }}
		public int Ascender { get { return _ascender; }}
		/// <summary>
		/// Offset from the top of the line bounds to the baseline.
		/// </summary>
		public int BaseLine { get { return _height - _descender; }}
		public List<Stroke> Strokes { get { return _strokes; }}

		public string GetRoughText()
		{
			if(_strokes.Count == 0) return null;

			StringBuilder sb = new StringBuilder();
			sb.Append(_strokes[0].Text);
			for(int x = 1; x < _strokes.Count; ++x)
			{
				Stroke stroke = _strokes[x];
				sb.Append("§");
				sb.Append(stroke.Text);
			}
			return sb.ToString();
		}
	}

	internal class ParagraphLine : TextLine
	{
	}

	/// <summary>
	/// Defines a font typeface, font size, effects such as bold or italic, and colour.
	/// </summary>
	internal class TextFormat
	{
		private Style.Font _font; // typeface, size, bold, italic, underline, strikeout
		private Color _color;

		public TextFormat(Style.Font font, Color color)
		{
			_font = font;
			_color = color;
		}
		public TextFormat(TextFormat src)
		{
			_font = new Style.Font
			{
				FamilyName = src.Font.FamilyName,
				Size = src.Font.Size,
				Bold = src.Font.Bold,
				Italic = src.Font.Italic
			};
			_color = new Color
			{
				Red   = src.Color.Red,
				Green = src.Color.Green,
				Blue  = src.Color.Blue
			};
		}
		public Style.Font Font { get { return _font; }}
		public Color Color { get { return _color; }}

		public override String ToString()
		{
			string s = "Format";
			if(_font != null)
			{
				if(_font.FamilyName != null)
					s += " " + _font.FamilyName;
				if(_font.Size != 0)
					s += $" {_font.Size}pt";
				if(_font.Bold)
					s += " bold";
				if(_font.Italic)
					s += " italic";
			}
			if(_color != null)
			{
				s += $" r{_color.Red} g{_color.Green} b{_color.Blue}";
			}
			return s;
		}
	}

	/// <summary>
	/// Holds a string of text of arbitrary length, with a single format. The
	/// text length can exceed the text layout bounds.
	/// </summary>
	internal class Verse
	{
		private string _text;
		private TextFormat _format;
		public Verse(string text, TextFormat format)
		{
			_text = text;
			_format = format;
		}

		public void DecodeEntities()
		{
			if(_text == null) return;

			StringBuilder sb = new StringBuilder();
			for(int x = 0; x < _text.Length; ++x)
			{
				switch(_text[x])
				{
					case '&':
						char entity = DecodeEntity(ref x);
						sb.Append(entity);
						break;
					case '\r':
						sb.Append(' ');
						break;
					case '\n':
						break; // skip this
					case '\t':
						sb.Append(' ');
						break;
					default:
						sb.Append(_text[x]);
						break;
				}
			}
			_text = sb.ToString();
		}

		private char DecodeEntity(ref int index)
		{
			if(_text[index] != '&')
				throw new InvalidOperationException($"Character entity must start with &, at position {index} in verse: {_text}");

			//	Find the closing semicolon
			int semicolon = -1;
			for(int x = index; x < _text.Length; ++x)
			{
				if(_text[x] == ';')
				{
					semicolon = x;
					break;
				}
			}
			if(semicolon == -1)
			{
//				Debug($"Unclosed character entity at position {index} in verse: {_text}");
				//	Return the raw character without decoding
				return _text[index];
			}
			
			//	Advance to the end of the entity, but not past it because
			//	the caller will advance it once more next time round its
			//	loop. Also keep a record of the original index in case we
			//	don't recognize the entity.
			string encoding = _text.Substring(index, semicolon - index + 1);
			int originalIndex = index;
			index = semicolon;
			switch(encoding)
			{
				case "&quot;"	:	return '\"';
				case "&amp;"	:	return '&';
				case "&apos;"	:	return '\'';
				case "&lt;"		:	return '<';
				case "&gt;"		:	return '>';
				case "&nbsp;"	:	return ' ';
				case "&cent;"	:	return '¢';
				case "&pound;"	:	return '£';
				case "&yen;"	:	return '¥';
				case "&copy;"	:	return '©';
				case "&reg;"	:	return '®';
				case "&deg;"	:	return '°';
				case "&plusmn;"	:	return '±';
				case "&sup2;"	:	return '²';
				case "&sup3;"	:	return '³';
				case "&acute;"	:	return '´';
				case "&micro;"	:	return 'µ';
				case "&frac4;"	:	return '¼';
				case "&frac2;"	:	return '½';
				case "&frac34;"	:	return '¾';
				case "&times;"	:	return '×';
				case "&divide;"	:	return '÷';
				case "&tilde;"	:	return '˜';
				case "&ndash;"	:	return '–';
				case "&mdash;"	:	return '—';
				case "&lsquo;"	:	return '‘';
				case "&rsquo;"	:	return '’';
				case "&ldquo;"	:	return '“';
				case "&rdquo;"	:	return '”';
				case "&bull;"	:	return '•';
				case "&permil;"	:	return '‰';
				case "&prime;"	:	return '′';
				case "&Prime;"	:	return '″';
				case "&frasl;"	:	return '⁄';
				case "&euro;"	:	return '€';
				case "&trade;"	:	return '™';
				case "&sum;"	:	return '∑';
				case "&minus;"	:	return '−';
				case "&radic;"	:	return '√';
				case "&prop;"	:	return '∝';
				case "&infin;"	:	return '∞';
				case "&ne;"		:	return '≠';
				case "&le;"		:	return '≤';
				case "&ge;"		:	return '≥';
			}

			//	Unrecognized entity, so just return the raw character. And restore
			//	the index because we haven't done any decoding.
			index = originalIndex;
//			Debug($"Unrecognised character entity at position {index} in verse: {_text}");
			return _text[index];
		}

		public void MapFontCharacters(Generator generator)
		{
			if(_text == null) return; // will be null on paragraph and line break verses

			Font.Font font = generator.GetFont(_format.Font);
			foreach(char c in _text)
				font.MapCharacter(c);

			//	Also map the hyphen in case it's not part of the content
			//	but gets inserted at a word break during drafting
			font.MapCharacter(TextBlock.Hyphen);
		}

		public string Text { get { return _text; }}
		public TextFormat Format { get { return _format; }}
		public override String ToString()
		{
			return _text + "¶";
		}
	}

	internal class ParagraphVerse : Verse
	{
		public ParagraphVerse()
			:base(null,null)
		{
		}
	}

	internal class LineBreakVerse : Verse
	{
		public LineBreakVerse()
			:base(null,null)
		{
		}
	}

	/// <summary>
	/// Holds a string of text with a single format, that fits horizontally
	/// within a given text layout bounds.
	/// </summary>
	internal class Stroke
	{
		private string _text;
		private TextFormat _format;
		private int _width;

		public Stroke(string text, TextFormat format, int width)
		{
			_text = text;
			_format = format;
			_width = width;
		}
		
		public string Text { get { return _text; }}
		public TextFormat Format { get { return _format; }}
		public int Width { get { return _width; }}
		public override String ToString()
		{
			return _text;
		}
	}
}
