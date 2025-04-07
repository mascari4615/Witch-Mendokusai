using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class SkillData : DataSO
	{
		[field: SerializeField] public bool AutoUse { get; set; }
		[field: SerializeField] public float Cooltime { get; set; }
		[field: SerializeField] public float PrevDelay { get; set; } = 0;
		[field: SerializeField] public float AfterDelay { get; set; } = 0;

		public void Use(UnitObject unitObject)
		{
			unitObject.StartCoroutine(SkillCoroutine(unitObject));
		}

		public IEnumerator SkillCoroutine(UnitObject unitObject)
		{
			yield return null;
			
			if (PrevDelay > 0)
			{
				GameManager.Instance.IsCooling = true;
				yield return new WaitForSeconds(PrevDelay);
				GameManager.Instance.IsCooling = false;
			}

			ActualUse(unitObject);

			if (AfterDelay > 0)
			{
				GameManager.Instance.IsCooling = true;
				yield return new WaitForSeconds(AfterDelay);
				GameManager.Instance.IsCooling = false;
			}
		}

		public abstract void ActualUse(UnitObject unitObject);
	}
}