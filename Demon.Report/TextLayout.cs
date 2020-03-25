using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Demon.Core.Domain;
using Demon.Report.Types;
using Demon.Report.Style;
using Demon.Path;

namespace Demon.Report
{
	internal class TextLayout : Layout
	{
		public override LayoutType LayoutType { get {return LayoutType.Text;} }
		private string _text;
		private int _lineSpacing;
		private int _paragraphSpacing;

		//	The text block holds pure content: text and formatting. To render that
		//	content we convert it to lines and then apply positioning information.
		private TextBlock _textBlock;
		private List<LineDraft> _lines;
		public List<LineDraft> Lines { get { return _lines; }}

		private bool _drafted; // see the comment in IsEmpty for an explanation of this

		//	A term stored in the Layout._termDictionary dictionary is static
		//	text defined in the report design. If it's not blank then it overrides
		//	any text supplied by the layout's source object.
		//	Default text is static text defined in the report design. If
		//	the layout's source object is null, or returns blank text
		//	content, then the layout renders the default text.
		private string _defaultText;

		private TextStyle _style;
		public override IStyle Style { get { return _style; }}

		private readonly double DefaultSoftBreakLimit = 0.85;

		public override int PaddingTop { get { return _style?.Padding?.Top ?? 0; }}
		public override int PaddingBottom { get { return _style?.Padding?.Bottom ?? 0; }}
		public override Color BackgroundColor { get { return _style?.BackColor; }}


		public TextLayout(Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			_lines = new List<LineDraft>();
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public TextLayout(TextLayout src)
			:base(src)
		{
			_defaultText = src._defaultText;
			_style = src._style;
			_drafted = src._drafted;

			//	When we copy a text layout, we want just its metadata. This constructor
			//	is used in processing page breaks, and we'll move lines, strokes and paths
			//	from the original into the copy as appropriate after calling this constructor.
			_lines = new List<LineDraft>();
		}

		public TextLayout(string text, TextStyle style, Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
			//	The photo row takes care of things like page break handling
//			_sourcePath = new Path.Path("", sourceLineNumber, sourceLinePosition);

			_defaultText = text;
			_style = style ?? _generator.ReportDesign.DefaultTextStyle;
			LoadContent();
			_lines = new List<LineDraft>();

			//	Conditions are not relevant when the layout is constructed from
			//	explicit text, because the caller will already have done its own
			//	checks of whether the content should be included
			_staticConditionsSatisfied = true;
		}

		/// <summary>
		/// Internal constructor to let ListLayout create text layouts for its items.
		/// </summary>
		internal TextLayout(Reference source, TextStyle style, Generator generator, int lineNumber, int linePosition)
			:base(generator, lineNumber, linePosition)
		{
//			_sourcePath = new Path.Path(source, sourceLineNumber, sourceLinePosition);
			_sourceObject = source;
			_style = style ?? _generator.ReportDesign.DefaultTextStyle;
			_lines = new List<LineDraft>();

			//	Conditions are not relevant when the layout is created by a list layout,
			//	because the list will already have done its own checks of whether the
			//	item should be included
			_staticConditionsSatisfied = true;
		}

		public override void Load(XElement root)
		{
			base.Load(root);

			XNamespace ns = root.GetDefaultNamespace();
			_style = _generator.ReportDesign.LoadStyle<TextStyle>(root.Element(ns + "Style"));
			if (_style == null) _style = _generator.ReportDesign.DefaultTextStyle;

			_defaultText = _generator.ReportDesign.LoadString(root.Element(ns + "DefaultText"));
		}

		/// <summary>
		/// Load the text content, parse it into verses, apply formatting. Do not break
		/// into strokes and lines.
		/// </summary>
		public override void LoadContent()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start");

				//	A text layout must have a style, and the style must have a font but
				//	the other style elements can be null
				if(_style == null)
					throw new InvalidOperationException($"TextLayout {_trackingInfo.Name} at {_trackingInfo.LineNumber}:{_trackingInfo.LinePosition} style is null.");
				if(_style.Font == null)
					throw new InvalidOperationException($"TextLayout {_trackingInfo.Name} at {_trackingInfo.LineNumber}:{_trackingInfo.LinePosition} font is null.");

				_lines = new List<LineDraft>();
				_text = LoadContent(_sourceObject, _sourcePath.Property, _defaultText);
				TextFormat initialFormat = new TextFormat(_style.Font, _style.Color);
				_textBlock = new TextBlock(_text, initialFormat, _generator);
			}
		}

		private string LoadContent(Reference source, Path.Property property, string defaultContent)
		{
			//	Load content into the layout. First-class content is derived directly
			//	from the source control or the default content, and is not subject
			//	to content conditions. Second-class content is derived from embedded
			//	references, and is subject to content conditions. Basically, if the
			//	layout is just loading content directly from a checkbox, for example,
			//	then the layout conditions decide whether to include the layout at all,
			//	based on the check state of the checkbox. But if that decision has
			//	already been made, and now we're loading embedded content, the content
			//	conditions apply to any controls that supply that embedded content.

			string content = null;
            if (source.Type != ContentSourceType.None && property != null && property.IsExtension)
            {
                content = LoadExtensionContent(source, property);
            }
            else
            {
                switch (source.Type)
                {
                    case ContentSourceType.StaticText:
                        content = LoadStaticText(source, property);
                        break;
                    case ContentSourceType.TextEntry:
                        content = LoadTextEntry(source, property);
                        break;
                    case ContentSourceType.Checkbox:
                        content = LoadCheckbox(source, property);
                        break;
                    case ContentSourceType.RadioButton:
                        content = LoadRadioButton(source, property);
                        break;
                    case ContentSourceType.SingleSelect:
                        content = LoadSingleSelect(source, property);
                        break;
                    case ContentSourceType.MultiSelect:
                        content = LoadMultiSelect(source, property);
                        break;
                    case ContentSourceType.Section:
                        content = LoadSection(source, property);
                        //	Tip: to render a section's title in a text layout, set the section
                        //	as the layout's source and wrap the layout in a section layout
                        //	with the same source:
                        //	<SectionLayout sourceType="Section" sourceId="the_section_template_id">
                        //		<Layouts>
                        //			<TextLayout sourceType="Section" sourceId="the_section_template_id"/>
                        //		</Layouts>
                        //	</SectionLayout>
                        break;
                    case ContentSourceType.Calculation:
                        content = LoadCalculation(source, property);
                        break;
                    case ContentSourceType.CalculationVariable:
                        content = LoadCalculationVariable(source, property);
                        break;
                    case ContentSourceType.CalculationList:
                        content = LoadCalculationList(source, property);
                        break;
                    case ContentSourceType.None:
                    default:
                        content = defaultContent;
                        break;
                }
            }

			//	If our source reference yielded no content then use our default
			//	text. The default text may also contain embedded references and
			//	properties.
			if(string.IsNullOrWhiteSpace(content))
				content = defaultContent;

			//	Expand any document property references in the text. Note that if the
			//	text includes a reference to any doc property that depends on the full
			//	document having been laid out, such as PageCount, then you must not
			//	lay out this text layout until after the document has been fully laid
			//	out. This effectively means that we can't include PageCount within
			//	the body of the text, only in headers and footers. (Headers and footers
			//	are laid out after the document has been fully drafted, including
			//	page break handling.)
			//TODO: find a better way to handle PageNumber and PageCount so that we
			//can include them in the page body, and so that we can lay out the
			//header and footer at the same time as the body.
			content = ExpandProperties(content);
			
			//	Expand any object references
			if(content != null)
			{
//				content = _generator.ResolveReferences(content);
				//	References are encoded like this: {type:id} where the curly braces
				//	mark out the reference, type is the object type such as TextEntry
				//	or Checkbox etc., and id is the object id.
				StringBuilder sb = new StringBuilder();
//				int normalTextStart = 0;
				for(int pos = 0; pos < content.Length; ++pos)
				{
					//	An embedded reference is introduced by the start-of-reference
					//	marker and terminated by the end-of-reference marker
					if(content[pos] == Marker.EmbeddedReference.Start)
					{
//						//	Capture the normal text up to this point
//						int normalTextLen = pos - normalTextStart;
//						sb.Append(content,normalTextStart,normalTextLen);

						Path.Path path = LoadEmbeddedReference(content, ref pos);
						Reference embeddedRef = _generator.ResolveOne(path, ReferenceContext);
						if(embeddedRef != null)
						{
							string embeddedContent = LoadContent(embeddedRef, path.Property, null);
							if(embeddedContent != null)
								sb.Append(embeddedContent);
						}
					}
					else
					{
						sb.Append(content[pos]);
					}
				}
				content = sb.ToString();
			}

			return content;
		}

		/// <summary>
		/// Returns a valid path or throws an exception.
		/// </summary>
		private Path.Path LoadEmbeddedReference(string text, ref int pos)
		{
			//	We expect to be given the position of the start marker
			if(text[pos] != Marker.EmbeddedReference.Start)
			{
				string msg = "No start-of-reference marker found.";
//				Trace(msg); //TODO: trace the position etc.
				throw new PathException(msg, text, ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			}
			int start = pos;

			//	Once we've got a start marker, we expect to find an end marker. If
			//	we don't find one then that's a fatal error. So it's safe to advance
			//	the ref pos marker, because if we don't find the end marker then the
			//	caller won't care about the pos any more.
			while(text[pos] != Marker.EmbeddedReference.End)
			{
				++pos;
				if(pos >= text.Length)
				{
					string msg = "No end-of-reference marker found.";
//					Trace(msg); //TODO: trace the position etc.
					throw new PathException(msg, text, ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				}
			}
			int end = pos;

			text = text.Substring(start + 1, end - start - 1);
			Path.Path source = new Path.Path(text, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			return source;
		}

		private string LoadStaticText(Reference source, Path.Property property)
		{
			StaticText statictext = _generator.ResolveOne<StaticText>(source);
			if(statictext == null) return null;

			string text = GetTermDefinition(ContentSourceType.StaticText,statictext.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Text":
					case null:
					default:
						text = statictext.Text;
						break;
				}
			}
			return text;
		}

		private string LoadTextEntry(Reference source, Path.Property property)
		{
			//	The data object is not guaranteed to be found in the document, for
			//	at least two reasons. First, the inspector could have deleted the
			//	section. Second, the inspection template and the report layout
			//	could have been updated to include a new object after the document
			//	was created.
			TextEntry textentry = _generator.ResolveOne<TextEntry>(source);
			if(textentry == null) return null;

			string text = GetTermDefinition(ContentSourceType.TextEntry,textentry.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Text":
					case null:
					default:
						text = textentry.Text;
						break;
					case "Caption":
						text = textentry.Caption;
						break;
				}
			}
			return text;
		}

		private string LoadCheckbox(Reference source, Path.Property property)
		{
			Checkbox checkbox = _generator.ResolveOne<Checkbox>(source);
			if(checkbox == null) return null;

			bool satisfies = _conditions.SatisfiesContentConditions<Checkbox>(checkbox);
			if(!satisfies) return null;

			//	First look for override text defined by the report layout. If there
			//	is no override then get the checkbox caption. The override text allows
			//	the designer to map the checkbox to more verbose or customer-friendly
			//	text in the report.
			string text = GetTermDefinition(ContentSourceType.Checkbox, checkbox.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Caption":
					case null:
					default:
						text = checkbox.Caption;
						break;
				}
			}
			return text;
		}

		private string LoadRadioButton(Reference source, Path.Property property)
		{
			RadioButton button = _generator.ResolveOne<RadioButton>(source);
			if(button == null) return null;

			bool satisfies = _conditions.SatisfiesContentConditions<RadioButton>(button);
			if(!satisfies) return null;

			//	First look for override text defined by the report layout. If there
			//	is no override then get the radio button caption. The override text allows
			//	the designer to map the radio button to more verbose or customer-friendly
			//	text in the report.
			string text = GetTermDefinition(ContentSourceType.RadioButton,button.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Caption":
					case null:
					default:
						text = button.Caption;
						break;
				}
			}
			return text;
		}

		private string LoadSingleSelect(Reference source, Path.Property property)
		{
			SingleSelectList list = _generator.ResolveOne<SingleSelectList>(source);
			if(list == null) return null;

			List<RadioButton> buttons =
				_generator.UnitOfWork.Repository<RadioButton>()
					.Query(b => b.ListId == list.Id)
					.Get(false)
					.ToList();

			_conditions.ApplyContentConditions<RadioButton>(buttons);
			buttons.Sort(Compare);

			StringBuilder sb = new StringBuilder();
			bool first = true;
			foreach(RadioButton button in buttons)
			{
				string text = GetTermDefinition(ContentSourceType.RadioButton,button.TemplateId);
				if(string.IsNullOrWhiteSpace(text))
				{
					//TODO: maybe use reflection to support arbitrary property names?
					//But I'm hesitant to pay the performance penalty that
					//reflection imposes.
					switch (property?.Name)
					{
						case "Caption":
						case null:
						default:
							text = button.Caption;
							break;
					}
				}
			
				if(first)
					first = false;
				else
					sb.Append(_style.ListSeparator);

				sb.Append(text);
			}
			if(buttons.Count > 0)
				sb.Append(_style.ListTerminator);

			return sb.ToString();
		}

		private string LoadMultiSelect(Reference source, Path.Property property)
		{
			MultiSelectList list = _generator.ResolveOne<MultiSelectList>(source);
			if(list == null) return null;

			//	Get all checkboxes in the list
			List<Checkbox> checkboxes =
				_generator.UnitOfWork.Repository<Checkbox>()
					.Query(c => c.ListId == list.Id)
					.Get(false)
					.ToList();

			_conditions.ApplyContentConditions<Checkbox>(checkboxes);
			checkboxes.Sort(Compare);
			
			StringBuilder sb = new StringBuilder();
			bool first = true;
			foreach(Checkbox box in checkboxes)
			{
				string text = GetTermDefinition(ContentSourceType.Checkbox,box.TemplateId);
				if(string.IsNullOrWhiteSpace(text))
				{
					//TODO: maybe use reflection to support arbitrary property names?
					//But I'm hesitant to pay the performance penalty that
					//reflection imposes.
					switch (property?.Name)
					{
						case "Caption":
						case null:
						default:
							text = box.Caption;
							break;
					}
				}

				if(first)
					first = false;
				else
					sb.Append(_style.ListSeparator);

				sb.Append(text);
			}
			if(checkboxes.Count > 0)
				sb.Append(_style.ListTerminator);

			return sb.ToString();
		}

		private string LoadCalculation(Reference source, Path.Property property)
		{
			Calculation calculation = _generator.ResolveOne<Calculation>(source);
			if(calculation == null) return null;

			bool satisfies = _conditions.SatisfiesContentConditions<Calculation>(calculation);
			if(!satisfies) return null;

			//	First look for override text defined by the report layout. If there
			//	is no override then get the calculation. The override text allows
			//	the designer to map the calculation to more verbose or customer-friendly
			//	text in the report.
			string text = GetTermDefinition(calculation.Type, calculation.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Value":
					case null:
					default:
						decimal value = _generator.Calculate(calculation);
						text = value.ToString(property?.Format);
						break;
					case "Caption":
						text = calculation.Caption;
						break;
					case "UnitOfMeasure":
						text = calculation.UnitOfMeasure;
						break;
				}
			}
			return text;
		}

		private string LoadCalculationVariable(Reference source, Path.Property property)
		{
			CalculationVariable variable = _generator.ResolveOne<CalculationVariable>(source);
			if(variable == null) return null;

			//	Apply the content conditions. For this we'll need to have
			//	the calculation object.
			if(variable.Calculation == null)
				variable.Calculation = _generator.UnitOfWork.Repository<Calculation>().GetById(variable.CalculationId);
			bool satisfies = _conditions.SatisfiesContentConditions<Calculation>(variable.Calculation);
			if(!satisfies) return null;

			//	First look for override text defined by the report layout. If there
			//	is no override then get the variable. The override text allows
			//	the designer to map the variable to more verbose or customer-friendly
			//	text in the report.
			string text = GetTermDefinition(variable.Type, variable.TemplateId);
			//TODO: maybe use reflection to support arbitrary property names?
			//But I'm hesitant to pay the performance penalty that
			//reflection imposes.
			if(string.IsNullOrWhiteSpace(text))
			{
				switch (property?.Name)
				{
					case "Value":
					case null:
					default:
						text = variable.Value?.ToString(property?.Format);
						break;
					case "Caption":
						text = variable.Caption;
						break;
					case "UnitOfMeasure":
						text = variable.UnitOfMeasure;
						break;
				}
			}
			return text;
		}

		private string LoadCalculationList(Reference source, Path.Property property)
		{
			CalculationList list = _generator.ResolveOne<CalculationList>(source);
			if(list == null) return null;

			//	First look for override text defined by the report layout. If there
			//	is no override then get the variable. The override text allows
			//	the designer to map the variable to more verbose or customer-friendly
			//	text in the report.
//TODO: is this what we want? If there's a term definition for the list then
//show just that, with no details?
			string text = GetTermDefinition(list.Type, list.TemplateId);
			if(string.IsNullOrWhiteSpace(text))
			{
				//TODO: maybe use reflection to support arbitrary property names?
				//But I'm hesitant to pay the performance penalty that
				//reflection imposes.
				switch (property?.Name)
				{
					case "Value":
					case null:
					default:
					{
						//	The value of a calculation list is the sum of the values
						//	of all its calculations. But only include those calculations
						//	that satisfy our content conditions.


						//	Get all calculations in the list, based on our if-selected rules.
						//	It's probably far more efficient to poke directly into the
						//	repository here in a query than to get all the calculations from
						//	the document structure and then resolve each one separately.
						List<Calculation> calculations =
							_generator.UnitOfWork.Repository<Calculation>()
								.Query(b => b.ListId == list.Id)
								.Get(false)
								.ToList();
						_conditions.ApplyContentConditions<Calculation>(calculations);
						decimal value = 0.0m;
						foreach(Calculation calculation in calculations)
							value += _generator.Calculate(calculation);
						text = value.ToString(property?.Format);
						break;
					}
					case "Caption":
						text = list.Caption;
						break;
					case "UnitOfMeasure":
						text = list.UnitOfMeasure;
						break;
				}
			}
			return text;
		}

		private string LoadSection(Reference source, Path.Property property)
		{
			Section section = _generator.ResolveOne<Section>(source);
			if(section == null) return null;
			return section.Title;
		}

        private string LoadExtensionContent(Reference source, Path.Property property)
        {
            string content = null;
            switch (property.ExtensionType)
            {
                case Core.Enums.ExtensionType.Asset:
                    content = LoadAsset(source, property);
                    break;
                case Core.Enums.ExtensionType.None:
                default:
                    content = null;
                    break;
            }
            return content;
        }

        private string LoadAsset(Reference source, Path.Property property)
        {
            Asset asset = _generator.ResolveAssetExtension(source);
            if (asset == null) return null;

            return asset.GetPropertyValue<string>(property.Name);
        }


		private string ExpandProperties(string text)
		{
			if(text == null) return null;

			//	Scan for start-of-property markers. At each start marker, append
			//	the text up to that point to the output, then parse and
			//	expand the property and append its value to the output.
			StringBuilder sb = new StringBuilder();
			string plain = null;
			int plainStart = 0;
			int pos = 0;
			int len = text.Length;
			while(pos < len)
			{
				char c = text[pos];
				if(c == Marker.DocumentProperty.Start)
				{
					//	Copy any preceding non-property text to the output, but
					//	don't include the start marker
					plain = text.Substring(plainStart, pos - plainStart);
					sb.Append(plain);

					//	Parse the property. The parse routine expects that pos will
					//	be pointing at the start marker.
					DocumentProperty prop = ParseDocumentProperty(text, ref pos);
					string value = _generator.EvaluateDocumentProperty(this.Page, prop);
					sb.Append(value);

					plainStart = pos + 1; // skip the end marker
				}
				++pos;
			}
			//	Copy any residual non-property text to the output. But in most cases
			//	there won't have been any properties to expand, and so we can just
			//	return the original text.
			if(plainStart == 0)
			{
				return text;
			}
			else
			{
				plain = text.Substring(plainStart);
				sb.Append(plain);
				return sb.ToString();
			}
		}

#if false //TODO: do we want this?
		protected override List<Layout> ExpandDocTags()
		{
			if(_sourcePath.TargetType != ContentSourceType.DocTag)
				throw new InvalidOperationException($"Expected doctag source but found {_sourcePath.TargetType}");

			List<Layout> expanded = new List<Layout>();

			//	Resolve the doctag template reference into a concrete reference
			//	(no need to go to the database to get the actual doctag object)
			Reference resolved = _generator.ResolveOne(_sourcePath);
			if(resolved == null) return expanded;

			//	Find all instances of the tag that are in the current document,
			//	regardless of context. (Context handling is different for doctags
			//	because DocTagList is not a content object.)
			IEnumerable<DocTagList> unsorted = _generator.UnitOfWork.Repository<DocTagList>()
				.Query(t => t.DocumentId == _generator.DocumentId
										&&
										t.DocTagId == resolved.Id)
				.Get(false);

			//	Keep only tag instances attached to objects that are in the current context
			List<DocTagList> tags = new List<DocTagList>();
			foreach(DocTagList tag in unsorted)
			{
				//	Fetch the object, provided that it's in the current context
				Path.Path sourceRef = new Path.Path(tag.SourceType, tag.SourceId, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				Reference source = _generator.ResolveOne(sourceRef);
				if(source != null)
					tags.Add(tag);
			}
			tags.Sort(Compare);

			//	A text layout can only host text-based objects. If the tagged object
			//	is a static text, a text entry, an individual checkbox or an individual
			//	radio button, then we create a new text layout to render it. If the
			//	tagged object is a list then we create one new text layout for each
			//	selected item in the list, thus rendering the list as a series of
			//	paragraphs, not as a bullet point list. We do this mainly because
			//	the current text layout has no way to supply list-specific styling
			//	to the tagged object. For the same reason we don't support rendering
			//	tagged photos at all in a text layout - the text layout has no way
			//	to supply photo styling. Of course the designer can supply a list
			//	layout to display tagged lists, or a photo layout to display tagged
			//	photos.
			foreach(DocTagList tag in tags)
			{
				string content = null;
				Reference sourceRef = new Reference(tag.SourceType, tag.SourceId, true);
				switch(tag.SourceType)
				{
					case ContentSourceType.Checkbox:
						content = LoadCheckbox(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.RadioButton:
						content = LoadRadioButton(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.TextEntry:
						content = LoadTextEntry(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.Calculation:
						content = LoadCalculation(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.CalculationList:
						content = LoadCalculationList(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.CalculationVariable:
						content = LoadCalculationVariable(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.StaticText: //TODO: static text probably makes no sense
						content = LoadStaticText(sourceRef, _sourcePath.Property);
						break;
					case ContentSourceType.Section:
						content = LoadSection(sourceRef, _sourcePath.Property);
						break;
				}
//					_generator.ReferenceResolver.ResolveReference(sourceRef, ReferenceContext);
					TextLayout layout = new TextLayout(content, _style, _generator, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
					layout._conditions = this._conditions;
					expanded.Add(layout);
			}
			return expanded;
		}
#endif

		private Path.DocumentProperty ParseDocumentProperty(string text, ref int pos)
		{
			//	We expect to be given the position of the start marker
			if(text[pos] != Marker.DocumentProperty.Start)
			{
				string msg = $"No start-of-property marker found.";
//				Trace(msg); //TODO: trace the position etc.
				throw new PathException(msg,
					text, ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			}
			int start = pos;

			//	Once we've got a start marker, we expect to find an end marker. If
			//	we don't find one then that's a fatal error. So it's safe to advance
			//	the ref pos marker, because if we don't find the end marker then the
			//	caller won't care about the pos any more.
			while(text[pos] != Marker.DocumentProperty.End)
			{
				++pos;
				if(pos >= text.Length)
				{
					string msg = $"No end-of-property marker found.";
//					Trace(msg); //TODO: trace the position etc.
					throw new PathException(msg,
						text, ReferenceContext.ToString(), _trackingInfo.LineNumber, _trackingInfo.LinePosition);
				}
			}
			int end = pos;

			text = text.Substring(start + 1, end - start - 1);
			Path.DocumentProperty prop = new Path.DocumentProperty(text, _trackingInfo.LineNumber, _trackingInfo.LinePosition);
			return prop;
		}

		/// <summary>
		/// Break the text block's verses into lines and strokes as necessary.
		/// For each stroke, calculate and store its bounding box relative to
		/// the layout origin, and its padding.
		/// </summary>
		public override Position Draft(Rectangle bounds)
		{
			if(!_staticConditionsSatisfied)
			{
				_drafted = true;
				return bounds.BottomLeft;
			}

			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start bounds={0}", bounds);

				//	If we have no content then don't draw ourself at all
				if(IsEmpty())
				{
					_drafted = true;
					return bounds.BottomLeft;
				}

				//	Calculate our line spacing and paragraph spacing. These are
				//	based on our default font, regardless of any fonts specified
				//	within the content, and our style's line spacing factor.
				Font.Font defaultFont = _generator.GetFont(_style.Font);
				int defaultLineHeight = defaultFont.GetDefaultLineSpacing(_style.Font.Size);
				double lineSpacingFactor = _style.LineSpacing > 0.0f ? _style.LineSpacing : 1.0f;
				lineSpacingFactor -= 1.0; // we want the space to add, not the total line height
				_lineSpacing = (int)(defaultLineHeight * lineSpacingFactor);
				double paragraphSpacingFactor = _style.ParagraphSpacing > 0.0f ? _style.ParagraphSpacing : 1.0f;
				paragraphSpacingFactor -= 1.0; // we want the space to add, not the total line height
				_paragraphSpacing = (int)(defaultLineHeight * paragraphSpacingFactor);

				//	Remember the original bounds, before we apply our own padding
				_bounds = new Rectangle(bounds);

				//	Apply our own padding within the given bounds
				bounds.Left  += _style.Padding?.Left  ?? 0;
				bounds.Right -= _style.Padding?.Right ?? 0;
				bounds.Top   -= _style.Padding?.Top   ?? 0;

				//	Create our own drawing position
				Position pos = new Position(bounds.Left,bounds.Top);

				//	Set the soft break limit. See TextBlock and the report design
				//	specification for a detailed explanation.
				//	The limit is given to us as a percentage of the available width;
				//	convert it to an absolute width. First, if the given limit is
				//	outside a sensible range then correct it: a negative limit
				//	is meaningless; a limit of zero indicates that the designer
				//	set no limit; a value greater one is meaningless as a percentage;
				//	a value of exactly one is impossible because it would cause us
				//	always to hard break into an infinite number of zero-length lines.
				double softBreakLimit = _style?.SoftBreakLimit ?? DefaultSoftBreakLimit;
				if((softBreakLimit <= 0.0) || (softBreakLimit >= 1.0))
					softBreakLimit = DefaultSoftBreakLimit;
				int softBreak = (int)(bounds.Width * softBreakLimit);

				//	The text block holds pure content: text and formatting information,
				//	defined in verses, all on a single infinitely long line. Break those
				//	verses into lines based on the available width, and then apply
				//	positioning information to them.
				List<TextLine> lines = _textBlock.BreakIntoLines(bounds.Width, 0, softBreak);

				//	Lay out the lines
				for(int lineNum = 0; lineNum < lines.Count; ++lineNum)
				{
					TextLine line = lines[lineNum];

					//	If the line is a special "new paragraph" line, and we're not at
					//	the top of our bounds, then add our paragraph spacing.
					//TODO: should that be "top of the page" instead of "top of our bounds"?
					if(line is ParagraphLine)
					{
						if(pos.Y < bounds.Top)
							pos.Y -= _paragraphSpacing;
						continue;
					}

					Rectangle lineBounds = new Rectangle(bounds.Left, pos.Y, bounds.Right, pos.Y);
					LineDraft lineDraft = new LineDraft(line, lineBounds);

					//	Align the text. Right-alignment is good enough for things like
					//	header and footer text, but not quite good enough for body text.
					switch(_style.Alignment)
					{
						case TextAlignment.Left:
							break; // nothing to do
						case TextAlignment.Right:
							pos.X = bounds.Right;
							foreach(Stroke stroke in line.Strokes)
								pos.X -= stroke.Width;
							break;
						case TextAlignment.Center:
							 pos.X = bounds.Left + ((bounds.Width - line.Width) / 2);
							break;
					}

					foreach(Stroke stroke in line.Strokes)
					{
						TextFormat format = stroke.Format;

						//	The text position that we pass to PDF is the position of the text
						//	baseline, but our position is the bottom left corner of the
						//	bounding rectangle
						int y = pos.Y - line.BaseLine;
						Position strokePos = new Position(pos.X, y);
						lineDraft.Add(new StrokeDraft(stroke, strokePos));

						if(format.Font.Underline)
						{
							Font.Font strokeFont = _generator.GetFont(format.Font);
							int lineY = y + strokeFont.GetUnderlinePosition(format.Font.Size);
							float thickness = strokeFont.GetUnderlineThickness(format.Font.Size);

							List<Position> points = new List<Position>();
							points.Add(new Position(pos.X, lineY));
							points.Add(new Position(pos.X + stroke.Width, lineY));
							
							lineDraft.Add(new UnderlineDraft(points, thickness, format.Color));
						}
						pos.X += stroke.Width;
					}

					//	Advance by one line, based on the actual line height that allows
					//	for varying font sizes within the line
					pos.Y -= line.Height;

					//	Add our line spacing, based on our default font. But don't do
					//	this on the last line.
					if(lineNum < lines.Count - 1)
						pos.Y -= _lineSpacing;

					lineBounds.Bottom = pos.Y;
					lineDraft.Bounds = lineBounds;
					_lines.Add(lineDraft);

					pos.X = bounds.Left;
				}

				//	Advance by the padding and record our bounds bottom
				pos.Y -= _style.Padding?.Bottom ?? 0;
				_bounds.Bottom = pos.Y;

				HandleEmpty();
				Trace("end _bounds={0}", _bounds);
				_drafted = true;
				return _bounds.BottomLeft;
			}
		}

		public override void Redraft(int top)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("start _bounds={0}", _bounds);

				//	If we have no content then there's nothing to do
				if(IsEmpty())
				{
					_bounds.Top = top;
					_bounds.Bottom = _bounds.Top;
					Trace("end _bounds={0}", _bounds);
					return;
				}

				//TODO: handle sublayouts

				//	Reposition all of our lines to fit with our new position,
				//	maintaining the inter-line spacing.
				//	Insert our top padding before the first line, and our
				//	bottom padding after the last.
				int pos = top - PaddingTop;
				for(int x = 0; x < _lines.Count - 1; ++x)
				{
					//	Get the current spacing between this line and the next
					LineDraft line = _lines[x];
					LineDraft next = _lines[x+1];
					int space = line.Bounds.Top - next.Bounds.Top; // top-to-top spacing

					line.Redraft(pos);
					pos -= space;
				}
				LineDraft last = _lines.Last();
				last.Redraft(pos);
				pos = last.Bounds.Bottom;

				//	Reposition ourself
				_bounds.Top = top;
				_bounds.Bottom = pos - PaddingBottom;
				Trace("end _bounds={0}", _bounds);
			}
		}

		protected override PageDisposition SetPageSplitIndex(Rectangle bodyBox, ref bool honourKeepWithNext)
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				PageDisposition disposition = PageDisposition.ThisPage;

				//	See how many of our lines we can fit
				for(_pageSplitIndex = 0; _pageSplitIndex < _lines.Count; ++_pageSplitIndex)
				{
					LineDraft line = _lines[_pageSplitIndex];
					int splitBottom = line.Bounds.Bottom - (_style?.Padding?.Bottom ?? 0);
					if(splitBottom < bodyBox.Bottom)
						break; // leaving _pageSplitIndex pointing at this line, which doesn't fit
					//TODO: don't include the padding after the last layout on the page?
				}
				Trace("split index {0}", _pageSplitIndex);
				if(_pageSplitIndex >= _lines.Count) // including when there are no lines
					disposition = PageDisposition.ThisPage;
				else if(_pageSplitIndex == 0)
					disposition = PageDisposition.Overflow;
				else
					disposition = PageDisposition.Split;

				//	Now that we know how many lines we can fit, evaluate our page break rules
				if(disposition == PageDisposition.Split)
				{
					bool canSplit = CanSplit(bodyBox);
					if(!canSplit)
					{
						Trace("cannot split, so set overflow disposition");
						_pageSplitIndex = 0;
						disposition = PageDisposition.Overflow;
					}
				}

				return disposition;
			}
		}

		/// <summary>
		/// Bump the page split index from zero to one to avoid infinite
		/// overflow when the first line is too large to fit on the
		/// page.
		/// </summary>
		public override Bump BumpPageSplitIndex()
		{
			if(_lines.Count == 0) return Bump.Impossible;
			if(_pageSplitIndex > 0) return Bump.Unnecessary;
			if(_lines.Count == 1) return Bump.Impossible;

			++_pageSplitIndex;
			return Bump.Bumped;
		}

		public override Layout DoPageBreak()
		{
			using (new TraceContextPusher(_generator, _traceContext))
			{
				Trace("split index {0}", _pageSplitIndex);

				//	If our split index is greater than our number of lines then
				//	we don't need to split at all
				if(_pageSplitIndex >= _lines.Count)
					return null;
				//	Same if we have no lines
				if(_lines.Count == 0)
					return null;

				//	Otherwise we do want to split. Make another layout with the same
				//	metadata as ourself, and move all our lines after the break
				//	position into the copy.
				TextLayout lower = (TextLayout)ShallowCopy();
				while(_lines.Count > _pageSplitIndex) // _pageSplitIndex points at the first line that doesn't fit
				{
					LineDraft line = _lines[_pageSplitIndex];
					_lines.Remove(line);
					lower._lines.Add(line);
				}
				return lower;
			}
		}

		public override void MapFontCharacters()
		{
			//	Our text block will be empty if we have no text content
			_textBlock?.MapFontCharacters();
		}

		/// <summary>
		/// Get the text, formatted and laid out in a single line. Suitable
		/// for Word.
		/// </summary>
		/// <returns></returns>
		public List<Verse> GetFormattedText()
		{
			return _textBlock?.Verses;
		}

		public override bool IsEmpty()
		{
			//	Before drafting, a text layout has raw text but no lines; after drafting
			//	it has the same raw text and the corresponding lines. And if it gets
			//	split during page break handling then both the original and the overflow
			//	will have the same raw text, but the lines will have been distributed
			//	between them. This all means that before drafting, a text layout is
			//	empty if it has no raw text (and its lines will always be empty at that
			//	stage); after drafting it's empty if it has no lines (and it may still
			//	have raw text.)

			if(!_drafted)
			{
				//	If we have non-empty text then we're not empty
				if(!string.IsNullOrWhiteSpace(_text)) return false;
			}
			else
			{
				foreach(LineDraft line in _lines)
					if(line.Strokes.Count > 0)
						return false;
			}

			//	If we have non-empty sublayouts then we're not empty
			if(!base.IsEmpty())
				return false;

			return true;
		}

		protected override void Clear()
		{
			base.Clear();
			_text = null;
			_lines.Clear();
		}

		public override bool CollapseTopSpace()
		{
			return true; // stop collapsing
		}

		public override bool CanSplit(Rectangle bodyBox)
		{
			//	A text layout can't split mid-line, so if our split index is zero
			//	then we can't split
			if(_pageSplitIndex == 0)
			{
				Trace("cannot split mid-line");
				return false;
			}

			//	If we have no rules then anything goes
			if(_pageBreakRules == null)
				return true;

#if false // new-page rule not applicable when splitting
			//--------------------------------------------------------
			//	1.	New-page rule. If we're at the top of the page then we're OK.
			if(_pageBreakRules.NewPage != 0)
			{
				if(_bounds.Top < pageLayout.BodyBox.Top)
					return false;
			}
#endif

			//--------------------------------------------------------
			//	2.	Keep-with-next rule. Not implemented on text layouts.
			//	TextLayout doesn't have to worry about keep-with-next because
			//	keep-with-next is implemented by the container layout, and the
			//	text layout doesn't have sublayouts.


#if false // max-position rule not applicable when splitting
			//--------------------------------------------------------
			//	3.	Max-position rule

			//	Our current position is an offset from the bottom of the page, not
			//	from the bottom of the body box, so subtract the body box bottom
			//	to recalibrate the calculations to start at zero.
			int inverseY = _bounds.Top - bodyBox.Bottom;

			//	Our position is measured from the bottom, but the rule is expressed
			//	in terms where zero is at the top, so invert the position.
			inverseY = bodyBox.Height - inverseY;

			//	Convert the position to a percentage
			float percent = (float)inverseY / (float)bodyBox.Height;

			//	Now check the rule
			if(percent > _pageBreakRules.MaximumPosition)
				return false;
#endif

			//--------------------------------------------------------
			//	4.	Min-lines rule

			int minLines = _lines.Count > _pageBreakRules.MinimumLines ? _pageBreakRules.MinimumLines : _lines.Count;
			if(_pageSplitIndex < minLines)
			{
				Trace("split index {0} breaks min-lines {1} rule", _pageSplitIndex, minLines);
				return false;
			}


			//--------------------------------------------------------
			//	No rule was unsatisfied, so we're OK
			return true;
		}

		internal override Layout DeepCopy()
		{
			//	The base class implementation copies sublayouts, but in a
			//	text layout the contents are in _lines
			TextLayout copy = (TextLayout)base.DeepCopy();

			foreach(LineDraft line in _lines)
				copy._lines.Add(new LineDraft(line));

			return copy;
		}

		public override void Dump(int indent)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append($"{_bounds}\t");
			
			for(int x = 0; x < indent; ++x)
				sb.Append("\t");
			sb.Append($"{GetType().Name} '{_trackingInfo.Name}'\r\n");

			if(_lines != null)
			{
				foreach(LineDraft line in _lines)
				{
					sb.Append($"{line.Bounds}\t");
					for(int x = 0; x < indent; ++x)
						sb.Append("\t");
					sb.Append($"Line : {line.RoughText}\r\n");
				}
			}
			System.Diagnostics.Debugger.Log(0,null,sb.ToString());

			foreach(Layout child in _subLayouts)
				child.Dump(indent+1);
		}
	}
}
