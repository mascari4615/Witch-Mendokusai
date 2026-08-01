using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 출전 유닛 1체에 붙어 전술(TacticProgram)을 TimeManager 틱으로 구동.
	/// 실제 행동(ITacticActuator)은 UnitObject(SkillHandler/UnitMovement)에 적용.
	/// ArenaMatch 가 Initialize 로 deps(전술/타겟팅/틱) 주입. RecompileTactic = 일시정지 핫스왑.
	/// </summary>
	[RequireComponent(typeof(ArenaCombatant))]
	public class TacticDriver : MonoBehaviour, ITacticActuator
	{
		[Header("접근 정지 (겹침 방지 — TASK-WM-194)")]
		[Tooltip("사거리가 0이어도 목표에서 이만큼은 떨어져 선다. 0이면 목표 안으로 파고들어 화면에서 사라진다.")]
		[SerializeField] private float minStopDistance = 2f;
		[Tooltip("같은 목표를 노리는 무리가 겹치지 않게 개체별로 정지 거리를 이 간격씩 벌린다.")]
		[SerializeField] private float ringSlotSpacing = 0.6f;
		[Tooltip("정지 거리 층 수 — 무리가 클수록 여러 겹의 고리로 둘러싼다.")]
		[SerializeField, Min(1)] private int ringSlotCount = 4;

		private float MIN_STOP_DISTANCE => minStopDistance;
		private float RING_SLOT_SPACING => ringSlotSpacing;
		private int RING_SLOT_COUNT => ringSlotCount;

		private ArenaCombatant self;
		private UnitObject unitObject;
		private TimeManager timeManager;
		private TacticBTRunner runner;
		private float tickAccum;

		private void Awake()
		{
			self = GetComponent<ArenaCombatant>();
			unitObject = GetComponent<UnitObject>();
		}

		public void Initialize(TacticProgram program, ITargetResolver targeting, TimeManager timeManager)
		{
			this.timeManager = timeManager;
			tickAccum = 0f; // 풀 재사용 driver 의 잔여 누적 리셋(재매치 첫 게이트 타이밍 결정성).
			TacticContext context = new(self, targeting, this, IsSkillReady);
			runner = new TacticBTRunner(context, program);

			// 전술 코어가 유일 시전자 → 자동시전 억제(트랩#1).
			unitObject.SkillHandler.AutoCastEnabled = false;
			timeManager.RegisterCallback(OnTick);
		}

		/// <summary> 일시정지 중 룰 재편집 → 다음 틱 재컴파일. </summary>
		public void RecompileTactic(TacticProgram program)
		{
			runner?.SetProgram(program);
		}

		/// <summary> 매치 종료 시 호출 — 전술 구동 정지(종료 후 좀비 틱/actuation 방지). </summary>
		public void StopDriving()
		{
			runner = null;
			if (timeManager != null)
				timeManager.RemoveCallback(OnTick);
		}

		private void OnDestroy()
		{
			if (timeManager != null)
				timeManager.RemoveCallback(OnTick);
		}

		private void OnTick()
		{
			if (runner == null || self.IsAlive == false)
				return;

			// TimeManager 0.05s 틱 → BT 0.1s 게이트.
			tickAccum += TimeManager.TICK;
			if (tickAccum < BTRunner.TICK)
				return;
			tickAccum -= BTRunner.TICK; // 잔여 보존(WorldClock house 패턴) — 비정수 비율 드리프트 방지

			runner.UpdateBT();
		}

		private bool IsSkillReady(int skillSlot)
		{
			return unitObject.SkillHandler.SkillDic.TryGetValue(skillSlot, out Skill skill) && skill.IsReady;
		}

		// --- ITacticActuator (UnitObject 에 적용) ---

		public void UseSkill(int skillSlot, ICombatant target)
		{
			UnitObject targetUnit = (target as ArenaCombatant)?.UnitObject;
			unitObject.SkillHandler.UseSkill(skillSlot, targetUnit);
		}

		public void MoveToward(ICombatant target)
		{
			if (target == null)
				return;

			Vector3 direction = target.Position - self.Position;
			direction.y = 0f;
			unitObject.UnitMovement.SetMoveDirection(direction.normalized);
		}

		/// <summary>
		/// 사거리에서 멈추는 접근 — 목표 안으로 파고들지 않는다.
		///
		/// ★ 겹침 방지가 본질(TASK-WM-194 실측): 목표에 겹쳐 선 유닛은 상대 스프라이트에 가려 **화면에서
		///   사라진다**. 개척에서 마수가 코어 좌표에 그대로 쌓여 플레이어가 "다 잡았는데 안 넘어간다"고
		///   판단했다. 그래서 stopDistance 가 0이어도 최소 간격(MIN_STOP_DISTANCE)은 항상 둔다.
		/// ★ 같은 목표를 여럿이 노리면 다 같은 점에 몰리므로 개체마다 간격을 조금씩 다르게 준다
		///   (CombatantId 파생 = 결정적, 리플레이 정합 유지). 결과적으로 목표를 둘러싼 고리가 된다.
		/// </summary>
		public void Approach(ICombatant target, float stopDistance)
		{
			if (target == null)
			{
				unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				return;
			}

			Vector3 direction = target.Position - self.Position;
			direction.y = 0f;

			float effectiveStop = Mathf.Max(stopDistance, MIN_STOP_DISTANCE) + PerUnitRingOffset();
			if (direction.sqrMagnitude <= effectiveStop * effectiveStop)
			{
				unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				return;
			}

			unitObject.UnitMovement.SetMoveDirection(direction.normalized);
		}

		// 개체별 정지 거리 흔들기 — 같은 목표를 노리는 무리가 한 점에 겹쳐 서는 것 방지.
		// id 파생이라 결정적(같은 입력 → 같은 배치).
		private float PerUnitRingOffset()
		{
			int id = self != null ? self.CombatantId : 0;
			return (id % RING_SLOT_COUNT) * RING_SLOT_SPACING;
		}

		public void Retreat(ICombatant target)
		{
			if (target == null)
			{
				unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				return;
			}

			Vector3 direction = self.Position - target.Position;
			direction.y = 0f;
			unitObject.UnitMovement.SetMoveDirection(direction.normalized);
		}

		public void Hold()
		{
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
		}
	}
}
