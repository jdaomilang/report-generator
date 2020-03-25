using Demon.Core.Domain;
using Demon.Core.Interface.Data;

namespace Demon.Path
{
	internal class Helpers
	{
		/// <summary>
		/// Map from source ControlType to SourceType.
		/// </summary>
		internal static ContentSourceType MapControlTypeToSourceType(ControlType controlType)
		{
			ContentSourceType sourceType = ContentSourceType.None;
			switch(controlType)
			{
				case ControlType.RadioButton:         sourceType = ContentSourceType.RadioButton;         break;
				case ControlType.Checkbox:            sourceType = ContentSourceType.Checkbox;            break;
				case ControlType.TextEntry:           sourceType = ContentSourceType.TextEntry;           break;
				case ControlType.StaticText:          sourceType = ContentSourceType.StaticText;          break;
				case ControlType.PhotoList:           sourceType = ContentSourceType.PhotoList;           break;
				case ControlType.MultiSelect:         sourceType = ContentSourceType.MultiSelect;         break;
				case ControlType.SingleSelect:        sourceType = ContentSourceType.SingleSelect;        break;
				case ControlType.Photo:               sourceType = ContentSourceType.Photo;               break;
				case ControlType.Form:                sourceType = ContentSourceType.Form;                break;
				case ControlType.Section:             sourceType = ContentSourceType.Section;             break;
				case ControlType.CalculationList:     sourceType = ContentSourceType.CalculationList;     break;
				case ControlType.Calculation:         sourceType = ContentSourceType.Calculation;         break;
				case ControlType.CalculationVariable: sourceType = ContentSourceType.CalculationVariable; break;
			}
			return sourceType;
		}
	}
}
