using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class EquipmentScreenWipe : SkillComponent, IEquipmentCooldown
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
		// 스킬창 장비 칸의 대기 표시용. 다음 공격 시각과 그 주기 (EquipmentCooldowns)
		private float readyTime;
		private float readyInterval;
		public CooldownState Cooldown => CooldownState.FromReadyTime(readyTime, readyInterval);

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
			readyTime = 0f;
			EquipmentCooldowns.Register(owner.Context.UsedEquipment, this);
			loop = StartCoroutine(Loop());
		}

		private void OnDisable()
		{
			if (loop != null)
				StopCoroutine(loop);
			EquipmentCooldowns.Unregister(owner.Context.UsedEquipment, this);
		}

		private IEnumerator Loop()
		{
			while (true)
			{
				UnitStat playerStat = playerProvider.Current.UnitStat;
				int stacks = cooldownStat != UnitStatType.NONE ? playerStat[cooldownStat] : 0;
				readyInterval = AttackCadence.WeaponInterval(baseCooldown, stacks, playerStat[UnitStatType.ATTACK_SPEED_BONUS],
					reductionPerStack, minCooldownRatio, playerStat[UnitStatType.COOLTIME_BONUS]);
				readyTime = Time.time + readyInterval;
				yield return new WaitForSeconds(readyInterval);
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

			// 몬스터 탄막의 탄도 같은 반경에서 걷음
			VolleyField.ClearWithin(origin, radius);
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
				// 숙련 조각은 판당 수가 고정이라 지우면 막대가 덜 참 (DollMasteryShardSchedule)
				if (drops[i].TryGetComponent(out MasteryShardObject _))
					continue;
				if ((drops[i].transform.position - origin).sqrMagnitude <= radiusSquared)
					drops[i].SetActive(false);
			}
		}
	}
}
