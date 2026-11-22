using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 전체 화면 (메달 벽, 도감, 단계 상세) 과 달성 띠의 모양과 수치. 색, 간격, 글자 크기는 Achievement.uss.
	/// 판, 키 칸, 닫기, 장소 고리는 ESC 메뉴의 FullMenu.uss 를 같이 씀 (MenuStyle)
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_Achievement", menuName = "WM/UI/" + nameof(AchievementScreenSettings))]
	public class AchievementScreenSettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("ESC 메뉴 USS. 색 토큰, 평행사변형 판, 키 칸, 닫기 버튼, 장소 고리")] public StyleSheet MenuStyle { get; private set; }
		[field: SerializeField, Tooltip("게임 화면 흐림 출력 (CustomBlurFeature). ESC 메뉴와 같은 것")] public RenderTexture BlurOutput { get; private set; }
		[field: SerializeField, Tooltip("도감 분류 판의 '전체' 아이콘")] public Sprite AllCategoryIcon { get; private set; }
		[field: SerializeField, Tooltip("메달 벽 오른쪽 도감 칸 아이콘")] public Sprite AlbumIcon { get; private set; }

		[field: Header("배치")]
		[field: SerializeField, Tooltip("USS 기준 화면 높이. 화면 높이에 맞춰 전체를 키우고 줄임")] public float ReferenceHeight { get; private set; } = 1080f;
		[field: SerializeField, Tooltip("기준 화면 최소 너비. 이보다 좁은 화면은 너비에 맞춤")] public float MinimumWidth { get; private set; } = 1600f;
		[field: SerializeField, Tooltip("메달 벽 칸 수. 위 줄이 한 칸 더 많게 두 줄로 엇갈림")] public int WallSlotCount { get; private set; } = 7;
		[field: SerializeField, Tooltip("벽 칸 사이 가로 간격 (기준 화면 px). 칸 크기는 USS")] public float WallSlotStep { get; private set; } = 216f;
		[field: SerializeField, Tooltip("벽 두 줄 사이 세로 간격 (기준 화면 px)")] public float WallRowStep { get; private set; } = 208f;
		[field: SerializeField, Tooltip("메달 벽의 최근 달성 줄 수")] public int RecentCount { get; private set; } = 3;
		[field: SerializeField, Tooltip("둥근 빛 그림 크기 (px)")] public int GlowTextureSize { get; private set; } = 128;

		[field: Header("칸 이동")]
		[field: SerializeField] public float NavOverlapTolerance { get; private set; } = 4f;
		[field: SerializeField] public float NavAcrossWeight { get; private set; } = 2f;
	}
}
