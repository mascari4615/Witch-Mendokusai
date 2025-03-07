using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UIProfile : MonoBehaviour
	{
		[SerializeField] private UISlot slotUI;

		private void Start()
		{
			GameEventManager.Instance.RegisterCallback(GameEventType.OnPlayerDollChange, UpdateUI);
			UpdateUI();
		}

		public void UpdateUI()
		{
			slotUI.SetSlot(Player.Instance.Object.UnitData);
		}
	}
}