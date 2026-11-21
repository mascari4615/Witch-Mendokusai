using UnityEngine;

namespace WitchMendokusai
{
	// 예고 뒤 한 번 실행하는 공격 (원거리, 장판). 박자는 BT_WindupAttack, 종류마다 다른 것은 예고 자리와 실행만
	public abstract class WindupMonsterAttack : MonsterAttack
	{
		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider, bool isSpriteLookLeft) =>
			new BT_WindupAttack(unitObject, playerProvider, this, isSpriteLookLeft);

		public override void Interrupt(BTRunner behaviour) => ((BT_WindupAttack)behaviour).Interrupt();

		// 공통 박자 외 준비 조건 (스킬 쿨타임 등)
		public virtual bool IsReady(UnitObject unitObject) => true;

		// 준비를 시작한 순간 고정할 목표 자리. 기본은 주인공 지금 자리. moveVelocity 는 입력 방향 기준 이동 속도
		public virtual Vector3 ResolveTarget(Vector3 playerPosition, Vector3 moveVelocity) => playerPosition;

		// 예고 위치. target 은 준비를 시작한 순간 고정한 자리 (ResolveTarget)
		public abstract CombatFxContext WindupContext(UnitObject unitObject, Vector3 target);

		// 예고가 끝난 순간. target 은 준비 시작 때 고정한 자리
		public abstract void Execute(UnitObject unitObject, PlayerObject player, Vector3 target);
	}
}
