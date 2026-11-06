using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ChainOnHit : SkillComponent, IHitEffect
	{
		[SerializeField] private UnitStatType chainCountStat = UnitStatType.NONE;
		[SerializeField, Min(0)] private int baseChainCount = 0;
		[SerializeField, Min(0f)] private float chainRange = 6f;
		[SerializeField] private GameObject linkPrefab;

		private readonly List<GameObject> visited = new();

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

		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			int stacks = chainCountStat != UnitStatType.NONE ? playerProvider.Current.UnitStat[chainCountStat] : 0;
			int remaining = baseChainCount + stacks;
			if (remaining <= 0)
				return;

			visited.Clear();
			visited.Add(victim.gameObject);
			Vector3 from = victim.transform.position;
			for (int i = 0; i < remaining; i++)
			{
				Transform next = SurvivalTargeting.Nearest(from, chainRange, visited);
				if (next == null)
					return;
				if (next.TryGetComponent(out UnitObject nextUnit) == false)
					return;

				visited.Add(next.gameObject);
				Vector3 to = next.position;
				SpawnLink(from, to);
				source.DealDirectDamage(nextUnit);
				from = to;
			}
		}

		private void SpawnLink(Vector3 from, Vector3 to)
		{
			if (linkPrefab == null)
				return;
			GameObject g = objectPoolManager.Spawn(linkPrefab);
			if (g.TryGetComponent(out ChainLink link))
				link.SetPoints(from, to);
			g.SetActive(true);
		}
	}
}
