using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentSword : SkillComponent
	{
		[SerializeField] private float originCoolTime = 1.5f;
		// 검 여러 자루가 연속으로 나갈 때의 자루 간 간격.
		[SerializeField] private float eachAttackDelay = 0.1f;
		// 조준 위치가 아직 안 잡혔을 때 다시 물어보기까지의 간격.
		[SerializeField] private float aimPollDelay = 0.1f;
		// 공격속도 보너스 1스택이 쿨타임을 깎는 비율.
		[SerializeField] private float attackSpeedBonusPerStack = 0.2f;
		[SerializeField, Min(0.01f)] private float minimumCoolTime = 0.15f;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject swordPrefab;
		[SerializeField] private ProceduralSlash basicSlashPrefab;
		[SerializeField] private ProceduralSlash resonanceSlashPrefab;
		[SerializeField] private CardData resonanceCard;
		[SerializeField] private Vector3 slashOffset = new(0, 0.9f, 2.19f);
		[SerializeField] private Vector3 slashAngles = new(70, -110, 270);
		[SerializeField] private Vector3 slashScale = new(1.4f, 1.4f, 1.4f);
#pragma warning disable CS0414 // 칼 회전 미구현 예약 필드
		[SerializeField] private float rotateSpeed = -30f;
#pragma warning restore CS0414

		private readonly List<Transform> swordTransforms = new();
		private float coolTime;
		private int damageBonus;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;
		private SOManager soManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, SOManager soManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			this.soManager = soManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public override void InitContext(SkillObject skillObject)
		{
			swordTransforms.Clear();
		}

		private void OnEnable()
		{
			PlayerStat.AddListener(UnitStatType.SWORD_COUNT, UpdateSword);
			PlayerStat.AddListener(UnitStatType.SWORD_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.AddListener(UnitStatType.SWORD_ATTACK_SPEED_BONUS, UpdateAttackSpeedBonus);
			UpdateSword(PlayerStat[UnitStatType.SWORD_COUNT]);
			UpdateDamageBonus(PlayerStat[UnitStatType.SWORD_DAMAGE_BONUS]);
			UpdateAttackSpeedBonus(PlayerStat[UnitStatType.SWORD_ATTACK_SPEED_BONUS]);

			StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			PlayerStat.RemoveListener(UnitStatType.SWORD_COUNT, UpdateSword);
			PlayerStat.RemoveListener(UnitStatType.SWORD_DAMAGE_BONUS, UpdateDamageBonus);
			PlayerStat.RemoveListener(UnitStatType.SWORD_ATTACK_SPEED_BONUS, UpdateAttackSpeedBonus);
		}

		private void Update()
		{
			transform.position = playerProvider.Current.transform.position;
			// transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

			transform.rotation = playerProvider.CurrentObject.UnitMovement.IsLookingRight
				? Quaternion.Euler(0, 0, 0)
				: Quaternion.Euler(0, 180, 0);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				if (playerProvider.Current.AimPos == Vector3.zero)
				{
					yield return new WaitForSeconds(aimPollDelay);
					continue;
				}

				UpdateSword(PlayerStat[UnitStatType.SWORD_COUNT]);
				StartCoroutine(AttackLoop());

				yield return new WaitForSeconds(coolTime);
			}
		}

		private IEnumerator AttackLoop()
		{
			WaitForSeconds wait = new WaitForSeconds(eachAttackDelay);
			bool playerWasLookingRight = playerProvider.CurrentObject.UnitMovement.IsLookingRight;
			for (int i = 0; i < swordTransforms.Count; i++)
			{
				Attack(swordTransforms[i], i);
				yield return wait;
			}

			void Attack(Transform swordTransform, int index)
			{
				GameObject g = objectPoolManager.Spawn(bulletPrefab);

				Vector3 spawnPosition = transform.position;
				spawnPosition.y = 0;
				g.transform.position = spawnPosition;

				// 플레이어 to swordTransforms 방향을 앞쪽으로
				// 근데 x축 y축만
				// Vector3 onlyXYSpawnPos = swordTransform.position;
				// onlyXYSpawnPos.y = 0;
				// Vector3 onlyXYPlayerPos = playerProvider.Current.transform.position;
				// onlyXYPlayerPos.y = 0;
				// g.transform.rotation = Quaternion.LookRotation(onlyXYSpawnPos - onlyXYPlayerPos);

				g.transform.rotation = Quaternion.LookRotation(index % 2 == 0 ? Vector3.right : Vector3.left);
				if (playerWasLookingRight == false)
					g.transform.Rotate(0, 180, 0);

				if (g.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager));

				if (g.GetComponentInChildren<DamagingObject>() is DamagingObject damagingObject)
					damagingObject.SetDamageBonus(damageBonus);

				g.SetActive(true);
				PlaySlash(g.transform);
			}
		}

		private void PlaySlash(Transform attack)
		{
			ProceduralSlash prefab = basicSlashPrefab;
			foreach (CardData card in soManager.SelectedCardBuffer.Data)
			{
				if (resonanceCard != null && card == resonanceCard)
				{
					prefab = resonanceSlashPrefab;
					break;
				}
			}
			if (prefab == null) return;
			GameObject visual = objectPoolManager.Spawn(prefab.gameObject);
			visual.transform.SetPositionAndRotation(attack.TransformPoint(slashOffset), attack.rotation * Quaternion.Euler(slashAngles));
			visual.transform.localScale = slashScale;
			visual.SetActive(true);
			visual.GetComponent<ProceduralSlash>().PlayPooled(objectPoolManager, gameObject);
		}

		private void UpdateSword(int swordCount)
		{
			int actualSwordCount = 1 + swordCount;

			if (swordTransforms.Count < actualSwordCount)
			{
				int diff = actualSwordCount - swordTransforms.Count;
				for (int i = 0; i < diff; i++)
				{
					GameObject g = objectPoolManager.Spawn(swordPrefab);
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
			coolTime = Mathf.Max(minimumCoolTime, originCoolTime * (1 - attackSpeedBonus * attackSpeedBonusPerStack));
		}
	}
}
