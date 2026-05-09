using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class EquipmentMagnet : MonoBehaviour
	{
		private UnitStat PlayerStat => PlayerProvider.Instance.Current.UnitStat;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_COLLIDER_SCALE, UpdateEquipment);
			UpdateEquipment();
		}

		public void UpdateEquipment()
		{
			PlayerProvider.Instance.Current.ExpCollider.transform.localScale =
				Vector3.one * (1 + (PlayerStat[UnitStatType.EXP_COLLIDER_SCALE] * .5f));
		}
	}
}