using UnityEngine;

namespace WitchMendokusai
{
	// 수선 수치. 장비 (EquipmentData.Mend) 가 들고 오고, 실 카드 (MEND_FREQUENCY) 를 한 장 받으면 돌기 시작
	// 판정 MendRules (DomainSDK), 실행 MendRunner
	[CreateAssetMenu(fileName = nameof(MendData), menuName = "WM/Variable/" + nameof(MendData))]
	public class MendData : ScriptableObject
	{
		[field: SerializeField, Tooltip("첫 실 카드 때 수선 주기 (레벨업 횟수)"), Min(1)] public int Interval { get; private set; } = 4;
		[field: SerializeField, Tooltip("실 카드로 줄여도 이 아래로는 안 감"), Min(1)] public int MinInterval { get; private set; } = 1;
		[field: SerializeField, Tooltip("한 번에 대상 장비 MendStats 에 더하는 양"), Min(0)] public int Amount { get; private set; } = 2;
		[field: SerializeField, Tooltip("수선할 때 주인공 머리 위 글자. {0} 장비 이름")] public string TextFormat { get; private set; } = "{0} 수선";
	}
}
