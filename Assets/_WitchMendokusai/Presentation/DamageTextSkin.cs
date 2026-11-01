using UnityEngine;

namespace WitchMendokusai.Presentation
{
	[CreateAssetMenu(fileName = "DTS_0_New", menuName = "WM/UI/Damage Text Skin")]
	public sealed class DamageTextSkin : ScriptableObject
	{
		[field: SerializeField] public string SkinId { get; private set; } = "classic";
		[field: SerializeField] public GameObject TextPrefab { get; private set; }
		[field: SerializeField, Min(12f)] public float FontSize { get; private set; } = 25f;
		[field: SerializeField, Min(1f)] public float CriticalScale { get; private set; } = 1f;
		[field: SerializeField] public string CriticalPrefix { get; private set; } = "크리티컬!\n";
		[field: SerializeField] public Color NormalColor { get; private set; } = Color.white;
		[field: SerializeField] public Color CriticalColor { get; private set; } = new Color(1f, 110f / 255f, 86f / 255f);
		[field: SerializeField] public Color HealColor { get; private set; } = Color.white;
		[field: SerializeField] public Color WarningColor { get; private set; } = Color.yellow;
		[field: SerializeField, Range(0.3f, 2f)] public float Lifetime { get; private set; } = 0.85f;
		[field: SerializeField, Min(0f)] public float Rise { get; private set; } = 42f;
		[field: SerializeField, Min(0f)] public float LaneSpacing { get; private set; } = 48f;
		[field: SerializeField, Range(1f, 1.6f)] public float ImpactScale { get; private set; } = 1.24f;
	}
}
