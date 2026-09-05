using UnityEngine;

namespace WitchMendokusai
{
    public enum BuildStampCorner
    {
        RightTop,
        RightBottom,
        LeftTop,
        LeftBottom,
    }

    /// <summary>
    /// TASK-WM-201 — 화면 구석 빌드 표시기 설정. 위치·크기·투명도 전부 노출한다
    /// (수치 하드코딩 금지 룰). `Resources/BuildStampSettings.asset` 하나가 정본.
    /// </summary>
    [CreateAssetMenu(fileName = nameof(BuildStampSettings), menuName = "WM/BuildStampSettings")]
    public class BuildStampSettings : ScriptableObject
    {
        [field: Header("켬끔")]
        [field: Tooltip("개발 빌드에서 표시. 실기에서 「지금 이게 어느 빌드인지」를 눈으로 확인하는 목적.")]
        [field: SerializeField] public bool ShowInDevelopmentBuild { get; private set; } = true;

        [field: Tooltip("릴리스 빌드에서도 표시. 기본 끔 — 플레이어가 볼 화면이다.")]
        [field: SerializeField] public bool ShowInReleaseBuild { get; private set; } = false;

        [field: Tooltip("에디터 플레이에서도 표시. 기본 끔 — 에디터는 이미 어느 코드인지 안다.")]
        [field: SerializeField] public bool ShowInEditor { get; private set; } = false;

        [field: Header("자리")]
        [field: SerializeField] public BuildStampCorner Corner { get; private set; } = BuildStampCorner.RightTop;

        [field: Tooltip("화면 가장자리에서 띄울 여백(px, 기준 해상도).")]
        [field: SerializeField] public float Margin { get; private set; } = 8f;

        [field: Tooltip("노치·제스처바를 피할 추가 여백(px).")]
        [field: SerializeField] public float SafeAreaPadding { get; private set; } = 4f;

        [field: Header("보임새")]
        [field: Tooltip("접힌 한 줄의 글자 크기(px).")]
        [field: SerializeField] public int CollapsedFontSize { get; private set; } = 11;

        [field: Tooltip("펼친 카드의 글자 크기(px).")]
        [field: SerializeField] public int ExpandedFontSize { get; private set; } = 13;

        [field: Tooltip("접힌 한 줄의 불투명도 (0~1). 게임 화면을 가리지 않게 낮게.")]
        [field: Range(0.05f, 1f)]
        [field: SerializeField] public float CollapsedOpacity { get; private set; } = 0.45f;

        [field: Tooltip("글자색.")]
        [field: SerializeField] public Color TextColor { get; private set; } = Color.white;

        [field: Tooltip("펼친 카드 배경색.")]
        [field: SerializeField] public Color PanelColor { get; private set; } = new Color(0f, 0f, 0f, 0.82f);

        [field: Header("그리는 층")]
        [field: Tooltip("다른 UI 위에 오게 하는 정렬 순서. 게임 UI 보다 커야 한다.")]
        [field: SerializeField] public float SortingOrder { get; private set; } = 9000f;
    }
}
