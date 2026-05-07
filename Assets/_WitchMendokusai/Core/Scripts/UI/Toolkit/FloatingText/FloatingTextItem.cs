using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class FloatingTextItem : VisualElement
	{
		public const string USS_CLASS = "wm-floating-text";
		public const string USS_ACTIVE = "wm-floating-text--active";
		public const string USS_NORMAL = "wm-floating-text--normal";
		public const string USS_CRITICAL = "wm-floating-text--critical";
		public const string USS_HEAL = "wm-floating-text--heal";
		public const string USS_EXP = "wm-floating-text--exp";
		public const string USS_WARNING = "wm-floating-text--warning";

		private readonly Label label;

		public FloatingTextItem()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;

			label = new Label();
			label.pickingMode = PickingMode.Ignore;
			Add(label);
		}

		public void Show(TextType textType, string message)
		{
			RemoveFromClassList(USS_NORMAL);
			RemoveFromClassList(USS_CRITICAL);
			RemoveFromClassList(USS_HEAL);
			RemoveFromClassList(USS_EXP);
			RemoveFromClassList(USS_WARNING);

			AddToClassList(GetVariantClass(textType));

			label.text = textType == TextType.Critical ? $"크리티컬!\n{message}" : message;

			RemoveFromClassList(USS_ACTIVE);
			schedule.Execute(() => AddToClassList(USS_ACTIVE)).StartingIn(0);
		}

		public void Deactivate()
		{
			RemoveFromClassList(USS_ACTIVE);
		}

		public void SetScreenPosition(Vector3 screenPos)
		{
			style.left = screenPos.x;
			style.top = Screen.height - screenPos.y;
		}

		private static string GetVariantClass(TextType textType)
		{
			return textType switch
			{
				TextType.Critical => USS_CRITICAL,
				TextType.Heal => USS_HEAL,
				TextType.Exp => USS_EXP,
				TextType.Warning => USS_WARNING,
				_ => USS_NORMAL,
			};
		}
	}
}
