using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 쿨마다 인형을 꺼내 정해진 시간 동안 곁에서 가까운 적에게 종이 폭탄을 던짐. 머리 위 막대가 남은 시간
	public class EquipmentStrawDollSummon : SkillComponent
	{
		[SerializeField] private GameObject dollPrefab;
		[SerializeField] private GameObject bulletPrefab;
		[SerializeField] private GameObject barPrefab;
		[SerializeField, Min(0.1f)] private float summonCooldown = 17f;
		[SerializeField, Min(0.1f)] private float lifetime = 8f;
		[SerializeField, Min(1)] private int maxActive = 1;
		[SerializeField, Min(0.1f)] private float fireInterval = 0.9f;
		[SerializeField, Min(0.1f)] private float range = 6f;
		[SerializeField, Min(0f)] private float followRadius = 2.2f;
		[SerializeField, Min(0.1f)] private float followSpeed = 6f;
		[SerializeField] private Vector3 barOffset = new(0f, 1.6f, 0f);
		[SerializeField] private Color barColor = new(0.95f, 0.78f, 0.35f, 1f);
		[SerializeField, Range(0f, 1f)] private float cooldownReductionPerStack = 0.15f;
		[SerializeField, Range(0.01f, 1f)] private float minCooldownRatio = 0.25f;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.NONE;

		private class Summon
		{
			public GameObject body;
			public GameObject bar;
			public HpBarView barView;
			public TimedSummonState time;
			public Vector3 anchor;
			public float fireRemain;
		}

		private readonly List<Summon> summons = new();

		private SkillObject owner;
		private float cooldownRemain;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		public int ActiveCount => summons.Count;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
			cooldownRemain = 0f;
			ClearSummons();
		}

		private void OnDisable() => ClearSummons();

		private void Update()
		{
			Vector3 playerPosition = playerProvider.Current.transform.position;
			transform.position = playerPosition;

			if (summons.Count < maxActive)
				cooldownRemain -= Time.deltaTime * CompanionTargeting.CooldownRate(playerProvider.Current.UnitStat, cooldownReductionPerStack, minCooldownRatio);
			if (SummonRules.CanSummon(summons.Count, maxActive, cooldownRemain))
			{
				Spawn(playerPosition);
				cooldownRemain = summonCooldown;
			}

			for (int i = summons.Count - 1; i >= 0; i--)
			{
				Summon summon = summons[i];
				summon.time.Tick(Time.deltaTime);
				if (summon.time.IsExpired)
				{
					Remove(i);
					continue;
				}
				UpdateSummon(summon, playerPosition);
			}
		}

		private void Spawn(Vector3 playerPosition)
		{
			Summon summon = new()
			{
				body = objectPoolManager.Spawn(dollPrefab),
				time = new TimedSummonState(lifetime),
				anchor = RandomAnchor(),
				fireRemain = fireInterval * 0.5f,
			};
			summon.body.transform.position = CompanionTargeting.Flat(playerPosition + summon.anchor);
			summon.body.SetActive(true);

			if (barPrefab != null)
			{
				summon.bar = Instantiate(barPrefab, summon.body.transform);
				summon.bar.transform.localPosition = barOffset;
				summon.barView = summon.bar.GetComponent<HpBarView>();
				foreach (SpriteRenderer fill in summon.bar.GetComponentsInChildren<SpriteRenderer>(true))
				{
					if (fill.name == "Sprite_HPBar")
						fill.color = barColor;
				}
				summon.barView.SetRatio(1f);
			}
			summons.Add(summon);
		}

		private Vector3 RandomAnchor()
		{
			Vector2 circle = Random.insideUnitCircle.normalized * followRadius;
			return new Vector3(circle.x, 0f, circle.y);
		}

		private void UpdateSummon(Summon summon, Vector3 playerPosition)
		{
			Vector3 goal = CompanionTargeting.Flat(playerPosition + summon.anchor);
			summon.body.transform.position = Vector3.MoveTowards(summon.body.transform.position, goal, followSpeed * Time.deltaTime);
			summon.barView?.SetRatio(summon.time.Ratio);

			summon.fireRemain -= Time.deltaTime;
			if (summon.fireRemain > 0f)
				return;

			Vector3 origin = summon.body.transform.position;
			GameObject target = CompanionTargeting.NearestMonster(origin, range);
			if (target == null)
				return;
			summon.fireRemain = fireInterval;
			Fire(origin, target.transform.position);
		}

		private void Fire(Vector3 origin, Vector3 targetPosition)
		{
			GameObject bullet = objectPoolManager.Spawn(bulletPrefab);
			bullet.transform.position = CompanionTargeting.Flat(origin);
			if (bullet.TryGetComponent(out SkillObject skillObject))
				skillObject.InitAttack(owner.Context);

			Vector3 direction = CompanionTargeting.Flat(targetPosition - origin).normalized;
			if (bullet.TryGetComponent(out BulletMovement movement))
				movement.SetMoveDirection(direction);

			int damageBonus = playerProvider.Current.UnitStat[damageBonusStat];
			if (bullet.TryGetComponent(out ExplodeOnEnd explodeOnEnd))
				explodeOnEnd.SetDamageBonus(damageBonus);
			else if (bullet.TryGetComponent(out DamagingObject damagingObject))
				damagingObject.SetDamageBonus(damageBonus);
			bullet.SetActive(true);
		}

		private void Remove(int index)
		{
			Release(summons[index]);
			summons.RemoveAt(index);
		}

		private void ClearSummons()
		{
			foreach (Summon summon in summons)
				Release(summon);
			summons.Clear();
		}

		private void Release(Summon summon)
		{
			if (summon.bar != null)
				Destroy(summon.bar);
			if (summon.body != null && ObjectPoolManager.TryGetExistingInstance(out _))
				objectPoolManager.Despawn(summon.body);
		}
	}
}
