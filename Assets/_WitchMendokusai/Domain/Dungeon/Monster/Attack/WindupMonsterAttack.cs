using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 예고 뒤 실행하는 공격 (원거리, 장판, 직선 띠, 소환). 박자는 BT_WindupAttack, 종류마다 다른 것은 예고 자리와 실행만
	public abstract class WindupMonsterAttack : MonsterAttack
	{
		[field: Header("몸과 물체")]
		[field: SerializeField] public AttackPose Pose { get; private set; } = new();
		[field: SerializeField, Tooltip("예고 동안 나타나는 물체 (날아오는 것, 땅에서 돋는 것). 때리는 자리마다 하나, Seconds 가 때리는 순간")] public CombatFx WindupFx { get; private set; }
		[field: SerializeField, Tooltip("때리는 순간 이펙트. 빗나가도 나옴. 때리는 자리마다 하나")] public CombatFx StrikeFx { get; private set; }

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupFx != null && WindupSeconds <= 0f)
				errors.Add($"{name}: 예고 물체가 있는데 예고 초가 0");
			// 애니메이터가 도는 몸은 매 프레임 그림을 덮음
			if (Pose.HasSprite && (owner.Sprites == null || owner.Sprites.Count == 0))
				errors.Add($"{name}: 자세 그림이 있는데 {owner.name} 이 애니메이터 몸 ({nameof(owner.Sprites)} 비어 있음)");
		}

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_WindupAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_WindupAttack)behaviour).Interrupt();

		// 공통 박자 외 준비 조건 (스킬 쿨타임 등)
		public virtual bool IsReady(UnitObject unitObject) => true;

		// 준비를 시작한 순간 고정할 목표 자리. 기본은 주인공 지금 자리. moveVelocity 는 입력 방향 기준 이동 속도
		public virtual Vector3 ResolveTarget(Vector3 playerPosition, Vector3 moveVelocity) => playerPosition;

		// 준비를 시작한 순간 고정할 목표 자리들. 예고는 하나마다 하나. 기본은 ResolveTarget 하나
		public virtual void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results) =>
			results.Add(ResolveTarget(playerPosition, moveVelocity));

		// 예고 위치. target 은 준비를 시작한 순간 고정한 자리 (ResolveTarget)
		public abstract CombatFxContext WindupContext(UnitObject unitObject, Vector3 target);

		// 자리마다 예고가 이만큼 더 길고 늦게 실행됨 (초). 기본은 모두 동시
		public virtual float StrikeDelay(int index) => 0f;

		// 예고가 끝난 순간. targets 는 준비 시작 때 고정한 자리들 중 지금 때가 된 것
		public abstract void Execute(UnitObject unitObject, PlayerObject player, List<Vector3> targets);
	}
}
