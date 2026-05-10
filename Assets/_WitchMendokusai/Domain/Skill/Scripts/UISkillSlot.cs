using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UISkillSlot : UISlot
	{
		[SerializeField] private Image coolTimeImage;

		public void UpdateCooltime(Skill skill)
		{
			// Debug.Log($"UpdateCooltime : {skill.Data.Name} : {skill.Cooldown.Remain} / {skill.Cooldown.Base}");
			coolTimeImage.fillAmount = skill.Cooldown.Remain / skill.Cooldown.Base;
		}
	}
}