using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 출전 유닛 1체에 붙어 전술(TacticProgram)을 TimeManager 틱으로 구동.
	/// 실제 행동(ITacticActuator)은 UnitObject(SkillHandler/UnitMovement)에 적용.
	/// ArenaMatch 가 Initialize 로 deps(전술/타겟팅/틱) 주입. RecompileTactic = 일시정지 핫스왑.
	/// </summary>
	[RequireComponent(typeof(MatchCombatant))]
	public class TacticDriver : MonoBehaviour, ITacticActuator
	{
		[Header("접근 정지 (겹침 방지 — TASK-WM-194)")]
		[Tooltip("사거리가 0이어도 목표에서 이만큼은 떨어져 선다. 0이면 목표 안으로 파고들어 화면에서 사라진다.")]
		[SerializeField] private float minStopDistance = 2f;
		[Tooltip("같은 목표를 노리는 무리가 겹치지 않게 개체별로 정지 거리를 이 간격씩 벌린다.")]
		[SerializeField] private float ringSlotSpacing = 0.6f;
		[Tooltip("정지 거리 층 수 — 무리가 클수록 여러 겹의 고리로 둘러싼다.")]
		[SerializeField, Min(1)] private int ringSlotCount = 4;

		/// <summary>
		/// 이 유닛이 목표에서 *가장 멀리* 멈춰 설 수 있는 거리 — 최소 간격 + 바깥 고리 몫.
		///
		/// ★ 왜 밖에 내주는가 (TASK-WM-194 실증): 개척은 「목표에 닿으면 샜다」로 마수를 치우는데,
		///   치우는 반경이 이 값보다 작으면 바깥 고리에 선 마수는 **영원히 닿지 않고 그 자리에 선다**
		///   — 살아있는 마수가 0이 안 되니 파도가 안 끝난다. 두 숫자를 각자 적으면 반드시 갈라지므로
		///   멈추는 쪽이 자기 거리를 알려주고, 치우는 쪽이 그걸 읽는다.
		/// </summary>
		public float MaxStopDistance => Mathf.Max(0f, minStopDistance) + Mathf.Max(0, ringSlotCount - 1) * ringSlotSpacing;

		private MatchCombatant self;
		private UnitObject unitObject;
		private TimeManager timeManager;
		private TacticBTRunner runner;
		private float tickAccum;

		private void Awake()
		{
			self = GetComponent<MatchCombatant>();
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
			UnitObject targetUnit = (target as MatchCombatant)?.UnitObject;
			unitObject.SkillHandler.UseSkill(skillSlot, targetUnit);

			// 걸으면서 쏘는 유닛은 *조준 때문에 목적지를 잊으면* 안 된다. 공격 룰이 선택되는 동안
			// 접근 룰은 안 돌므로, 마지막으로 향하던 곳을 여기서 다시 겨눠준다
			// (안 그러면 옛 방향으로 계속 걸어가 판 밖으로 나간다 — 굳는 것만 피하고 새 사고를 만든다).
			if (StopsToAttack == false && lastApproachTarget != null && lastApproachTarget.IsAlive)
				unitObject.UnitMovement.SetMoveDirection(SteerToward(lastApproachTarget.Position));
		}

		// 마지막으로 접근하던 목표 — 걸으면서 쏠 때 방향을 되찾는 데 쓴다.
		private ICombatant lastApproachTarget;

		/// <summary>
		/// 길 안내자 — 꽂히면 지형을 우회하고, 없으면 직선(투기장 기존 동작 그대로).
		/// 소유 매치가 스폰 시 넘긴다.
		/// </summary>
		public ITacticNavigator Navigator { get; set; }

		/// <summary>
		/// 이 개체의 고유 값(0~1) — 같은 거리의 길이 여럿일 때 어느 길을 밟을지를 가른다.
		/// 태어날 때 한 번 정해지고 안 바뀐다(매번 다시 뽑으면 걸음마다 길이 바뀌어 덜덜 떤다).
		/// </summary>
		public float Lane { get; } = UnityEngine.Random.value;

		public void MoveToward(ICombatant target)
		{
			if (target == null)
				return;

			unitObject.UnitMovement.SetMoveDirection(SteerToward(target.Position));
		}

		/// <summary>
		/// self 위치에서 목표로 향하는 실제 이동 방향. 안내자가 있으면 그쪽 말을 듣는다 —
		/// 없거나 안내 불가면 직선(지형이 없는 판에선 이게 정답이고, 안내 실패 시에도 굳어버리지 않는다).
		/// </summary>
		private Vector3 SteerToward(Vector3 targetPosition)
		{
			if (Navigator != null && Navigator.TryGetSteering(self.Position, targetPosition, Lane, out Vector3 steered))
				return steered;

			Vector3 direction = targetPosition - self.Position;
			direction.y = 0f;
			return direction.normalized;
		}

		/// <summary>
		/// 사거리에서 멈추는 접근 — 목표 안으로 파고들지 않는다.
		///
		/// ★ 겹침 방지가 본질(TASK-WM-194 실측): 목표에 겹쳐 선 유닛은 상대 스프라이트에 가려 **화면에서
		///   사라진다**. 개척에서 마수가 코어 좌표에 그대로 쌓여 플레이어가 "다 잡았는데 안 넘어간다"고
		///   판단했다. 그래서 stopDistance 가 0이어도 최소 간격(minStopDistance)은 항상 둔다.
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

			lastApproachTarget = target;

			Vector3 direction = target.Position - self.Position;
			direction.y = 0f;

			float effectiveStop = Mathf.Max(stopDistance, minStopDistance) + PerUnitRingOffset();
			if (direction.sqrMagnitude <= effectiveStop * effectiveStop)
			{
				unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
				return;
			}

			unitObject.UnitMovement.SetMoveDirection(SteerToward(target.Position));
		}

		// 개체별 정지 거리 흔들기 — 같은 목표를 노리는 무리가 한 점에 겹쳐 서는 것 방지.
		// id 파생이라 결정적(같은 입력 → 같은 배치).
		private float PerUnitRingOffset()
		{
			int id = self != null ? self.CombatantId : 0;
			return (id % ringSlotCount) * ringSlotSpacing;
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

		/// <summary>
		/// 공격할 때 발을 멈추는가 — 기본은 멈춘다(투기장). 개척의 마수만 끄고 걸으면서 쏜다
		/// (멈추면 서로 못 죽이는 짝을 만났을 때 판이 영영 안 끝난다 — 사용자 2회 보고).
		/// </summary>
		public bool StopsToAttack { get; set; } = true;

		public void Hold()
		{
			unitObject.UnitMovement.SetMoveDirection(Vector3.zero);
		}
	}
}
