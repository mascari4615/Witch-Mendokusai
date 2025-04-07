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
			unitObject.StartCoroutine(DashLoop());
		}

		private IEnumerator DashLoop()
		{
			GameManager.Instance.IsDashing = true;
			yield return new WaitForSeconds(SOManager.Instance.DashDuration.RuntimeValue);
			GameManager.Instance.IsDashing = false;
		}
	}
}