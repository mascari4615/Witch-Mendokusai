using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 상태이상 구동기. 시간 감소, 틱 피해, 몸 물들임, 아이콘을 한곳에서 매 프레임 갱신
	// - 유닛마다 Update 를 두지 않음. 수백 마리여도 갱신은 이 Update 하나
	// - 아이콘과 몸 효과 묶음은 상한 (MaxViews) 이 있는 풀. 넘으면 물들임만
	// - 게임 시간으로 흐름. 히트스톱과 일시정지 동안 같이 멈춤
	public sealed class UnitStatusDriver : MonoBehaviour
	{
		private static UnitStatusDriver instance;

		public static bool HasInstance => instance != null;

		public static UnitStatusDriver Instance
		{
			get
			{
				if (instance == null)
					instance = new GameObject(nameof(UnitStatusDriver)).AddComponent<UnitStatusDriver>();
				return instance;
			}
		}

		private readonly List<UnitStatus> tracked = new();
		// 틱 피해로 죽은 유닛이 도는 중에 목록에서 빠져도 순서가 안 흔들리게 사본으로 돔
		private readonly List<UnitStatus> ticking = new();
		private int viewsCreatedThisFrame;
		private readonly Stack<UnitStatusView> freeViews = new();
		private int createdViews;
		private MaterialPropertyBlock properties;

		public int TrackedCount => tracked.Count;
		public int ViewCount => createdViews - freeViews.Count;

		private static UnitStatusConfig Config => ResourceManager.Instance.UnitStatus;

		private void Awake() => properties = new MaterialPropertyBlock();

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
		}

		internal void Track(UnitStatus status)
		{
			if (status.IsTracked)
				return;
			status.IsTracked = true;
			tracked.Add(status);
		}

		internal void Untrack(UnitStatus status)
		{
			if (status.IsTracked == false)
				return;
			status.IsTracked = false;
			tracked.Remove(status);
			ReleaseView(status);
			ClearBody(status);
		}

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			if (deltaTime <= 0f)
				return;
			UnitStatusConfig config = Config;
			ticking.Clear();
			ticking.AddRange(tracked);
			foreach (UnitStatus status in ticking)
			{
				if (status == null)
					continue;
				if (status.IsTracked == false)
					continue;
				if (status.Unit == null || status.isActiveAndEnabled == false)
				{
					Untrack(status);
					continue;
				}
				Tick(status, config, deltaTime);
			}
			ticking.Clear();
			tracked.RemoveAll(IsDestroyed);
		}

		private static bool IsDestroyed(UnitStatus status) => status == null;

		private void Tick(UnitStatus status, UnitStatusConfig config, float deltaTime)
		{
			float total = deltaTime * 1000f + status.MillisecondCarry;
			int elapsed = (int)total;
			status.MillisecondCarry = total - elapsed;

			UnitObject unit = status.Unit;
			bool anyActive = false;
			bool stopAnimation = false;
			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				UnitStatType statType = UnitStatusRules.StatType(type);
				int remaining = unit.UnitStat[statType];
				if (remaining <= 0)
					continue;
				UnitStatusStyle style = config.Style(type);
				int index = (int)type;
				if (style.TickSeconds > 0f)
				{
					status.TickTimers[index] += deltaTime;
					while (status.TickTimers[index] >= style.TickSeconds)
					{
						status.TickTimers[index] -= style.TickSeconds;
						DealTick(status, type, style);
						// 틱 피해로 죽으면 꺼지면서 이미 해제됨
						if (status.IsTracked == false)
							return;
					}
				}
				int next = UnitStatusRules.Elapse(remaining, elapsed);
				unit.UnitStat[statType] = next;
				if (next <= 0)
				{
					status.Stacks[index] = 0;
					continue;
				}
				anyActive = true;
				stopAnimation |= style.StopAnimation;
				// 공포. AI 가 방향을 안 줘도 계속 도망치게 매 프레임 한 번 방향을 다시 줌 (UnitMovement 가 플레이어 반대로 덮어씀)
				if (style.FleeFromPlayer && unit is not PlayerObject)
					unit.UnitMovement.SetMoveDirection(Vector2.zero);
			}
			status.SetAnimationStopped(stopAnimation);
			if (anyActive == false)
				Untrack(status);
		}

		private static void DealTick(UnitStatus status, UnitStatusType type, UnitStatusStyle style)
		{
			UnitObject unit = status.Unit;
			int damage = UnitStatusRules.StackedTickDamage(unit.UnitStat[UnitStatType.HP_MAX], style.TickMaxHealthPercent, style.TickMinDamage, status.Stacks[(int)type]);
			if (damage <= 0 || unit.IsAlive == false)
				return;
			IDamageSource source = status.Sources[(int)type];
			Vector3 origin = unit.transform.position;
			StrikeTick(unit, type, damage, source);
			// 적이 주인공에게 건 감전은 옆 몬스터로 안 튐
			if (style.ChainCount > 0 && (unit is PlayerObject && source is MonsterObject) == false)
				Chain(unit, origin, type, style, damage, source);
		}

		private static void StrikeTick(UnitObject target, UnitStatusType type, int damage, IDamageSource source)
		{
			if (target.IsAlive == false)
				return;
			target.ReceiveDamage(new DamageInfo
			{
				type = DamageType.StatusTick,
				statusType = type,
				damage = damage,
				damageSource = source,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
				ignoreInvincible = true,
			});
		}

		private static readonly List<(float Distance, MonsterObject Monster)> chainCandidates = new();

		// 감전. 틱을 맞은 몬스터 가까이의 다른 몬스터 ChainCount 마리에 틱 피해의 일부. 튄 피해는 상태이상을 새로 걸지 않음 (연쇄 폭주 방지)
		private static void Chain(UnitObject from, Vector3 origin, UnitStatusType type, UnitStatusStyle style, int tickDamage, IDamageSource source)
		{
			int chainDamage = UnitStatusRules.ChainDamage(tickDamage, style.ChainDamagePercent);
			if (chainDamage <= 0)
				return;
			float radiusSquared = style.ChainRadius * style.ChainRadius;
			chainCandidates.Clear();
			foreach (GameObject monsterObject in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (monsterObject == null || monsterObject.activeInHierarchy == false || MonsterObject.IsHiddenMonster(monsterObject))
					continue;
				if (monsterObject.TryGetComponent(out MonsterObject monster) == false || monster == from || monster.IsAlive == false)
					continue;
				float distanceSquared = (monster.transform.position - origin).sqrMagnitude;
				if (distanceSquared <= radiusSquared)
					chainCandidates.Add((distanceSquared, monster));
			}
			chainCandidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
			int count = Mathf.Min(style.ChainCount, chainCandidates.Count);
			for (int i = 0; i < count; i++)
				StrikeTick(chainCandidates[i].Monster, type, chainDamage, source);
			chainCandidates.Clear();
		}

		private void LateUpdate()
		{
			if (tracked.Count == 0)
				return;
			UnitStatusConfig config = Config;
			Camera camera = Camera.main;
			float time = Time.time;
			float deltaTime = Time.deltaTime;
			viewsCreatedThisFrame = 0;
			for (int i = 0; i < tracked.Count; i++)
			{
				UnitStatus status = tracked[i];
				SpriteRenderer body = status.Unit.StatusBodies[0];
				ApplyBody(status, config, time);
				// 화면 밖 유닛은 표시 묶음을 돌려줘 상한을 화면 안 유닛에 씀
				if (body.isVisible == false)
				{
					ReleaseView(status);
					continue;
				}
				if (status.View == null)
					status.View = RentView(config);
				if (status.View != null)
					status.View.Draw(status, body, camera, config, time, deltaTime);
			}
		}

		// 몸 물들임은 위 줄 우선 하나, 테두리는 걸린 것 중 첫 테두리
		private void ApplyBody(UnitStatus status, UnitStatusConfig config, float time)
		{
			Color tint = Color.clear;
			Color rim = Color.clear;
			bool hasTint = false;
			bool hasRim = false;
			for (int i = 0; i < config.Styles.Count; i++)
			{
				UnitStatusStyle style = config.Styles[i];
				if (UnitStatus.Has(status.Unit, style.Type) == false)
					continue;
				if (hasTint == false && style.Tint.a > 0f)
				{
					tint = style.Tint;
					if (style.TintPulseRate > 0f)
						tint.a *= 1f - style.TintPulseDepth * (0.5f + 0.5f * Mathf.Sin(time * style.TintPulseRate * Mathf.PI * 2f));
					hasTint = true;
				}
				if (hasRim == false && style.Rim.a > 0f)
				{
					rim = style.Rim;
					hasRim = true;
				}
			}
			foreach (SpriteRenderer body in status.Unit.StatusBodies)
			{
				body.GetPropertyBlock(properties);
				properties.SetColor(UnitBodyProperties.STATUS_TINT_ID, tint);
				properties.SetColor(UnitBodyProperties.STATUS_RIM_ID, rim);
				properties.SetFloat(UnitBodyProperties.STATUS_RIM_WIDTH_ID, config.RimWidth);
				UnitBodyProperties.Commit(body, properties);
			}
		}

		private void ClearBody(UnitStatus status)
		{
			if (status.Unit == null)
				return;
			foreach (SpriteRenderer body in status.Unit.StatusBodies)
			{
				if (body == null)
					continue;
				body.GetPropertyBlock(properties);
				properties.SetColor(UnitBodyProperties.STATUS_TINT_ID, Color.clear);
				properties.SetColor(UnitBodyProperties.STATUS_RIM_ID, Color.clear);
				UnitBodyProperties.Commit(body, properties);
			}
		}

		// 걸린 상태이상 중 피격 흔들림 배율이 가장 작은 것
		public static void HitShakeScale(UnitObject unit, out float amplitudeScale, out float frequencyScale)
		{
			amplitudeScale = 1f;
			frequencyScale = 1f;
			if (unit.TryGetComponent(out UnitStatus status) == false || status.IsTracked == false)
				return;
			UnitStatusConfig config = Config;
			foreach (UnitStatusType type in UnitStatusRules.ALL)
			{
				if (UnitStatus.Has(unit, type) == false)
					continue;
				UnitStatusStyle style = config.Style(type);
				if (style.HitShakeScale < amplitudeScale)
				{
					amplitudeScale = style.HitShakeScale;
					frequencyScale = style.HitShakeFrequencyScale;
				}
			}
		}

		private UnitStatusView RentView(UnitStatusConfig config)
		{
			UnitStatusView view;
			if (freeViews.Count > 0)
				view = freeViews.Pop();
			else if (createdViews < config.MaxViews && viewsCreatedThisFrame < config.ViewsCreatedPerFrame)
			{
				view = new UnitStatusView(transform, config);
				createdViews++;
				viewsCreatedThisFrame++;
			}
			else
				return null;
			view.Begin();
			return view;
		}

		private void ReleaseView(UnitStatus status)
		{
			if (status.View == null)
				return;
			status.View.End();
			freeViews.Push(status.View);
			status.View = null;
		}
	}
}
