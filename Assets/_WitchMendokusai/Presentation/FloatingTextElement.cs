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
		private IVisualElementScheduledItem activation;

		public FloatingTextElement()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;

			label = new Label();
			label.pickingMode = PickingMode.Ignore;
			Add(label);
		}

		public void Show(FloatingTextKind kind, string message, DamageTextSkin skin = null)
		{
			activation?.Pause();
			style.opacity = StyleKeyword.Null;
			style.scale = StyleKeyword.Null;
			label.style.unityFontDefinition = StyleKeyword.Null;
			label.style.fontSize = StyleKeyword.Null;
			label.style.color = StyleKeyword.Null;
			label.style.unityTextOutlineColor = StyleKeyword.Null;
			label.style.unityTextOutlineWidth = StyleKeyword.Null;
			for (int index = 0; index < VARIANT_CLASSES.Length; index++)
			{
				RemoveFromClassList(VARIANT_CLASSES[index]);
			}

			int variant = Mathf.Clamp((int)kind, 0, VARIANT_CLASSES.Length - 1);
			AddToClassList(VARIANT_CLASSES[variant]);
			label.text = message;
			skin?.Apply(label, kind);

			RemoveFromClassList(USS_ACTIVE);
			activation = schedule.Execute(() => AddToClassList(USS_ACTIVE)).StartingIn(0);
		}

		public void SetScreenPosition(Vector3 screenPosition, Vector2 offset = default)
		{
			Vector2 position = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
			if (panel != null)
				position = RuntimePanelUtils.ScreenToPanel(panel, position);
			if (parent != null)
				position = parent.WorldToLocal(position);
			style.left = position.x + offset.x;
			style.top = position.y + offset.y;
		}

		public void SetProgress(float progress, DamageTextSkin skin)
		{
			float impact = skin != null ? skin.ImpactScale : 1.24f;
			float scale = Mathf.Lerp(impact, 1f, Mathf.Clamp01(progress / 0.18f));
			style.scale = new Scale(new Vector3(scale, scale, 1f));
			style.opacity = 1f - Mathf.Clamp01((progress - 0.65f) / 0.35f);
		}

		public void Hide()
		{
			activation?.Pause();
			RemoveFromClassList(USS_ACTIVE);
			style.display = DisplayStyle.None;
		}
	}
}
