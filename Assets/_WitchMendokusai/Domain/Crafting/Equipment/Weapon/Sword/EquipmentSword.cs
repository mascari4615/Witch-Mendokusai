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
		private ResourceManager resourceManager;
		private readonly List<(float Score, Vector3 Direction)> targetBuffer = new();
		private Vector3 lastDirection = Vector3.right;
		private float swingSide = 1f;
		private int resonanceVolley;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, SOManager soManager, ResourceManager resourceManager)
		{
			this.resourceManager = resourceManager;
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			this.soManager = soManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public override void InitContext(SkillObject skillObject)
		{
			swingCount = 1;
			resonanceVolley = 0;
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
			SwordPattern pattern = resourceManager.SurvivalFeel.Sword;
			for (int i = 0; i < swingCount; i++)
			{
				Attack(PickDirection(i, pattern), 0);
				yield return wait;
			}
			if (HasResonance())
			{
				resonanceVolley++;
				if ((resonanceVolley - 1) % pattern.ResonanceBurstEvery == 0)
					ResonanceBurst(pattern);
			}
		}

		private void ResonanceBurst(SwordPattern pattern)
		{
			Vector3 first = PickDirection(0, pattern);
			float step = 360f / pattern.ResonanceBurstSlashes;
			for (int i = 0; i < pattern.ResonanceBurstSlashes; i++)
				Attack(Quaternion.AngleAxis(step * i, Vector3.up) * first, pattern.ResonanceBurstDamage);
			SurvivalFeel.Instance.OnResonanceBurst(transform.position);
		}

		private void Attack(Vector3 direction, int extraDamage)
		{
			GameObject g = objectPoolManager.Spawn(bulletPrefab);

			Vector3 spawnPosition = transform.position;
			spawnPosition.y = 0;
			g.transform.position = spawnPosition;
			g.transform.rotation = Quaternion.LookRotation(direction);

			if (g.TryGetComponent(out SkillObject skillObject))
				skillObject.InitContext(new SkillContext(playerProvider.CurrentObject, playerProvider, objectPoolManager));

			if (g.GetComponentInChildren<DamagingObject>() is DamagingObject damagingObject)
				damagingObject.SetDamageBonus(damageBonus + extraDamage);

			g.SetActive(true);
			swingSide = -swingSide;
			PlaySwing(g.transform, swingSide);
		}

		// 이동 방향으로 치우친 가까운 순서의 index 번째 적, 적이 모자라면 부채꼴로 벌림
		private Vector3 PickDirection(int index, SwordPattern pattern)
		{
			Vector3 origin = transform.position;
			Vector3 moveDirection = playerProvider.CurrentObject.UnitMovement.MoveDirectionLocal;
			moveDirection.y = 0f;
			bool moving = moveDirection.sqrMagnitude > 0.001f;
			if (moving)
				lastDirection = moveDirection.normalized;

			targetBuffer.Clear();
			float rangeSquared = pattern.TargetRange * pattern.TargetRange;
			foreach (GameObject monster in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				Vector3 offset = monster.transform.position - origin;
				offset.y = 0f;
				float distanceSquared = offset.sqrMagnitude;
				if (distanceSquared > rangeSquared || distanceSquared < 0.0001f)
					continue;
				float distance = Mathf.Sqrt(distanceSquared);
				Vector3 toMonster = offset / distance;
				float score = moving ? distance - Vector3.Dot(toMonster, lastDirection) * pattern.ForwardBias : distance;
				targetBuffer.Add((score, toMonster));
			}
			targetBuffer.Sort((left, right) => left.Score.CompareTo(right.Score));

			if (index < targetBuffer.Count)
			{
				lastDirection = index == 0 ? targetBuffer[0].Direction : lastDirection;
				return targetBuffer[index].Direction;
			}
			int extra = index - targetBuffer.Count + 1;
			float angle = pattern.FanDegrees * Mathf.Ceil(extra * 0.5f) * (extra % 2 == 0 ? -1f : 1f);
			return Quaternion.AngleAxis(angle, Vector3.up) * lastDirection;
		}

		private bool HasResonance()
		{
			if (resonanceCard == null)
				return false;
			foreach (CardData card in soManager.SelectedCardBuffer.Data)
				if (card.ID == resonanceCard.ID)
					return true;
			return false;
		}

		// 판정 방향으로 실제 검을 휘두르는 연출, 강화 카드 보유 시 강화 휘두르기
		// 연출 크기는 이번 판정 오브젝트에서 측정한 범위에 맞춤
		private void PlaySwing(Transform attack, float side)
		{
			SwordSwing prefab = HasResonance() ? resonanceSwingPrefab : basicSwingPrefab;
			GameObject visual = objectPoolManager.Spawn(prefab.gameObject);
			visual.transform.SetPositionAndRotation(attack.position, attack.rotation);
			visual.SetActive(true);
			visual.GetComponent<SwordSwing>().PlayPooled(objectPoolManager, gameObject, playerProvider.Current.transform, SwordSwingArea.FromHitbox(attack), side);
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
