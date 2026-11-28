using System;
using UnityEngine;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UnitHealth : MonoBehaviour, IDamageable
	{
		public UnitObject Unit { get; private set; }
		private Vector3 originScale;
		private float scaleMultiplier = 1f;

		// 피격 연출 — 잠깐 부풀었다 돌아오는 타격감. 손으로 만져봐야 하는 수치라 인스펙터에 꺼내둔다.
		[Header("Hit Feedback")]
		[SerializeField] private float hitScaleMultiplier = 1.4f;
		[SerializeField] private float hitScaleUpDuration = 0.1f;
		[SerializeField] private float hitScaleDownDuration = 0.2f;

		public bool IsAlive => Unit != null && Unit.UnitStat[UnitStatType.HP_CUR] > 0;

		// 마지막으로 피해가 들어간 게임 시각. 피격 무적 판정용
		private float lastHitTime = float.NegativeInfinity;
		// 안 맞은 동안만 도는 효과 (스킬 재료 지속 재생) 가 읽음. 상태이상 틱은 안 셈
		public float LastHitTime => lastHitTime;

		// 피격 무적 중. 같은 유닛이 맞은 직후라 다음 타격이 안 들어감. 값은 UnitObject.HitInvincibleSeconds
		public bool IsHitInvincible => Unit != null && HitInvincibilityRules.IsActive(lastHitTime, Time.time, Unit.HitInvincibleSeconds);

		// 개발자 진열장 허수아비. 죽을 피해를 받으면 최대 체력으로 다시 참. 켜고 끄는 쪽은 DevShowcaseLease
		public bool IsImmortal { get; set; }

		public event Action<DamageInfo> OnTakeDamage;
		public event Action<int> OnHealed;
		public event Action OnDied;
		public event Action<int, int> OnHealthChanged;

		public void Init(UnitObject unitObject)
		{
			Unit = unitObject;
			lastHitTime = float.NegativeInfinity;

			Unit.MeshParent.DOKill();
			if (originScale != Vector3.zero)
				Unit.MeshParent.localScale = originScale;
			originScale = Unit.MeshParent.localScale;
			
			OnHealthChanged?.Invoke(Unit.UnitStat[UnitStatType.HP_CUR], Unit.UnitStat[UnitStatType.HP_MAX]);
		}

		// 마지막으로 받은 피해 (무적에 막힌 것 제외). 사망 반응이 죽인 타격의 종류와 방향을 읽음
		public DamageInfo LastDamage { get; private set; }

		public void ReceiveDamage(DamageInfo damageInfo)
		{
			if (IsAlive == false) return;

			// 피격 무적. 상태이상 틱과 강제 피해는 통과
			bool passesInvincible = damageInfo.ignoreInvincible || damageInfo.type == DamageType.StatusTick;
			if (passesInvincible == false && IsHitInvincible)
				return;
			// 통과한 피해 (틱, 강제) 는 무적 시간을 다시 시작시키지 않음
			if (passesInvincible == false)
				lastHitTime = Time.time;

			// 표식 상태이상. 상태이상 틱은 제외 (표식이 틱까지 불리면 틱 숫자가 과해짐)
			if (damageInfo.type != DamageType.StatusTick)
				damageInfo.damage = UnitStatusRules.AmplifiedDamage(damageInfo.damage, UnitStatus.DamageTakenPercent(Unit));

			// 사망 반응 (OnDied) 이 OnTakeDamage 보다 먼저 불려서, 죽인 타격은 여기서 남겨 둠
			LastDamage = damageInfo;

			// 상태 틱으로 죽으면 전염. 죽으면 곧 꺼지며 상태가 지워지므로 세대는 미리 받아 둠
			StatusContagion.Origin contagionOrigin = default;
			bool contagious = damageInfo.type == DamageType.StatusTick
				&& Unit.UnitStat[UnitStatType.HP_CUR] - damageInfo.damage <= 0
				&& IsImmortal == false
				&& StatusContagion.Begin(Unit, damageInfo.statusType, damageInfo.damageSource, out contagionOrigin);

			SetHp(Unit.UnitStat[UnitStatType.HP_CUR] - damageInfo.damage);

			if (contagious)
				StatusContagion.Spread(contagionOrigin);

			// Pivot 스케일 잠깐 키웠다가 줄이기 (기존 UnitObject의 연출 이동)
			Unit.MeshParent.DOScale(BaseScale * hitScaleMultiplier, hitScaleUpDuration).OnComplete(() =>
				Unit.MeshParent.DOScale(BaseScale, hitScaleDownDuration));

			OnTakeDamage?.Invoke(damageInfo);
		}

		private Vector3 BaseScale => originScale * scaleMultiplier;

		public void SetScaleMultiplier(float multiplier)
		{
			scaleMultiplier = multiplier;
			Unit.MeshParent.DOKill();
			Unit.MeshParent.localScale = BaseScale;
		}

		// 정지 중에도 재생되는 짧은 부풀림
		public void PunchScale(float multiplier, float duration)
		{
			Transform mesh = Unit.MeshParent;
			mesh.DOKill();
			mesh.localScale = BaseScale * multiplier;
			mesh.DOScale(BaseScale, duration).SetUpdate(true);
		}

		public void ReceiveHeal(int healAmount)
		{
			if (IsAlive == false) return;

			SetHp(Unit.UnitStat[UnitStatType.HP_CUR] + healAmount);
			OnHealed?.Invoke(healAmount);
		}

		// 피해가 아닌 체력 지불 (맵 오브젝트 체력 비용). 피격 이벤트, 무적, 반격, 증폭 없음. 체력 1 은 남겨야 함
		public void SpendHealth(int amount)
		{
			if (amount < 0 || Unit.UnitStat[UnitStatType.HP_CUR] - amount < 1)
				throw new ArgumentOutOfRangeException(nameof(amount), amount, $"체력 {Unit.UnitStat[UnitStatType.HP_CUR]} 에서 낼 수 없는 값");
			SetHp(Unit.UnitStat[UnitStatType.HP_CUR] - amount);
		}

		private void SetHp(int newHp)
		{
			if (Unit == null) return;

			// 「얼마 남고 죽었나」는 판정 층이 정한다 (TASK-WM-215) — 여기선 그 결과를 씬에 반영만 한다.
			int maxHp = Unit.UnitStat[UnitStatType.HP_MAX];
			HealthChange change = HealthResolution.Apply(Unit.UnitStat[UnitStatType.HP_CUR], maxHp, newHp - Unit.UnitStat[UnitStatType.HP_CUR]);

			if (change.Died && IsImmortal)
			{
				Unit.UnitStat[UnitStatType.HP_CUR] = maxHp;
				OnHealthChanged?.Invoke(maxHp, maxHp);
				return;
			}

			Unit.UnitStat[UnitStatType.HP_CUR] = change.NewHp;
			OnHealthChanged?.Invoke(change.NewHp, change.MaxHp);

			if (change.Died)
			{
				Die();
			}
		}

		private void Die()
		{
			OnDied?.Invoke();
		}
	}
}
