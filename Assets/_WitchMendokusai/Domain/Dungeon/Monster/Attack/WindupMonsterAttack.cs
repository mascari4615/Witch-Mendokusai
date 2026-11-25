using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 예고 뒤 실행하는 공격 (원거리, 장판, 고리, 직선 띠, 소환). 박자는 BT_WindupAttack, 종류마다 다른 것은 예고 자리와 실행만
	public abstract class WindupMonsterAttack : MonsterAttack
	{
		[field: SerializeField, Tooltip("예고 물체를 첫 자리에만. 한 물체가 여러 번 때릴 때")] public bool WindupFxOnce { get; private set; }
		[field: SerializeField, Tooltip("늦게 때리는 자리의 바닥 예고를 앞 자리가 때린 뒤에 띄움. 꺼져 있으면 모두 처음부터")] public bool TelegraphInTurn { get; private set; }
		[field: SerializeField, Tooltip("때린 자리에 남는 물체. 초가 0 이면 안 남음")] public LingerSettings Linger { get; private set; } = new();

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			Linger.Validate(name, errors);
			if (Linger.IsDangerous && TargetsArePositions == false)
				errors.Add($"{name}: 방향을 고정하는 공격은 아픈 물체를 남길 수 없음");
			if (Linger.Seconds > 0f && WindupFx == null)
				errors.Add($"{name}: 남는 물체가 있는데 그것을 그릴 {nameof(WindupFx)} 이 비어 있음");
		}

		protected override bool PlaysWindupFx => true;
		protected override bool PlaysStrikeFx => true;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_WindupAttack(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_WindupAttack)behaviour).Interrupt();

		// ResolveTargets 가 주는 것이 때리는 자리인가 (방향이면 false)
		protected virtual bool TargetsArePositions => true;

		// 공통 박자 외 준비 조건 (스킬 쿨타임 등)
		public virtual bool IsReady(UnitObject unitObject) => true;

		// 준비를 시작한 순간 고정할 목표 자리. 기본은 주인공 지금 자리. moveVelocity 는 입력 방향 기준 이동 속도
		public virtual Vector3 ResolveTarget(Vector3 playerPosition, Vector3 moveVelocity) => playerPosition;

		// 준비를 시작한 순간 고정할 목표 자리들. 예고는 하나마다 하나. 기본은 ResolveTarget 하나
		public virtual void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results) =>
			results.Add(ResolveTarget(playerPosition, moveVelocity));

		// 예고 위치. target 은 준비를 시작한 순간 고정한 자리, index 는 그 차례
		public abstract CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index);

		// 자리마다 예고가 이만큼 더 길고 늦게 실행됨 (초). 기본은 모두 동시
		public virtual float StrikeDelay(int index) => 0f;

		// 예고가 끝난 자리 하나를 실행. 주인공을 때렸으면 true (같은 순간의 나머지 자리는 안 때림)
		public abstract bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index);
	}
}
