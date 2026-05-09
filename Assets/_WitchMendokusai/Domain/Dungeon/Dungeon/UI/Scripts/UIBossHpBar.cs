using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIBossHpBar : MonoBehaviour
	{
		[SerializeField] private Image hpBar;
		[SerializeField] private CanvasGroup canvasGroup;
		[SerializeField] private TextMeshProUGUI hpBarText;
		[SerializeField] private TextMeshProUGUI nameText;

		[SerializeField] private bool disableOnDied = false;

		[SerializeField] private MonsterObjectVariable lastHitEnemyObject;

		private UnitObject targetUnit;

		private void OnEnable()
		{
			UpdateUI();
		}

		public void UpdateUI()
		{
			canvasGroup.alpha = 1;

			targetUnit = lastHitEnemyObject.RuntimeValue;
			nameText.text = targetUnit.UnitData.Name;

			hpBarText.text = $"{targetUnit.UnitStat[UnitStatType.HP_CUR]} / {targetUnit.UnitStat[UnitStatType.HP_MAX]}";
			hpBar.fillAmount = (float)targetUnit.UnitStat[UnitStatType.HP_CUR] / targetUnit.UnitStat[UnitStatType.HP_MAX];

			if (targetUnit.UnitStat[UnitStatType.HP_CUR] != 0)
				return;

			if (disableOnDied)
				canvasGroup.alpha = 0;
		}

		public void UpdateUI(MonsterObject targetEnemy, int curHp)
		{
			canvasGroup.alpha = 1;

			targetUnit = targetEnemy;
			nameText.text = targetUnit.UnitData.Name;

			hpBarText.text = $"{curHp} / {targetUnit.UnitStat[UnitStatType.HP_MAX]}";
			hpBar.fillAmount = (float)curHp / targetUnit.UnitStat[UnitStatType.HP_MAX];

			if (curHp != 0)
				return;

			if (disableOnDied)
				canvasGroup.alpha = 0;
		}

		public void Disable()
		{
			canvasGroup.alpha = 0;
		}
	}
}