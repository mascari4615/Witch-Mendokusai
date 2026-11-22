using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 하늘 화면의 모양과 수치. 색, 간격, 글자 크기는 MagicBook.uss.
	/// 무대 기준 크기, 칸 이동 수치, 빛 그림 크기, FullMenu.uss 는 ESC 메뉴와 같은 것 (Menu).
	/// 장마다 다른 각도와 별 간격은 ChapterSO 에
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_MagicBook", menuName = "WM/UI/" + nameof(MagicBookSettings))]
	public class MagicBookSettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("ESC 메뉴 설정. 무대 기준 크기, 칸 이동 수치, 빛 그림 크기, FullMenu.uss 를 같이 씀")] public FullMenuSettings Menu { get; private set; }
		[field: SerializeField, Tooltip("가운데 마도서 별 아이콘")] public Sprite HubIcon { get; private set; }

		[field: Header("별 자리")]
		[field: SerializeField, Tooltip("가운데에서 장의 첫 별까지 거리 (하늘 px)")] public float FirstOffset { get; private set; } = 140f;
		[field: SerializeField, Tooltip("같은 깊이의 갈래 사이 간격 (하늘 px)")] public float BranchStep { get; private set; } = 132f;
		[field: SerializeField, Tooltip("별을 줄에서 살짝 흔드는 폭 (하늘 px). 0 이면 자로 잰 줄")] public float Jitter { get; private set; } = 14f;
		[field: SerializeField, Tooltip("별자리 맨 위 (또는 맨 아래) 별에서 장 이름까지 거리 (하늘 px)")] public float ChapterNameGap { get; private set; } = 64f;
		[field: SerializeField, Tooltip("별 아래 이름이 차지하는 높이 (하늘 px). 아래쪽 장 이름을 그만큼 더 내림")] public float BelowLabelHeight { get; private set; } = 60f;

		// 선과 가루의 색은 MagicBook.uss 의 사용자 속성 (--wm-mb-line-done 등)
		[field: Header("선")]
		[field: SerializeField, Tooltip("다 깬 길의 굵기 (하늘 px)")] public float DoneLineWidth { get; private set; } = 2.8f;
		[field: SerializeField, Tooltip("다 깬 길 뒤 번짐 굵기")] public float DoneGlowWidth { get; private set; } = 9f;
		[field: SerializeField, Tooltip("열린 길 (깬 별에서 아직 안 깬 별로) 의 굵기")] public float OpenLineWidth { get; private set; } = 2f;
		[field: SerializeField, Tooltip("열린 길 점선의 한 칸과 빈칸 길이")] public Vector2 OpenDash { get; private set; } = new(10f, 7f);
		[field: SerializeField, Tooltip("아직 꺼진 길의 굵기")] public float DimLineWidth { get; private set; } = 1.2f;

		[field: Header("하늘 바탕")]
		[field: SerializeField, Tooltip("고운 별 가루 수")] public int DustCount { get; private set; } = 420;
		[field: SerializeField, Tooltip("가루 밝기 범위 (투명도)")] public Vector2 DustAlpha { get; private set; } = new(0.25f, 0.7f);
		[field: SerializeField, Tooltip("몇 개마다 하나를 큰 가루로")] public int DustBigEvery { get; private set; } = 9;

		[field: Header("카메라")]
		[field: SerializeField] public float ZoomMin { get; private set; } = 0.45f;
		[field: SerializeField] public float ZoomMax { get; private set; } = 1.8f;
		[field: SerializeField, Tooltip("휠 한 칸에 곱하는 배율")] public float ZoomStep { get; private set; } = 1.12f;
		[field: SerializeField, Tooltip("열 때 담는 칸을 별 자리보다 넓히는 양 (하늘 px). 별 이름 길이만큼")] public Vector2 FitPadding { get; private set; } = new(200f, 70f);
		[field: SerializeField, Tooltip("열 때 하늘 전체를 담고 남기는 여백 (화면 비율)")] public float FitMargin { get; private set; } = 0.94f;
		[field: SerializeField, Tooltip("고른 별이 이 안쪽 (화면 비율) 을 벗어나면 카메라가 따라감")] public float FollowInset { get; private set; } = 0.18f;
		[field: SerializeField, Tooltip("카메라가 목표로 다가가는 빠르기 (초당, 클수록 빠름)")] public float FollowSharpness { get; private set; } = 9f;
		[field: SerializeField, Tooltip("카메라가 목표에서 이만큼 (화면 px) 안이면 멈춤")] public float SettleDistance { get; private set; } = 0.05f;
		[field: SerializeField, Tooltip("카메라 배율이 목표에서 이만큼 안이면 멈춤")] public float SettleZoom { get; private set; } = 0.0005f;
		[field: SerializeField, Tooltip("장 이름을 골랐을 때 다가가는 배율")] public float ChapterZoom { get; private set; } = 1.15f;

		[field: Header("팝업")]
		[field: SerializeField, Tooltip("별 가운데에서 팝업 가장자리까지 (무대 px)")] public float PopupOffset { get; private set; } = 64f;
		[field: SerializeField, Tooltip("팝업이 무대 가장자리에서 떨어지는 최소 거리 (무대 px)")] public float PopupEdge { get; private set; } = 40f;
	}
}
