using UnityEngine;

namespace WitchMendokusai
{
	// 형태 카드 (갈래 확정, 최종) 의 겉모습. 일반 카드와 같은 틀에 둘레의 빛, 흐르는 빛, 아이콘 하나만 다름
	// 형태 자산 (DollFormData.CardLook) 이 가리킴. 인물과 갈래마다 값만 다른 자산
	[CreateAssetMenu(fileName = "FCL_New", menuName = "WM/Variable/" + nameof(FormCardLook))]
	public sealed class FormCardLook : ScriptableObject
	{
		[field: SerializeField, Tooltip("공개 전 뒷면. 등급 뒷면 대신")] public Sprite Back { get; private set; }
		[field: SerializeField, Tooltip("뒷면 광택 (6열 4행)")] public Texture2D BackEffect { get; private set; }
		[field: SerializeField, Tooltip("흐르는 빛과 테두리 (WM/UI/FormCardLight)")] public Shader LightShader { get; private set; }
		[field: SerializeField, Tooltip("흰 아이콘 하나")] public Sprite Icon { get; private set; }

		[field: Header("색 (어둠에서 밝음)")]
		[field: SerializeField] public Color Dark { get; private set; } = new(0.26f, 0.18f, 0.05f);
		[field: SerializeField] public Color Mid { get; private set; } = new(0.84f, 0.62f, 0.16f);
		[field: SerializeField, Tooltip("카드 꾸러미와 공개 때 빛의 색이기도 함")] public Color Main { get; private set; } = new(1f, 0.86f, 0.4f);
		[field: SerializeField] public Color Bright { get; private set; } = new(1f, 0.99f, 0.93f);

		[field: Header("크기")]
		[field: SerializeField, Min(1f)] public float IconSize { get; private set; } = 104f;
		[field: SerializeField, Tooltip("아이콘 그림자가 내려가는 거리")] public Vector2 IconShadow { get; private set; } = new(0f, -5f);
		[field: SerializeField, Range(0f, 1f)] public float IconShadowAlpha { get; private set; } = 0.5f;
		[field: SerializeField, Min(0f), Tooltip("테두리 빛이 카드 밖으로 번지는 폭")] public float BorderMargin { get; private set; } = 60f;

		[field: Header("고른 뒤 (실제 초)")]
		[field: SerializeField] public Vector2 PillarSize { get; private set; } = new(460f, 2200f);
		[field: SerializeField, Min(0.05f)] public float PillarSeconds { get; private set; } = 0.6f;
		[field: SerializeField, Min(0.05f), Tooltip("고른 순간 카드가 희게 번쩍이는 길이")] public float PickFlashSeconds { get; private set; } = 0.22f;
	}
}
