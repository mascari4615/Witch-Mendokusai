using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class JRPG_UIManager : MonoBehaviour
	{
		[SerializeField] private List<UIJRPGTurnIcon> turnIcons;
		[SerializeField] private Transform[] turnIconPos;

		[SerializeField] private JRPG_Manager combatManager;
		[SerializeField] private GameObject skillPanel;

		public void UpdatePanel()
		{
			skillPanel.SetActive(combatManager.CurState == JRPG_Manager.CombatStage.MyTurn);
		}

		public void UpdateTurnUI()
		{
			foreach (var turnIcon in turnIcons)
			{
				turnIcon.gameObject.SetActive(false);
			}

			var units = combatManager.UnitInstances;
			var notReadyUnits = new List<JRPG_UnitInstance>();
			var turnOrder = 0;

			foreach (var unit in units)
			{
				if (unit.IsReady)
				{
					turnIcons[unit.UnitInstanceID].gameObject.SetActive(true);
					turnIcons[unit.UnitInstanceID].SetIconSprite(unit.UnitSpirte);
					turnIcons[unit.UnitInstanceID].transform.localScale = Vector3.one * (turnOrder == 0 ? 1.2f : 1f);
					turnIcons[unit.UnitInstanceID].transform.position = turnIconPos[turnOrder++].position;
				}
				else
				{
					notReadyUnits.Add(unit);
				}
			}

			notReadyUnits = notReadyUnits.OrderByDescending(instance => instance.curActPoint).ToList();

			foreach (var notReadyUnit in notReadyUnits)
			{
				turnIcons[notReadyUnit.UnitInstanceID].gameObject.SetActive(true);
				turnIcons[notReadyUnit.UnitInstanceID].SetIconSprite(notReadyUnit.UnitSpirte);
				turnIcons[notReadyUnit.UnitInstanceID].transform.localScale = Vector3.one * (turnOrder == 0 ? 1.2f : 1f);
				turnIcons[notReadyUnit.UnitInstanceID].transform.position = turnIconPos[turnOrder++].position;
			}
		}
	}
}