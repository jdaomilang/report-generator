using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using Demon.Core.Interface.Services;
using Demon.Core.Interface.Data;
using Demon.Report.Style;
using Demon.Report.Types;

//	A style reference can override any part of the referenced style.
//	For example, this font reference switches typeface and turns the
//	red component of the text colour all the way up:
//
//		<Font ref="Body Font">
//			<FamilyName>Wingdings</FamilyName>
//			<Color>
//				<Red>1.0</Red>
//			</Color>
//		</Font>
//
//	In this example, all other components of the font, such as size, bold
//	etc. are unchanged. Also, the colour is not entirely replaced with a
//	red colour - the blue and green components are unchanged.
//
//	Overriding only applies to style references. It's meaningless to
//	override a style at its point of definition, such as for shared
//	and embedded style definitions. In a style reference, an override
//	is indicated by the presence of child elements. For example, this
//	font reference is not overridden, because it contains no child
//	element:
//
//		<Font ref="Body Font"/>
//
//	You can override elements of a style, but there's no facility to inherit
//	a style fully. That is, you can't do this:
//
//		<TextStyle id="Normal Text">
//			...
//		</TextStyle>
//		<TextStyle id="Highlight Text" ref="NormalText">
//			<Font><Bold>true</Bold></Font>
//		</TextStyle>
//
//	We can do element overriding because we load style types in an order that
//	recognizes dependencies, such as Font and Color before TextStyle, but
//	to support inheritance we'd have to load style instances in a corresponding
//	order. That's just something we haven't tried yet.

namespace Demon.Report
{
	internal class ReportDesign
	{
		private Stream _file;
		private string _id;
		private string _name;
		private string _inspectionTemplateId;
		private int _fileFormatVersion;
		private XNamespace _ns;
		private ILog _logger;

		private static XmlSchema _designSchemaXML;
#if JSON_SCHEMA
		private static JSchema _designSchemaJSON;
#else
		private static string _designSchemaJSON;
#endif // JSON_SCHEMA
		private static object SchemaLocker = new object();

		public string Id { get { return _id; }}
		public string Name { get { return _name; }}
		public string InspectionTemplateId { get { return _inspectionTemplateId; }}
		public int FileFormatVersion { get { return _fileFormatVersion; }}

		//	Shareable styles. These are stored last in the XML file but should be loaded
		//	first so that layouts can refer to them.
		private Dictionary<string, Style.Font>     _fonts        = new Dictionary<string, Style.Font>();
		private Dictionary<string, Color>          _colors       = new Dictionary<string, Color>();
		private Dictionary<string, Border>         _borders      = new Dictionary<string, Border>();
		private Dictionary<string, Padding>        _paddings     = new Dictionary<string, Padding>();
		private Dictionary<string, TextStyle>      _textStyles   = new Dictionary<string, TextStyle>();
		private Dictionary<string, ListStyle>      _listStyles   = new Dictionary<string, ListStyle>();
		private Dictionary<string, PhotoStyle>     _photoStyles  = new Dictionary<string, PhotoStyle>();
		private Dictionary<string, TableStyle>     _tableStyles  = new Dictionary<string, TableStyle>();
		private Dictionary<string, TableRowStyle>  _rowStyles    = new Dictionary<string, TableRowStyle>();
		private Dictionary<string, TableCellStyle> _cellStyles   = new Dictionary<string, TableCellStyle>();
		private Dictionary<string, BulletStyle>    _bulletStyles = new Dictionary<string, BulletStyle>();
		private Dictionary<string, LineStyle>      _lineStyles   = new Dictionary<string, LineStyle>();

		//	Resource files that can be reused
		private Dictionary<string, Resource> _resources = new Dictionary<string, Resource>();


		private Style.Font     _defaultFont;
		private Color          _defaultColor;
		private Border         _defaultBorder;
		private Padding        _defaultPadding;
		private TextStyle      _defaultTextStyle;
		private ListStyle      _defaultListStyle;
		private PhotoStyle     _defaultPhotoStyle;
		private TableStyle     _defaultTableStyle;
		private TableRowStyle  _defaultTableRowStyle;
		private TableCellStyle _defaultTableCellStyle;
		private BulletStyle    _defaultBulletStyle;
		private LineStyle      _defaultLineStyle;

		public Dictionary<string, Style.Font>     Fonts        { get { return _fonts; } }
		public Dictionary<string, Color>          Colors       { get { return _colors; } }
		public Dictionary<string, Border>         Borders      { get { return _borders; } }
		public Dictionary<string, Padding>        Paddings     { get { return _paddings; } }
		public Dictionary<string, TextStyle>      TextStyles   { get { return _textStyles; } }
		public Dictionary<string, ListStyle>      ListStyles   { get { return _listStyles; } }
		public Dictionary<string, PhotoStyle>     PhotoStyles  { get { return _photoStyles; } }
		public Dictionary<string, TableStyle>     TableStyles  { get { return _tableStyles; } }
		public Dictionary<string, TableRowStyle>  RowStyles    { get { return _rowStyles; } }
		public Dictionary<string, TableCellStyle> CellStyles   { get { return _cellStyles; } }
		public Dictionary<string, BulletStyle>    BulletStyles { get { return _bulletStyles; } }
		public Dictionary<string, LineStyle>      LineStyles   { get { return _lineStyles; } }

		public Style.Font     DefaultFont           { get { return _defaultFont;  } }
		public Color          DefaultColor          { get { return _defaultColor; } }
		public Border         DefaultBorder         { get { return _defaultBorder; } }
		public Padding        DefaultPadding        { get { return _defaultPadding; } }
		public TextStyle      DefaultTextStyle      { get { return _defaultTextStyle; } }
		public ListStyle      DefaultListStyle      { get { return _defaultListStyle; } }
		public PhotoStyle     DefaultPhotoStyle     { get { return _defaultPhotoStyle; } }
		public TableStyle     DefaultTableStyle     { get { return _defaultTableStyle; } }
		public TableRowStyle  DefaultTableRowStyle  { get { return _defaultTableRowStyle; } }
		public TableCellStyle DefaultTableCellStyle { get { return _defaultTableCellStyle; } }
		public BulletStyle    DefaultBulletStyle    { get { return _defaultBulletStyle; } }
		public LineStyle      DefaultLineStyle      { get { return _defaultLineStyle; } }

		public ReportDesign(Stream file, ILog logger)
		{
			_file = file;
			_logger = logger;
		}

		public ReportLayout Load(Generator generator)
		{
			//ValidateXML(_file);

			_file.Seek(0, SeekOrigin.Begin);
			XDocument doc = XDocument.Load(_file,LoadOptions.SetLineInfo);
			_ns = doc.Root.GetDefaultNamespace();
			XElement root = doc.Element(_ns + "Design");

			_id = root.Attribute("id").Value;
			_name = root.Attribute("name").Value;
			_inspectionTemplateId = root.Attribute("inspectionTemplateId").Value;
			_fileFormatVersion = LoadInt(root.Attribute("fileFormatVersion")).Value;

			//	Load the styles first so that layouts can refer to them
			LoadStyleLibrary(root.Element(_ns + "Styles"));

			//	Load reusable resources that layouts can use
			LoadResources(root.Element(_ns + "Resources"));

			ReportLayout layout = new ReportLayout(generator);
			layout.Load(root);
			return layout;
		}

		private void LoadStyleLibrary(XElement root)
		{
			//	The procedure is this:
			//
			//	1.	For each style type, in a particular order...
			//
			//	2.	Load all styles of that type in the library. If any
			//			one of these styles is marked as the default then
			//			assign it to the our default.
			//
			//	3.	Ensure that we have a default for this type. If we
			//			don't, then create one now.
			//
			//	4.	For each loaded style of this type, fill in any
			//			properties that are missing with the defaults for
			//			those property types.
			//
			//	Load style types in a certain order - for example load fonts
			//	and colours before loading text styles, because text styles may
			//	reference those fonts and colours.
			LoadLibraryFonts          (root?.Element(_ns + "Fonts"          ));
			LoadLibraryColors         (root?.Element(_ns + "Colors"         ));
			LoadLibraryBorders        (root?.Element(_ns + "Borders"        ));
			LoadLibraryPaddings       (root?.Element(_ns + "Paddings"       ));
			LoadLibraryTextStyles     (root?.Element(_ns + "TextStyles"     ));
			LoadLibraryBulletStyles   (root?.Element(_ns + "BulletStyles"   ));
			LoadLibraryListStyles     (root?.Element(_ns + "ListStyles"     ));
			LoadLibraryPhotoStyles    (root?.Element(_ns + "PhotoStyles"    ));
			LoadLibraryTableStyles    (root?.Element(_ns + "TableStyles"    ));
			LoadLibraryTableRowStyles (root?.Element(_ns + "TableRowStyles" ));
			LoadLibraryTableCellStyles(root?.Element(_ns + "TableCellStyles"));
			LoadLibraryLineStyles     (root?.Element(_ns + "LineStyles"     ));
		}

		private void LoadLibraryFonts(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "Font"))
					LoadLibraryStyle<Style.Font>(node);
			}
			if(_defaultFont == null)
			{
				_defaultFont = new Style.Font();
				_fonts.Add("", _defaultFont);
			}
			foreach(Style.Font font in _fonts.Values)
				FillDefaults(font);
		}

		private void LoadLibraryColors(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "Color"))
					LoadLibraryStyle<Color>(node);
			}
			if(_defaultColor == null)
			{
				_defaultColor = new Color();
				_colors.Add("", _defaultColor);
			}
			foreach(Color color in _colors.Values)
				FillDefaults(color);
		}

		private void LoadLibraryBorders(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "Border"))
					LoadLibraryStyle<Border>(node);
			}
			if(_defaultBorder == null)
			{
				_defaultBorder = new Border();
				_borders.Add("", _defaultBorder);
			}
			foreach(Border border in _borders.Values)
				FillDefaults(border);
		}

		private void LoadLibraryPaddings(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "Padding"))
					LoadLibraryStyle<Padding>(node);
			}
			if(_defaultPadding == null)
			{
				_defaultPadding = new Padding();
				_paddings.Add("", _defaultPadding);
			}
			foreach(Padding padding in _paddings.Values)
				FillDefaults(padding);
		}

		private void LoadLibraryTextStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "TextStyle"))
					LoadLibraryStyle<TextStyle>(node);
			}
			if(_defaultTextStyle == null)
			{
				_defaultTextStyle = new TextStyle();
				_textStyles.Add("", _defaultTextStyle);
			}
			foreach(TextStyle style in _textStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryListStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "ListStyle"))
					LoadLibraryStyle<ListStyle>(node);
			}
			if(_defaultListStyle == null)
			{
				_defaultListStyle = new ListStyle
				{
					//	This property is unusual in that the style could actually
					//	define a zero indent, and so FillDefaults can't infer from
					//	a zero that value is not set. So we have to set the default
					//	here instead.
					ItemIndent = 10
				};
				_listStyles.Add("", _defaultListStyle);
			}
			foreach(ListStyle style in _listStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryBulletStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "BulletStyle"))
					LoadLibraryStyle<BulletStyle>(node);
			}
			if(_defaultBulletStyle == null)
			{
				_defaultBulletStyle = new BulletStyle();
				_bulletStyles.Add("", _defaultBulletStyle);
			}
			foreach(BulletStyle style in _bulletStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryPhotoStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "PhotoStyle"))
					LoadLibraryStyle<PhotoStyle>(node);
			}
			if(_defaultPhotoStyle == null)
			{
				_defaultPhotoStyle = new PhotoStyle();
				_photoStyles.Add("", _defaultPhotoStyle);
			}
			foreach(PhotoStyle style in _photoStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryTableStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "TableStyle"))
					LoadLibraryStyle<TableStyle>(node);
			}
			if(_defaultTableStyle == null)
			{
				_defaultTableStyle = new TableStyle();
				_tableStyles.Add("", _defaultTableStyle);
			}
			foreach(TableStyle style in _tableStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryTableRowStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "TableRowStyle"))
					LoadLibraryStyle<TableRowStyle>(node);
			}
			if(_defaultTableRowStyle == null)
			{
				_defaultTableRowStyle = new TableRowStyle();
				_rowStyles.Add("", _defaultTableRowStyle);
			}
			foreach(TableRowStyle style in _rowStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryTableCellStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "TableCellStyle"))
					LoadLibraryStyle<TableCellStyle>(node);
			}
			if(_defaultTableCellStyle == null)
			{
				_defaultTableCellStyle = new TableCellStyle();
				_cellStyles.Add("", _defaultTableCellStyle);
			}
			foreach(TableCellStyle style in _cellStyles.Values)
				FillDefaults(style);
		}

		private void LoadLibraryLineStyles(XElement root)
		{
			if(root != null)
			{
				XNamespace ns = root.GetDefaultNamespace();
				foreach(XElement node in root.Elements(ns + "LineStyle"))
					LoadLibraryStyle<LineStyle>(node);
			}
			if(_defaultLineStyle == null)
			{
				_defaultLineStyle = new LineStyle();
				_lineStyles.Add("", _defaultLineStyle);
			}
			foreach(LineStyle style in _lineStyles.Values)
				FillDefaults(style);
		}

		private T LoadLibraryStyle<T>(XElement root) where T : class, IStyle
		{
			try
			{
				string id = root.Attribute("id")?.Value?.Trim();
				if(string.IsNullOrWhiteSpace(id)) id = "";

				//	Only library styles can be defaults, so we only check for
				//	isDefault here and not in the LoadXxx implementations
				bool isDefault = LoadBoolean(root.Attribute("isDefault")) ?? false;

				//	A non-default library style with no id is useless because it
				//	can't be referenced. A default style is referenced implicitly
				//	and so doesn't absolutely need an id.
				if(id == "" && !isDefault) return null;

				//	Load the style
				T t = LoadExplicitStyle<T>(root);

				//	Add to the library and set as default, as appropriate
				if(typeof(T) == typeof(Style.Font))
				{
					Style.Font style = t as Style.Font;
					if(isDefault)
					{
						if(_defaultFont != null)
							throw new ReportDesignException("A default font is already defined.");
						_defaultFont = style;
						style.IsDefault = true;
					}
					if(id != null)
						_fonts.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(Color))
				{
					Color style = t as Color;
					if(isDefault)
					{
						if(_defaultColor != null)
							throw new ReportDesignException("A default color is already defined.");
						_defaultColor = style;
						style.IsDefault = true;
					}
					if(id != null)
						_colors.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(Border))
				{
					Border style = t as Border;
					if(isDefault)
					{
						if(_defaultBorder != null)
							throw new ReportDesignException("A default border style is already defined.");
						_defaultBorder = style;
						style.IsDefault = true;
					}
					if(id != null)
						_borders.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(Padding))
				{
					Padding style = t as Padding;
					if(isDefault)
					{
						if(_defaultPadding != null)
							throw new ReportDesignException("A default padding is already defined.");
						_defaultPadding = style;
						style.IsDefault = true;
					}
					if(id != null)
						_paddings.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(TextStyle))
				{
					TextStyle style = t as TextStyle;
					if(isDefault)
					{
						if(_defaultTextStyle != null)
							throw new ReportDesignException("A default text style is already defined.");
						_defaultTextStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_textStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(ListStyle))
				{
					ListStyle style = t as ListStyle;
					if(isDefault)
					{
						if(_defaultListStyle != null)
							throw new ReportDesignException("A default list style is already defined.");
						_defaultListStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_listStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(PhotoStyle))
				{
					PhotoStyle style = t as PhotoStyle;
					if(isDefault)
					{
						if(_defaultPhotoStyle != null)
							throw new ReportDesignException("A default photo style is already defined.");
						_defaultPhotoStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_photoStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(TableStyle))
				{
					TableStyle style = t as TableStyle;
					if(isDefault)
					{
						if(_defaultTableStyle != null)
							throw new ReportDesignException("A default table style is already defined.");
						_defaultTableStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_tableStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(TableRowStyle))
				{
					TableRowStyle style = t as TableRowStyle;
					if(isDefault)
					{
						if(_defaultTableRowStyle != null)
							throw new ReportDesignException("A default table row style is already defined.");
						_defaultTableRowStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_rowStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(TableCellStyle))
				{
					TableCellStyle style = t as TableCellStyle;
					if(isDefault)
					{
						if(_defaultTableCellStyle != null)
							throw new ReportDesignException("A default table cell style is already defined.");
						_defaultTableCellStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_cellStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(BulletStyle))
				{
					BulletStyle style = t as BulletStyle;
					if(isDefault)
					{
						if(_defaultBulletStyle != null)
							throw new ReportDesignException("A default bullet style is already defined.");
						_defaultBulletStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_bulletStyles.Add(id, style);
					return style as T;
				}
				else if(typeof(T) == typeof(LineStyle))
				{
					LineStyle style = t as LineStyle;
					if(isDefault)
					{
						if(_defaultLineStyle != null)
							throw new ReportDesignException("A default line style is already defined.");
						_defaultLineStyle = style;
						style.IsDefault = true;
					}
					if(id != null)
						_lineStyles.Add(id, style);
					return style as T;
				}
				else
				{
					throw new Exception($"Unrecognized style type '{typeof(T).Name}'.");
				}
			}
			catch(Exception ex)
			{
				ex.Data.Add("LineNumber",   ((IXmlLineInfo)root).LineNumber);
				ex.Data.Add("LinePosition", ((IXmlLineInfo)root).LinePosition);
				throw;
			}
		}

		private T LoadExplicitStyle<T>(XElement root) where T : class, IStyle
		{
			if(typeof(T) == typeof(Style.Font))
			{
				Style.Font style = LoadFont(root);
				return style as T;
			}
			else if(typeof(T) == typeof(Color))
			{
				Color style = LoadColor(root);
				return style as T;
			}
			else if(typeof(T) == typeof(Border))
			{
				Border style = LoadBorder(root);
				return style as T;
			}
			else if(typeof(T) == typeof(Padding))
			{
				Padding style = LoadPadding(root);
				return style as T;
			}
			else if(typeof(T) == typeof(TextStyle))
			{
				TextStyle style = LoadTextStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(ListStyle))
			{
				ListStyle style = LoadListStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(PhotoStyle))
			{
				PhotoStyle style = LoadPhotoStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(TableStyle))
			{
				TableStyle style = LoadTableStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(TableRowStyle))
			{
				TableRowStyle style = LoadTableRowStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(TableCellStyle))
			{
				TableCellStyle style = LoadTableCellStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(BulletStyle))
			{
				BulletStyle style = LoadBulletStyle(root);
				return style as T;
			}
			else if(typeof(T) == typeof(LineStyle))
			{
				LineStyle style = LoadLineStyle(root);
				return style as T;
			}
			else
			{
				throw new Exception($"Unrecognized style type '{typeof(T).Name}'.");
			}
		}

		/// <summary>
		/// Load an explicit style, possibly loading the nominated style
		/// or any of its substyles from the library, and and possibly
		/// overriding some parts of those library styles.
		/// </summary>
		public T LoadStyle<T>(XElement root) where T : class, IStyle
		{
			//	A style can appear in the design file in three contexts:
			//
			//	1.	In the design file's style section: <XxxStyle id="whatever">.
			//			Load the style and add it to our library. This method does
			//			not handle this case - that's coveredy by LoadLibraryStyle.
			//
			//	2.	As a reference in a layout: <Style ref="whatever">. Look up
			//			the style in the library. The reference can identify the
			//			style by its id, or it can specify an id of "" to indicate
			//			the default style.
			//
			//	3.	Embedded in a layout: <XxxLayout><Style>...</Style></XxxLayout>.
			//			Load the style but do not add it to the library.

			//	If a style reference has been specified then we'll load that
			//	style from the library, otherwise we'll load the default style,
			//	and then we may override parts of it
			string id = root?.Attribute("ref")?.Value;
			if(string.IsNullOrWhiteSpace(id)) id = ""; // use default

			//	Get the style from the library
			T t = GetStyle<T>(id);
			t = OverrideStyle<T>(t, root); // override if required
			return t;
		}

		/// <summary>
		/// Get a style from the library. If the style id is blank then
		/// return the default style of the specified type.
		/// </summary>
		private T GetStyle<T>(string id) where T : class, IStyle
		{
			T t = null;
			bool found = false;
			if(typeof(T) == typeof(Style.Font))
			{
				Style.Font style = null;
				found = _fonts.TryGetValue(id, out style);
				if(style == null) style = _defaultFont;
				t = style as T;
			}
			else if(typeof(T) == typeof(Color))
			{
				Color style = null;
				found = _colors.TryGetValue(id, out style);
				if(style == null) style = _defaultColor;
				t = style as T;
			}
			else if(typeof(T) == typeof(Border))
			{
				Border style = null;
				found = _borders.TryGetValue(id, out style);
				if(style == null) style = _defaultBorder;
				t = style as T;
			}
			else if(typeof(T) == typeof(Padding))
			{
				Padding style = null;
				found = _paddings.TryGetValue(id, out style);
				if(style == null) style = _defaultPadding;
				t = style as T;
			}
			else if(typeof(T) == typeof(TextStyle))
			{
				TextStyle style = null;
				found = _textStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultTextStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(ListStyle))
			{
				ListStyle style = null;
				found = _listStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultListStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(PhotoStyle))
			{
				PhotoStyle style = null;
				found = _photoStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultPhotoStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(TableStyle))
			{
				TableStyle style = null;
				found = _tableStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultTableStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(TableRowStyle))
			{
				TableRowStyle style = null;
				found = _rowStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultTableRowStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(TableCellStyle))
			{
				TableCellStyle style = null;
				found = _cellStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultTableCellStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(BulletStyle))
			{
				BulletStyle style = null;
				found = _bulletStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultBulletStyle;
				t = style as T;
			}
			else if(typeof(T) == typeof(LineStyle))
			{
				LineStyle style = null;
				found = _lineStyles.TryGetValue(id, out style);
				if(style == null) style = _defaultLineStyle;
				t = style as T;
			}
			else
			{
				throw new Exception($"Unrecognized style type '{typeof(T).Name}'.");
			}

			return t; // could be null
		}

		private T OverrideStyle<T>(T t, XElement root) where T : class, IStyle
		{
			if(t == null) return t;
			if(root == null) return t;

			if(typeof(T) == typeof(Style.Font))
				t = OverrideFont(t as Style.Font, root) as T;
			else if(typeof(T) == typeof(Color))
				t = OverrideColor(t as Color, root) as T;
			else if(typeof(T) == typeof(Border))
				t = OverrideBorder(t as Border, root) as T;
			else if(typeof(T) == typeof(Padding))
				t = OverridePadding(t as Padding, root) as T;
			else if(typeof(T) == typeof(TextStyle))
				t = OverrideTextStyle(t as TextStyle, root) as T;
			else if(typeof(T) == typeof(ListStyle))
				t = OverrideListStyle(t as ListStyle, root) as T;
			else if(typeof(T) == typeof(PhotoStyle))
				t = OverridePhotoStyle(t as PhotoStyle, root) as T;
			else if(typeof(T) == typeof(TableStyle))
				t = OverrideTableStyle(t as TableStyle, root) as T;
			else if(typeof(T) == typeof(TableRowStyle))
				t = OverrideTableRowStyle(t as TableRowStyle, root) as T;
			else if(typeof(T) == typeof(TableCellStyle))
				t = OverrideTableCellStyle(t as TableCellStyle, root) as T;
			else if(typeof(T) == typeof(BulletStyle))
				t = OverrideBulletStyle(t as BulletStyle, root) as T;
			else if(typeof(T) == typeof(LineStyle))
				t = OverrideLineStyle(t as LineStyle, root) as T;
			else
				throw new Exception($"Unrecognized style type '{typeof(T).Name}'.");

			return t;
		}

		private TextStyle LoadTextStyle(XElement root)
		{
			if(root == null) return null;
			TextStyle style = new TextStyle();

			style.Name             = LoadString(root.Attribute("id"));
			style.LineSpacing      = LoadDouble(root.Element(_ns + "LineSpacing"     )) ?? 1.0;
			style.ParagraphSpacing = LoadDouble(root.Element(_ns + "ParagraphSpacing")) ?? 1.0;
			style.Alignment        = LoadEnum<TextAlignment>(root.Element(_ns + "Alignment"));
			style.SoftBreakLimit   = LoadDouble(root.Element(_ns + "SoftBreakLimit"  )) ?? 0.0;

			style.Font      = LoadStyle<Style.Font>(root.Element(_ns + "Font"   ));
			style.Color     = LoadStyle<Color>     (root.Element(_ns + "Color"  ));
			style.Border    = LoadStyle<Border>    (root.Element(_ns + "Border" ));
			style.Padding =   LoadStyle<Padding>   (root.Element(_ns + "Padding"));

			//	BackColor can be null - don't substitute the default if not specified
			//TODO: why do only TextStyle and TableRowStyle have BackColor?
			XElement elem = root.Element(_ns + "BackColor");
			if(elem != null)
				style.BackColor = LoadStyle<Color>(elem);

			if(style.Font  == null) style.Font  = _defaultFont;
			if(style.Color == null) style.Color = _defaultColor;
			//	No need for a default background color

			style.ListSeparator  = LoadString(root.Element(_ns + "ListSeparator" ));
			style.ListTerminator = LoadString(root.Element(_ns + "ListTerminator"));

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private ListStyle LoadListStyle(XElement root)
		{
			if(root == null) return null;
			ListStyle style = new ListStyle();

			style.Name = LoadString(root.Attribute("id"));

			//	Get the bullet styles. For each of the selected bullet style and
			//	the unselected bullet style, if it's not specified then use the
			//	plain bullet style.
			style.BulletStyle = LoadStyle<BulletStyle>(root.Element(_ns + "BulletStyle"));
			XElement elem = root.Element(_ns + "SelectedBulletStyle");
			if(elem != null)
				style.SelectedBulletStyle   = LoadStyle<BulletStyle>(elem);
			elem = root.Element(_ns + "UnselectedBulletStyle");
			if(elem != null)
				style.UnselectedBulletStyle = LoadStyle<BulletStyle>(elem);

			style.ItemStyle    = LoadStyle<TextStyle>  (root.Element(_ns + "ItemStyle"    ));
			style.ItemIndent   = LoadInt               (root.Element(_ns + "ItemIndent"   )) ?? 0;
			style.BulletIndent = LoadInt               (root.Element(_ns + "BulletIndent" )) ?? 0;
			style.Border       = LoadStyle<Border>     (root.Element(_ns + "Border"       ));
			style.Padding      = LoadStyle<Padding>    (root.Element(_ns + "Padding"      ));

			//	Note that the item style is applied only to items based on the list’s
			//	source reference. Items added from the list’s sublayouts collection
			//	already have styling applied before they’re added to the list, and
			//	do not use this item style.
			if (style.ItemStyle == null)
			{
				style.ItemStyle = _defaultTextStyle;
				//	On adopting the default text style, if that style leaves either
				//	font or color null then don't overrule that decision made by the
				//	default style
			}
			else
			{
				if(style.ItemStyle.Font  == null) style.ItemStyle.Font  = _defaultFont;
				if(style.ItemStyle.Color == null) style.ItemStyle.Color = _defaultColor;
			}

			if(style.BulletStyle == null)
			{
				style.BulletStyle = _defaultBulletStyle;
				//	On adopting the default bullet style, if that style leaves either
				//	font or color null then don't overrule that decision made by the
				//	default style
			}
			else
			{
				if(style.BulletStyle.Font  == null) style.BulletStyle.Font  = _defaultFont;
				if(style.BulletStyle.Color == null) style.BulletStyle.Color = _defaultColor;
			}

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private PhotoStyle LoadPhotoStyle(XElement root)
		{
			if(root == null) return null;
			PhotoStyle style = new PhotoStyle();

			style.Name = LoadString(root.Attribute("id"));

			style.CaptionStyle = LoadStyle<TextStyle>(root.Element(_ns + "CaptionStyle"));
			if(style.CaptionStyle == null) style.CaptionStyle = _defaultTextStyle;

			style.Border  = LoadStyle<Border> (root.Element(_ns + "Border" ));
			style.Padding = LoadStyle<Padding>(root.Element(_ns + "Padding"));

			style.MaxWidth   = LoadInt(root.Element(_ns + "MaxWidth"  )) ?? 0;
			style.MaxHeight  = LoadInt(root.Element(_ns + "MaxHeight" )) ?? 0;
			style.Resolution = LoadInt(root.Element(_ns + "Resolution")) ?? 0;
			style.Quality    = LoadInt(root.Element(_ns + "Quality"   )) ?? 0;

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private TableStyle LoadTableStyle(XElement root)
		{
			if(root == null) return null;
			TableStyle style = new TableStyle();

			style.Name    = LoadString        (root.Attribute("id"));
			style.Border  = LoadStyle<Border> (root.Element(_ns + "Border"));
			style.Padding = LoadStyle<Padding>(root.Element(_ns + "Padding"));

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private TableRowStyle LoadTableRowStyle(XElement root)
		{
			if(root == null) return null;
			TableRowStyle style = new TableRowStyle();

			style.Name    = LoadString        (root.Attribute("id"));
			style.Padding = LoadStyle<Padding>(root.Element(_ns + "Padding"));

			//	BackColor can be null - don't substitute the default if not specified
			XElement elem = root.Element(_ns + "BackColor");
			if(elem != null)
				style.BackColor = LoadStyle<Color>(elem);

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private TableCellStyle LoadTableCellStyle(XElement root)
		{
			if(root == null) return null;
			TableCellStyle style = new TableCellStyle();

			style.Name    = LoadString(root.Attribute("id"));
			style.Padding = LoadStyle<Padding>(root.Element(_ns + "Padding"));

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private BulletStyle LoadBulletStyle(XElement root)
		{
			if(root == null) return null;
			BulletStyle style = new BulletStyle();

			style.Name = LoadString(root.Attribute("id"));

			style.BulletText = LoadString(root.Element(_ns + "BulletText" ));
			if(style.BulletText == null) style.BulletText = "";

			style.NumberStyle = LoadEnum<ListNumberStyle>(root.Element(_ns + "NumberStyle"));
			style.StartAt = LoadInt(root.Element(_ns + "StartAt")) ?? 1;

			style.Font    = LoadStyle<Style.Font>(root.Element(_ns + "Font"   ));
			style.Color   = LoadStyle<Color>     (root.Element(_ns + "Color"  ));
			style.Padding = LoadStyle<Padding>   (root.Element(_ns + "Padding"));

			if(style.Font  == null) style.Font  = _defaultFont;
			if(style.Color == null) style.Color = _defaultColor;

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private LineStyle LoadLineStyle(XElement root)
		{
			if(root == null) return null;
			LineStyle style = new LineStyle();

			style.Name      = LoadString(root.Attribute("id"));
			style.Thickness = LoadInt(root.Element(_ns + "Thickness")) ?? 1;
			style.Color     = LoadStyle<Color>  (root.Element(_ns + "Color"    ));
			style.Padding =   LoadStyle<Padding>(root.Element(_ns + "Padding"  ));

			if(style.Color == null) style.Color = _defaultColor;

			style.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			style.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return style;
		}

		private Style.Font LoadFont(XElement root)
		{
			if(root == null) return null;
			Style.Font font = new Style.Font();

			font.FamilyName = LoadString (root.Element(_ns + "FamilyName"));
			font.Size       = LoadInt    (root.Element(_ns + "Size"     )) ?? 0;
			font.Bold       = LoadBoolean(root.Element(_ns + "Bold"     )) ?? false;
			font.Italic     = LoadBoolean(root.Element(_ns + "Italic"   )) ?? false;
			font.Underline  = LoadBoolean(root.Element(_ns + "Underline")) ?? false;

			font.Name = LoadString(root.Attribute("id"));
			font.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			font.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return font;
		}

		private Color LoadColor(XElement root)
		{
			if(root == null) return null;
			Color color = new Color();

			color.Red   = LoadDouble(root.Element(_ns + "Red"  )) ?? 0.0;
			color.Green = LoadDouble(root.Element(_ns + "Green")) ?? 0.0;
			color.Blue  = LoadDouble(root.Element(_ns + "Blue" )) ?? 0.0;

			color.Name = LoadString(root.Attribute("id"));
			color.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			color.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return color;
		}

		private Border LoadBorder(XElement root)
		{
			if(root == null) return null;
			Border border = new Border();

			XElement stroke = root.Element(_ns + "Stroke");
			if(stroke != null)
				border.Thickness = LoadInt(stroke.Element(_ns + "Thickness")) ?? 1;

			border.Color = LoadStyle<Color>(root.Element(_ns + "Color"));

			XElement parts = root.Element(_ns + "Parts");
			if(parts != null)
			{
				bool set = LoadBoolean(parts.Element(_ns + "Left")) ?? false;
				if(set)
					border.Parts |= BorderPart.Left;

				set = LoadBoolean(parts.Element(_ns + "Bottom")) ?? false;
				if(set)
					border.Parts |= BorderPart.Bottom;

				set = LoadBoolean(parts.Element(_ns + "Right")) ?? false;
				if(set)
					border.Parts |= BorderPart.Right;

				set = LoadBoolean(parts.Element(_ns + "Top")) ?? false;
				if(set)
					border.Parts |= BorderPart.Top;

				set = LoadBoolean(parts.Element(_ns + "InnerHorizontal")) ?? false;
				if(set)
					border.Parts |= BorderPart.InnerHorizontal;

				set = LoadBoolean(parts.Element(_ns + "InnerVertical")) ?? false;
				if(set)
					border.Parts |= BorderPart.InnerVertical;
			}

			border.Name = LoadString(root.Attribute("id"));
			border.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			border.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return border;
		}

		private Padding LoadPadding(XElement root)
		{
			if(root == null) return null;
			Padding padding = new Padding();

			padding.Left   = LoadInt(root.Element(_ns + "Left"  )) ?? 0;
			padding.Bottom = LoadInt(root.Element(_ns + "Bottom")) ?? 0;
			padding.Right  = LoadInt(root.Element(_ns + "Right" )) ?? 0;
			padding.Top    = LoadInt(root.Element(_ns + "Top"   )) ?? 0;

			padding.Name = LoadString(root.Attribute("id"));
			padding.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			padding.LinePosition = ((IXmlLineInfo)root).LinePosition;

			return padding;
		}

		private void FillDefaults(Style.Font font)
		{
			if(string.IsNullOrWhiteSpace(font.FamilyName))
				font.FamilyName = "Verdana";
			if(font.Size   <= 0) font.Size   =  10;
			if(font.Weight <= 0) font.Weight = 400; // normal - see the OS/2 True Type font table
		}

		private void FillDefaults(Color color)
		{
			if(color.Red   < 0.0) color.Red   = 0.0;
			if(color.Green < 0.0) color.Green = 0.0;
			if(color.Blue  < 0.0) color.Blue  = 0.0;

			if(color.Red   > 1.0) color.Red   = 1.0;
			if(color.Green > 1.0) color.Green = 1.0;
			if(color.Blue  > 1.0) color.Blue  = 1.0;
		}

		private void FillDefaults(Border border)
		{
			if(border.Thickness < 0) border.Thickness = 0;
			if(border.Color == null) border.Color = _defaultColor;

			FillDefaults(border.Color);
		}

		private void FillDefaults(Padding padding)
		{
			if(padding.Left   < 0) padding.Left   = 0;
			if(padding.Bottom < 0) padding.Bottom = 0;
			if(padding.Right  < 0) padding.Right  = 0;
			if(padding.Top    < 0) padding.Top    = 0;
		}

		private void FillDefaults(TextStyle style)
		{
			if(!Enum.IsDefined(typeof(TextAlignment), style.Alignment))
				style.Alignment = TextAlignment.Left;

			if(style.SoftBreakLimit < 0.0 || style.SoftBreakLimit > 1.0) style.SoftBreakLimit = 0.8;

			if(style.LineSpacing      < 0.0) style.LineSpacing      = 1.0;
			if(style.ParagraphSpacing < 0.0) style.ParagraphSpacing = 1.0;

			if(style.Font    == null) style.Font    = _defaultFont;
			if(style.Color   == null) style.Color   = _defaultColor;
			if(style.Border  == null) style.Border  = _defaultBorder;
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Font);
			FillDefaults(style.Color);
			FillDefaults(style.Border);
			FillDefaults(style.Padding);
		}

		private void FillDefaults(BulletStyle style)
		{
			if(!Enum.IsDefined(typeof(ListNumberStyle), style.NumberStyle))
				style.NumberStyle = ListNumberStyle.Bullet;

			if(style.StartAt < 1) style.StartAt = 1;
			if(style.BulletText == null) style.BulletText = "•"; //	Empty bullet text is ok

			if(style.Font    == null) style.Font    = _defaultFont;
			if(style.Color   == null) style.Color   = _defaultColor;
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Font);
			FillDefaults(style.Color);
			FillDefaults(style.Padding);
		}

		private void FillDefaults(ListStyle style)
		{
			if(style.ItemIndent   < 0) style.ItemIndent   = 0;
			if(style.BulletIndent < 0) style.BulletIndent = 10;

			if(style.ItemStyle   == null) style.ItemStyle   = _defaultTextStyle;
			if(style.BulletStyle == null) style.BulletStyle = _defaultBulletStyle;
			if(style.Padding     == null) style.Padding     = _defaultPadding;
			if(style.Border      == null) style.Border      = _defaultBorder;

			FillDefaults(style.ItemStyle);
			FillDefaults(style.BulletStyle);
			if(style.SelectedBulletStyle != null)
				FillDefaults(style.SelectedBulletStyle);
			if(style.UnselectedBulletStyle != null)
				FillDefaults(style.UnselectedBulletStyle);
			FillDefaults(style.Border);
			FillDefaults(style.Padding);
		}

		private void FillDefaults(PhotoStyle style)
		{
			if(style.MaxHeight <= 0) style.MaxHeight = 300;
			if(style.MaxWidth  <= 0) style.MaxWidth  = 300;
			if(style.Resolution <= 0) style.Resolution = 150;
			if(style.Quality <= 0) style.Quality = 90;

			if(style.CaptionStyle == null) style.CaptionStyle = _defaultTextStyle;
			if(style.Border       == null) style.Border       = _defaultBorder;
			if(style.Padding      == null) style.Padding      = _defaultPadding;

			FillDefaults(style.CaptionStyle);
			FillDefaults(style.Border);
			FillDefaults(style.Padding);
		}

		private void FillDefaults(TableStyle style)
		{
			if(style.Border  == null) style.Border  = _defaultBorder;
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Border);
			FillDefaults(style.Padding);
		}

		private void FillDefaults(TableRowStyle style)
		{
			//	Null BackColor is ok
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Padding);
		}

		private void FillDefaults(TableCellStyle style)
		{
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Padding);
		}

		private void FillDefaults(LineStyle style)
		{
			if(style.Thickness < 0) style.Thickness = 1;
			if(style.Color   == null) style.Color   = _defaultColor;
			if(style.Padding == null) style.Padding = _defaultPadding;

			FillDefaults(style.Color);
			FillDefaults(style.Padding);
		}

		/// <summary>
		/// Load the resource definitions. We don't actually load the resource files
		/// at this time because that could be time-consuming, and also because we don't
		/// know for sure that all resources will actually be used.
		/// </summary>
		private void LoadResources(XElement root)
		{
			if(root == null) return;
			XNamespace ns = root.GetDefaultNamespace();
			foreach(XElement node in root.Elements(ns + "Resource"))
			{
				string id = LoadString(node.Attribute("id"));
				string filename = LoadString(node.Element(_ns + "Filename"));
				Resource resource = new Resource(id, filename);
				_resources.Add(id, resource);
			}
		}

		public string GetResourceFilename(string resourceId)
		{
			Resource resource;
			bool ok = _resources.TryGetValue(resourceId, out resource);
			return resource?.Filename;
		}

		public async Task<byte[]> GetResource(string resourceId, IResourceService service)
		{
			if(resourceId == null) return null;

			byte[] bits = null;
			Resource resource = null;
			bool ok = _resources.TryGetValue(resourceId, out resource);
			if(resource != null)
				bits = await resource.GetFile(service);
			return bits;
		}

		public PageMetrics LoadPageMetrics(XElement root)
		{
			if(root == null) return null;
			PageMetrics metrics = new PageMetrics();

			XElement box = root.Element(_ns + "MediaBox");
			if(box != null)
			{
				metrics.MediaBoxLeft   = LoadInt(box.Attribute("left"  )) ?? 0;
				metrics.MediaBoxBottom = LoadInt(box.Attribute("bottom")) ?? 0;
				metrics.MediaBoxRight  = LoadInt(box.Attribute("right" )) ?? 0;
				metrics.MediaBoxTop    = LoadInt(box.Attribute("top"   )) ?? 0;
			}

			box = root.Element(_ns + "BodyBox");
			if(box != null)
			{
				metrics.BodyBoxLeft   = LoadInt(box.Attribute("left"  )) ?? 0;
				metrics.BodyBoxBottom = LoadInt(box.Attribute("bottom")) ?? 0;
				metrics.BodyBoxRight  = LoadInt(box.Attribute("right" )) ?? 0;
				metrics.BodyBoxTop    = LoadInt(box.Attribute("top"   )) ?? 0;
			}

			box = root.Element(_ns + "HeaderBox");
			if(box != null)
			{
				metrics.HeaderBoxLeft   = LoadInt(box.Attribute("left"  )) ?? 0;
				metrics.HeaderBoxBottom = LoadInt(box.Attribute("bottom")) ?? 0;
				metrics.HeaderBoxRight  = LoadInt(box.Attribute("right" )) ?? 0;
				metrics.HeaderBoxTop    = LoadInt(box.Attribute("top"   )) ?? 0;
			}

			box = root.Element(_ns + "FooterBox");
			if(box != null)
			{
				metrics.FooterBoxLeft   = LoadInt(box.Attribute("left"  )) ?? 0;
				metrics.FooterBoxBottom = LoadInt(box.Attribute("bottom")) ?? 0;
				metrics.FooterBoxRight  = LoadInt(box.Attribute("right" )) ?? 0;
				metrics.FooterBoxTop    = LoadInt(box.Attribute("top"   )) ?? 0;
			}

			return metrics;
		}

		public PageBreakRules LoadPageBreakRules(XElement root)
		{
			if(root == null) return null;
			PageBreakRules rules = new PageBreakRules();

			rules.NewPage         = LoadBoolean(root.Element(_ns + "NewPage"     )) ?? false;
			rules.KeepWithNext    = LoadBoolean(root.Element(_ns + "KeepWithNext")) ?? false;
			rules.MinimumLines    = LoadInt    (root.Element(_ns + "MinLines"    )) ?? 0;
			rules.MaximumPosition = LoadFloat  (root.Element(_ns + "MaxPosition" )) ?? 1.0f;

			return rules;
		}

		public Condition LoadCondition(XElement root)
		{
			if(root == null) return null;

			switch(root.Name.LocalName)
			{
				case "EmptyLayoutCondition":
					return LoadEmptyLayoutCondition(root);
				case "OptionSelectedCondition":
					return LoadOptionSelectedCondition(root);
				case "PhotoCountCondition":
					return LoadPhotoCountCondition(root);
				case "ItemCountCondition":
					return LoadItemCountCondition(root);
				case "DocTagCondition":
					return LoadDocTagCondition(root);
				case "ContentSelectedCondition":
					return LoadContentSelectedCondition(root);
				case "ContentDocTagCondition":
					return LoadContentDocTagCondition(root);
			}
			return null;
		}

		private EmptyLayoutCondition LoadEmptyLayoutCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			EmptyLayoutCondition condition = new EmptyLayoutCondition(lineNumber, linePosition);

			condition.Context = LoadInt(root.Attribute("context")) ?? Condition.ChapterContext;
			condition.RefType = LoadEnum<LayoutType>(root.Attribute("refType"));
			condition.RefId = LoadString(root.Attribute("refId"));

			bool require, prohibit;
			LoadConditionRequireProhibit(root, out require, out prohibit);
			condition.Require = require;
			condition.Prohibit = prohibit;

			if (condition.RefType == LayoutType.None)
				throw new InvalidConditionException("Invalid refType", root);
			if (string.IsNullOrWhiteSpace(condition.RefId))
				throw new InvalidConditionException("Invalid refId", root);

			return condition;
		}

		private OptionSelectedCondition LoadOptionSelectedCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			OptionSelectedCondition condition = new OptionSelectedCondition(lineNumber, linePosition);

			string source = LoadString(root.Attribute("source"));
			if(!string.IsNullOrWhiteSpace(source))
				condition.Source = new Path.Path(source, lineNumber, linePosition);

			bool require, prohibit;
			LoadConditionRequireProhibit(root, out require, out prohibit);
			condition.Require = require;
			condition.Prohibit = prohibit;

			return condition;
		}

		private PhotoCountCondition LoadPhotoCountCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			PhotoCountCondition condition = new PhotoCountCondition(lineNumber, linePosition);

			condition.Context = LoadInt(root.Attribute("context")) ?? Condition.ChapterContext;
			string source = LoadString(root.Attribute("source"));
			if(!string.IsNullOrWhiteSpace(source))
				condition.Source = new Path.Path(source, lineNumber, linePosition);
			condition.RefType = LoadEnum<LayoutType>(root.Attribute("refType"));
			condition.RefId   = LoadString(root.Attribute("refId"));
			condition.Minimum = LoadInt(root.Element(_ns + "Minimum")) ?? 0;
			condition.Maximum = LoadInt(root.Element(_ns + "Maximum")) ?? int.MaxValue;

			return condition;
		}

		private ItemCountCondition LoadItemCountCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			ItemCountCondition condition = new ItemCountCondition(lineNumber, linePosition);

			condition.Context = LoadInt(root.Attribute("context")) ?? Condition.ChapterContext;
			condition.RefType = LoadEnum<LayoutType>(root.Attribute("refType"));
			condition.RefId   = LoadString(root.Attribute("refId"));
			condition.Minimum = LoadInt(root.Element(_ns + "Minimum")) ?? 0;
			condition.Maximum = LoadInt(root.Element(_ns + "Maximum")) ?? int.MaxValue;

			return condition;
		}

		private DocTagCondition LoadDocTagCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			DocTagCondition condition = new DocTagCondition(lineNumber, linePosition);

			condition.DocTag = LoadString(root.Attribute("tag"));
			if (string.IsNullOrWhiteSpace(condition.DocTag))
				throw new InvalidConditionException("Invalid doctag", root);

			bool require, prohibit;
			LoadConditionRequireProhibit(root, out require, out prohibit);
			condition.Require = require;
			condition.Prohibit = prohibit;

			return condition;
		}

		private ContentSelectedCondition LoadContentSelectedCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			ContentSelectedCondition condition = new ContentSelectedCondition(lineNumber, linePosition);

			bool require, prohibit;
			LoadConditionRequireProhibit(root, out require, out prohibit);
			condition.Require = require;
			condition.Prohibit = prohibit;

			return condition;
		}

		private ContentDocTagCondition LoadContentDocTagCondition(XElement root)
		{
			int lineNumber   = ((IXmlLineInfo)root).LineNumber;
			int linePosition = ((IXmlLineInfo)root).LinePosition;
			ContentDocTagCondition condition = new ContentDocTagCondition(lineNumber, linePosition);

			condition.DocTag = LoadString(root.Attribute("tag"));
			if (string.IsNullOrWhiteSpace(condition.DocTag))
				throw new InvalidConditionException("Invalid doctag", root);

			bool require, prohibit;
			LoadConditionRequireProhibit(root, out require, out prohibit);
			condition.Require = require;
			condition.Prohibit = prohibit;

			return condition;
		}

		private void LoadConditionRequireProhibit(XElement root, out bool require, out bool prohibit)
		{
			bool? requireSpecification  = LoadBoolean(root.Element(_ns + "Require" ));
			bool? prohibitSpecification = LoadBoolean(root.Element(_ns + "Prohibit"));

			if(requireSpecification == null && prohibitSpecification == null)
			{
				//	Neither is set. Apply the default condition of require=true.
				//	This lets the designer get the most common condition of including
				//	only checked checkboxes, for example, with the simplest expression:
				//	<OptionSelectedCondition/>
				require = true;
				prohibit = false;
			}
			else if(requireSpecification == null)
			{
				//	Require is not set but prohibit is. Set require to false.
				require = false;
				prohibit = prohibitSpecification.Value;
			}
			else if(prohibitSpecification == null)
			{
				//	Require is set and prohibit is not. Set prohibit to false.
				require = requireSpecification.Value;
				prohibit = false;
			}
			else
			{
				//	Both are set, so just accept them.
				//	This can give us the illegal situation of both
				//	being true, which will raise an exception when
				//	the condition is evaluated
				require = requireSpecification.Value;
				prohibit = prohibitSpecification.Value;
			}
		}

		private TextStyle OverrideTextStyle(TextStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<TextStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			TextStyle newStyle = new TextStyle();
			newStyle.Base = style;
			newStyle.LineSpacing = style?.LineSpacing ?? 0.0;
			newStyle.ParagraphSpacing = style?.ParagraphSpacing ?? 0.0;
			newStyle.Alignment = style?.Alignment ?? TextAlignment.Left;
			newStyle.SoftBreakLimit = style?.SoftBreakLimit ?? 0.0;
			newStyle.Font = style?.Font;
			newStyle.Color = style?.Color;
			newStyle.BackColor = style?.BackColor;
			newStyle.Border = style?.Border;
			newStyle.Padding = style?.Padding;
			newStyle.ListSeparator = style?.ListSeparator;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			double? lineSpacing      = LoadDouble(root.Element(_ns + "LineSpacing"));
			double? paragraphSpacing = LoadDouble(root.Element(_ns + "ParagraphSpacing"));
			double? softBreakLimit   = LoadDouble(root.Element(_ns + "SoftBreakLimit"));

			if(lineSpacing      != null) style.LineSpacing      = lineSpacing.Value;
			if(paragraphSpacing != null) style.ParagraphSpacing = paragraphSpacing.Value;
			if(softBreakLimit   != null) style.SoftBreakLimit   = softBreakLimit.Value;

			//	LoadEnum can't return null because enums are structs, and so we have to
			//	check ourself whether the alignment is overridden
			XElement elem = root.Element(_ns + "Alignment");
			if(elem != null)
				style.Alignment = LoadEnum<TextAlignment>(elem);

			style.Font      = OverrideFont   (style.Font,      root.Element(_ns + "Font"     ));
			style.Color     = OverrideColor  (style.Color,     root.Element(_ns + "Color"    ));
			style.BackColor = OverrideColor  (style.BackColor, root.Element(_ns + "BackColor"));
			style.Border    = OverrideBorder (style.Border,    root.Element(_ns + "Border"   ));
			style.Padding   = OverridePadding(style.Padding,   root.Element(_ns + "Padding"  ));

			string listSeparator = LoadString(root.Element(_ns + "ListSeparator"));
			if(listSeparator != null)
				style.ListSeparator = listSeparator;

			string listTerminator = LoadString(root.Element(_ns + "ListTerminator"));
			if(listTerminator != null)
				style.ListTerminator = listTerminator;

			return style;
		}

		private ListStyle OverrideListStyle(ListStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<ListStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			ListStyle newStyle = new ListStyle();
			newStyle.Base = style;
			newStyle.ItemStyle = style?.ItemStyle;
			newStyle.ItemIndent = style?.ItemIndent ?? 0;
			newStyle.BulletStyle = style?.BulletStyle;
			newStyle.SelectedBulletStyle = style?.SelectedBulletStyle;
			newStyle.UnselectedBulletStyle = style?.UnselectedBulletStyle;
			newStyle.BulletIndent = style?.BulletIndent ?? 0;
			newStyle.Border = style?.Border;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			style.ItemStyle = OverrideTextStyle(style.ItemStyle, root.Element(_ns + "ItemStyle"));
			int? itemIndent = LoadInt(root.Element(_ns + "ItemIndent"));
			if(itemIndent != null) style.ItemIndent = itemIndent.Value;

			style.BulletStyle = OverrideBulletStyle(style.BulletStyle, root.Element(_ns + "BulletStyle"));
			int? bulletIndent = LoadInt(root.Element(_ns + "BulletIndent"));
			if(bulletIndent != null) style.BulletIndent = bulletIndent.Value;

			if(style.SelectedBulletStyle != null)
				style.SelectedBulletStyle = OverrideBulletStyle(style.SelectedBulletStyle, root.Element(_ns + "SelectedBulletStyle"));
			if(style.UnselectedBulletStyle != null)
				style.UnselectedBulletStyle = OverrideBulletStyle(style.UnselectedBulletStyle, root.Element(_ns + "UnselectedBulletStyle"));
			//int? bulletIndent = LoadInt(root.Element(_ns + "BulletIndent"));
			//if(bulletIndent != null) style.BulletIndent = bulletIndent.Value;

			style.Border  = OverrideBorder (style.Border, root.Element(_ns + "Border" ));
			style.Padding = OverridePadding(style.Padding,root.Element(_ns + "Padding"));

			return style;			
		}

		private PhotoStyle OverridePhotoStyle(PhotoStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<PhotoStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			PhotoStyle newStyle = new PhotoStyle();
			newStyle.Base = style;
			newStyle.MaxHeight = style?.MaxHeight ?? 0;
			newStyle.MaxWidth = style?.MaxWidth ?? 0;
			newStyle.Resolution = style?.Resolution ?? 0;
			newStyle.Quality = style?.Quality ?? 0;
			newStyle.CaptionStyle = style?.CaptionStyle;
			newStyle.Border = style?.Border;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			int? maxHeight = LoadInt(root.Element(_ns + "MaxHeight"));
			int? maxWidth  = LoadInt(root.Element(_ns + "MaxWidth" ));
			if(maxHeight != null) style.MaxHeight = maxHeight.Value;
			if(maxWidth  != null) style.MaxWidth  = maxWidth .Value;

			style.CaptionStyle = OverrideTextStyle(style.CaptionStyle, root.Element(_ns + "CaptionStyle"));
			style.Border       = OverrideBorder   (style.Border,       root.Element(_ns + "Border"      ));
			style.Padding      = OverridePadding  (style.Padding,      root.Element(_ns + "Padding"     ));

			int? resolution = LoadInt(root.Element(_ns + "Resolution"));
			int? quality    = LoadInt(root.Element(_ns + "Quality"   ));
			if(resolution != null) style.Resolution = resolution.Value;
			if(quality    != null) style.Quality    = quality   .Value;

			return style;			
		}

		private TableStyle OverrideTableStyle(TableStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<TableStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			TableStyle newStyle = new TableStyle();
			newStyle.Base = style;
			newStyle.Border = style?.Border;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			style.Border  = OverrideBorder (style.Border, root.Element(_ns + "Border" ));
			style.Padding = OverridePadding(style.Padding,root.Element(_ns + "Padding"));

			return style;			
		}

		private TableRowStyle OverrideTableRowStyle(TableRowStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<TableRowStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			TableRowStyle newStyle = new TableRowStyle();
			newStyle.Base = style;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			style.Padding   = OverridePadding(style.Padding, root.Element(_ns + "Padding"));
			style.BackColor = OverrideColor(style.BackColor, root.Element(_ns + "BackColor"));

			return style;			
		}

		private TableCellStyle OverrideTableCellStyle(TableCellStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<TableCellStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			TableCellStyle newStyle = new TableCellStyle();
			newStyle.Base = style;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			style.Padding = OverridePadding(style.Padding,root.Element(_ns + "Padding"));

			return style;			
		}

		private BulletStyle OverrideBulletStyle(BulletStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<BulletStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			BulletStyle newStyle = new BulletStyle();
			newStyle.Base = style;
			newStyle.BulletText = style?.BulletText;
			newStyle.NumberStyle = style?.NumberStyle ?? ListNumberStyle.Bullet;
			newStyle.StartAt = style?.StartAt ?? 1;
			newStyle.Font = style?.Font;
			newStyle.Color = style?.Color;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			string bulletText = LoadString(root.Element(_ns + "BulletText"));
			if(!string.IsNullOrWhiteSpace(bulletText)) style.BulletText = bulletText;
			
			string numberStyleString = LoadString(root.Element(_ns + "NumberStyle"));
			if(!string.IsNullOrWhiteSpace(numberStyleString))
			{
				ListNumberStyle numberStyle = style.NumberStyle;
				bool ok = Enum.TryParse<ListNumberStyle>(numberStyleString, out numberStyle);
				style.NumberStyle = numberStyle;
			}

			int? start = LoadInt(root.Element(_ns + "StartAt"));
			if(start != null) style.StartAt = start.Value;

			style.Font    = OverrideFont (style.Font,      root.Element(_ns + "Font"   ));
			style.Color   = OverrideColor(style.Color,     root.Element(_ns + "Color"  ));
			style.Padding = OverridePadding(style.Padding, root.Element(_ns + "Padding"));
			return style;			
		}

		private LineStyle OverrideLineStyle(LineStyle style, XElement root)
		{
			if(root == null) return style;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				style = GetStyle<LineStyle>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return style;

			LineStyle newStyle = new LineStyle();
			newStyle.Base = style;
			newStyle.Thickness = style?.Thickness ?? 0;
			newStyle.Color = style?.Color;
			newStyle.Padding = style?.Padding;
			newStyle.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newStyle.LinePosition = ((IXmlLineInfo)root).LinePosition;

			style = newStyle;
			FillDefaults(style);

			int? thickness = LoadInt(root.Element(_ns + "Thickness"));
			if(thickness != null) style.Thickness = thickness.Value;

			style.Color   = OverrideColor  (style.Color,   root.Element(_ns + "Color"  ));
			style.Padding = OverridePadding(style.Padding, root.Element(_ns + "Padding"));

			return style;
		}

		private Style.Font OverrideFont(Style.Font font, XElement root)
		{
			if(root == null) return font;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				font = GetStyle<Style.Font>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return font;

			Style.Font newFont = new Style.Font();
			newFont.Base = font;
			newFont.FamilyName = font?.FamilyName;
			newFont.Size = font?.Size ?? 0;
			newFont.Weight = font?.Weight ?? 0;
			newFont.Bold = font?.Bold ?? false;
			newFont.Italic = font?.Italic ?? false;
			newFont.Underline = font?.Underline ?? false;
			newFont.Strikeout = font?.Strikeout ?? false;
			newFont.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newFont.LinePosition = ((IXmlLineInfo)root).LinePosition;
			
			font = newFont;
			FillDefaults(font);

			string familyName = LoadString (root.Element(_ns + "FamilyName"));
			int? size         = LoadInt    (root.Element(_ns + "Size"      ));
			bool? bold        = LoadBoolean(root.Element(_ns + "Bold"      ));
			bool? italic      = LoadBoolean(root.Element(_ns + "Italic"    ));
			bool? underline   = LoadBoolean(root.Element(_ns + "Underline" ));

			if(familyName != null) font.FamilyName = familyName;
			if(size       != null) font.Size       = size     .Value;
			if(bold       != null) font.Bold       = bold     .Value;
			if(italic     != null) font.Italic     = italic   .Value;
			if(underline  != null) font.Underline  = underline.Value;

			return font;
		}

		private Color OverrideColor(Color color, XElement root)
		{
			if(root == null) return color;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				color = GetStyle<Color>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return color;

			Color newColor = new Color();
			newColor.Base = color;
			newColor.Red   = color?.Red   ?? 0;
			newColor.Green = color?.Green ?? 0;
			newColor.Blue  = color?.Blue  ?? 0;
			newColor.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newColor.LinePosition = ((IXmlLineInfo)root).LinePosition;

			color = newColor;
			FillDefaults(color);

			double? red   = LoadDouble(root.Element(_ns + "Red"  ));
			double? green = LoadDouble(root.Element(_ns + "Green"));
			double? blue  = LoadDouble(root.Element(_ns + "Blue" ));

			if(red   != null) color.Red   = red  .Value;
			if(green != null) color.Green = green.Value;
			if(blue  != null) color.Blue  = blue .Value;

			return color;
		}

		private Border OverrideBorder(Border border, XElement root)
		{
			if(root == null) return border;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				border = GetStyle<Border>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return border;

			Border newBorder = new Border();
			newBorder.Base = border;
			newBorder.Thickness = border?.Thickness ?? 0;
			newBorder.Parts = border?.Parts ?? BorderPart.None;
			newBorder.Color = border?.Color;
			newBorder.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newBorder.LinePosition = ((IXmlLineInfo)root).LinePosition;

			border = newBorder;
			FillDefaults(border);

			int? thickness = LoadInt(root.Element(_ns + "Stroke")?.Element(_ns + "Thickness"));
			if(thickness != null) border.Thickness = thickness.Value;

			border.Color = OverrideColor(border.Color, root.Element(_ns + "Color"));

			XElement parts = root.Element(_ns + "Parts");
			if(parts != null)
			{
				bool? left   = LoadBoolean(parts.Element(_ns + "Left"           ));
				bool? bottom = LoadBoolean(parts.Element(_ns + "Bottom"         ));
				bool? right  = LoadBoolean(parts.Element(_ns + "Right"          ));
				bool? top    = LoadBoolean(parts.Element(_ns + "Top"            ));
				bool? horz   = LoadBoolean(parts.Element(_ns + "InnerHorizontal"));
				bool? vert   = LoadBoolean(parts.Element(_ns + "InnerVertical"  ));

				if(left != null)
				{
					if(left.Value)
						border.Parts |= BorderPart.Left;
					else
						border.Parts &= ~BorderPart.Left;
				}
				if(bottom != null)
				{
					if(bottom.Value)
						border.Parts |= BorderPart.Bottom;
					else
						border.Parts &= ~BorderPart.Bottom;
				}
				if(right != null)
				{
					if(right.Value)
						border.Parts |= BorderPart.Right;
					else
						border.Parts &= ~BorderPart.Right;
				}
				if(top != null)
				{
					if(top.Value)
						border.Parts |= BorderPart.Top;
					else
						border.Parts &= ~BorderPart.Top;
				}
				if(horz != null)
				{
					if(horz.Value)
						border.Parts |= BorderPart.InnerHorizontal;
					else
						border.Parts &= ~BorderPart.InnerHorizontal;
				}
				if(vert != null)
				{
					if(vert.Value)
						border.Parts |= BorderPart.InnerVertical;
					else
						border.Parts &= ~BorderPart.InnerVertical;
				}
			}

			return border;
		}

		private Padding OverridePadding(Padding padding, XElement root)
		{
			if(root == null) return padding;

			//	If the XML has a ref attribute then replace the original
			//	style with the referenced style
			string id = root.Attribute("ref")?.Value;
			if(id != null)
				padding = GetStyle<Padding>(id);

			//	If the XML has no sub-elements then we're done, otherwise override
			//	whatever parts are specified
			if(!root.HasElements) return padding;

			Padding newPadding = new Padding();
			newPadding.Base = padding;
			newPadding.Left   = padding?.Left   ?? 0;
			newPadding.Bottom = padding?.Bottom ?? 0;
			newPadding.Right  = padding?.Right  ?? 0;
			newPadding.Top    = padding?.Top    ?? 0;
			newPadding.LineNumber   = ((IXmlLineInfo)root).LineNumber;
			newPadding.LinePosition = ((IXmlLineInfo)root).LinePosition;
			
			padding = newPadding;
			FillDefaults(padding);

			int? left   = LoadInt(root.Element(_ns + "Left"  ));
			int? bottom = LoadInt(root.Element(_ns + "Bottom"));
			int? right  = LoadInt(root.Element(_ns + "Right" ));
			int? top    = LoadInt(root.Element(_ns + "Top"   ));

			if(left   != null) padding.Left   = left  .Value;
			if(bottom != null) padding.Bottom = bottom.Value;
			if(right  != null) padding.Right  = right .Value;
			if(top    != null) padding.Top    = top   .Value;

			return padding;
		}

		/// <exception cref="InvalidDesignException"></exception>
		public static void ValidateXML(Stream docStream)
		{
			_designSchemaXML = GetDesignSchemaXML();

			XDocument doc = XDocument.Load(docStream, LoadOptions.SetLineInfo);
			string docNamespace = doc.Root.GetDefaultNamespace().ToString();
			string schemaNamespace = _designSchemaXML.TargetNamespace;
			if(docNamespace != schemaNamespace)
				throw new InvalidDataException($"The design namespace '{docNamespace}' does not match the schema namespace '{schemaNamespace}'.");
			XmlSchemaSet schemas = new XmlSchemaSet();
			schemas.Add(_designSchemaXML);
			doc.Validate(schemas, OnSchemaValidationEvent);
		}

		private static void OnSchemaValidationEvent(object sender, System.Xml.Schema.ValidationEventArgs e)
		{
			throw new InvalidDesignException(e.Exception);
			//TODO: collect all errors before throwing?
		}

#if JSON_SCHEMA
		public static void ValidateJSON(Stream designStream)
		{
			//	Get the schema. We do this outside the try/catch block because
			//	exceptions here are system errors and not design file errors
			JSchema schema = GetDesignSchemaJSON();

			StreamReader srDesign = null;
			JSchemaValidatingReader validatingReader = null;
			try
			{
				//	Attach a reader to the stream, but don't let it close the stream
				//	when it's finished because we don't own the stream
				srDesign = new StreamReader(designStream, Encoding.Default, true, 4096, true);
				JsonTextReader jrDesign = new JsonTextReader(srDesign);

				validatingReader = new JSchemaValidatingReader(jrDesign);
				validatingReader.Schema = schema;
				validatingReader.ValidationEventHandler += OnJsonValidationError;

				JsonSerializer serializer = new JsonSerializer();
				object o = serializer.Deserialize(validatingReader);
			}
			catch(JsonSerializationException ex)
			{
				throw new InvalidDesignException(ex);
			}
			catch(JsonReaderException ex)
			{
				throw new InvalidDesignException(ex);
			}
			catch(JSchemaReaderException ex)
			{
				throw new InvalidDesignException(ex);
			}
			catch(JSchemaException ex)
			{
				throw new InvalidDesignException(ex);
			}
			finally
			{
				srDesign?.Dispose();
				if(validatingReader != null)
					validatingReader.ValidationEventHandler -= OnJsonValidationError;
			}
		}

		private static void OnJsonValidationError(object sender, SchemaValidationEventArgs e)
		{
			throw new InvalidDesignException(e.ValidationError);
		}
#endif // JSON_SCHEMA

		public static XmlSchema GetDesignSchemaXML()
		{
			lock(SchemaLocker)
			{
				if(_designSchemaXML == null)
				{
					//	Load the schema from the assembly resource
					Stream xsdStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Demon.Report.report-design.xsd");
					_designSchemaXML = XmlSchema.Read(xsdStream, OnSchemaValidationEvent);
					_designSchemaXML.ElementFormDefault = XmlSchemaForm.Qualified;
				}
				return _designSchemaXML;
			}
		}

#if JSON_SCHEMA
		public static JSchema GetDesignSchemaJSON()
#else
		public static string GetDesignSchemaJSON()
#endif
		{
			lock(SchemaLocker)
			{
				if(_designSchemaJSON == null)
				{
					//	Load the schema from the assembly resource
					Stream schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Demon.Report.report-design.json");
					using(StreamReader srSchema = new StreamReader(schemaStream, true))
					{
#if JSON_SCHEMA
						JsonTextReader jrSchema = new JsonTextReader(srSchema);
						_designSchemaJSON = JSchema.Load(jrSchema);
#else
						_designSchemaJSON = srSchema.ReadToEnd();
#endif
					}
				}
				return _designSchemaJSON;
			}
		}

		public int? LoadInt(XElement element)
		{
			if(element == null) return null;
			int i;
			bool ok = int.TryParse(element.Value, out i);
			if(ok)
				return i;
			else
				return null;
		}

		public int? LoadInt(XAttribute attribute)
		{
			if(attribute == null) return null;
			int i;
			bool ok = int.TryParse(attribute.Value, out i);
			if(ok)
				return i;
			else
				return null;
		}

		public string LoadString(XElement element)
		{
			return element?.Value;
		}

		public string LoadString(XAttribute attribute)
		{
			return attribute?.Value;
		}

		public double? LoadDouble(XElement element)
		{
			if(element == null) return null;
			double d;
			bool ok = double.TryParse(element.Value, out d);
			if(ok)
				return d;
			else
				return null;
		}

		public double? LoadDouble(XAttribute attribute)
		{
			if(attribute == null) return null;
			double d;
			bool ok = double.TryParse(attribute.Value, out d);
			if(ok)
				return d;
			else
				return null;
		}

		public float? LoadFloat(XElement element)
		{
			if(element == null) return null;
			float f;
			bool ok = float.TryParse(element.Value, out f);
			if(ok)
				return f;
			else
				return null;
		}

		public float? LoadFloat(XAttribute attribute)
		{
			if(attribute == null) return null;
			float f;
			bool ok = float.TryParse(attribute.Value, out f);
			if(ok)
				return f;
			else
				return null;
		}

		public bool? LoadBoolean(XElement element)
		{
			if(element == null) return null;
			bool b;
			bool ok = bool.TryParse(element.Value, out b);
			if(ok)
				return b;
			else
				return null;
		}

		public bool? LoadBoolean(XAttribute attribute)
		{
			if(attribute == null) return null;
			bool b;
			bool ok = bool.TryParse(attribute.Value, out b);
			if(ok)
				return b;
			else
				return null;
		}

		public T LoadEnum<T>(XElement element) where T : struct
		{
			if(element == null) return default(T);

			string s = element?.Value;
			if(s != null)
			{
				T t;
				bool ok = Enum.TryParse<T>(s, out t);
				return t;
			}
			return default(T);
		}

		public T LoadEnum<T>(XAttribute attribute) where T : struct
		{
			if(attribute == null) return default(T);

			string s = attribute?.Value;
			if(s != null)
			{
				T t;
				bool ok = Enum.TryParse<T>(s, out t);
				return t;
			}
			return default(T);
		}
	}
}
