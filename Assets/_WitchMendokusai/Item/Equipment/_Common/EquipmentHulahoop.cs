using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class EquipmentHulahoop : SkillComponent
	{
		[SerializeField] private GameObject satellitePrefab;
		[SerializeField] private float originRotateSpeed = 3;

		private readonly List<DamagingObject> satellites = new();
		private float rotateSpeed;

		private UnitStat PlayerStat => Player.Instance.UnitStat;

		private void OnEnable()
		{
			UpdateSatellite();
			UpdateRotationSpeed();
			UpdateDamageBonus();
			UpdateSatelliteScale();
		}

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.SATELLITE_COUNT, UpdateSatellite);
			PlayerStat.AddListener(UnitStatType.SATELLITE_ROTATE_SPEED_BONUS, UpdateRotationSpeed);
			PlayerStat.AddListener(UnitStatType.SATELLITE_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.AddListener(UnitStatType.SATELLITE_SCALE_BONUS, UpdateSatelliteScale);
		}

		private void Update()
		{
			transform.position = Player.Instance.transform.position;
			transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);
		}

		public override void InitContext(SkillObject skillObject)
		{
			satellites.Clear();
		}

		private void UpdateSatellite()
		{
			int satelliteCount = 1 + PlayerStat[UnitStatType.SATELLITE_COUNT];

			if (transform.childCount < satelliteCount)
			{
				int diff = satelliteCount - transform.childCount;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = ObjectPoolManager.Instance.Spawn(satellitePrefab);
					g.transform.SetParent(transform);
					g.transform.localPosition = Vector3.zero;
					g.SetActive(true);

					DamagingObject damagingObject = g.GetComponentInChildren<DamagingObject>(true);
					if (damagingObject != null)
						satellites.Add(damagingObject);
				}
			}

			float delta = 360f / satelliteCount;
			for (int i = 0; i < transform.childCount; i++)
			{
				transform.GetChild(i).transform.localRotation = Quaternion.Euler(Vector3.up * (delta * i));
			}
		}

		private void UpdateRotationSpeed()
		{
			rotateSpeed = originRotateSpeed * (1 + PlayerStat[UnitStatType.SATELLITE_ROTATE_SPEED_BONUS] * .3f);
		}

		private void UpdateDamageBonus()
		{
			foreach (DamagingObject satellite in satellites)
				satellite.SetDamageBonus(PlayerStat[UnitStatType.SATELLITE_DAMAGE_BONUS]);
		}

		private void UpdateSatelliteScale()
		{
			foreach (DamagingObject satellite in satellites)
				satellite.transform.localScale = Vector3.one * (1 + PlayerStat[UnitStatType.SATELLITE_SCALE_BONUS] * .2f);
		}
	}
}