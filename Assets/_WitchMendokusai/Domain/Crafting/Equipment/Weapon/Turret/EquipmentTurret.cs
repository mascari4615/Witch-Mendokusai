using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentTurret : SkillComponent, IEquipmentCooldown
	{
		[SerializeField] private float originCoolTime = 1.5f;
		// 조준 위치가 아직 안 잡혔을 때 다시 물어보기까지의 간격.
		[SerializeField] private float aimPollDelay = 0.1f;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject turretPrefab;

		[Header("Stat")]
		[SerializeField, Range(0f, 1f)] private float attackSpeedReductionPerStack = 0.2f;
		[SerializeField, Range(0.01f, 1f)] private float minCoolTimeRatio = 0.2f;

		private readonly List<Transform> turretTransforms = new();
		private int damageBonus;

		private Coroutine loop;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;
		// 구독한 대상에서 해제. 플레이어가 먼저 사라져도 OnDisable 이 안전
		private UnitStat subscribedStat;

		private IItemData usedEquipment;
		// 스킬창 장비 칸의 대기 표시용. 다음 공격 시각과 그 주기 (EquipmentCooldowns)
		private float readyTime;
		private float readyInterval;
		public CooldownState Cooldown => CooldownState.FromReadyTime(readyTime, readyInterval);


		public override void InitContext(SkillObject skillObject)
		{
			usedEquipment = skillObject.Context.UsedEquipment;
			turretTransforms.Clear();
		}

		private void OnEnable()
		{
			subscribedStat = PlayerStat;
			PlayerStat.AddListener(UnitStatType.TURRET_COUNT, UpdateTurret);
			PlayerStat.AddListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateTurret);
			PlayerStat.AddListener(UnitStatType.TURRET_DAMAGE_BONUS, UpdateDamageBonus);
			UpdateTurret();
			UpdateDamageBonus();

			readyTime = 0f;
			EquipmentCooldowns.Register(usedEquipment, this);
			loop = StartCoroutine(Loop());

			transform.position = playerProvider.Current.transform.position;
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
			EquipmentCooldowns.Unregister(usedEquipment, this);
			subscribedStat?.RemoveListener(UnitStatType.TURRET_COUNT, UpdateTurret);
			subscribedStat?.RemoveListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateTurret);
			subscribedStat?.RemoveListener(UnitStatType.TURRET_DAMAGE_BONUS, UpdateDamageBonus);
			subscribedStat = null;
		}

		private IEnumerator Loop()
		{
			int turretIndex = 0;
			while (true)
			{
				if (playerProvider.Current.AimPos == Vector3.zero)
				{
					yield return new WaitForSeconds(aimPollDelay);
					continue;
				}

				turretIndex = ++turretIndex % turretTransforms.Count;

				GameObject bulletObject = objectPoolManager.Spawn(bulletPrefab);

				Vector3 bulletSpawnPos = turretTransforms[turretIndex].position;
				bulletSpawnPos.y = 0;
				bulletObject.transform.position = bulletSpawnPos;
				CombatSfx.Play(CombatSfxEvent.WeaponThrow, bulletSpawnPos);

				if (bulletObject.TryGetComponent(out SkillObject skillObject))
					skillObject.InitAttack(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager, usedEquipment));

				if (bulletObject.TryGetComponent(out DamagingObject damagingObject))
					damagingObject.SetDamageBonus(damageBonus);

				bulletObject.SetActive(true);

				readyInterval = CoolTime / turretTransforms.Count;
				readyTime = Time.time + readyInterval;
				yield return new WaitForSeconds(readyInterval);
			}
		}

		private void UpdateTurret()
		{
			int turretCount = 1 + PlayerStat[UnitStatType.TURRET_COUNT] + PlayerStat[UnitStatType.PROJECTILE_COUNT_BONUS];

			if (turretTransforms.Count < turretCount)
			{
				int diff = turretCount - turretTransforms.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = objectPoolManager.Spawn(turretPrefab);
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

		// 쓸 때마다 계산. 공용 공격속도 카드와 쿨타임 감소도 같이 받음
		private float CoolTime => AttackCadence.WeaponInterval(originCoolTime, PlayerStat[UnitStatType.TURRET_ATTACK_SPEED_BONUS], PlayerStat[UnitStatType.ATTACK_SPEED_BONUS],
			attackSpeedReductionPerStack, minCoolTimeRatio, PlayerStat[UnitStatType.COOLTIME_BONUS]);
	}
}
