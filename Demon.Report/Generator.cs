using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Demon.Core.Domain;
using Demon.Core.Subscription;
using Demon.Core.Interface.Data;
using Demon.Core.Interface.Services;
using Demon.Path;
using Demon.Calculate;
using Demon.Report.Types;
using Demon.Font;
using Demon.Core;

namespace Demon.Report
{
	public class Generator : ITrace
	{
		private User _user;
		private Client _client;
		private IUnitOfWork _unitOfWork;
		public IUnitOfWork UnitOfWork { get { return _unitOfWork; }} //TODO: can this be internal?

		private string _documentId;
		private int _documentVersion;
		private DateTimeOffset _timestamp;
		private ReportDesign _design;
		private string _reportMimeType;
		private ReportLayout _report;

		internal string DocumentId { get { return _documentId; }}
		internal ReportDesign ReportDesign { get { return _design; }}
		public string ReportMimeType { get { return _reportMimeType; }}
		internal ReportLayout Report { get { return _report; }}

		private Resolver _resolver;
		public Resolver Resolver { get { return _resolver; }}

		private FontCache _fonts;
		private Dictionary<PropertyOwnerType, Dictionary<string, string>> _propertyCache;
		private IPhotoService _photoService;
		private IResourceService _resourceService;
		
		private Dictionary<string, List<string>> _missingPhotos = new Dictionary<string, List<string>>();
		private List<string> _missingResources = new List<string>();

		private TraceContextStack _traceContext;
		//	For development-time debugging, to help identify layouts
		//	created and copied during page layout.
		private int _nextTrackingId = 0;
		public int NextTrackingId { get { return Interlocked.Increment(ref _nextTrackingId); }}

		private Demon.Path.TracePathDelegate _tracePathDelegate;

		private ILog _logger;
		public ILog Logger { get { return _logger; }} //TODO: can this be internal?


		/// <summary>
		/// A generator is only good for a single use. If you want to create many
		/// reports then you must create a new generator for each one.
		/// </summary>
		public Generator(
			User user , Client client,
			string documentId, int documentVersion, DateTimeOffset timestamp,
			string fontDirectory,
			IPhotoService photoService, IResourceService pictureService,
			IUnitOfWork unitOfWork, ILog logger,
			bool traceLayout, bool traceText, bool tracePath, bool traceOutline)
		{
			_user = user;
			_client = client;

			_documentId = documentId;
			_documentVersion = documentVersion;
			_timestamp = timestamp;

			_unitOfWork = unitOfWork;
			_logger = logger;
			_fonts = new FontCache(fontDirectory);
			_photoService = photoService;
			_resourceService = pictureService;

			//	Layout tracing is done directly by calling Trace, but path tracing
			//	requires a delegate that can be sent to the path library
			_traceContext = new TraceContextStack(0, traceLayout, traceText, tracePath, traceOutline);
			_tracePathDelegate = new Demon.Path.TracePathDelegate(TracePathResolution);
		}

		public Stream Generate(
			Stream designFile, int[] chapters, bool drawRules, bool drawPageBoxes,
			string mimeType,
			string photoUri, string resourceUri) //TODO: return byte[]?
		{
			_reportMimeType = mimeType;
			_fonts.Load();

			//	Set up a reference resolver
			DocumentStructure structure = new DocumentStructure(_documentId, _unitOfWork);
			DocTaggedObjects taggedObjects = new DocTaggedObjects(_documentId, _unitOfWork);
			_resolver = new Resolver(structure, taggedObjects, _tracePathDelegate);

			//	Load the report structure as it's designed
			_design = new ReportDesign(designFile,_logger);
			_report = _design.Load(this);
			_report.Validate();

			_report.Subset(chapters);

			//	Load properties from the database and prepare them for matching
			//	against expansion strings in text
			LoadProperties();

			//	Resolve all source references from ref-to-template-object
			//	to ref-to-concrete-object, duplicating layouts as necessary
			TraceLayoutActivity("Resolve references");
			_report.ResolveSublayoutReferences();
			TraceLayoutActivity("Validate conditions");
			_report.ValidateConditions();
			TraceLayoutActivity("Apply static conditions");
			_report.ApplyStaticLayoutConditions();

			//	Load content
			TraceLayoutActivity("Load content");
			_report.LoadContent();
			_report.MergeContent(null);

			//	Remove content based on conditions
			TraceLayoutActivity("Apply dynamic conditions");
			_report.ApplyDynamicConditions();
//			TraceLayoutActivity("Remove empty layouts");
//			layout.RemoveEmptyLayouts();
//			TraceLayoutActivity("Redraft");
//			layout.Redraft();

//			//	Measure and cut content
//			TraceLayoutActivity("Measure and cut content");
//			List<PageLayout> pages = layout.LayOut();

			ReportRenderer renderer = null;
			switch(_reportMimeType)
			{
				case Demon.Core.MimeType.PDF:
					renderer = new PDF();
					break;
				case Demon.Core.MimeType.Word:
					renderer = new Word();
					break;
				case Demon.Core.MimeType.HTML:
					renderer = new HTML();
					break;
				case Demon.Core.MimeType.SVG:
					renderer = new SVG();
					break;
				default:
					throw new ArgumentOutOfRangeException($"Unsupported report content type '{_reportMimeType}'.");
			}

			Stream report = renderer.Render(
				_report, _documentId, _design.Id, _design.Name,
				_documentVersion, _timestamp,
				photoUri, resourceUri, drawRules, drawPageBoxes, this, this); 

			return report;
		}

		/// <summary>
		/// Load client, user and tenant properties from the database and cache
		/// them so that they're available for efficient expansion in text content.
		/// </summary>
		private void LoadProperties()
		{
			_propertyCache = new Dictionary<PropertyOwnerType, Dictionary<string, string>>();
			_propertyCache.Add(PropertyOwnerType.Tenant,   new Dictionary<string, string>());
			_propertyCache.Add(PropertyOwnerType.Client,   new Dictionary<string, string>());
			_propertyCache.Add(PropertyOwnerType.User,     new Dictionary<string, string>());
			_propertyCache.Add(PropertyOwnerType.Document, new Dictionary<string, string>());
			
			LoadProperties(PropertyOwnerType.Client,   _client.Id,                "client");
			LoadProperties(PropertyOwnerType.Tenant,   _user.TenantId.ToString(), "tenant");
			LoadProperties(PropertyOwnerType.User,     _user.Id.ToString(),       "user");
			LoadProperties(PropertyOwnerType.Document, _documentId,               "doc");
		}

		private void LoadProperties(PropertyOwnerType ownerType, string ownerId, string prefix)
		{
			Dictionary<string, string> subcache = _propertyCache[ownerType];

			List<Core.Domain.Property> props = _unitOfWork.Repository<Core.Domain.Property>()
				.Query(p => p.OwnerType == ownerType
										&&
										p.OwnerId == ownerId)
				.Get(false)
				.ToList();
			foreach(Core.Domain.Property prop in props)
				subcache.Add(prop.Name, prop.Value);
		}

		internal string EvaluateDocumentProperty(PageLayout layout, DocumentProperty prop)
		{
			switch(prop.Scope)
			{
				case PropertyOwnerType.None:
					return EvaluateSystemProperty(layout, prop.Name, prop.Format);

				case PropertyOwnerType.Document:
				case PropertyOwnerType.Client:
				case PropertyOwnerType.Tenant:
				case PropertyOwnerType.User:
					return EvaluateScopedProperty(prop.Scope, prop.Name, prop.Format);
			
				default:
					return null;
			}
		}

		private string EvaluateSystemProperty(PageLayout layout, string name, string format)
		{
			switch(name)
			{
				case "PageNumber": return layout?.Page?.PageNumber.ToString(format);
				case "PageCount": return layout?.Page.PageCount.ToString(format);
				case "DocumentTitle": return _design.Name;
				case "ClientName": return _client.Name;
				case "Today": return DateTime.Now.ToString(format);
				default: return null;
			}
		}

		private string EvaluateScopedProperty(PropertyOwnerType scope, string name, string format)
		{
			//TODO: In the current implementation it makes no sense to pass a format
			//specification here, because user-defined properties are always stored
			//as strings. But in the future we might support typed properties such
			//as dates and numbers.

			//	The scope sub-cache is guaranteed to exist because we created it in
			//	LoadProperties, but the named property is not guaranteed to exist.
			string value = null;
			bool found = _propertyCache[scope].TryGetValue(name, out value);
			return value;
		}

        /// <summary>
        /// Resolve a reference into zero or one concrete object.
        /// </summary>
        /// Resolution into an object involves a call into the database.
        internal IContentSource ResolveOne(Reference reference)
		{
			switch(reference.Type)
			{
				case ContentSourceType.TextEntry:    return ResolveOne<TextEntry>       (reference);
				case ContentSourceType.StaticText:   return ResolveOne<StaticText>      (reference);
				case ContentSourceType.MultiSelect:  return ResolveOne<MultiSelectList> (reference);
				case ContentSourceType.SingleSelect: return ResolveOne<SingleSelectList>(reference);
				case ContentSourceType.Checkbox:     return ResolveOne<Checkbox>        (reference);
				case ContentSourceType.RadioButton:  return ResolveOne<RadioButton>     (reference);
				case ContentSourceType.Section:      return ResolveOne<Section>         (reference);
				case ContentSourceType.DocTag:       return ResolveOne<DocTag>          (reference);
				case ContentSourceType.PhotoList:    return ResolveOne<PhotoList>       (reference);
				case ContentSourceType.Photo:        return ResolveOne<Photo>           (reference);
				default: return null;
			}
		}

		/// <summary>
		/// Resolve a reference into zero or one concrete object.
		/// </summary>
		/// Resolution into an object involves a call into the database.
		internal T ResolveOne<T>(Reference reference) where T : class, IContentSource
		{
			return _unitOfWork.Repository<T>().GetById(reference.Id);
		}

        /// <summary>
        /// Resolve a path into zero or one concrete object.
        /// </summary>
        /// Resolution into an object involves a call into the database.
        /// <exception cref="AmbiguousPathException"></exception>
        internal T ResolveOne<T>(Path.Path path, Reference context) where T : class, IContentSource
		{
			Reference reference = ResolveOne(path, context);
			if(reference == null) return null;
			return ResolveOne<T>(reference);
		}

		/// <summary>
		/// Resolve a path into zero or more concrete objects. The returned
		/// list is sorted by ordinal.
		/// </summary>
		/// Resolution into an object involves a call into the database.
		internal List<T> ResolveMany<T>(Path.Path path, Reference context) where T : class, IContentSource
		{
			List<Reference> references = ResolveMany(path, context);
			List<string> ids = references.Select(r => r.Id).ToList();
			return _unitOfWork.Repository<T>()
				.Query(t => ids.Contains(t.Id))
				.Get()
				.ToList();
		}

		/// <summary>
		/// Resolve a path into zero or one reference.
		/// </summary>
		/// Resolution into a reference is done in the in-memory
		/// document structure, and does not call into the database.
		/// <exception cref="AmbiguousPathException"></exception>
		internal Reference ResolveOne(Path.Path path, Reference context)
		{
			List<Reference> resolved = path.Resolve(context, _resolver);
			switch(resolved.Count)
			{
				case 0:
					return null;
					
				case 1:
					return resolved[0];
					
				default:
					throw new AmbiguousPathException(
						$"Path resolves to {resolved.Count} objects",
						path.ToString(), context.ToString(), path.LineNumber, path.LinePosition);
			}
		}

		/// <summary>
		/// Resolve a path into zero or more references. The
		/// returned list is sorted by ordinal.
		/// </summary>
		/// Resolution into a reference is done in the in-memory
		/// document structure, and does not call into the database.
		internal List<Reference> ResolveMany(Path.Path path, Reference context)
		{
			return path.Resolve(context, _resolver);
		}

        internal Asset ResolveAssetExtension(Reference reference)
        {
            SectionAsset sectionAsset = _unitOfWork.Repository<SectionAsset>().Query(s => s.SectionId == reference.Id).GetSingle();
            if (sectionAsset != null)
            {
               return _unitOfWork.Repository<Asset>().GetById(sectionAsset.AssetId);
            }
            return null;
        }

        internal decimal Calculate(Calculation calculation)
		{
			//	If the variables haven't been loaded yet, load them now
			if(calculation.CalculationVariables.Count == 0)
			{
				calculation.CalculationVariables = new List<CalculationVariable>(
					_unitOfWork.Repository<CalculationVariable>()
						.Query(v => v.CalculationId == calculation.Id)
						.Get(true));
			}

			Dictionary<string, decimal> variables = new Dictionary<string, decimal>();
			foreach(CalculationVariable variable in calculation.CalculationVariables)
				variables.Add(variable.VarName, variable.Value.Value);
			decimal value = Calculator.Evaluate(calculation.Formula, variables);
			return value;
		}

		/// <summary>
		/// Logs layout trace information if the TraceLayout flag is set.
		/// Automatically includes information about the calling method and
		/// the current layout, so you don't need to include that stuff in
		/// the text argument.
		/// </summary>
		/// <param name="format">Will be formatted with the args only if the
		/// TraceLayout flag is set.
		/// </param>
		/// <param name="args">Will be evaluated only if the TraceLayout flag is set.
		/// </param>
		/// <param name="skipFrames">How many stack frames to skip when looking
		/// for the method name.
		/// </param>
		/// To minimize the performance cost of tracing, we use
		/// string.Format(text,args) to construct the trace message inside Trace
		/// but only if the trace flag is set, rather than always construct the
		/// message in the caller. That is, we call Trace like this:
		///		Trace("values = {0} {1}", 0, value1, value2);
		///	rather than
		///		Trace($"values = {value1} {value2}", 0);
		public void TraceLayoutActivity(string format, int skipFrames = 0, params object[] args)
		{
			if(_traceContext.TraceLayout)
				Trace(format, skipFrames + 1, args);
		}

		/// <summary>
		/// Logs trace information while resolving template references if the
		/// TracePath flag is set. Automatically includes information about the
		/// calling method, so you don't need to include that stuff in the text
		/// argument.
		/// </summary>
		/// <param name="format">Will be formatted with the args only if the
		/// TracePath flag is set.
		/// </param>
		/// <param name="args">Will be evaluated only if the TracePath flag is set.
		/// </param>
		/// <param name="skipFrames">How many stack frames to skip when looking
		/// for the method name.
		/// </param>
		/// To minimize the performance cost of tracing, we use
		/// string.Format(text,args) to construct the trace message inside Trace
		/// but only if the trace flag is set, rather than always construct the
		/// message in the caller. That is, we call Trace like this:
		///		Trace("values = {0} {1}", 0, value1, value2);
		///	rather than
		///		Trace($"values = {value1} {value2}", 0);
		public void TracePathResolution(string text, int skipFrames = 0, params object[] args)
		{
			if(_traceContext.TracePath)
				Trace(text, skipFrames + 1, args);
		}

		/// <summary>
		/// Logs layout trace information if the TraceText flag is set.
		/// Automatically includes information about the calling method and
		/// the current layout, so you don't need to include that stuff in
		/// the text argument.
		/// </summary>
		/// <param name="format">Will be formatted with the args only if the
		/// TraceText flag is set.
		/// </param>
		/// <param name="args">Will be evaluated only if the TraceText flag is set.
		/// </param>
		/// <param name="skipFrames">How many stack frames to skip when looking
		/// for the method name.
		/// </param>
		/// To minimize the performance cost of tracing, we use
		/// string.Format(text,args) to construct the trace message inside Trace
		/// but only if the trace flag is set, rather than always construct the
		/// message in the caller. That is, we call Trace like this:
		///		Trace("values = {0} {1}", 0, value1, value2);
		///	rather than
		///		Trace($"values = {value1} {value2}", 0);
		public void TraceTextProcessing(string format, int skipFrames = 0, params object[] args)
		{
			if(_traceContext.TraceText)
				Trace(format, skipFrames + 1, args);
		}

		/// <summary>
		/// Unconditionally logs a message to the log file. Automatically
		/// includes information about the calling method, so you don't
		/// need to include that stuff in the text argument.
		/// </summary>
		/// <param name="format">Same as for String.Format.
		/// </param>
		/// <param name="args">Same as for String.Format.
		/// </param>
		/// <param name="skipFrames">How many stack frames to skip when looking
		/// for the method name.
		/// </param>
		/// To minimize the performance cost of tracing, we use
		/// string.Format(text,args) to construct the trace message inside Trace
		/// but only if the trace flag is set, rather than always construct the
		/// message in the caller. That is, we call Trace like this:
		///		Trace("values = {0} {1}", 0, value1, value2);
		///	rather than
		///		Trace($"values = {value1} {value2}", 0);
		public void Trace(string text, int skipFrames = 0, params object[] args)
		{
			StringBuilder sb = new StringBuilder();

			System.Diagnostics.StackFrame frame = new System.Diagnostics.StackFrame(skipFrames + 1, true);
			MethodBase method = frame.GetMethod();
			sb.Append(method.Name);
			sb.Append(" ");

			//	Get filename and line number if debugging info is available
			string filename = frame.GetFileName();
			if(filename != null)
			{
				sb.Append("[");
				filename = System.IO.Path.GetFileName(filename);
				sb.Append(filename);
				sb.Append(":");
				sb.Append(frame.GetFileLineNumber());
				sb.Append("]");
			}

			if(text != null)
			{
				sb.Append(" ");
				sb.AppendFormat(text, args);
			}

			_logger.Log(sb.ToString(),false);
		}

		/// <summary>
		/// Realize a font from the cache
		/// </summary>
		internal Font.Font GetFont(Demon.Report.Style.Font style)
		{
			Font.Font font = GetFont(style.FamilyName, style.Weight, style.Bold, style.Italic, style.Underline, style.Strikeout);
			return font;
		}

		/// <summary>
		/// Realize a font from the cache
		/// </summary>
		internal Font.Font GetFont(string familyName, int weight, bool bold, bool italic, bool underline, bool strikeout)
		{
			return _fonts.GetFont(familyName, weight, bold, italic, underline, strikeout);
		}

		public FontCache FontCache => _fonts;

		internal IPhotoService PhotoService { get { return _photoService; }}
		internal IResourceService ResourceService { get { return _resourceService; }}
		public Dictionary<string, List<string>> MissingPhotos { get { return _missingPhotos; }}
		public List<string> MissingResources { get { return _missingResources; }}
		
		internal void RecordMissingPhoto(string photoId)
		{
			if(_missingPhotos.ContainsKey(photoId)) return;

			List<string> context = new List<string>();
			GetObjectPath(ControlType.Photo, photoId, context);
			_missingPhotos.Add(photoId, context);
		}

		internal void RecordMissingResource(string resourceId)
		{
			string filename = _design.GetResourceFilename(resourceId);
			_missingResources.Add(filename);
		}

		/// <summary>
		/// Return a user-friendly navigation path from the
		/// root of a document to a particular control.
		/// </summary>
		/// <param name="path">The path is returned in this list. The list is
		/// sorted in reverse order, working up from the control to the document.
		/// </param>
		internal void GetObjectPath(ControlType controlType, string controlId, List<string> path)
		{
			switch(controlType)
			{
				case ControlType.Section:
				{
					Section section = _unitOfWork.Repository<Section>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(section.Title))
						path.Add(section.Title);

					if(section.ParentSectionId != null)
						GetObjectPath(ControlType.Section, section.ParentSectionId, path);
					break;
				}

				case ControlType.Checkbox:
				{
					Checkbox checkbox = _unitOfWork.Repository<Checkbox>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(checkbox.Caption))
						path.Add(checkbox.Caption);

					GetObjectPath(ControlType.MultiSelect, checkbox.ListId, path);
					break;
				}

				case ControlType.RadioButton:
				{
					RadioButton radioButton = _unitOfWork.Repository<RadioButton>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(radioButton.Caption))
						path.Add(radioButton.Caption);

					GetObjectPath(ControlType.SingleSelect, radioButton.ListId, path);
					break;
				}

				case ControlType.MultiSelect:
				{
					MultiSelectList list = _unitOfWork.Repository<MultiSelectList>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(list.Caption))
						path.Add(list.Caption);

					GetObjectPath(ControlType.Form, list.FormId, path);
					break;
				}

				case ControlType.SingleSelect:
				{
					SingleSelectList list = _unitOfWork.Repository<SingleSelectList>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(list.Caption))
						path.Add(list.Caption);

					GetObjectPath(ControlType.Form, list.FormId, path);
					break;
				}

				case ControlType.Photo:
				{
					Photo photo = _unitOfWork.Repository<Photo>().GetById(controlId);
					string caption = PhotoTableLayout.GetPhotoCaption(photo, this);
					if(!string.IsNullOrWhiteSpace(caption))
						path.Add(caption);

					GetObjectPath(ControlType.PhotoList, photo.ListId, path);
					break;
				}

				case ControlType.PhotoList:
				{
					PhotoList list = _unitOfWork.Repository<PhotoList>().GetById(controlId);
					//	There's nothing useful to say about the photo list itself

					GetObjectPath(ControlType.Form, list.FormId, path);
					break;
				}

				case ControlType.Form:
				{
					Form form = _unitOfWork.Repository<Form>().GetById(controlId);
					if(!string.IsNullOrWhiteSpace(form.Description))
						path.Add(form.Description);

					GetObjectPath((ControlType)form.TriggerType, form .TriggerId, path);
					break;
				}
			}
		}

		/// <summary>
		/// Push a trace context onto the generator's stack. Stacked contexts are
		/// cumulative in the positive sense: a newer context can set a trace flag
		/// but it cannot clear it.
		/// </summary>
		/// <param name="context"></param>
		public void PushTraceContext(TraceContext context)
		{
			_traceContext.Push(context);
		}

		public void PopTraceContext()
		{
			_traceContext.Pop();
		}

		/// <summary>
		/// Trace layout actions such as drafting and page break handling on all layouts.
		/// </summary>
		public bool TraceLayout { get { return _traceContext.TraceLayout; }}
		/// <summary>
		/// Trace text processing actions such as working out line breaks on all layouts.
		/// </summary>
		public bool TraceText { get { return _traceContext.TraceText; }}
		/// <summary>
		/// Trace resolution of template references to object references.
		/// </summary>
		public bool TracePath { get { return _traceContext.TracePath; }}
		/// <summary>
		/// Trace layout outlines.
		/// </summary>
		public bool TraceOutline { get { return _traceContext.TraceOutline; }}

		/// <exception cref="InvalidDesignException"></exception>
		public static void ValidateDesignXML(Stream designFile)
		{
			ReportDesign.ValidateXML(designFile);

			//TODO: add application-level validation: for example, does the design have
			//references to objects that don't appear in the document template, etc.
		}

#if JSON_SCHEMA
		/// <exception cref="InvalidDesignException"></exception>
		public static void ValidateDesignJSON(Stream designFile)
		{
			ReportDesign.ValidateJSON(designFile);

			//TODO: add application-level validation: for example, does the design have
			//references to objects that don't appear in the document template, etc.
		}
#endif // JSON_SCHEMA

		public static string GetDesignSchemaXML()
		{
			System.Xml.Schema.XmlSchema schema = ReportDesign.GetDesignSchemaXML();
			MemoryStream stream = new MemoryStream();
			schema.Write(stream);
			return Encoding.UTF8.GetString(stream.GetBuffer());
		}

		public static string GetDesignSchemaJSON()
		{
			return ReportDesign.GetDesignSchemaJSON().ToString();
		}
	}
}
