using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class EquipmentSword : SkillComponent
	{
		private const float EachAttackDelay = 0.1f;

		// [SerializeField] private int originDamage = 2;
		[SerializeField] private float originCoolTime = 1.5f;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject swordPrefab;
		[SerializeField] private float rotateSpeed = -30f;

		private readonly List<Transform> swordTransforms = new();
		private float coolTime;
		private int damageBonus;

		private Coroutine loop;

		private UnitStat PlayerStat => Player.Instance.UnitStat;

		public override void InitContext(SkillObject skillObject)
		{
			swordTransforms.Clear();
		}

		private void OnEnable()
		{
			UpdateSword(PlayerStat[UnitStatType.SWORD_COUNT]);
			UpdateDamageBonus(PlayerStat[UnitStatType.SWORD_DAMAGE_BONUS]);
			UpdateAttackSpeedBonus(PlayerStat[UnitStatType.SWORD_ATTACK_SPEED_BONUS]);

			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private void Start()
		{
			PlayerStat.AddListener(UnitStatType.SWORD_COUNT, UpdateSword);
			PlayerStat.AddListener(UnitStatType.SWORD_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.AddListener(UnitStatType.SWORD_ATTACK_SPEED_BONUS, UpdateAttackSpeedBonus);
		}

		private void Update()
		{
			transform.position = Player.Instance.transform.position;
			// transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

			transform.rotation = Player.Instance.Object.UnitMovement.IsLookingRight
				? Quaternion.Euler(0, 0, 0)
				: Quaternion.Euler(0, 180, 0);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				if (Player.Instance.AimPos == Vector3.zero)
				{
					yield return new WaitForSeconds(.1f);
					continue;
				}

				UpdateSword(PlayerStat[UnitStatType.SWORD_COUNT]);
				StartCoroutine(AttackLoop());

				yield return new WaitForSeconds(coolTime);
			}
		}

		private IEnumerator AttackLoop()
		{
			WaitForSeconds wait = new WaitForSeconds(EachAttackDelay);
			bool playerWasLookingRight = Player.Instance.Object.UnitMovement.IsLookingRight;
			for (int i = 0; i < swordTransforms.Count; i++)
			{
				Attack(swordTransforms[i], i);
				yield return wait;
			}

			void Attack(Transform swordTransform, int index)
			{
				GameObject g = ObjectPoolManager.Instance.Spawn(bulletPrefab);

				Vector3 spawnPosition = transform.position;
				spawnPosition.y = 0;
				g.transform.position = spawnPosition;

				// 플레이어 to swordTransforms 방향을 앞쪽으로
				// 근데 x축 y축만
				// Vector3 onlyXYSpawnPos = swordTransform.position;
				// onlyXYSpawnPos.y = 0;
				// Vector3 onlyXYPlayerPos = Player.Instance.transform.position;
				// onlyXYPlayerPos.y = 0;
				// g.transform.rotation = Quaternion.LookRotation(onlyXYSpawnPos - onlyXYPlayerPos);

				g.transform.rotation = Quaternion.LookRotation(index % 2 == 0 ? Vector3.right : Vector3.left);
				if (playerWasLookingRight == false)
					g.transform.Rotate(0, 180, 0);

				if (g.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(Player.Instance.Object);

				if (g.TryGetComponent(out DamagingObject damagingObject))
					damagingObject.SetDamageBonus(damageBonus);

				g.SetActive(true);
			}
		}

		private void UpdateSword(int swordCount)
		{
			int actualSwordCount = 1 + swordCount;

			if (swordTransforms.Count < actualSwordCount)
			{
				int diff = actualSwordCount - swordTransforms.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = ObjectPoolManager.Instance.Spawn(swordPrefab);
					// GameObject g = new("SwordParent");
					g.transform.SetParent(transform);
					g.transform.localPosition = Vector3.zero;
					g.SetActive(true);

					swordTransforms.Add(g.transform.GetChild(0).transform);
				}
			}

			// 짝수 번호는 Vector3.up * 90, 홀수 번호는 Vector3.up * -90
			for (int i = 0; i < transform.childCount; i++)
			{
				Vector3 rotation = (i % 2 == 0) ? Vector3.up * 90 : Vector3.up * -90;
				transform.GetChild(i).transform.localRotation = Quaternion.Euler(rotation);
			}
		}

		private void UpdateDamageBonus(int damageBonus)
		{
			this.damageBonus = damageBonus;
		}

		private void UpdateAttackSpeedBonus(int attackSpeedBonus)
		{
			coolTime = originCoolTime * (1 - attackSpeedBonus * .2f);
		}
	}
}