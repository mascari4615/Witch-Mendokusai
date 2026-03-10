using System;
using UnityEngine;
using DG.Tweening;

namespace WitchMendokusai
{
	public class UnitHealth : MonoBehaviour, IDamageable
	{
		private UnitObject unit;
		private Vector3 originScale;

		public bool IsAlive => unit != null && unit.UnitStat[UnitStatType.HP_CUR] > 0;

		public event Action<DamageInfo> OnTakeDamage;
		public event Action<int> OnHealed;
		public event Action OnDied;
		public event Action<int, int> OnHealthChanged;

		public void Init(UnitObject unitObject)
		{
			unit = unitObject;
			originScale = unit.MeshParent.localScale;
			
			OnHealthChanged?.Invoke(unit.UnitStat[UnitStatType.HP_CUR], unit.UnitStat[UnitStatType.HP_MAX]);
		}

		public void ReceiveDamage(DamageInfo damageInfo)
		{
			if (!IsAlive) return;

			SetHp(unit.UnitStat[UnitStatType.HP_CUR] - damageInfo.damage);

			// Pivot 스케일 잠깐 키웠다가 줄이기 (기존 UnitObject의 연출 이동)
			unit.MeshParent.DOScale(originScale * 1.4f, .1f).OnComplete(() =>
				unit.MeshParent.DOScale(originScale, .2f));

			OnTakeDamage?.Invoke(damageInfo);
		}

		public void ReceiveHeal(int healAmount)
		{
			if (!IsAlive) return;

			SetHp(unit.UnitStat[UnitStatType.HP_CUR] + healAmount);
			OnHealed?.Invoke(healAmount);
		}

		private void SetHp(int newHp)
		{
			if (unit == null) return;

			int maxHp = unit.UnitStat[UnitStatType.HP_MAX];
			newHp = Mathf.Clamp(newHp, 0, maxHp);
			unit.UnitStat[UnitStatType.HP_CUR] = newHp;

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