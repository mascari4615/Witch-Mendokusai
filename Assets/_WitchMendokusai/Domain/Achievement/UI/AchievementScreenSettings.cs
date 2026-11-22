using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 전체 화면 (메달 벽, 도감, 단계 상세) 과 달성 띠의 모양과 수치. 색, 간격, 글자 크기는 Achievement.uss.
	/// 무대 기준 크기, 흐린 배경, 칸 이동 수치, 판과 키 칸 USS 는 ESC 메뉴와 같은 것 (Menu)
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_Achievement", menuName = "WM/UI/" + nameof(AchievementScreenSettings))]
	public class AchievementScreenSettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("ESC 메뉴 설정. 무대 기준 크기, 흐림 출력, 칸 이동 수치, FullMenu.uss 를 같이 씀")] public FullMenuSettings Menu { get; private set; }
		[field: SerializeField, Tooltip("도감 분류 판의 '전체' 아이콘")] public Sprite AllCategoryIcon { get; private set; }
		[field: SerializeField, Tooltip("메달 벽 오른쪽 도감 칸 아이콘")] public Sprite AlbumIcon { get; private set; }
		[field: SerializeField, Tooltip("메달 벽의 빈 자리 그림")] public Sprite WallSocket { get; private set; }

		[field: Header("배치")]
		[field: SerializeField, Tooltip("메달 벽 칸 수 상한. 업적 수가 더 적으면 업적 수만큼. 위 줄이 한 칸 더 많게 두 줄로 엇갈림")] public int WallSlotCount { get; private set; } = 7;
		[field: SerializeField, Tooltip("벽 칸 사이 가로 간격 (기준 화면 px). 칸 크기는 USS")] public float WallSlotStep { get; private set; } = 216f;
		[field: SerializeField, Tooltip("벽 두 줄 사이 세로 간격 (기준 화면 px)")] public float WallRowStep { get; private set; } = 208f;
		[field: SerializeField, Tooltip("벽과 양옆 판 사이 최소 간격 (기준 화면 px, ui.md 판 사이 5vh)")] public float WallSideGap { get; private set; } = 54f;
		[field: SerializeField, Tooltip("메달 벽의 최근 달성 줄 수")] public int RecentCount { get; private set; } = 3;
	}
}
