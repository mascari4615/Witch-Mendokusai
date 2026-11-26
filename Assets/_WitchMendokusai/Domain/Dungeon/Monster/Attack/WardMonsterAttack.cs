using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 부수면 취소되는 물체. 준비 때 둘레에 체력 있는 물체 (몬스터) 를 세우고, 예고 (WindupSeconds) 와 큰 공격 (Payoff) 의 예고가 끝나기 전에 다 부서지면
	// 큰 공격을 취소하고 숨 고르기 (RecoverySeconds, 반격 창 RecoveryFx) 동안 주저앉음. 못 부수면 큰 공격, 남은 물체는 사라짐
	// 참고 니플헤임 얼음 기둥, 클리포트 마름모, 스우 방패, False Son 기둥. 정본 memo wm/design/boss-choice-20261126
	[CreateAssetMenu(fileName = "MA_Ward", menuName = "WM/Variable/MonsterAttack/" + nameof(WardMonsterAttack))]
	public class WardMonsterAttack : MonsterAttack
	{
		public override string KindName => "부술 물체";

		[field: Header("부술 물체")]
		[field: SerializeField, Tooltip("세울 물체. 움직이지 않고 (이동 속도 0) 공격 없는 몬스터")] public Monster Ward { get; private set; }
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 3;
		[field: SerializeField, Tooltip("시전자에게서 거리 (m)"), Min(0.5f)] public float Distance { get; private set; } = 3f;
		[field: SerializeField, Tooltip("다 못 부수면 하는 큰 공격. 예고형 낱개 공격. 그 예고가 끝나기 전까지 부수면 취소")] public WindupMonsterAttack Payoff { get; private set; }
		[field: SerializeField, Tooltip("예고 동안 시전자 발밑 판 반지름 (m). 큰 공격이 올 자리"), Min(0.1f)] public float TelegraphRadius { get; private set; } = 3f;
		[field: SerializeField, Tooltip("물체가 서는 자리 이펙트 (WindupFx) 크기 (m)"), Min(0.1f)] public float WardFxRadius { get; private set; } = 0.8f;
		[field: SerializeField, Tooltip("다 부서진 순간 시전자에게 나오는 이펙트 (금 가며 깨짐). 비우면 없음")] public CombatFx BreakFx { get; private set; }

		// WindupFx 는 물체가 서는 자리마다 (솟음)
		protected override bool PlaysWindupFx => true;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_WardAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_WardAttack)behaviour).Interrupt();

		// 물체가 선 뒤 부술 수 있는 가장 짧은 시간 (큰 공격이 바로 시작할 때)
		public float MinBreakSeconds => WindupSeconds + (Payoff != null ? Payoff.WindupSeconds : 0f);

		// 서 있는 물체가 없고 아직 큰 공격 전이면 취소
		public static bool IsCancelled(int standing, float now, float deadline) => standing == 0 && now < deadline;

		// 물체 자리. 시전자 둘레 고리에 고르게, 첫 자리는 주인공 쪽
		public void WardPositions(Vector3 caster, Vector3 player, List<Vector3> results)
		{
			float towardPlayer = Quaternion.LookRotation(ScatterZoneMonsterAttack.FlatDirection(caster, player)).eulerAngles.y;
			ScatterZoneMonsterAttack.Arrange(ScatterArrangement.Ring, caster, player, Count, Distance, 0f, towardPlayer, () => 0f, results);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (Ward == null)
				errors.Add($"{name}: 세울 물체가 비어 있음");
			else if (Ward.Prefab == null || Ward.Prefab.GetComponent<MonsterObject>() == null)
				errors.Add($"{name}: {Ward.name} 프리팹에 {nameof(MonsterObject)} 이 없음");
			else if (Ward == owner || Ward.Type == MonsterType.Boss)
				errors.Add($"{name}: 자기 자신이나 보스를 물체로 세울 수 없음");
			else if (Ward.Attack != null)
				errors.Add($"{name}: 물체 {Ward.name} 이 공격을 가짐 (서 있기만 해야 함)");
			if (Payoff == null)
				errors.Add($"{name}: 큰 공격이 비어 있음");
			else
				Payoff.Validate(owner, errors);
			// 큰 공격 사거리가 더 짧으면 물체를 세운 뒤 쫓기만 하며 멈춤
			if (Payoff != null && Payoff.TriggerRange < TriggerRange)
				errors.Add($"{name}: 큰 공격 사거리 {Payoff.TriggerRange} 가 {TriggerRange} 보다 짧음");
			if (RecoveryFx == null)
				errors.Add($"{name}: 다 부쉈을 때 반격 창 ({nameof(RecoveryFx)}) 이 비어 있음");
			if (Payoff != null && RecoverySeconds <= Payoff.RecoverySeconds)
				errors.Add($"{name}: 부쉈을 때 주저앉는 {RecoverySeconds} 초가 큰 공격 뒤 숨 고르기 {Payoff.RecoverySeconds} 초보다 길어야 보상");
		}
	}
}
