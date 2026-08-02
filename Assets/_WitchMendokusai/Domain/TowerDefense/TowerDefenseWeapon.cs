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

		// 효과가 *실제로 일어났는가*를 세는 계수기 — 검증이 로그 문자열이 아니라 상태를 읽게 한다
		// (게임 코드에 진단 로그를 영구히 남기지 않으면서도 「광역이 진짜 터졌나」를 증명할 수 있다).
		public int PierceHits { get; private set; }
		public int SplashHits { get; private set; }
		public int SlowApplied { get; private set; }

		private float cooldownRemaining;
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
			System.Func<Vector3, bool> isVisible = null)
		{
			visibilityTest = isVisible;
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

			cooldownRemaining = archetype.Cooldown;
			Fire(target);
		}

		/// <summary> 사거리 안에서 가장 가까운 마수 — 가까운 것부터 처리하는 게 방어의 기본. </summary>
		private ICombatant FindTarget()
		{
			float rangeSqr = archetype.Range * archetype.Range;
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

				ApplyHit(candidate);
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

				ApplyHit(candidate);
				SplashHits++;
			}
		}

		private void ApplyHit(ICombatant target)
		{
			ArenaCombatant combatant = target as ArenaCombatant;
			if (combatant == null || combatant.UnitObject == null)
				return;

			DamageInfo damageInfo = new DamageInfo
			{
				damage = archetype.Damage,
				type = DamageType.Normal,
				damageSource = self,
				equipmentDataId = DamageInfo.NO_DATA_ID,
				skillDataId = DamageInfo.NO_DATA_ID,
			};
			combatant.UnitObject.Health.ReceiveDamage(damageInfo);

			if (archetype.SlowFactor > 0f)
			{
				TowerDefenseSlow.Apply(combatant.UnitObject, archetype.SlowFactor, archetype.SlowSeconds);
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
				tracer.material = new Material(
					Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
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
