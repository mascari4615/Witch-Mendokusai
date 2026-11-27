using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 성질 놀이 화면의 자료와 수치. 색, 간격, 글자 크기는 AspectPuzzle.uss.
	/// 무대와 칸 이동은 ESC 메뉴 (Menu), 하늘 색은 마도서 (MagicBook), 마름모는 스킬 트리 시트를 같이 씀
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_AspectPuzzle", menuName = "WM/UI/" + nameof(AspectPuzzleSettings))]
	public class AspectPuzzleSettings : ScriptableObject
	{
		[field: Header("자료")]
		[field: SerializeField, Tooltip("조합, 인형, 종이, 번갈아. 시험도 같은 파일을 읽음")] public TextAsset Book { get; private set; }
		[field: SerializeField] public string PanelName { get; private set; } = "성질 놀이 (임시)";
		[field: SerializeField, Tooltip("봉인된 두루마리 두 종류와 든 것")] public ScrollTableSO Scrolls { get; private set; }

		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField] public StyleSheet SkillTreeStyle { get; private set; }
		[field: SerializeField] public MagicBookSettings MagicBook { get; private set; }

		[field: Header("판")]
		[field: SerializeField, Tooltip("육각 칸의 가장 큰 반지름 (무대 px). 종이가 크면 판에 맞게 줄임")] public float HexMaxRadius { get; private set; } = 92f;
		[field: SerializeField, Tooltip("육각 칸 사이 틈 (무대 px)")] public float HexGap { get; private set; } = 3f;
		[field: SerializeField, Tooltip("마름모 칸의 가장 큰 반지름 (무대 px)")] public float DiamondMaxRadius { get; private set; } = 84f;
		[field: SerializeField, Tooltip("마름모 칸의 크기. 1 이면 이웃과 맞닿음"), Range(0.5f, 1f)] public float DiamondFill { get; private set; } = 0.94f;
		[field: SerializeField, Tooltip("붙은 칸을 잇는 선이 두 칸 사이에서 차지하는 길이"), Range(0f, 1f)] public float LinkLength { get; private set; } = 0.4f;
		[field: SerializeField, Tooltip("붙은 칸을 잇는 선의 굵기 (무대 px)")] public float LinkWidth { get; private set; } = 6f;
		[field: SerializeField, Tooltip("인형이 놓을 성질을 몇 개 앞까지 보이나")] public int DollQueueShown { get; private set; } = 3;
	}
}
