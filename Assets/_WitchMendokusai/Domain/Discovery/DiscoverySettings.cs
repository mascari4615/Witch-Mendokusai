using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 화면의 모양. 색, 간격, 글자 크기는 USS.
	/// 무대 크기, 칸 이동 수치, 판과 키 칸은 ESC 메뉴 (Menu), 카드와 분류 판과 달성 띠는 업적 (Cards) 과 같은 것
	/// </summary>
	[CreateAssetMenu(fileName = "SET_0_Discovery", menuName = "WM/UI/" + nameof(DiscoverySettings))]
	public class DiscoverySettings : ScriptableObject
	{
		[field: Header("모양")]
		[field: SerializeField] public StyleSheet Style { get; private set; }
		[field: SerializeField, Tooltip("업적 USS. 카드, 분류 판, 거르기, 단계 마름모, 막대, 달성 띠를 같이 씀")] public StyleSheet Cards { get; private set; }
		[field: SerializeField, Tooltip("ESC 메뉴 설정. 무대 기준 크기, 칸 이동 수치, FullMenu.uss 를 같이 씀")] public FullMenuSettings Menu { get; private set; }
		[field: SerializeField, Tooltip("몬스터 분류 아이콘")] public Sprite MonsterIcon { get; private set; }
		[field: SerializeField, Tooltip("아이콘이 없는 분류")] public Sprite OtherIcon { get; private set; }

		[field: Header("완성 도장")]
		[field: SerializeField, Tooltip("금판과 금색 단계. 값은 재질에서")] public Material StampMaterial { get; private set; }
		[field: SerializeField, Tooltip("도장 한 장의 텍스처 크기. 장에서 크게 보이는 것 기준")] public int StampTextureSize { get; private set; } = 256;
	}
}
