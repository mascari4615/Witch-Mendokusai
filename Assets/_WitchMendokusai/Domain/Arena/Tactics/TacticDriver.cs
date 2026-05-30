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
