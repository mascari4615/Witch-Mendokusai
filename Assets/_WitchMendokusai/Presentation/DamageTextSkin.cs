using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	[CreateAssetMenu(menuName = "WM/UI/Damage Text Skin")]
	public sealed class DamageTextSkin : ScriptableObject
	{
		[field: SerializeField] public string SkinId { get; private set; } = "classic";
		[field: SerializeField] public Font Font { get; private set; }
		[field: SerializeField, Min(12f)] public float FontSize { get; private set; } = 25f;
		[field: SerializeField, Min(1f)] public float CriticalScale { get; private set; } = 1.3f;
		[field: SerializeField] public Color NormalColor { get; private set; } = new Color(1f, 0.96f, 0.82f);
		[field: SerializeField] public Color CriticalColor { get; private set; } = new Color(1f, 0.71f, 0.24f);
		[field: SerializeField] public Color HealColor { get; private set; } = new Color(0.48f, 1f, 0.56f);
		[field: SerializeField] public Color HurtColor { get; private set; } = new Color(1f, 0.34f, 0.3f);
		[field: SerializeField] public Color OutlineColor { get; private set; } = new Color(0.08f, 0.05f, 0.1f);
		[field: SerializeField, Range(0f, 4f)] public float OutlineWidth { get; private set; } = 2f;
		[field: SerializeField, Range(0.3f, 2f)] public float Lifetime { get; private set; } = 0.85f;
		[field: SerializeField, Min(0f)] public float Rise { get; private set; } = 42f;
		[field: SerializeField, Min(0f)] public float LaneSpacing { get; private set; } = 30f;
		[field: SerializeField, Range(1f, 1.6f)] public float ImpactScale { get; private set; } = 1.24f;

		public void Apply(Label label, FloatingTextKind kind)
		{
			label.style.unityFontDefinition = Font != null ? FontDefinition.FromFont(Font) : StyleKeyword.Null;
			label.style.fontSize = FontSize * (kind == FloatingTextKind.Critical ? CriticalScale : 1f);
			label.style.unityTextOutlineColor = OutlineColor;
			label.style.unityTextOutlineWidth = OutlineWidth;
			label.style.color = kind switch
			{
				FloatingTextKind.Critical => CriticalColor,
				FloatingTextKind.Heal => HealColor,
				FloatingTextKind.Hurt => HurtColor,
				FloatingTextKind.Experience => new Color(0.68f, 0.8f, 1f),
				FloatingTextKind.Warning => new Color(1f, 0.9f, 0.32f),
				FloatingTextKind.Buff => new Color(0.38f, 0.88f, 1f),
				_ => NormalColor,
			};
		}
	}
}
