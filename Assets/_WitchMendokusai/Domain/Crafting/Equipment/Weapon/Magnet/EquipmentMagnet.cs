using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentMagnet : MonoBehaviour
	{
		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(PlayerProvider playerProvider)
		{
			this.playerProvider = playerProvider;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.EXP_COLLIDER_SCALE, UpdateEquipment);
			UpdateEquipment();
		}

		public void UpdateEquipment()
		{
			playerProvider.Current.ExpCollider.transform.localScale =
				Vector3.one * (1 + (PlayerStat[UnitStatType.EXP_COLLIDER_SCALE] * .5f));
		}
	}
}
