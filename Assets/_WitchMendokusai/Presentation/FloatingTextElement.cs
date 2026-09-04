using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	public enum FloatingTextKind
	{
		Normal = 0,
		Critical = 1,
		Heal = 2,
		Experience = 3,
		Warning = 4,
		Hurt = 5,
		Buff = 6,
	}

	/// <summary>WM과 Idle이 함께 쓰는 화면 공간 전투 숫자.</summary>
	public sealed class FloatingTextElement : VisualElement
	{
		public const string USS_CLASS = "wm-floating-text";
		public const string USS_ACTIVE = "wm-floating-text--active";

		private static readonly string[] VARIANT_CLASSES =
		{
			"wm-floating-text--normal",
			"wm-floating-text--critical",
			"wm-floating-text--heal",
			"wm-floating-text--exp",
			"wm-floating-text--warning",
			"wm-floating-text--hurt",
			"wm-floating-text--buff",
		};

		private readonly Label label;

		public FloatingTextElement()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;

			label = new Label();
			label.pickingMode = PickingMode.Ignore;
			Add(label);
		}

		public void Show(FloatingTextKind kind, string message)
		{
			for (int index = 0; index < VARIANT_CLASSES.Length; index++)
			{
				RemoveFromClassList(VARIANT_CLASSES[index]);
			}

			int variant = Mathf.Clamp((int)kind, 0, VARIANT_CLASSES.Length - 1);
			AddToClassList(VARIANT_CLASSES[variant]);
			label.text = kind == FloatingTextKind.Critical ? "크리티컬!\n" + message : message;

			RemoveFromClassList(USS_ACTIVE);
			schedule.Execute(() => AddToClassList(USS_ACTIVE)).StartingIn(0);
		}

		public void SetScreenPosition(Vector3 screenPosition)
		{
			Rect box = panel?.visualTree != null ? panel.visualTree.worldBound : Rect.zero;
			float panelWidth = box.width > 0f ? box.width : Screen.width;
			float panelHeight = box.height > 0f ? box.height : Screen.height;
			float screenWidth = Mathf.Max(1f, Screen.width);
			float screenHeight = Mathf.Max(1f, Screen.height);

			style.left = screenPosition.x * panelWidth / screenWidth;
			style.top = (screenHeight - screenPosition.y) * panelHeight / screenHeight;
		}

		public void Hide()
		{
			RemoveFromClassList(USS_ACTIVE);
			style.display = DisplayStyle.None;
		}
	}
}
