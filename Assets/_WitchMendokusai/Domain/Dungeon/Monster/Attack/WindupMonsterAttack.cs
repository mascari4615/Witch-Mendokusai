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
		[field: SerializeField, Tooltip("예고 동안 안전 자리 (안전 섬, 빈칸) 에 띄우는 밝은 표시. 안전 자리가 없는 종류는 안 씀")] public CombatFx SafeFx { get; private set; }

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			Linger.Validate(name, errors);
			if (Linger.IsDangerous && TargetsArePositions == false)
				errors.Add($"{name}: 방향을 고정하는 공격은 아픈 물체를 남길 수 없음");
			if (SafeFx != null && HasSafeSpots == false)
				errors.Add($"{name}: {KindName} 은 안전 자리를 내지 않아 {nameof(SafeFx)} 를 안 씀");
			if (TrackingSeconds > 0f && TargetsArePositions)
				errors.Add($"{name}: 따라 도는 조준은 방향을 고정하는 종류만");
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

		// 목표 자리와 안전 자리 (때리지 않고 밝게 표시만). 기본은 안전 자리 없음
		public virtual void ResolveTargets(UnitObject unitObject, Vector3 playerPosition, Vector3 moveVelocity, List<Vector3> results, List<Vector3> safeSpots) =>
			ResolveTargets(unitObject, playerPosition, moveVelocity, results);

		// 안전 자리를 내는 종류인가. 아니면 SafeFx 를 끼웠을 때 검사가 알림
		protected virtual bool HasSafeSpots => false;

		// 안전 자리 표시 위치. spot 은 ResolveTargets 가 준 안전 자리
		public virtual CombatFxContext SafeContext(UnitObject unitObject, Vector3 spot) => new() { Position = spot, Seconds = WindupSeconds };

		// 예고 시작부터 이 초까지 목표를 주인공 쪽으로 갱신 (따라 도는 조준). 0 이면 준비 시작에 고정
		public virtual float TrackingSeconds => 0f;

		// 따라 도는 동안 매 프레임 새 목표. 기본은 그대로
		public virtual Vector3 Track(UnitObject unitObject, Vector3 playerPosition, Vector3 target, int index, float deltaTime) => target;

		// 따라 돌기가 끝나 고정되는 순간 자리마다 띄우는 표시 (색이 바뀐 예고)
		public virtual CombatFx LockFx => null;

		// 예고 위치. target 은 준비를 시작한 순간 고정한 자리, index 는 그 차례
		public abstract CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index);

		// 자리마다 예고가 이만큼 더 길고 늦게 실행됨 (초). 기본은 모두 동시
		public virtual float StrikeDelay(int index) => 0f;

		// 예고가 끝난 자리 하나를 실행. 주인공을 때렸으면 true (같은 순간의 나머지 자리는 안 때림)
		public abstract bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index);
	}
}
