using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(Dash), menuName = "Skill/" + nameof(Dash))]
	public class Dash : SkillData
	{
		public override void ActualUse(UnitObject unitObject)
		{
			unitObject.StartCoroutine(DashLoop(unitObject));
		}

		private IEnumerator DashLoop(UnitObject unitObject)
		{
			unitObject.UnitStat[UnitStatType.FORCE_MOVE]++;
			yield return new WaitForSeconds(SOManager.Instance.DashDuration.RuntimeValue);
			unitObject.UnitStat[UnitStatType.FORCE_MOVE]--;
		}
	}
}