using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class UIStatus : UIBase
	{
		private List<UISlot> statusSlots;
		private bool isInit = false;

		private readonly List<UnitStatType> showOnStatPanelTypes = new()
		{
			UnitStatType.EXP_BONUS,
			UnitStatType.MOVEMENT_SPEED,
			UnitStatType.PICKUP_RADIUS,
			UnitStatType.COOLTIME_BONUS,
			UnitStatType.ATTACK_SPEED_BONUS,
			UnitStatType.PROJECTILE_COUNT_BONUS,
			UnitStatType.PROJECTILE_SPEED_BONUS,
			UnitStatType.PROJECTILE_DURATION_BONUS,
			UnitStatType.PROJECTILE_SCALE_BONUS,
			UnitStatType.PROJECTILE_PIERCE_BONUS,
			UnitStatType.DAMAGE_BONUS,
			UnitStatType.CRITICAL_CHANCE,
			UnitStatType.CRITICAL_DAMAGE,
			UnitStatType.ARMOR,
			UnitStatType.DODGE,
			UnitStatType.INVINCIBLE_TIME,
			UnitStatType.GOLD_BONUS,
		};

		public override void Init()
		{
			statusSlots = GetComponentsInChildren<UISlot>(true).ToList();
			foreach (UISlot slot in statusSlots)
			{
				slot.Init();
				slot.UpdateUI();
				slot.gameObject.SetActive(false);
			}

			isInit = true;
		}

		public override void UpdateUI()
		{
			// List<UnitStatType> stats = Enum.GetValues(typeof(UnitStatType)).Cast<UnitStatType>().ToList();
			List<UnitStatType> stats = showOnStatPanelTypes.ToList();

			for (int i = 0; i < statusSlots.Count; i++)
			{
				if (i >= stats.Count)
				{
					statusSlots[i].gameObject.SetActive(false);
					continue;
				}

				UnitStatData unitStatData = Get<UnitStatData>((int)stats[i]);
				UnitStatType targetType = stats[i];

				statusSlots[i].gameObject.SetActive(true);

				int curValue = Player.Instance.UnitStat[targetType];
				statusSlots[i].SetSlot(unitStatData, curValue);
				statusSlots[i].UpdateUI();
			}
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				UpdateUI();
				yield return new WaitForSeconds(TimeManager.TICK);
			}
		}

		private void OnEnable()
		{
			if (isInit)
				StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			StopAllCoroutines();
		}
	}
}