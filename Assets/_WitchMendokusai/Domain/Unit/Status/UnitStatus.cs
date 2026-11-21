using UnityEngine;

namespace WitchMendokusai
{
	// 유닛 하나의 상태이상. 남은 시간 정본은 UnitStat 의 상태이상 칸 (밀리초)
	// - 처음 걸릴 때 유닛에 붙음. 풀 재사용 몸이라 꺼질 때 전부 해제
	// - 시간 감소, 틱 피해, 표시는 UnitStatusDriver 한 곳
	public sealed class UnitStatus : MonoBehaviour
	{
		public UnitObject Unit { get; private set; }

		internal readonly float[] TickTimers = new float[UnitStatusRules.Count];
		internal readonly IDamageSource[] Sources = new IDamageSource[UnitStatusRules.Count];
		// 종류마다 쌓임 수 (MaxStacks 1 이면 걸린 동안 1), 걸릴 때의 전체 밀리초 (남은 시간 테두리 비율의 분모)
		internal readonly int[] Stacks = new int[UnitStatusRules.Count];
		internal readonly int[] TotalMilliseconds = new int[UnitStatusRules.Count];
		// 전염 연쇄. 세대 (무기가 직접 걸면 0) 와 전염으로 받은 횟수
		internal readonly int[] Generations = new int[UnitStatusRules.Count];
		internal readonly int[] ContagionReceives = new int[UnitStatusRules.Count];
		internal float MillisecondCarry;
		// 유닛마다 효과 위상을 달리하는 값. 무리가 같이 깜빡이지 않게
		internal float PhaseSeed;
		internal bool IsTracked;
		internal UnitStatusView View;

		private Animator animator;
		private bool animatorSearched;
		private bool animatorStopped;
		private float animatorSpeed;

		private static UnitStatusConfig Config => ResourceManager.Instance.UnitStatus;

		public static void Apply(UnitObject target, UnitStatusType type, IDamageSource source)
		{
			UnitStatusStyle style = Config.Style(type);
			Apply(target, type, source, style.Seconds);
		}

		public static void Apply(UnitObject target, UnitStatusType type, IDamageSource source, float seconds)
		{
			UnitStatusStyle style = Config.Style(type);
			Engage(target, type, source, seconds, style.SlowRatio, 0, false);
		}

		// 둔화 비율을 거는 쪽이 정함 (무기 부품)
		public static void ApplySlow(UnitObject target, IDamageSource source, float seconds, float slowRatio) => Engage(target, UnitStatusType.Slow, source, seconds, slowRatio, 0, false);

		public static bool Has(UnitObject target, UnitStatusType type) => target.UnitStat[UnitStatusRules.StatType(type)] > 0;

		// 걸린 상태이상의 비트 집합 (StatusDetonationRules.Bit)
		public static int ActiveMask(UnitObject target)
		{
			int mask = 0;
			foreach (UnitStatusType type in UnitStatusRules.ALL)
				if (Has(target, type))
					mask |= StatusDetonationRules.Bit(type);
			return mask;
		}

		// 상태 폭발이 상태 하나를 지움. 남은 상태가 없으면 구동기가 다음 갱신에 추적을 풀음
		public static void Consume(UnitObject target, UnitStatusType type)
		{
			if (Has(target, type) == false)
				return;
			target.UnitStat[UnitStatusRules.StatType(type)] = 0;
			if (target.TryGetComponent(out UnitStatus status) == false)
				return;
			int index = (int)type;
			status.Stacks[index] = 0;
			status.TotalMilliseconds[index] = 0;
			status.TickTimers[index] = 0f;
			status.Sources[index] = null;
			status.Generations[index] = 0;
			status.ContagionReceives[index] = 0;
		}

		// 전염 연쇄 세대. 안 걸렸으면 0
		public static int Generation(UnitObject target, UnitStatusType type)
		{
			if (Has(target, type) == false || target.TryGetComponent(out UnitStatus status) == false)
				return 0;
			return status.Generations[(int)type];
		}

		// 전염으로 받은 횟수. 걸려 있지 않아도 이전 기록이 남아 있으면 그 값 (무기가 직접 걸면 초기화)
		public static int ContagionReceiveCount(UnitObject target, UnitStatusType type)
		{
			if (target.TryGetComponent(out UnitStatus status) == false)
				return 0;
			return status.ContagionReceives[(int)type];
		}

		// 처치 전염으로 걺. 세대와 받은 횟수를 올림
		public static void ApplyContagion(UnitObject target, UnitStatusType type, IDamageSource source, int generation)
		{
			Engage(target, type, source, Config.Style(type).Seconds, Config.Style(type).SlowRatio, generation, true);
		}

		public static int StackCount(UnitObject target, UnitStatusType type)
		{
			if (Has(target, type) == false || target.TryGetComponent(out UnitStatus status) == false)
				return 0;
			return status.Stacks[(int)type];
		}

		// 걸린 상태이상 중 받는 피해 증가 퍼센트의 합 (표식)
		public static float DamageTakenPercent(UnitObject target)
		{
			UnitStatusConfig config = Config;
			if (config == null)
				return 0f;
			float percent = 0f;
			foreach (UnitStatusStyle style in config.Styles)
				if (style.DamageTakenPercent > 0f && Has(target, style.Type))
					percent += style.DamageTakenPercent;
			return percent;
		}

		// 공포가 걸려 플레이어 반대로 도망치는 중. 플레이어 자신은 제외
		public static bool IsFleeing(UnitObject target) => AnyFlag(target, fleeing: true);

		// 침묵이 걸려 스킬을 못 쓰는 중
		public static bool IsSkillBlocked(UnitObject target) => AnyFlag(target, fleeing: false);

		private static bool AnyFlag(UnitObject target, bool fleeing)
		{
			UnitStatusConfig config = Config;
			if (config == null)
				return false;
			foreach (UnitStatusStyle style in config.Styles)
			{
				bool flagged = fleeing ? style.FleeFromPlayer : style.BlockSkills;
				if (flagged && Has(target, style.Type))
					return true;
			}
			return false;
		}

		// 공포 중이면 플레이어 반대 방향 (x, z 입력) 으로 바꿈. 플레이어를 못 찾으면 그대로
		public static Vector2 ApplyFear(UnitObject unit, Vector2 input)
		{
			if (unit is PlayerObject || IsFleeing(unit) == false)
				return input;
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (player == null)
				return input;
			Vector3 away = unit.transform.position - player.transform.position;
			Vector2 flat = new(away.x, away.z);
			if (flat.sqrMagnitude < 0.0001f)
				flat = Vector2.right;
			return flat.normalized;
		}

		private static void Engage(UnitObject target, UnitStatusType type, IDamageSource source, float seconds, float slowRatio, int generation, bool contagion)
		{
			if (target.IsAlive == false || target.isActiveAndEnabled == false)
				return;
			if (target is MonsterObject monster && monster.UnitData.Type == MonsterType.Boss)
				seconds = UnitStatusRules.BossSeconds(seconds, Config.Style(type).BossRatio);
			int milliseconds = UnitStatusRules.Milliseconds(seconds);
			if (milliseconds <= 0)
				return;

			if (target.TryGetComponent(out UnitStatus status) == false)
			{
				status = target.gameObject.AddComponent<UnitStatus>();
				status.PhaseSeed = Random.value;
			}
			status.Unit = target;

			int index = (int)type;
			UnitStatType statType = UnitStatusRules.StatType(type);
			int current = target.UnitStat[statType];
			if (current <= 0)
				status.TickTimers[index] = 0f;
			int refreshed = UnitStatusRules.Refresh(current, milliseconds);
			target.UnitStat[statType] = refreshed;
			status.TotalMilliseconds[index] = refreshed;
			status.Stacks[index] = UnitStatusRules.NextStacks(status.Stacks[index], current > 0, Config.Style(type).MaxStacks);
			status.Sources[index] = source;
			// 무기가 직접 걸면 연쇄가 새로 시작. 전염이면 받은 횟수가 쌓임
			status.Generations[index] = generation;
			status.ContagionReceives[index] = contagion ? status.ContagionReceives[index] + 1 : 0;

			if (type == UnitStatusType.Slow && slowRatio > 0f)
				UnitSlow.Apply(target, slowRatio, seconds);

			UnitStatusDriver.Instance.Track(status);
		}

		// 얼음처럼 그림 움직임을 멈추는 종류가 하나라도 걸렸으면 정지
		internal void SetAnimationStopped(bool stopped)
		{
			if (stopped == animatorStopped)
				return;
			if (animatorSearched == false)
			{
				animator = GetComponentInChildren<Animator>(true);
				animatorSearched = true;
			}
			// 그림 움직임이 없는 유닛
			if (animator == null)
				return;
			animatorStopped = stopped;
			if (stopped)
			{
				animatorSpeed = animator.speed;
				animator.speed = 0f;
			}
			else
				animator.speed = animatorSpeed;
		}

		internal void ClearAll()
		{
			if (Unit != null)
				foreach (UnitStatusType type in UnitStatusRules.ALL)
					Unit.UnitStat[UnitStatusRules.StatType(type)] = 0;
			for (int i = 0; i < Sources.Length; i++)
			{
				Sources[i] = null;
				Stacks[i] = 0;
				TotalMilliseconds[i] = 0;
				Generations[i] = 0;
				ContagionReceives[i] = 0;
			}
			MillisecondCarry = 0f;
			SetAnimationStopped(false);
			if (UnitStatusDriver.HasInstance)
				UnitStatusDriver.Instance.Untrack(this);
			// 구동기가 먼저 사라진 경우 (씬 정리 순서) 에도 다음 판에 다시 등록되게
			IsTracked = false;
			View = null;
		}

		private void OnDisable() => ClearAll();
	}
}
