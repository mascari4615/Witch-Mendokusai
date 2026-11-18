using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 알림 한 종류. 어느 띠에 어떤 글을 얼마나 띄울지
	/// 글의 자리표: {0} 남은 초, {1} 몬스터 이름, {2} 마릿수
	/// </summary>
	[Serializable]
	public class SurvivalAnnounceLine
	{
		[field: SerializeField, Tooltip("띄울 띠. 비우면 이 알림은 안 뜸")] public CombatFx Banner { get; private set; }
		[field: SerializeField, Tooltip("큰 글. {0} 남은 초, {1} 몬스터 이름, {2} 마릿수")] public string Title { get; private set; } = "";
		[field: SerializeField, Tooltip("작은 글. 자리표는 큰 글과 같음. 비워도 됨")] public string Subtitle { get; private set; } = "";
		[field: SerializeField, Min(0f), Tooltip("머무는 시간 (초). 0 이면 띠 에셋의 값")] public float HoldSeconds { get; private set; }
	}

	/// <summary> 생존 모드 한 판의 화면 알림 설정. SurvivalAnnouncer 가 읽음 </summary>
	[CreateAssetMenu(fileName = "Announce_0_New", menuName = "WM/Fx/" + nameof(SurvivalAnnounceSettings))]
	public class SurvivalAnnounceSettings : ScriptableObject
	{
		[field: Header("정예")]
		[field: SerializeField, Tooltip("정예 예고 (사건표의 예고 시점)")] public SurvivalAnnounceLine EliteAnnounced { get; private set; } = new();
		[field: SerializeField, Tooltip("정예가 실제로 나온 순간")] public SurvivalAnnounceLine EliteStarted { get; private set; } = new();

		[field: Header("대규모 무리")]
		[field: SerializeField, Tooltip("무리 예고 (사건표의 예고 시점)")] public SurvivalAnnounceLine SwarmAnnounced { get; private set; } = new();
		[field: SerializeField, Tooltip("무리가 실제로 나온 순간")] public SurvivalAnnounceLine SwarmStarted { get; private set; } = new();

		[field: Header("보스")]
		[field: SerializeField, Tooltip("보스 예고. 시점은 MonsterSpawner 의 보스 예고 초")] public SurvivalAnnounceLine BossAnnounced { get; private set; } = new();
		[field: SerializeField, Tooltip("보스가 나온 순간. 이름 띠")] public SurvivalAnnounceLine BossSpawned { get; private set; } = new();
	}
}
