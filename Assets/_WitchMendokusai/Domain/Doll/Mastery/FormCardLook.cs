using UnityEngine;

namespace WitchMendokusai
{
	// 형태 카드 (갈래 확정, 최종) 의 겉모습. 일반 카드와 다른 틀, 전용 그림, 공개와 고른 뒤 빛
	// 형태 자산 (DollFormData.CardLook) 이 가리킴. 인물과 갈래마다 값만 다른 자산
	[CreateAssetMenu(fileName = "FCL_New", menuName = "WM/Variable/" + nameof(FormCardLook))]
	public sealed class FormCardLook : ScriptableObject
	{
		[field: SerializeField] public Color Main { get; private set; } = new(1f, 0.82f, 0.36f);
		[field: SerializeField] public Color Bright { get; private set; } = new(1f, 0.97f, 0.8f);
		[field: SerializeField] public Color Dark { get; private set; } = new(0.36f, 0.2f, 0.05f);
		[field: SerializeField, Tooltip("흐르는 빛 (WM/UI/FormCardFlow)")] public Shader FlowShader { get; private set; }
		[field: SerializeField, Tooltip("그림 속 무기 (WM/Fx/FormSolid)")] public Shader SolidShader { get; private set; }
		[field: SerializeField, ColorUsage(false, true)] public Color WeaponGlow { get; private set; } = new(1.08f, 0.9f, 0.5f);

		[field: Header("전용 그림")]
		[field: SerializeField] public HeldWeapon.Shape Weapon { get; private set; } = HeldWeapon.Shape.Wand;
		[field: SerializeField, Min(1), Tooltip("그림 속 무기 수. 여럿이면 부채로 펼침")] public int WeaponCount { get; private set; } = 1;
		[field: SerializeField] public float FanDegrees { get; private set; } = 130f;
		[field: SerializeField, Min(0.1f)] public float WeaponSize { get; private set; } = 1.9f;
		[field: SerializeField] public float WeaponTilt { get; private set; } = 18f;
		[field: SerializeField, Min(0f)] public float SwayDegrees { get; private set; } = 9f;
		[field: SerializeField, Min(16), Tooltip("그림 가로 텍셀. 점 필터로 키움")] public int ArtWidth { get; private set; } = 152;

		[field: Header("틀")]
		[field: SerializeField, Min(0f)] public float FrameThickness { get; private set; } = 8f;
		[field: SerializeField, Min(0f), Tooltip("틀이 카드 밖으로 나가는 폭")] public float FrameOutset { get; private set; } = 10f;
		[field: SerializeField, Min(0f)] public float CornerSize { get; private set; } = 34f;
		[field: SerializeField, Range(0f, 1f), Tooltip("바탕에 깔리는 흐름의 세기")] public float BackFlow { get; private set; } = 0.1f;

		[field: Header("공개와 고른 뒤 (실제 초)")]
		[field: SerializeField, Min(0.05f), Tooltip("빛으로 덮였다가 걷히는 길이")] public float CoverSeconds { get; private set; } = 0.45f;
		[field: SerializeField, Min(0f), Tooltip("덮인 채 머무는 길이")] public float CoverHold { get; private set; } = 0.12f;
		[field: SerializeField, Min(0)] public int ShardCount { get; private set; } = 22;
		[field: SerializeField, Min(0f)] public float ShardDistance { get; private set; } = 260f;
		[field: SerializeField, Min(0.05f)] public float ShardSeconds { get; private set; } = 0.55f;
		[field: SerializeField, Tooltip("조각 크기 (작은 쪽, 큰 쪽)")] public Vector2 ShardSize { get; private set; } = new(10f, 34f);
		[field: SerializeField, Min(0f)] public float ShardSpinDegrees { get; private set; } = 260f;
		[field: SerializeField] public Vector2 PillarSize { get; private set; } = new(190f, 1500f);
		[field: SerializeField, Min(0.05f)] public float PillarSeconds { get; private set; } = 0.6f;
	}
}
