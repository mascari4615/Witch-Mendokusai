using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 끌기. 예고 -> 끌어당김 (안쪽 원에 닿으면 간격마다 피해) -> 숨 고르기. 세기는 주인공 걷기 속도의 비율 (1 미만이면 반대로 걸어 벗어남)
	// 아쿠아쿠 빨아들이기 (SlimeKingPullPattern) 와 같은 계산 (SlimeKingPatternMath). 아쿠아쿠는 그 코드 그대로
	[CreateAssetMenu(fileName = "MA_Pull", menuName = "WM/Variable/MonsterAttack/" + nameof(PullMonsterAttack))]
	public class PullMonsterAttack : MonsterAttack
	{
		public override string KindName => "끌기";

		[field: Header("끌기")]
		[field: SerializeField, Tooltip("끄는 거리 (m)"), Min(0.5f)] public float Radius { get; private set; } = 7f;
		[field: SerializeField, Tooltip("끄는 초"), Min(0.1f)] public float PullSeconds { get; private set; } = 2f;
		[field: SerializeField, Tooltip("주인공 걷기 속도에 대한 비율"), Range(0f, 0.99f)] public float SpeedRatio { get; private set; } = 0.75f;
		[field: SerializeField, Tooltip("안쪽 피해 원 (m)"), Min(0.1f)] public float CoreRadius { get; private set; } = 1.4f;
		[field: SerializeField, Tooltip("안쪽 피해 원 예고. 예고 초와 끄는 초 동안 이어짐")] public CombatFx CoreTelegraph { get; private set; }
		[field: SerializeField, Min(1)] public int CoreDamage { get; private set; } = 3;
		[field: SerializeField, Tooltip("안쪽 피해 간격 (초)"), Min(0.1f)] public float CoreInterval { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("한 번 건 끌림이 남는 초. 시전자가 사라지면 이 시간 뒤 풀림"), Min(0.05f)] public float HoldSeconds { get; private set; } = 0.25f;

		[field: SerializeField, Tooltip("끄는 거리 안에 이 유닛이 남긴 물체 (Linger) 를 빨아들여 터뜨림")] public bool CracksLinger { get; private set; }
		[field: SerializeField, Tooltip("끌기 시작에서 첫 터짐까지 (초)"), Min(0.3f)] public float CrackDelaySeconds { get; private set; } = 0.7f;
		[field: SerializeField, Tooltip("가까운 것부터 먼 것으로 터짐이 퍼지는 빠르기 (m/s)"), Min(0.5f)] public float CrackSpeed { get; private set; } = 6f;

		// 때리는 순간 이펙트는 끌기 시작에, 길이는 끄는 초
		protected override bool PlaysStrikeFx => true;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_PullZone(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_PullZone)behaviour).Interrupt();

		public DamageInfo MakeCoreDamage(UnitObject source) => MakeDamage(source, CoreDamage);

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 끌기는 예고가 있어야 피할 수 있음 ({nameof(WindupSeconds)} 0)");
			if (CoreTelegraph == null)
				errors.Add($"{name}: 안쪽 피해 원의 예고가 없음 ({nameof(CoreTelegraph)})");
			if (CoreRadius >= Radius)
				errors.Add($"{name}: 안쪽 원 {CoreRadius} 이 끄는 거리 {Radius} 보다 큼");
			if (CracksLinger && CrackDelaySeconds + Radius / CrackSpeed > PullSeconds + RecoverySeconds)
				errors.Add($"{name}: 먼 물체가 끌기와 숨 고르기가 끝난 뒤에 터짐 (거리 {Radius}, 빠르기 {CrackSpeed})");
		}
	}
}
