using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 개척 포탑의 사격(TASK-WM-194) — 개척이 직접 소유하는 무기.
	///
	/// ★ 왜 기존 스킬 시스템을 안 쓰는가: 지금까지 포탑은 *플레이어 인형(알리사)의 마나불릿 프리팹*을
	///   빌려 쏘고 있었다. 그 위에서 광역·관통·둔화를 만들려면 공유 프리팹을 건드려야 하고, 그건
	///   본편 조작감을 바꾼다. 종류를 늘리는 게 목적인데 늘릴수록 본편이 위험해지는 구조는 근본이 아니다.
	///   포탑은 움직이지 않는 고정 포대라 전술(이동+스킬) 기계도 과하다 — 표적 고르고 쏘는 것뿐.
	///
	/// 히트스캔 + 짧은 예광선: 투사체 프리팹 0(공유 자산 의존 0), 빗나감 없음(고정 포대의 의도),
	/// 그리고 「누가 누구를 쏘는지」가 선으로 보인다 = 화면이 읽힌다.
	/// </summary>
	public sealed class TowerDefenseWeapon : MonoBehaviour
	{
		private const float TRACER_SECONDS = 0.08f;

		private TowerDefenseTowerArchetype archetype;
		private TargetingSystem targeting;
		private ICombatant self;
		private IReadOnlyList<ICombatant> enemyPool;

		// 「보이는가」 판정 — 시야가 장식이 아니라 규칙이 되는 지점. 안 꽂히면 전부 보이는 것으로 본다.
		private System.Func<Vector3, bool> visibilityTest;

		// 매 발사 때 읽는다 — 나중에 세운 연구 인형이 이미 서 있던 포탑에도 바로 반영되게.
		private System.Func<float> damageMultiplier;

		// 마수가 무엇에 익숙해졌나 — 매 발사 때 읽는다(적응은 웨이브 사이에 변한다).
		private System.Func<TowerDefenseAdaptationState> adaptation;

		// 효과가 *실제로 일어났는가*를 세는 계수기 — 검증이 로그 문자열이 아니라 상태를 읽게 한다
		// (게임 코드에 진단 로그를 영구히 남기지 않으면서도 「광역이 진짜 터졌나」를 증명할 수 있다).
		public int PierceHits { get; private set; }
		public int SplashHits { get; private set; }
		public int SlowApplied { get; private set; }

		/// <summary> 지어질 때 든 값 — 팔 때 환불 기준. 종류가 늘어도 판매 쪽이 다시 계산할 필요가 없다. </summary>
		public int Cost => archetype != null ? archetype.Cost : 0;

		/// <summary>
		/// 이 인형이 든 무기의 종류 그 자체 — 「같은 종류인가」를 값이 아니라 *정체*로 묻기 위해 연다.
		/// ★ 값으로 물으면 값이 같은 두 종류가 같은 것으로 취급돼, 다른 무기가 조용히 승급된다.
		/// </summary>
		public TowerDefenseTowerArchetype Archetype => archetype;

		/// <summary> 승급 단계(1부터). 같은 자리에 같은 종류를 다시 지으면 오른다. </summary>
		public int Level { get; private set; } = 1;

		/// <summary> 단계에 따른 배수 — 피해·사거리에 함께 걸린다. </summary>
		private float LevelScale => 1f + (Level - 1) * (archetype != null ? archetype.UpgradeGrowth : 0f);

		/// <summary> 지금 이 순간의 사거리·피해 — 툴팁이 읽는다. 화면이 규칙과 다른 숫자를 말하면 안 된다. </summary>
		// 레벨업으로 고른 것들이 여기 쌓인다 — 종류별 배수.
		private float perkDamage;
		private float perkRange;
		private float perkSpeed;

		/// <summary> 고른 것을 수치에 건다(한 단계당 step 비율). </summary>
		public void ApplyPerk(TowerDefenseBuildingPerk perk, float step)
		{
			switch (perk)
			{
				case TowerDefenseBuildingPerk.Damage:
					perkDamage += step;
					break;
				case TowerDefenseBuildingPerk.Range:
					perkRange += step;
					break;
				case TowerDefenseBuildingPerk.Speed:
					perkSpeed += step;
					break;
				default:
					break;
			}
		}

		// 연구로 늘어난 사거리 — *물을 때마다* 읽는다(연구는 판 도중에 늘어나므로 캐싱하면 안 는다).
		private System.Func<float> rangeMultiplier;

		public float Range => archetype != null
			? archetype.Range * LevelScale * (1f + perkRange) * (rangeMultiplier != null ? rangeMultiplier() : 1f)
			: 0f;

		/// <summary>
		/// 반올림 전 피해 — 화면이 읽는 값과 실제로 때리는 값이 *같은 식*에서 나오게 하는 자리.
		/// ★ 예전엔 이 식이 두 벌이었다(툴팁용 / 사격용). 한쪽만 고치면 화면이 거짓말한다.
		///   화면은 여기에 반올림만 얹고, 사격은 여기에 배수를 더 곱한다 — 갈라질 수가 없다.
		/// </summary>
		private float RawDamage => archetype != null ? archetype.Damage * LevelScale * (1f + perkDamage) : 0f;

		public int CurrentDamage => Mathf.RoundToInt(RawDamage);

		/// <summary> 한 단계 올린다. 최대치면 false(값을 치르기 전에 호출자가 확인해야 한다). </summary>
		public bool TryUpgrade()
		{
			if (archetype == null || Level >= archetype.MaxLevel)
				return false;

			Level++;
			return true;
		}

		private float cooldownRemaining;

		/// <summary>
		/// 다음 발사까지 얼마나 찼나(0~1, 1 = 준비 완료) — 화면의 작은 바가 이 값을 그린다.
		/// 쿨타임이 없는 것(패시브)은 언제나 1 이다: 「항상 준비됨」이 곧 그 건물의 진실이다.
		/// </summary>
		public float ReadyRatio
		{
			get
			{
				if (archetype == null || archetype.Cooldown <= 0f)
					return 1f;
				return Mathf.Clamp01(1f - cooldownRemaining / archetype.Cooldown);
			}
		}
		private LineRenderer tracer;
		private float tracerRemaining;

		/// <summary>
		/// 사격 준비. enemyPool = 사거리 안에서 고를 후보(매치가 관리하는 웨이브 목록) —
		/// 표적 선택을 매치의 생존 목록과 같은 출처로 두어야 「죽은 걸 계속 쏘는」 어긋남이 안 생긴다.
		/// </summary>
		public void Configure(
			TowerDefenseTowerArchetype towerArchetype,
			TargetingSystem targetingSystem,
			ICombatant owner,
			IReadOnlyList<ICombatant> enemies,
			System.Func<Vector3, bool> isVisible = null,
			System.Func<float> towerDamageMultiplier = null,
			System.Func<TowerDefenseAdaptationState> adaptationState = null,
			System.Func<float> towerRangeMultiplier = null)
		{
			rangeMultiplier = towerRangeMultiplier;
			adaptation = adaptationState;
			visibilityTest = isVisible;
			damageMultiplier = towerDamageMultiplier;
			archetype = towerArchetype;
			targeting = targetingSystem;
			self = owner;
			enemyPool = enemies;
			cooldownRemaining = 0f;
		}

		private void Update()
		{
			UpdateTracer();

			if (archetype == null || self == null || self.IsAlive == false || enemyPool == null)
				return;

			cooldownRemaining -= Time.deltaTime;
			if (cooldownRemaining > 0f)
				return;

			ICombatant target = FindTarget();
			if (target == null)
				return;

			cooldownRemaining = archetype.Cooldown / (1f + perkSpeed);
			Fire(target);
		}

		/// <summary> 사거리 안에서 가장 가까운 마수 — 가까운 것부터 처리하는 게 방어의 기본. </summary>
		private ICombatant FindTarget()
		{
			float range = Range; // 툴팁이 말하는 그 사거리 — 두 벌로 두면 화면과 실물이 갈라진다.
			float rangeSqr = range * range;
			ICombatant best = null;
			float bestSqr = float.MaxValue;

			foreach (ICombatant candidate in enemyPool)
			{
				if (candidate == null || candidate.IsAlive == false)
					continue;
				if (candidate.TeamId == self.TeamId)
					continue;

				float sqr = (candidate.Position - self.Position).sqrMagnitude;
				if (sqr > rangeSqr || sqr >= bestSqr)
					continue;
				if (CanSee(candidate.Position) == false)
					continue; // 사거리 안이라도 「안 보이는」 마수는 못 쏜다.

				best = candidate;
				bestSqr = sqr;
			}
			return best;
		}

		/// <summary>
		/// 이번 한 발의 피해 — 승급 단계 × 연구 인형 배수 × **둔화 보너스**.
		/// ★ 둔화 보너스가 이 게임의 「조합」이다: 둔화 포탑이 밑밥을 깔고 다른 포탑이 마무리한다.
		///   보너스를 *때리는 쪽*이 아니라 *맞는 상태*로 판정하므로, 누가 둔화를 걸었든 상관없다.
		/// </summary>
		private int ComputeDamage(ICombatant target)
		{
			float damage = RawDamage;
			damage *= damageMultiplier != null ? damageMultiplier() : 1f;

			if (archetype.SlowedTargetBonus > 0f && IsSlowed(target))
				damage *= 1f + archetype.SlowedTargetBonus;

			return Mathf.Max(1, Mathf.RoundToInt(damage));
		}

		private static bool IsSlowed(ICombatant target)
		{
			MatchCombatant combatant = target as MatchCombatant;
			if (combatant == null || combatant.UnitObject == null)
				return false;

			TowerDefenseSlow slow = combatant.UnitObject.GetComponent<TowerDefenseSlow>();
			return slow != null && slow.IsActive;
		}

		private bool CanSee(Vector3 worldPosition)
		{
			return visibilityTest == null || visibilityTest(worldPosition);
		}

		private void Fire(ICombatant target)
		{
			ShowTracer(self.Position, target.Position);
			ApplyHit(target);

			// 관통 — 표적 너머로 이어지는 직선상의 마수를 추가로 맞힌다(줄지어 오는 무리에 강함).
			if (archetype.Pierce > 1)
				ApplyPierce(target);

			// 광역 — 착탄 지점 주변을 함께 때린다(뭉쳐 오는 무리에 강함).
			if (archetype.SplashRadius > 0f)
				ApplySplash(target);
		}

		private void ApplyPierce(ICombatant primary)
		{
			Vector3 direction = (primary.Position - self.Position).normalized;
			float rangeSqr = archetype.Range * archetype.Range * 4f; // 관통은 표적 너머까지 이어진다.
			int remaining = archetype.Pierce - 1;

			foreach (ICombatant candidate in enemyPool)
			{
				if (remaining <= 0)
					break;
				if (candidate == null || candidate == primary || candidate.IsAlive == false)
					continue;
				if (candidate.TeamId == self.TeamId)
					continue;

				Vector3 toCandidate = candidate.Position - self.Position;
				if (toCandidate.sqrMagnitude > rangeSqr)
					continue;
				// 사격선에서 한 칸 안쪽이면 꿰뚫린 것으로 본다.
				if (Vector3.Dot(toCandidate.normalized, direction) < 0.94f)
					continue;
				if (CanSee(candidate.Position) == false)
					continue;

				ApplyHit(candidate, 1f - CurrentAdaptation().PierceResist);
				PierceHits++;
				remaining--;
			}
		}

		private void ApplySplash(ICombatant center)
		{
			float splashSqr = archetype.SplashRadius * archetype.SplashRadius;

			foreach (ICombatant candidate in enemyPool)
			{
				if (candidate == null || candidate == center || candidate.IsAlive == false)
					continue;
				if (candidate.TeamId == self.TeamId)
					continue;
				if ((candidate.Position - center.Position).sqrMagnitude > splashSqr)
					continue;
				if (CanSee(candidate.Position) == false)
					continue;

				ApplyHit(candidate, 1f - CurrentAdaptation().SplashResist);
				SplashHits++;
			}
		}

		private TowerDefenseAdaptationState CurrentAdaptation()
		{
			return adaptation != null ? adaptation() : default;
		}

		private void ApplyHit(ICombatant target, float damageScale = 1f)
		{
			MatchCombatant combatant = target as MatchCombatant;
			if (combatant == null || combatant.UnitObject == null)
				return;

			DamageInfo damageInfo = new DamageInfo
			{
				damage = Mathf.Max(1, Mathf.RoundToInt(ComputeDamage(target) * damageScale)),
				type = DamageType.Normal,
				damageSource = self,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			};
			combatant.UnitObject.Health.ReceiveDamage(damageInfo);

			if (archetype.SlowFactor > 0f)
			{
				// 둔화 저항 — 통하되 예전만큼은 아니게(상한 절반, 봉인 X).
				float resisted = archetype.SlowFactor * (1f - CurrentAdaptation().SlowResist);
				TowerDefenseSlow.Apply(combatant.UnitObject, resisted, archetype.SlowSeconds);
				SlowApplied++;
			}
		}

		// --- 예광선: 「누가 누구를 쏘는지」를 보여준다. 안 보이면 포탑이 일하는지조차 알 수 없다. ---

		private void ShowTracer(Vector3 from, Vector3 to)
		{
			if (tracer == null)
			{
				GameObject tracerObject = new GameObject("Tracer");
				tracerObject.transform.SetParent(transform, false);
				tracer = tracerObject.AddComponent<LineRenderer>();
				tracer.useWorldSpace = true;
				tracer.positionCount = 2;
				tracer.widthMultiplier = 0.07f;
				tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				tracer.receiveShadows = false;
				tracer.material = TowerDefenseVisuals.CreateUnlit();
			}

			Color color = archetype.Tint;
			tracer.startColor = color;
			tracer.endColor = new Color(color.r, color.g, color.b, 0.2f);
			tracer.SetPosition(0, from + Vector3.up * 0.6f);
			tracer.SetPosition(1, to + Vector3.up * 0.4f);
			tracer.enabled = true;
			tracerRemaining = TRACER_SECONDS;
		}

		private void UpdateTracer()
		{
			if (tracer == null || tracer.enabled == false)
				return;

			tracerRemaining -= Time.deltaTime;
			if (tracerRemaining <= 0f)
				tracer.enabled = false;
		}
	}
}
