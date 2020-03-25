using Demon.Core.Domain;

namespace Demon.Report
{
	public class Helpers
	{
		public static uint ReadULong(byte[] buf)
		{
			uint value = 0;
			for(int x = 0; x < 4; ++x)
			{
				value <<= 8;
				value += buf[x];
			}
			return value;
		}

		public static string ReadString(byte[] buf, int len)
		{
			string value = "";
			for(int x = 0; x < len; ++x)
				value += (char)buf[x];
			return value;
		}

		/// <summary>
		/// Map from source SourceType to ControlType.
		/// </summary>
		public static ControlType MapSourceTypeToControlType(ContentSourceType sourceType)
		{
			ControlType controlType = ControlType.None;
			switch(sourceType)
			{
				case ContentSourceType.RadioButton:         controlType = ControlType.RadioButton;         break;
				case ContentSourceType.Checkbox:            controlType = ControlType.Checkbox;            break;
				case ContentSourceType.TextEntry:           controlType = ControlType.TextEntry;           break;
				case ContentSourceType.StaticText:          controlType = ControlType.StaticText;          break;
				case ContentSourceType.PhotoList:           controlType = ControlType.PhotoList;           break;
				case ContentSourceType.MultiSelect:         controlType = ControlType.MultiSelect;         break;
				case ContentSourceType.SingleSelect:        controlType = ControlType.SingleSelect;        break;
				case ContentSourceType.Photo:               controlType = ControlType.Photo;               break;
				case ContentSourceType.Form:                controlType = ControlType.Form;                break;
				case ContentSourceType.Section:             controlType = ControlType.Section;             break;
				case ContentSourceType.CalculationList:     controlType = ControlType.CalculationList;     break;
				case ContentSourceType.Calculation:         controlType = ControlType.Calculation;         break;
				case ContentSourceType.CalculationVariable: controlType = ControlType.CalculationVariable; break;
			}
			return controlType;
		}

		/// <summary>
		/// Map from source ControlType to SourceType.
		/// </summary>
		public static ContentSourceType MapControlTypeToSourceType(ControlType controlType)
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
