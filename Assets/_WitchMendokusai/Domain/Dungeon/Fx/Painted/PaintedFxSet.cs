using UnityEngine;

namespace WitchMendokusai
{
	// 그린 이펙트 텍스처 한 벌. 같은 결의 연출 (일제 사격, 검, 카드) 이 나눠 씀
	[CreateAssetMenu(fileName = "PFX_", menuName = "WM/Fx/PaintedFxSet")]
	public sealed class PaintedFxSet : ScriptableObject
	{
		[field: SerializeField] public Shader Shader { get; private set; }
		[field: SerializeField, Tooltip("침식과 왜곡 잡음 (R 덩어리, G 가닥)")] public Texture2D Noise { get; private set; }
		[field: Header("무기")]
		[field: SerializeField, Tooltip("손에 든 빛 광석 총. 한 칸이 월드의 32분의 1, 총구가 오른쪽")] public Texture2D Gun { get; private set; }
		[field: SerializeField, Tooltip("알리사 완드. 마법봉이 아니라 직사각형의 빛나는 광석 총 (정본 memo wm/systems/signature-weapon.md 완드의 뜻). 총구가 오른쪽인 옆모습")] public Texture2D[] Wands { get; private set; }
		[field: Header("빛")]
		[field: SerializeField, Tooltip("가로 줄기 네 줄 (위부터 바늘, 꼬인 띠, 붓, 연기)")] public Texture2D Streaks { get; private set; }
		[field: SerializeField] public Texture2D Spark { get; private set; }
		[field: SerializeField, Tooltip("4x4. 원점은 칸의 왼쪽에서 MuzzleOrigin")] public Texture2D Muzzle { get; private set; }
		[field: SerializeField, Range(0f, 1f)] public float MuzzleOrigin { get; private set; } = 0.24f;
		[field: SerializeField, Tooltip("4x4, 칸 가운데")] public Texture2D Impact { get; private set; }
		[field: SerializeField, Tooltip("4x4, 칸 가운데. 막타")] public Texture2D Grand { get; private set; }
		[field: Header("검")]
		[field: SerializeField, Tooltip("날이 오른쪽인 검기")] public Texture2D Crescent { get; private set; }
		[field: SerializeField, Tooltip("얇은 검기. 잔상")] public Texture2D CrescentThin { get; private set; }
		[field: SerializeField, Tooltip("정면에서 본 날개 한 쌍")] public Texture2D Wings { get; private set; }
		[field: SerializeField, Tooltip("끝이 오른쪽인 옆모습")] public Texture2D Blade { get; private set; }
		[field: Header("원과 바닥")]
		[field: SerializeField, Tooltip("중심이 밝은 문. 앞의 것은 소용돌이치고 나머지는 돌기만 함")] public Texture2D[] Portals { get; private set; }
		[field: SerializeField, Tooltip("붓결 고리")] public Texture2D Shock { get; private set; }
		[field: SerializeField] public Texture2D Sigil { get; private set; }
		[field: SerializeField] public Texture2D Scorch { get; private set; }
		[field: SerializeField] public Texture2D Soft { get; private set; }

		// 줄기 텍스처의 줄
		public static Rect StreakRow(int row) => new(0f, 1f - (row + 1) * 0.25f, 1f, 0.25f);
	}
}
