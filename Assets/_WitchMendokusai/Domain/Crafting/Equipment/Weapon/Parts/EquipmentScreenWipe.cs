using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentScreenWipe : SkillComponent
	{
		[SerializeField, Min(1f)] private float baseCooldown = 60f;
		[SerializeField] private UnitStatType cooldownStat = UnitStatType.NONE;
		[SerializeField, Range(0f, 1f)] private float reductionPerStack = 0.1f;
		[SerializeField, Range(0.05f, 1f)] private float minCooldownRatio = 0.4f;
		[SerializeField, Min(0f)] private float radius = 16f;
		[SerializeField, Min(0)] private int killDamage = 100000;
		[SerializeField, Min(0)] private int bossDamage = 100;
		[SerializeField] private UnitStatType bossDamageStat = UnitStatType.NONE;
		[SerializeField] private bool destroyDrops = true;
		[SerializeField] private GameObject effectPrefab;

		private readonly List<GameObject> targets = new();
		private Coroutine loop;
		private SkillObject owner;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		public override void InitContext(SkillObject skillObject)
		{
			owner = skillObject;
		}

		private void OnEnable()
		{
			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				int stacks = cooldownStat != UnitStatType.NONE ? playerProvider.Current.UnitStat[cooldownStat] : 0;
				yield return new WaitForSeconds(AttackCadence.Interval(baseCooldown, stacks, reductionPerStack, minCooldownRatio));
				Wipe();
				yield return null;
				if (destroyDrops)
					ClearDrops();
			}
		}

		public void Wipe()
		{
			Vector3 origin = playerProvider.Current.transform.position;
			if (effectPrefab != null)
			{
				GameObject effect = objectPoolManager.Spawn(effectPrefab, origin, Quaternion.identity);
				effect.SetActive(true);
			}

			targets.Clear();
			targets.AddRange(ObjectBufferManager.GetObjects(ObjectType.Monster));
			float radiusSquared = radius * radius;
			int bossHit = bossDamage + (bossDamageStat != UnitStatType.NONE ? playerProvider.Current.UnitStat[bossDamageStat] : 0);
			foreach (GameObject target in targets)
			{
				if ((target.transform.position - origin).sqrMagnitude > radiusSquared)
					continue;
				if (target.TryGetComponent(out MonsterObject monster) == false)
					continue;

				bool isBoss = monster.UnitData.Type == MonsterType.Boss;
				monster.ReceiveDamage(new DamageInfo
				{
					type = DamageType.Normal,
					damage = isBoss ? bossHit : killDamage,
					damageSource = owner != null && owner.Context != null ? owner.Context.User : null,
					equipmentDataId = DamageInfo.NO_DATA_ID,
					skillDataId = DamageInfo.NO_DATA_ID,
				});
			}
			targets.Clear();
		}

		private void ClearDrops()
		{
			List<GameObject> drops = ObjectBufferManager.GetObjects(ObjectType.Drop);
			Vector3 origin = playerProvider.Current.transform.position;
			float radiusSquared = radius * radius;
			for (int i = drops.Count - 1; i >= 0; i--)
			{
				if ((drops[i].transform.position - origin).sqrMagnitude <= radiusSquared)
					drops[i].SetActive(false);
			}
		}
	}
}
