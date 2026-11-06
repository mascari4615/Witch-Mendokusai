using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentHulahoop : SkillComponent
	{
		[SerializeField] private GameObject satellitePrefab;
		[SerializeField] private float originRotateSpeed = 3;

		private readonly List<DamagingObject> satellites = new();
		private float rotateSpeed;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		private void OnEnable()
		{
			PlayerStat.AddListener(UnitStatType.SATELLITE_COUNT, UpdateSatellite);
			PlayerStat.AddListener(UnitStatType.SATELLITE_ROTATE_SPEED_BONUS, UpdateRotationSpeed);
			PlayerStat.AddListener(UnitStatType.SATELLITE_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.AddListener(UnitStatType.SATELLITE_SCALE_BONUS, UpdateSatelliteScale);
			PlayerStat.AddListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSatellite);
			PlayerStat.AddListener(UnitStatType.PROJECTILE_SCALE_BONUS, UpdateSatelliteScale);
			UpdateSatellite();
			UpdateRotationSpeed();
			UpdateDamageBonus();
			UpdateSatelliteScale();
		}

		private void OnDisable()
		{
			PlayerStat.RemoveListener(UnitStatType.SATELLITE_COUNT, UpdateSatellite);
			PlayerStat.RemoveListener(UnitStatType.SATELLITE_ROTATE_SPEED_BONUS, UpdateRotationSpeed);
			PlayerStat.RemoveListener(UnitStatType.SATELLITE_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.RemoveListener(UnitStatType.SATELLITE_SCALE_BONUS, UpdateSatelliteScale);
			PlayerStat.RemoveListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateSatellite);
			PlayerStat.RemoveListener(UnitStatType.PROJECTILE_SCALE_BONUS, UpdateSatelliteScale);
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);
		}

		public override void InitContext(SkillObject skillObject)
		{
			satellites.Clear();
		}

		private void UpdateSatellite()
		{
			int satelliteCount = 1 + PlayerStat[UnitStatType.SATELLITE_COUNT] + PlayerStat[UnitStatType.PROJECTILE_COUNT_BONUS];

			if (satellites.Count < satelliteCount)
			{
				int diff = satelliteCount - satellites.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = objectPoolManager.Spawn(satellitePrefab);
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
			// 수 증가로 새로 생긴 위성에도 이미 선택한 강화 적용
			UpdateDamageBonus();
			UpdateSatelliteScale();
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
			float rangeMultiplier = PlayerStat.PercentMultiplier(UnitStatType.PROJECTILE_SCALE_BONUS);
			foreach (DamagingObject satellite in satellites)
				satellite.transform.localScale = Vector3.one * ((1 + PlayerStat[UnitStatType.SATELLITE_SCALE_BONUS] * .2f) * rangeMultiplier);
		}
	}
}
