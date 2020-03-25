using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Demon.Core.Domain;
using Demon.Path;

namespace Demon.Report
{
	internal class ConditionSet
	{
		private Layout _layout;

		//	Conditions break down into three major types:
		//
		//		1.	Static layout conditions. Used to decide whether a layout
		//				should be included in the report. Evaluated with respect
		//				to content objects such as checkboxes, radio buttons etc.
		//				Evaluated before the initial draft stage, after source
		//				references have been resolved.
		//
		//		2.	Content conditions. Used to decide whether a content object
		//				should be included in a layout's content. Simmilar to
		//				static layout conditions. Evaluated with respect to content
		//				objects such as checkboxes, radio buttons etc. Evaluated
		//				during the initial draft stage, after references have been
		//				resolved and while layouts are loading their content.
		//
		//		3.	Dynamic layout conditions. Also used to decide whether a
		//				layout should be included in the report. Evaluated with
		//				respect to the target layout or any other layout. Evaluated
		//				after the initial draft, after layouts have loaded their
		//				content. These conditions remove layouts, necessitating
		//				the redraft.

		//	Static layout conditions
		private List<OptionSelectedCondition> _optionSelectedConditions = new List<OptionSelectedCondition>();
		private List<DocTagCondition> _doctagConditions = new List<DocTagCondition>();

		//	Distilled representations of static layout conditions. The
		//	conditions are distilled for convenience and performance. At design
		//	time a static layout condition can indicate a target path, and that
		//	could resolve to any number of objects at generate time. And any
		//	object could be a target of any number of conditions. Distillation
		//	works out all of these details and stores the results in a format
		//	that's easier to evaluate than the raw condition format.
		private Dictionary<OptionSelectedCondition, List<Reference>> _requireSelected;
		private Dictionary<OptionSelectedCondition, List<Reference>> _prohibitSelected;
		private Dictionary<DocTagCondition, List<Reference>> _requireDoctags;
		private Dictionary<DocTagCondition, List<Reference>> _prohibitDoctags;

		//	Dynamic layout conditions
		private List<EmptyLayoutCondition> _emptyLayoutConditions = new List<EmptyLayoutCondition>();
		private List<ItemCountCondition> _itemCountConditions = new List<ItemCountCondition>();
		private List<PhotoCountCondition> _photoCountConditions = new List<PhotoCountCondition>();
		
		//	Need to expose these to the Layout class. See the comment in Layout.ApplyDynamicConditions.
		public IReadOnlyList<EmptyLayoutCondition> EmptyLayoutConditions
		{
			get { return _emptyLayoutConditions; }
		}
		public IReadOnlyList<ItemCountCondition> ItemCountConditions
		{
			get { return _itemCountConditions; }
		}
		public IReadOnlyList<PhotoCountCondition> PhotoCountConditions
		{
			get { return _photoCountConditions; }
		}

		//	Content conditions. These don't need distillation because they don't
		//	define their own targets - the target is always supplied at evaluation
		//	time.
		private List<ContentSelectedCondition> _contentSelectedConditions = new List<ContentSelectedCondition>();
		private List<ContentDocTagCondition> _contentDocTagConditions = new List<ContentDocTagCondition>();

		public ConditionSet(Layout layout)
		{
			_layout = layout;
		}

		/// <summary>
		/// Copy constructor used during layout expansion and page break handling.
		/// </summary>
		public ConditionSet(ConditionSet src)
		{
			_layout = src._layout;

			foreach(OptionSelectedCondition condition in src._optionSelectedConditions)
				_optionSelectedConditions.Add(new OptionSelectedCondition(condition));

			foreach(DocTagCondition condition in src._doctagConditions)
				_doctagConditions.Add(new DocTagCondition(condition));

			foreach(EmptyLayoutCondition condition in src._emptyLayoutConditions)
				_emptyLayoutConditions.Add(new EmptyLayoutCondition(condition));

			foreach(ItemCountCondition condition in src._itemCountConditions)
				_itemCountConditions.Add(new ItemCountCondition(condition));

			foreach(PhotoCountCondition condition in src._photoCountConditions)
				_photoCountConditions.Add(new PhotoCountCondition(condition));

			foreach(ContentSelectedCondition condition in src._contentSelectedConditions)
				_contentSelectedConditions.Add(new ContentSelectedCondition(condition));

			foreach(ContentDocTagCondition condition in src._contentDocTagConditions)
				_contentDocTagConditions.Add(new ContentDocTagCondition(condition));

			//	This new condition set must distill its own conditions. It can't
			//	just copy the source set's distillation.

			SetTrackingId();
		}

		public void Load(XElement root)
		{
			if(root != null)
			{
				foreach (XElement node in root.Elements())
				{
					Condition condition = _layout.Generator.ReportDesign.LoadCondition(node);
					if(condition == null)
						continue;
					else if(condition is OptionSelectedCondition)
						_optionSelectedConditions.Add(condition as OptionSelectedCondition);
					else if(condition is DocTagCondition)
						_doctagConditions.Add(condition as DocTagCondition);
					else if(condition is EmptyLayoutCondition)
						_emptyLayoutConditions.Add(condition as EmptyLayoutCondition);
					else if(condition is ItemCountCondition)
						_itemCountConditions.Add(condition as ItemCountCondition);
					else if(condition is PhotoCountCondition)
						_photoCountConditions.Add(condition as PhotoCountCondition);
					else if(condition is ContentSelectedCondition)
						_contentSelectedConditions.Add(condition as ContentSelectedCondition);
					else if(condition is ContentDocTagCondition)
						_contentDocTagConditions.Add(condition as ContentDocTagCondition);
				}
			}

			//	Every layout whose source object type is selectable must have
			//	an option-selected condition: if it has no such condition designed
			//	in, then give it an implicit one with default settings. This means
			//	that the designer doesn't have to apply an explicit condition to
			//	every layout just to get the natural default behaviour of "only
			//	show me if my source is selected."
			if(_optionSelectedConditions.Count == 0 && IsSelectable(_layout.SourcePath.TargetType))
			{
				OptionSelectedCondition condition = new OptionSelectedCondition(_layout.TrackingInfo.LineNumber, _layout.TrackingInfo.LinePosition);
				condition.Source = Path.Path.Empty;
				condition.Require = true;
				condition.Prohibit = false;
				condition.IsImplicit = true;
				_optionSelectedConditions.Add(condition);
			}

			//	For the same reasons, every layout must have a content-selected
			//	condition, so create a default one if necessary. Content conditions
			//	don't relate to the layout's source because embedded references
			//	carry their own sources, so there's no need to check for selectability
			//	here.
			if(_contentSelectedConditions.Count == 0)
			{
				ContentSelectedCondition condition = new ContentSelectedCondition(_layout.TrackingInfo.LineNumber, _layout.TrackingInfo.LinePosition);
				condition.Require = true;
				condition.Prohibit = false;
				condition.IsImplicit = true;
				_contentSelectedConditions.Add(condition);
			}

			SetTrackingId();
		}

		public void Add(ConditionSet additional)
		{
			_optionSelectedConditions .AddRange(additional._optionSelectedConditions );
			_doctagConditions         .AddRange(additional._doctagConditions         );
			_emptyLayoutConditions    .AddRange(additional._emptyLayoutConditions    );
			_itemCountConditions      .AddRange(additional._itemCountConditions      );
			_photoCountConditions     .AddRange(additional._photoCountConditions     );
			_contentSelectedConditions.AddRange(additional._contentSelectedConditions);
			_contentDocTagConditions  .AddRange(additional._contentDocTagConditions  );
		}

		/// <summary>
		/// Throws an InvalidConditionException if any condition is invalid.
		/// </summary>
		public void Validate(Reference defaultSource, Reference context)
		{
			List<Condition> conflicts = new List<Condition>();

			ValidateOptionSelectedConditions (defaultSource, context, conflicts);
			ValidateDocTagConditions         (defaultSource, context, conflicts);
			ValidateEmptyLayoutConditions    (defaultSource, context, conflicts);
			ValidateContentSelectedConditions(                        conflicts);
			ValidateContentDocTagConditions  (                        conflicts);
			ValidateItemCountConditions      (defaultSource, context, conflicts);
			ValidatePhotoCountConditions     (defaultSource, context, conflicts);

			if(conflicts.Count > 0)
			{
				Exception ex = new InvalidConditionException(
					$"Conflicting conditions on layout {_layout}", _layout);
				for(int x = 0; x < conflicts.Count; ++x)
					ex.Data.Add($"Conflict {x+1}", conflicts[x].ToString());
				throw ex;
			}
		}

		private void ValidateOptionSelectedConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	Distil our option-selected conditions into a more easily evaluated form.
			//	After distillation we have some straightforward maps of conditions
			//	to fully resolved references, including the default source as
			//	appropriate. The maps are separated into require/prohibit lists,
			//	and all we have to do then to evaluate the conditions is to check
			//	whether each item in the map is selected according to those lists.
			//
			//	Distillation also checks for invalid require+prohibit conditions.
			DistillOptionSelectedConditions(defaultSource, context, conflicts);
		}

		private void ValidateDocTagConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	Distil our doctag conditions into a more easily evaluated form.
			//	After distillation we have some straightforward maps of conditions
			//	to fully resolved references, including the default source as
			//	appropriate. The maps are separated into require/prohibit lists,
			//	and all we have to do then to evaluate the conditions is to check
			//	whether each item in the map is tagged according to those lists.
			//
			//	Distillation also checks for invalid require+prohibit conditions.
			DistillDocTagConditions(defaultSource, context, conflicts);
		}

		private void ValidateEmptyLayoutConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	We don't distil empty-layout conditions because their targets
			//	aren't known until draft time. This Validate method
			//	is called after reference resolution but before drafting.

			//	Validating empty-layout is fairly straightforward because the
			//	condition will be evaluated against a layout based on its type
			//	and id, and the ids are guaranteed to be unique at design time,
			//	so there's no question of conflicting conditions referring to
			//	the same layout by different ids. The ids might not be unique
			//	after reference resolution because layouts can be duplicated,
			//	but we are guaranteed that two conditions with the same refid
			//	on the same layout were intended to refer to the same controlling
			//	layout or control.

			//	Find any layouts for which we have both require and prohibit
			//	conditions
			List<string> require = _emptyLayoutConditions
				.Where(c => c.Require)
				.Select(c => c.RefId)
				.Distinct()
				.ToList();
			List<string> prohibit = _emptyLayoutConditions
				.Where(c => c.Prohibit)
				.Select(c => c.RefId)
				.Distinct()
				.ToList();
			List<string> requireAndProhibit = require.Intersect(prohibit).ToList();

			foreach(string id in requireAndProhibit)
			{
				//	Go back and find all conditions that either require or prohibit
				//	this layout
				List<EmptyLayoutCondition> conflictingCondition = _emptyLayoutConditions
					.Where(c => (c.RefId == id) && (c.Require || c.Prohibit))
					.ToList();

				//	Add the conflicting conditions to the return list
				foreach(EmptyLayoutCondition condition in conflictingCondition)
					if(!conflicts.Contains(condition))
						conflicts.Add(condition);
			}
		}

		private void ValidateContentSelectedConditions(List<Condition> conflicts)
		{
			//	We don't distil content-selected conditions because their targets
			//	aren't known until content load time. This Validate method
			//	is called after reference resolution but before content load.

			//	Validating content-selected is fairly straightforward because the
			//	condition will be evaluated against a single resolved reference at
			//	content load time, so there's no question of whether different
			//	reference paths resolve to the same object.

			//	If any condition specifies both require and prohibit then it's not
			//	valid. Or if we have two separate conditions, one with require and
			//	the other with prohibit, then that combination is invalid too. So
			//	find all conditions that require, and all that prohibit, and if
			//	there's any overlap then return all of those conditions as conflicts.
			//	Note that conditions that neither require nor prohibit don't give
			//	rise to conflicts.
			List<ContentSelectedCondition> allRequire  = _contentSelectedConditions.Where(c => c.Require ).ToList();
			List<ContentSelectedCondition> allProhibit = _contentSelectedConditions.Where(c => c.Prohibit).ToList();
			List<ContentSelectedCondition> overlap = allRequire.Intersect(allProhibit).Distinct().ToList();
			conflicts.AddRange(overlap);
		}

		private void ValidateContentDocTagConditions(List<Condition> conflicts)
		{
			//	We don't distil content-doctag conditions because their targets
			//	aren't known until content load time. This Validate method
			//	is called after reference resolution but before content load.

			//	Validating content-doctag is fairly straightforward because the
			//	condition will be evaluated against a single resolved reference at
			//	content load time, so there's no question of whether different
			//	reference paths resolve to the same object.

			//	If any condition specifies both require and prohibit for the same
			//	doctag then it's not valid. Or if we have two separate conditions
			//	for the same doctag, one with require and the other with prohibit,
			//	then that combination is invalid too.

			//	Find any doctags for which we have both require and prohibit conditions
			List<string> require = _contentDocTagConditions
				.Where(c => c.Require)
				.Select(c => c.DocTag)
				.Distinct()
				.ToList();
			List<string> prohibit = _contentDocTagConditions
				.Where(c => c.Prohibit)
				.Select(c => c.DocTag)
				.Distinct()
				.ToList();
			List<string> requireAndProhibit = require.Intersect(prohibit).ToList();

			foreach(string doctag in requireAndProhibit)
			{
				//	Go back and find all conditions that either require or prohibit
				//	this doctag
				List<ContentDocTagCondition> conflictingCondition = _contentDocTagConditions
					.Where(c => (c.DocTag == doctag) && (c.Require || c.Prohibit))
					.ToList();

				//	Add the conflicting conditions to the return list
				foreach(ContentDocTagCondition condition in conflictingCondition)
					if(!conflicts.Contains(condition))
						conflicts.Add(condition);
			}
		}

		private void ValidateItemCountConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	We don't distil item-count conditions because their targets
			//	aren't known until draft time. This Validate method
			//	is called after reference resolution but before drafting.

			//	Validating item-count is fairly straightforward because the
			//	condition will be evaluated against a layout based on its type
			//	and id, and the ids are guaranteed to be unique at design time,
			//	so there's no question of conflicting conditions referring to
			//	the same layout by different ids. The ids might not be unique
			//	after reference resolution because layouts can be duplicated,
			//	but we are guaranteed that two conditions with the same refid
			//	on the same layout are intended to refer to the same controlling
			//	layout or control.

			//	If we have more than one item-count condition for any controlling
			//	layout, with different ranges, then they will conflict. If they have
			//	the same range then they're redundant but not conflicting.
			foreach(ItemCountCondition condition in _itemCountConditions)
			{
				int count = _itemCountConditions
					.Where(c =>
						(c.RefId == condition.RefId)
						&&
						(c.Minimum != condition.Minimum || c.Maximum != condition.Maximum))
					.Count();
				if(count != 0)
					conflicts.Add(condition);
			}
		}

		private void ValidatePhotoCountConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	We don't distil photo-count conditions because their targets
			//	aren't known until draft time. This Validate method
			//	is called after reference resolution but before drafting.

			//	Validating photo-count is fairly straightforward because the
			//	condition will be evaluated against a layout based on its type
			//	and id, and the ids are guaranteed to be unique at design time,
			//	so there's no question of conflicting conditions referring to
			//	the same layout by different ids. The ids might not be unique
			//	after reference resolution because layouts can be duplicated,
			//	but we are guaranteed that two conditions with the same refid
			//	on the same layout are intended to refer to the same controlling
			//	layout or control.

			//	If we have more than one photo-count condition for any controlling
			//	layout or control, with different ranges, then they will conflict.
			//	If they have the same range then they're redundant but not conflicting.
			ReferenceComparer comparer = new ReferenceComparer();
			foreach(PhotoCountCondition condition in _photoCountConditions)
			{
				int count = _photoCountConditions
					.Where(c =>
						((c.RefId == condition.RefId) || (comparer.Equals(c.Source.Target, condition.Source.Target)))
						&&
						(c.Minimum != condition.Minimum || c.Maximum != condition.Maximum))
					.Count();
				if(count != 0)
					conflicts.Add(condition);
			}
		}

		private void DistillOptionSelectedConditions(Reference defaultSource, Reference context, List<Condition> conflicts)
		{
			//	The default policy is to render the content in the report,
			//	and conditions act to change that policy and remove the
			//	content. So basically we're looking for reasons not to
			//	include the content, meaning that we start with all flags
			//	set to true and then switch them off if we find a reason.
			//	This means that if we have competing conditions, the
			//	negatives win.
			_requireSelected  = new Dictionary<OptionSelectedCondition, List<Reference>>();
			_prohibitSelected = new Dictionary<OptionSelectedCondition, List<Reference>>();

			//	For checking for conflicts
			List<Reference> allRequired = new List<Reference>();
			List<Reference> allProhibited = new List<Reference>();

			foreach(OptionSelectedCondition condition in _optionSelectedConditions)
			{
				//	The condition is evaluated against a source object, which is
				//	searched for in the following order:
				//		1.	The condition's own source reference.
				//		2.	The layout's source object.
				//		3.	The context object.
				List<Reference> sources = new List<Reference>();
				if(condition.Source != Path.Path.Empty)
				{
					List<Reference> resolved = _layout.Generator.ResolveMany(condition.Source, context);
					if(resolved.Count == 0)
					{
						//TODO: if the condition reference resolves to nothing then does that mean that
						//we don't really have a condition, or that our condition fails?
					}
					sources.AddRange(resolved);
				}
				else if(defaultSource != Reference.Empty && defaultSource != Reference.Null)
				{
					//	The generator resolves source references before validating conditions,
					//	so we don't need to check it here. (But not that Reference.Null is by
					//	definition unresolved.)
					sources.Add(defaultSource);
				}
				else if(context != Reference.Empty)
				{
					sources.Add(context);
				}
				sources.RemoveAll(r => !IsSelectable(r.Type));
				if(sources.Count == 0) continue;

				//	Don't bother checking for both require and prohibit as we go,
				//	because even if each individual condition is OK we could have
				//	two conditions that conflict. So we'll do a thorough check
				//	for conflicts once we've resolved all the conditions.
				if(condition.Require)
				{
					_requireSelected.Add(condition, sources);
					allRequired.AddRange(sources);
				}
				if(condition.Prohibit)
				{
					_prohibitSelected.Add(condition, sources);
					allProhibited.AddRange(sources);
				}
				string list = string.Join<Reference>(" ", sources);
				Trace("Condition {0} {1}", condition, list);
			}

			//	Find any sources for which we have both require and prohibit
			//	conditions
			List<Reference> requireAndProhibit = allRequired.Intersect(allProhibited, new ReferenceComparer()).ToList();
			ReferenceComparer comparer = new ReferenceComparer();
			foreach(Reference source in requireAndProhibit)
			{
				//	Go back and find all conditions that either require or prohibit
				//	this source
				List<OptionSelectedCondition> conflictingCondition = _optionSelectedConditions
					.Where(c => comparer.Equals(c.Source.Target, source) && (c.Require || c.Prohibit))
					.ToList();

				//	Add the conflicting conditions to the return list
				foreach(OptionSelectedCondition condition in conflictingCondition)
					if(!conflicts.Contains(condition))
						conflicts.Add(condition);
			}
		}

		private void DistillDocTagConditions(Reference defaultSource, Reference context, List<Condition> conflict)
		{
			//	The default policy is to render the content in the report,
			//	and conditions act to change that policy and remove the
			//	content. So basically we're looking for reasons not to
			//	include the content, meaning that we start with all flags
			//	set to true and then switch them off if we find a reason.
			//	This means that if we have competing conditions, the
			//	negatives win.
			_requireDoctags   = new Dictionary<DocTagCondition, List<Reference>>();
			_prohibitDoctags  = new Dictionary<DocTagCondition, List<Reference>>();

			Dictionary<Reference, List<string>> allRequired = new Dictionary<Reference, List<string>>();
			Dictionary<Reference, List<string>> allProhibited = new Dictionary<Reference, List<string>>();

			foreach(DocTagCondition condition in _doctagConditions)
			{
				//	The condition is evaluated against a source object, which is
				//	searched for in the following order:
				//		1.	The condition's own source reference.
				//		2.	The layout's source object.
				//		3.	The context object.
				List<Reference> sources = new List<Reference>();
				if(condition.Source != Path.Path.Empty)
				{
					List<Reference> resolved = _layout.Generator.ResolveMany(condition.Source, context);
					if(resolved.Count == 0)
					{
						//TODO: if the condition reference resolves to nothing then does that mean that
						//we don't really have a condition, or that our condition fails?
					}
					sources.AddRange(resolved);
				}
				else if(defaultSource != Reference.Empty && defaultSource != Reference.Null)
				{
					//	The generator resolves source references before validating conditions,
					//	so we don't need to check it here. (But not that Reference.Null is by
					//	definition unresolved.)
					sources.Add(defaultSource);
				}
				else if(context != Reference.Empty)
				{
					sources.Add(context);
				}
				if(sources.Count == 0) continue;

				if(condition.Require)
				{
					foreach(Reference source in sources)
					{
						Add(_requireDoctags, condition, source);
						Add(allRequired, source, condition.DocTag);
					}
				}
				if(condition.Prohibit)
				{
					foreach(Reference source in sources)
					{
						Add(_prohibitDoctags, condition, source);
						Add(allProhibited, source, condition.DocTag);
					}
				}
				string list = string.Join<Reference>(" ", sources);
				Trace("Condition {0} {1}", condition, list);
			}
				
			//	Find any tags for which we have both require and prohibit conditions.
			//	First find all sources that appear in both lists, and then check
			//	whether they've got the same tags in the two lists.
			List<Reference> requireAndProhibitSources = allRequired.Keys.Intersect(allProhibited.Keys, new ReferenceComparer()).ToList();
			foreach(Reference source in requireAndProhibitSources)
			{
				List<string> requiredTags = allRequired[source];
				List<string> prohibitedTags = allProhibited[source];
				List<string> requireAndProhibitTags = requiredTags.Intersect(prohibitedTags).ToList();
				if(requireAndProhibitTags.Count > 0)
				{
					//	Go back and find all conditions that either require or prohibit
					//	this tag on this source
					ReferenceComparer comparer = new ReferenceComparer();
					List<DocTagCondition> conflictingConditions = _doctagConditions
						.Where(c =>
							comparer.Equals(c.Source.Target, source)
							&&
							(requireAndProhibitTags.Contains(c.DocTag)))
						.ToList();

					//	Add the conflicting conditions to the return list
					foreach(DocTagCondition condition in conflictingConditions)
						if(!conflict.Contains(condition))
							conflict.Add(condition);
				}
			}
		}

		/// <summary>
		/// Check whether the static layout conditions in this set are satisfied by
		/// the controlling control or layout. Does not check static content conditions
		/// because their sources are unknown until content load time.
		/// </summary>
		/// <param name="defaultSource">The default source object against which the
		/// condition is evaluated, if the condition doesn't provide its own source.</param>
		public bool AreStaticLayoutConditionsSatisfied(Reference defaultSource, Reference context)
		{
			if(!AreOptionSelectedConditionsSatisfied()) return false;
			if(!AreDocTagConditionsSatisfied()) return false;
			return true;
		}

		private bool AreOptionSelectedConditionsSatisfied()
		{
			foreach(KeyValuePair<OptionSelectedCondition, List<Reference>> conditions in _requireSelected)
			{
				OptionSelectedCondition condition = conditions.Key;
				List<Reference> targets = conditions.Value;
				foreach(Reference target in targets)
				{
					if(!IsOptionSelected(target))
					{
						Trace("Condition {0} unsatisfied by unselected object {1}", condition, target);
						return false;
					}
				}
			}

			foreach(KeyValuePair<OptionSelectedCondition, List<Reference>> conditions in _prohibitSelected)
			{
				OptionSelectedCondition condition = conditions.Key;
				List<Reference> targets = conditions.Value;
				foreach(Reference target in targets)
				{
					if(IsOptionSelected(target))
					{
						Trace("Condition {0} unsatisfied by selected object {1}", condition, target);
						return false;
					}
				}
			}

			//TODO: AND/OR conditions together?
			return true;
		}

		private bool AreDocTagConditionsSatisfied()
		{
			foreach(KeyValuePair<DocTagCondition, List<Reference>> conditions in _requireDoctags)
			{
				DocTagCondition condition = conditions.Key;
				List<Reference> targets = conditions.Value;
				foreach(Reference target in targets)
				{
					if(!_layout.Generator.Resolver.IsObjectTagged(target, condition.DocTag))
					{
						Trace("Condition {0} on unsatisfied by untagged object {1}", condition, target);
						return false;
					}
				}
			}

			foreach(KeyValuePair<DocTagCondition, List<Reference>> conditions in _prohibitDoctags)
			{
				DocTagCondition condition = conditions.Key;
				List<Reference> targets = conditions.Value;
				foreach(Reference target in targets)
				{
					if(_layout.Generator.Resolver.IsObjectTagged(target, condition.DocTag))
					{
						Trace("Condition {0} unsatisfied by tagged object {1}", condition, target);
						return false;
					}
				}
			}

			//TODO: AND/OR conditions together?
			return true;
		}

		/// <summary>
		/// Test whether a single item satisfies the content conditions in this set.
		/// </summary>
		public bool SatisfiesContentConditions<T>(T target) where T : class, IContentSource
		{
			return SatisfiesContentConditions(Reference.Create(target.Type, target.Id, true));
		}

		/// <summary>
		/// Test whether a single item satisfies the content conditions in this set.
		/// </summary>
		public bool SatisfiesContentConditions(Reference target)
		{
			foreach(ContentSelectedCondition condition in _contentSelectedConditions)
				if(!IsConditionSatisfied(condition, target))
					return false;

			foreach(ContentDocTagCondition condition in _contentDocTagConditions)
				if(!IsConditionSatisfied(condition, target))
					return false;

			return true;
		}

		/// <summary>
		/// Apply content conditions to a list of items, removing those
		/// that don't satisfy the conditions.
		/// </summary>
		public void ApplyContentConditions<T>(List<T> items) where T : class, IContentSource
		{
			List<Reference> references = new List<Reference>();
			foreach(T item in items)
				references.Add(Reference.Create(item.Type, item.Id, true));

			//	Remove all references that do satisfy our content conditions,
			//	leaving only those that don't
			references.RemoveAll(r => SatisfiesContentConditions(r));

			//	Remove any items whose ids are in the unsatisfying list, leaving
			//	only that items that do satisfy the conditions
			items.RemoveAll(cb => references.Select(r => r.Id).Contains(cb.Id));
		}

		private bool IsConditionSatisfied(OptionSelectedCondition condition, Reference target)
		{
			bool selected = IsOptionSelected(target);
			if(!selected && condition.Require)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			if(selected && condition.Prohibit)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			Trace("Condition satisfied {0} {1}", condition, target);
			return true;
		}

		private bool IsConditionSatisfied(DocTagCondition condition, Reference target)
		{
			bool tagged = _layout.Generator.Resolver.IsObjectTagged(target, condition.DocTag);
			if(!tagged && condition.Require)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			if(tagged && condition.Prohibit)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			Trace("Condition satisfied {0} {1}", condition, target);
			return true;
		}

		private bool IsConditionSatisfied(ContentSelectedCondition condition, Reference target)
		{
			bool selected = IsOptionSelected(target);
			if(!selected && condition.Require)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			if(selected && condition.Prohibit)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			Trace("Condition satisfied {0} {1}", condition, target);
			return true;
		}

		private bool IsConditionSatisfied(ContentDocTagCondition condition, Reference target)
		{
			bool tagged = _layout.Generator.Resolver.IsObjectTagged(target, condition.DocTag);
			if(!tagged && condition.Require)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			if(tagged && condition.Prohibit)
			{
				Trace("Condition not satisfied {0} {1}", condition, target);
				return false;
			}
			Trace("Condition satisfied {0} {1}", condition, target);
			return true;
		}

		public bool IsConditionSatisfied(EmptyLayoutCondition condition, Layout layout)
		{
			//	An empty-layout condition must have a reference to a layout, not to a control
			if (condition.RefType == LayoutType.None) return false;
			if (condition.RefId == null) return false;
			Layout refLayout = layout.FindLayout(condition.Context, condition.RefType, condition.RefId);

			//	If the ref layout wasn't found then treat that the same as its being empty.
			//	This might happen because the ref layout's own conditions caused it to
			//	delete itself.
			bool isEmpty = refLayout == null;
			if(refLayout != null)
			{
				//	Apply the referenced layout's conditions because they're effectively
				//	nested conditions of our own
				if(refLayout != layout) refLayout.ApplyDynamicConditions();

				isEmpty = refLayout.IsEmpty();
			}

			if(isEmpty && condition.Prohibit)
			{
				Trace("Condition not satisfied {0} [{1}]", condition, refLayout);
				return false;
			}
			else if(!isEmpty && condition.Require)
			{
				Trace("Condition not satisfied {0} [{1}]", condition, refLayout);
				return false;
			}
			Trace("Condition satisfied {0} [{1}]", condition, refLayout);
			return true;
		}

		/// <summary>
		/// May cause recursion to Layout.ApplyDynamicConditions to find other
		/// layouts referenced by the condition.
		/// </summary>
		public bool IsConditionSatisfied(PhotoCountCondition condition, Layout layout)
		{
			//	The condition can have a source reference that points to a control, and
			//	a layout reference, so sum the number of photos found by each
			int numPhotos = 0;

			if(condition.Source.TargetType != ContentSourceType.None)
			{
				switch(condition.Source.TargetType)
				{
					case ContentSourceType.PhotoList:
					{
						Reference list = _layout.Generator.ResolveOne(condition.Source, layout.ReferenceContext);
						if(list != null)
						{
							numPhotos += _layout.Generator.UnitOfWork.Repository<Photo>()
								.Query(p => p.ListId == list.Id)
								.Get(false)
								.Count();
						}
						break;
					}
				}
			}

			if(condition.RefType != LayoutType.None)
			{
				Layout refLayout = layout.FindLayout(condition.Context, condition.RefType, condition.RefId);
				if(refLayout != null)
				{
					switch(condition.RefType)
					{
						case LayoutType.PhotoTable:
						{
							//	Apply the referenced layout's conditions because they're effectively
							//	nested conditions of our own
							if(refLayout != layout) refLayout.ApplyDynamicConditions();

							PhotoTableLayout table = (PhotoTableLayout)refLayout;
							numPhotos += table.NumPhotos;
							break;
						}
					}
				}
			}

			bool satisfied = numPhotos >= condition.Minimum && numPhotos <= condition.Maximum;
			if(satisfied)
				Trace("Condition satisfied {0} photos={1}", condition, numPhotos);
			else
				Trace("Condition not satisfied {0} photos={1}", condition, numPhotos);
			return satisfied;
		}

		public bool IsConditionSatisfied(ItemCountCondition condition, Layout layout)
		{
			Layout refLayout = layout.FindLayout(condition.Context, condition.RefType, condition.RefId);
			if(refLayout == null) return false;

			//	Apply the referenced layout's conditions because they're effectively
			//	nested conditions of our own
			if(refLayout != layout) refLayout.ApplyDynamicConditions();

			int numSubLayouts = refLayout.NumSubLayouts;
			bool satisfied = numSubLayouts >= condition.Minimum && numSubLayouts <= condition.Maximum;
			if(satisfied)
				Trace("Condition satisfied {0} sublayouts={1}", condition, numSubLayouts);
			else
				Trace("Condition not satisfied {0} sublayouts={1}", condition, numSubLayouts);
			return satisfied;
		}

		private bool IsOptionSelected(Checkbox checkbox)
		{
			return checkbox?.State != 0;
		}

		private bool IsOptionSelected(RadioButton radio)
		{
			return radio?.State != 0;
		}

		private bool IsOptionSelected(Calculation calc)
		{
			return calc?.State != 0;
		}

		private bool IsOptionSelected(MultiSelectList list)
		{
			if(list == null) return false;

			//	A multiselect list is selected if at least one checkbox is selected
			return _layout.Generator.UnitOfWork.Repository<Checkbox>()
				.Query(b => b.ListId == list.Id
										&&
										b.State != 0)
				.Get(false)
				.Any();
		}

		private bool IsOptionSelected(SingleSelectList list)
		{
			if(list == null) return false;

			//	A single-select list is selected if at least one radio button is selected
			return _layout.Generator.UnitOfWork.Repository<RadioButton>()
				.Query(b => b.ListId == list.Id
										&&
										b.State != 0)
				.Get(false)
				.Any();
		}

		private bool IsOptionSelected(CalculationList list)
		{
			if(list == null) return false;

			//	A calculation list is selected if at least one calculation is selected
			return _layout.Generator.UnitOfWork.Repository<Calculation>()
				.Query(b => b.ListId == list.Id
										&&
										b.State != 0)
				.Get(false)
				.Any();
		}

		public bool IsOptionSelected(Reference target)
		{
			//	The notion of being selected makes sense only for checkboxes, radio buttons,
			//	calculations, and their associated list types multi-select, single-select and
			//	calculation list. For all other types, consider the conditions satisfied. Except
			//	for type "none", which is always unselected.
			switch(target.Type)
			{
				case ContentSourceType.Checkbox:
					Checkbox checkbox = _layout.Generator.ResolveOne<Checkbox>(target);
					return IsOptionSelected(checkbox);

				case ContentSourceType.RadioButton:
					RadioButton radio = _layout.Generator.ResolveOne<RadioButton>(target);
					return IsOptionSelected(radio);

				case ContentSourceType.Calculation:
					Calculation calc = _layout.Generator.ResolveOne<Calculation>(target);
					return IsOptionSelected(calc);

				case ContentSourceType.MultiSelect:
					MultiSelectList mlist = _layout.Generator.ResolveOne<MultiSelectList>(target);
					return IsOptionSelected(mlist);

				case ContentSourceType.SingleSelect:
					SingleSelectList slist = _layout.Generator.ResolveOne<SingleSelectList>(target);
					return IsOptionSelected(slist);

				case ContentSourceType.CalculationList:
					CalculationList clist = _layout.Generator.ResolveOne<CalculationList>(target);
					return IsOptionSelected(clist);

				case ContentSourceType.None:
					//	If the target doesn't exist then consider it unselected. This
					//	is used, for example, for the dummy photo assoc inserted when
					//	a photo has no real assoc.
					return false;

				default:
					//	For all non-selectable types, consider the conditions satisfied
					return true;
			}
		}

		public bool IsSelectable(ContentSourceType type)
		{
			switch(type)
			{
				case ContentSourceType.RadioButton:
				case ContentSourceType.Checkbox:
				case ContentSourceType.MultiSelect:
				case ContentSourceType.SingleSelect:
				case ContentSourceType.Calculation:
				case ContentSourceType.CalculationList:
					return true;

				case ContentSourceType.None:
				case ContentSourceType.TextEntry:
				case ContentSourceType.PhotoList:
				case ContentSourceType.StaticText:
				case ContentSourceType.Photo:
				case ContentSourceType.Form:
				case ContentSourceType.Section:
				case ContentSourceType.DocTag:
				case ContentSourceType.DocProp:
				case ContentSourceType.Picture:
				case ContentSourceType.CalculationVariable:
				default:
					return false;
			}
		}

		/// <summary>
		///	Apply the set's option-selected conditions to a list of checkboxes,
		///	removing those that don't satisfy all of the conditions.
		/// </summary>
		private void ApplyOptionSelectedConditions(List<Checkbox> list)
		{
			list.RemoveAll(m => !IsOptionSelected(m));
		}

		/// <summary>
		/// Add a value to a dictionary, creating the key if necessary.
		/// </summary>
		private void Add<K, V>(Dictionary<K, List<V>> dictionary, K key, V value)
		{
			List<V> values = null;
			bool haveit = dictionary.TryGetValue(key, out values);
			if(!haveit)
			{
				values = new List<V>();
				dictionary.Add(key, values);
			}
			values.Add(value);
		}

		private void SetTrackingId()
		{
			foreach(Condition condition in _optionSelectedConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _doctagConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _emptyLayoutConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _photoCountConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _itemCountConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _contentSelectedConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
			foreach(Condition condition in _contentDocTagConditions)
				condition.TrackingId = _layout.TrackingInfo.TrackingId;
		}

		private void Trace(string text, params object[] args)
		{
			_layout.Trace(text, args);
		}
	}
}
