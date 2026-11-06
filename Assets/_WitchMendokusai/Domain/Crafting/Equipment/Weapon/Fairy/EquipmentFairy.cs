using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentFairy : SkillComponent
	{
		[SerializeField] private float originCoolTime = 1.5f;
		// 조준 위치가 아직 안 잡혔을 때 다시 물어보기까지의 간격.
		[SerializeField] private float aimPollDelay = 0.1f;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject fairyPrefab;
		[SerializeField] private float rotateSpeed = -30f;

		[Header("Stat")]
		[SerializeField] private UnitStatType countStat = UnitStatType.FAIRY_COUNT;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.FAIRY_DAMAGE_BONUS;
		[SerializeField] private UnitStatType attackSpeedBonusStat = UnitStatType.FAIRY_ATTACK_SPEED_BONUS;
		[SerializeField, Range(0f, 1f)] private float attackSpeedReductionPerStack = 0.2f;
		[SerializeField, Range(0.01f, 1f)] private float minCoolTimeRatio = 0.2f;

		private readonly List<Transform> fairyTransforms = new();
		private float coolTime;
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

		public override void InitContext(SkillObject skillObject)
		{
			fairyTransforms.Clear();
		}

		private void OnEnable()
		{
			PlayerStat.AddListener(countStat, UpdateFairy);
			PlayerStat.AddListener(damageBonusStat, UpdateDamageBonus);
			PlayerStat.AddListener(attackSpeedBonusStat, UpdateAttackSpeedBonus);
			PlayerStat.AddListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateFairyByCountBonus);
			UpdateFairy(PlayerStat[countStat]);
			UpdateDamageBonus(PlayerStat[damageBonusStat]);
			UpdateAttackSpeedBonus(PlayerStat[attackSpeedBonusStat]);

			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
			// 비활성 정령의 강화 알림 해제
			PlayerStat.RemoveListener(countStat, UpdateFairy);
			PlayerStat.RemoveListener(damageBonusStat, UpdateDamageBonus);
			PlayerStat.RemoveListener(attackSpeedBonusStat, UpdateAttackSpeedBonus);
			PlayerStat.RemoveListener(UnitStatType.PROJECTILE_COUNT_BONUS, UpdateFairyByCountBonus);
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);
		}

		private IEnumerator Loop()
		{
			int fairyIndex = 0;
			while (true)
			{
				if (playerProvider.Current.AimPos == Vector3.zero)
				{
					yield return new WaitForSeconds(aimPollDelay);
					continue;
				}

				fairyIndex = ++fairyIndex % fairyTransforms.Count;

				GameObject g = objectPoolManager.Spawn(bulletPrefab);

				Vector3 spawnPosition = fairyTransforms[fairyIndex].position;
				spawnPosition.y = 0;
				g.transform.position = spawnPosition;

				if (g.TryGetComponent(out SkillObject skillObject))
					skillObject.InitAttack(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager));

				if (g.TryGetComponent(out DamagingObject damagingObject))
					damagingObject.SetDamageBonus(damageBonus);
				else if (g.TryGetComponent(out ExplodeOnEnd explodeOnEnd))
					explodeOnEnd.SetDamageBonus(damageBonus);

				g.SetActive(true);

				yield return new WaitForSeconds(coolTime / fairyTransforms.Count);
			}
		}

		private void UpdateFairyByCountBonus() => UpdateFairy(PlayerStat[countStat]);

		private void UpdateFairy(int fairyCount)
		{
			int actualFairyCount = 1 + fairyCount + PlayerStat[UnitStatType.PROJECTILE_COUNT_BONUS];

			if (fairyTransforms.Count < actualFairyCount)
			{
				int diff = actualFairyCount - fairyTransforms.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = objectPoolManager.Spawn(fairyPrefab);
					g.transform.SetParent(transform);
					g.transform.localPosition = Vector3.zero;
					g.SetActive(true);

					fairyTransforms.Add(g.transform.GetChild(0).transform);
				}
			}

			float delta = 360f / actualFairyCount;
			for (int i = 0; i < transform.childCount; i++)
			{
				transform.GetChild(i).transform.localRotation = Quaternion.Euler(Vector3.up * (delta * i));
			}
		}

		private void UpdateDamageBonus(int fairyDamageBonus)
		{
			damageBonus = fairyDamageBonus;
		}

		private void UpdateAttackSpeedBonus(int fairyAttackSpeedBonus)
		{
			coolTime = AttackCadence.Interval(originCoolTime, fairyAttackSpeedBonus, attackSpeedReductionPerStack, minCoolTimeRatio);
		}
	}
}
