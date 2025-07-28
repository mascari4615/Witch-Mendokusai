using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class EquipmentTurret : SkillComponent
	{
		[SerializeField] private float originCoolTime = 1.5f;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject turretPrefab;

		private readonly List<Transform> turretTransforms = new();
		private float coolTime;
		private int damageBonus;

		private Coroutine loop;

		private UnitStat PlayerStat => Player.Instance.UnitStat;

		public override void InitContext(SkillObject skillObject)
		{
			turretTransforms.Clear();
		}

		private void OnEnable()
		{
			UpdateTurret();
			UpdateDamageBonus();
			UpdateAttackSpeedBonus();

			loop = StartCoroutine(Loop());

			transform.position = Player.Instance.transform.position;
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.TURRET_COUNT, UpdateTurret);
			PlayerStat.AddListener(UnitStatType.TURRET_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.AddListener(UnitStatType.TURRET_ATTACK_SPEED_BONUS, UpdateAttackSpeedBonus);
		}

		private IEnumerator Loop()
		{
			int turretIndex = 0;
			while (true)
			{
				if (Player.Instance.AimPos == Vector3.zero)
				{
					yield return new WaitForSeconds(.1f);
					continue;
				}

				turretIndex = ++turretIndex % turretTransforms.Count;

				GameObject bulletObject = ObjectPoolManager.Instance.Spawn(bulletPrefab);

				Vector3 bulletSpawnPos = turretTransforms[turretIndex].position;
				bulletSpawnPos.y = 0;
				bulletObject.transform.position = bulletSpawnPos;

				if (bulletObject.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(Player.Instance.Object);

				if (bulletObject.TryGetComponent(out DamagingObject damagingObject))
					damagingObject.SetDamageBonus(damageBonus);

				bulletObject.SetActive(true);

				yield return new WaitForSeconds(coolTime / turretTransforms.Count);
			}
		}

		private void UpdateTurret()
		{
			int turretCount = 1 + PlayerStat[UnitStatType.TURRET_COUNT];

			if (turretTransforms.Count < turretCount)
			{
				int diff = turretCount - turretTransforms.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = ObjectPoolManager.Instance.Spawn(turretPrefab);
					g.transform.SetParent(transform);
					// g.transform.localPosition = Vector3.zero;
					Vector3 randomPos = Random.insideUnitSphere * 2;
					randomPos.y = 0;
					g.transform.localPosition = randomPos;
					g.SetActive(true);

					turretTransforms.Add(g.transform.GetChild(0).transform);
				}
			}

			// float delta = 360f / turretCount;
			// for (int i = 0; i < transform.childCount; i++)
			// {
			// 	transform.GetChild(i).transform.localRotation = Quaternion.Euler(Vector3.up * (delta * i));
			// }
		}

		private void UpdateDamageBonus()
		{
			damageBonus = PlayerStat[UnitStatType.TURRET_DAMAGE_BONUS];
		}

		private void UpdateAttackSpeedBonus()
		{
			coolTime = originCoolTime * (1 - PlayerStat[UnitStatType.TURRET_ATTACK_SPEED_BONUS] * .2f);
		}
	}
}