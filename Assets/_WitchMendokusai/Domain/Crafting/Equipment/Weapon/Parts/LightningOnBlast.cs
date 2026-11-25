using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 폭발 하나에 전기를 한 번. 판정 LightningChainRules. 맞은 적마다 부르면 무리에서 수가 곱해져, 한 프레임 모아 한 번만
	// 피해 배율 부품보다 뒤에 두어 폭발의 마지막 피해를 기준으로 함. 쓰는 곳: 알리사 십자 수리검
	public class LightningOnBlast : SkillComponent, IDamageModifier, IHitEffect
	{
		[Header("Chain (적에서 적으로)")]
		[SerializeField] private UnitStatType chainCountStat = UnitStatType.NONE;
		[SerializeField, Min(0f)] private float chainRange = 4f;
		[SerializeField, Range(0, 200)] private int chainPercent = 50;

		[Header("Burst (폭발에서 여럿에게)")]
		[SerializeField] private UnitStatType burstStat = UnitStatType.NONE;
		[SerializeField, Min(0)] private int burstCount = 5;
		[SerializeField, Min(0f)] private float burstRange = 6f;
		[SerializeField, Range(0, 200)] private int burstPercent = 40;

		[SerializeField] private GameObject linkPrefab;
		[SerializeField, Min(0f), Tooltip("폭발 판정 반지름. 크기 배율이 곱해짐")] private float blastRadius = 2f;

		private readonly HashSet<UnitObject> struck = new();
		private readonly List<UnitObject> victims = new();
		private readonly List<LightningTarget> targets = new();
		private readonly List<LightningHop> hops = new();
		private SkillContext context;
		private DamageInfo lastDamage;
		private bool fired;

		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(ObjectPoolManager objectPoolManager) => this.objectPoolManager = objectPoolManager;

		public override void InitContext(SkillObject skillObject)
		{
			context = skillObject.Context;
			struck.Clear();
			fired = false;
		}

		public DamageInfo Modify(DamagingObject source, UnitObject victim, DamageInfo damageInfo)
		{
			lastDamage = damageInfo;
			return damageInfo;
		}

		public void OnHit(DamagingObject source, UnitObject victim) => struck.Add(victim);

		// 같은 물리 걸음의 적중이 다 모인 다음 프레임에
		private void Update()
		{
			if (fired || struck.Count == 0)
				return;
			fired = true;
			int chainCount = chainCountStat == UnitStatType.NONE ? 0 : SkillSpawn.Stacks(context, chainCountStat);
			if (chainCount > 0)
			{
				CollectTargets();
				LightningChainRules.Chain(transform.position.x, transform.position.z, blastRadius * transform.lossyScale.x, targets, chainCount, chainRange, hops);
				Strike(chainPercent);
			}
			if (burstStat != UnitStatType.NONE && SkillSpawn.Stacks(context, burstStat) > 0)
			{
				CollectTargets();
				LightningChainRules.Burst(transform.position.x, transform.position.z, targets, burstCount, burstRange, hops);
				Strike(burstPercent);
			}
		}

		private void CollectTargets()
		{
			victims.Clear();
			targets.Clear();
			foreach (GameObject monster in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (monster.TryGetComponent(out UnitObject unit) == false || unit.IsAlive == false)
					continue;
				Vector3 position = unit.transform.position;
				victims.Add(unit);
				targets.Add(new LightningTarget(position.x, position.z, struck.Contains(unit)));
			}
		}

		private void Strike(int percent)
		{
			if (hops.Count > 0)
				CombatSfx.Play(CombatSfxEvent.WeaponZap, transform.position);
			foreach (LightningHop hop in hops)
			{
				UnitObject victim = victims[hop.To];
				DrawLink(hop.From == LightningChainRules.BLAST ? transform.position : victims[hop.From].transform.position, victim.transform.position);
				DamageInfo damageInfo = lastDamage;
				damageInfo.damage = LightningChainRules.Damage(lastDamage.damage, percent);
				damageInfo.type = DamageType.Normal;
				damageInfo.knockbackForce = 0f;
				damageInfo.hitstopDuration = 0f;
				victim.ReceiveDamage(damageInfo);
			}
		}

		private void DrawLink(Vector3 from, Vector3 to)
		{
			if (linkPrefab == null)
				return;
			GameObject spawned = objectPoolManager.Spawn(linkPrefab);
			if (spawned.TryGetComponent(out ChainLink link))
				link.SetPoints(from, to);
			spawned.SetActive(true);
		}
	}
}
