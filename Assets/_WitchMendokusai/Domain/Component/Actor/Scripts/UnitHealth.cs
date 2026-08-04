using System;
using UnityEngine;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UnitHealth : MonoBehaviour, IDamageable
	{
		public UnitObject Unit { get; private set; }
		private Vector3 originScale;

		// 피격 연출 — 잠깐 부풀었다 돌아오는 타격감. 손으로 만져봐야 하는 수치라 인스펙터에 꺼내둔다.
		[Header("Hit Feedback")]
		[SerializeField] private float hitScaleMultiplier = 1.4f;
		[SerializeField] private float hitScaleUpDuration = 0.1f;
		[SerializeField] private float hitScaleDownDuration = 0.2f;

		public bool IsAlive => Unit != null && Unit.UnitStat[UnitStatType.HP_CUR] > 0;

		public event Action<DamageInfo> OnTakeDamage;
		public event Action<int> OnHealed;
		public event Action OnDied;
		public event Action<int, int> OnHealthChanged;

		public void Init(UnitObject unitObject)
		{
			Unit = unitObject;

			Unit.MeshParent.DOKill();
			if (originScale != Vector3.zero)
				Unit.MeshParent.localScale = originScale;
			originScale = Unit.MeshParent.localScale;
			
			OnHealthChanged?.Invoke(Unit.UnitStat[UnitStatType.HP_CUR], Unit.UnitStat[UnitStatType.HP_MAX]);
		}

		public void ReceiveDamage(DamageInfo damageInfo)
		{
			if (IsAlive == false) return;

			SetHp(Unit.UnitStat[UnitStatType.HP_CUR] - damageInfo.damage);

			// Pivot 스케일 잠깐 키웠다가 줄이기 (기존 UnitObject의 연출 이동)
			Unit.MeshParent.DOScale(originScale * hitScaleMultiplier, hitScaleUpDuration).OnComplete(() =>
				Unit.MeshParent.DOScale(originScale, hitScaleDownDuration));

			OnTakeDamage?.Invoke(damageInfo);
		}

		public void ReceiveHeal(int healAmount)
		{
			if (IsAlive == false) return;

			SetHp(Unit.UnitStat[UnitStatType.HP_CUR] + healAmount);
			OnHealed?.Invoke(healAmount);
		}

		private void SetHp(int newHp)
		{
			if (Unit == null) return;

			int maxHp = Unit.UnitStat[UnitStatType.HP_MAX];
			newHp = Mathf.Clamp(newHp, 0, maxHp);
			Unit.UnitStat[UnitStatType.HP_CUR] = newHp;

			OnHealthChanged?.Invoke(newHp, maxHp);

			if (newHp <= 0)
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