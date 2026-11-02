using System.Collections;
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
		[SerializeField] private SwordSwing basicSwingPrefab;
		[SerializeField] private SwordSwing resonanceSwingPrefab;
		[SerializeField] private CardData resonanceCard;
#pragma warning disable CS0414 // 칼 회전 미구현 예약 필드
		[SerializeField] private float rotateSpeed = -30f;
#pragma warning restore CS0414

		private int swingCount = 1;
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
			swingCount = 1;
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
			for (int i = 0; i < swingCount; i++)
			{
				Attack(i);
				yield return wait;
			}

			void Attack(int index)
			{
				GameObject g = objectPoolManager.Spawn(bulletPrefab);

				Vector3 spawnPosition = transform.position;
				spawnPosition.y = 0;
				g.transform.position = spawnPosition;

				g.transform.rotation = Quaternion.LookRotation(index % 2 == 0 ? Vector3.right : Vector3.left);
				if (playerWasLookingRight == false)
					g.transform.Rotate(0, 180, 0);

				if (g.TryGetComponent(out SkillObject skillObject))
					skillObject.InitContext(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager));

				if (g.GetComponentInChildren<DamagingObject>() is DamagingObject damagingObject)
					damagingObject.SetDamageBonus(damageBonus);

				g.SetActive(true);
				PlaySwing(g.transform);
			}
		}

		// 판정 방향으로 실제 검을 휘두르는 연출, 강화 카드 보유 시 강화 휘두르기
		// 연출 크기는 이번 판정 오브젝트에서 측정한 범위에 맞춤
		private void PlaySwing(Transform attack)
		{
			SwordSwing prefab = basicSwingPrefab;
			foreach (CardData card in soManager.SelectedCardBuffer.Data)
			{
				if (resonanceCard != null && card == resonanceCard)
				{
					prefab = resonanceSwingPrefab;
					break;
				}
			}
			GameObject visual = objectPoolManager.Spawn(prefab.gameObject);
			visual.transform.SetPositionAndRotation(attack.position, attack.rotation);
			visual.SetActive(true);
			visual.GetComponent<SwordSwing>().PlayPooled(objectPoolManager, gameObject, playerProvider.Current.transform, SwordSwingArea.FromHitbox(attack));
		}

		private void UpdateSword(int swordCount)
		{
			swingCount = 1 + swordCount;
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
