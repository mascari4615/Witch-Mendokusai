using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class EquipmentMagnet : MonoBehaviour
	{
		private UnitStat PlayerStat => Player.Instance.UnitStat;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_COLLIDER_SCALE, UpdateEquipment);
			UpdateEquipment();
		}

		public void UpdateEquipment()
		{
			Player.Instance.ExpCollider.transform.localScale =
				Vector3.one * (1 + (PlayerStat[UnitStatType.EXP_COLLIDER_SCALE] * .5f));
		}
	}
}