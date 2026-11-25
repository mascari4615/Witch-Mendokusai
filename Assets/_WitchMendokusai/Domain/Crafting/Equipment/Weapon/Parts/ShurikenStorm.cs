using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 표창 폭풍. 주인공과 분신 둘레를 도는 표창. 수, 각도, 흩어짐 판정은 ShurikenStormRules
	// 피해는 표창마다 RepeatingDamagingObject. 빠른 회전 카드는 재타격이 짧은 프리팹으로 바꿈
	public class ShurikenStorm : SkillComponent
	{
		private class Ring
		{
			public readonly List<GameObject> Blades = new();
		}

		[SerializeField] private GameObject bladePrefab;
		[SerializeField, Tooltip("빠른 회전 카드의 표창 (재타격이 짧음)")] private GameObject fastBladePrefab;
		[SerializeField] private ShadowCloneRunner cloneRunner;
		[SerializeField, Min(0)] private int baseCount = 4;
		[SerializeField, Min(1)] private int maxCount = 12;
		[SerializeField, Min(0), Tooltip("분신 하나가 도는 표창 수의 상한")] private int maxCountPerClone = 4;
		[SerializeField, Min(0f)] private float radius = 2.5f;
		[SerializeField, Min(0f)] private float wideRadius = 3.5f;
		[SerializeField, Min(0f)] private float cloneRadius = 1.5f;
		[SerializeField] private float degreesPerSecond = 180f;
		[SerializeField, Min(1f)] private float fastMultiplier = 1.5f;
		[SerializeField, Min(0f)] private float height = 0.5f;

		[Header("Scatter")]
		[SerializeField, Min(0.1f)] private float scatterIntervalSeconds = 6f;
		[SerializeField, Min(0f)] private float scatterDistance = 8f;
		[SerializeField, Min(0f)] private float scatterOutSeconds = 0.3f;
		[SerializeField, Min(0f)] private float scatterReturnSpeed = 18f;

		[Header("Stat")]
		[SerializeField] private UnitStatType countStat = UnitStatType.STORM_COUNT;
		[SerializeField] private UnitStatType wideStat = UnitStatType.STORM_WIDE;
		[SerializeField] private UnitStatType fastStat = UnitStatType.STORM_FAST;
		[SerializeField] private UnitStatType scatterStat = UnitStatType.STORM_SCATTER;
		[SerializeField] private UnitStatType cloneStat = UnitStatType.STORM_CLONE;
		[SerializeField] private UnitStatType damageBonusStat = UnitStatType.SIGNATURE_RANGED_DAMAGE_BONUS;

		private readonly List<Ring> rings = new();
		private SkillContext context;
		private bool fastBlades;
		private float spin;
		private float scatterElapsed;
		private float nextScatterTime;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;
		private float Radius => PlayerStat[wideStat] > 0 ? wideRadius : radius;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			spin = 0f;
			scatterElapsed = float.MaxValue;
			nextScatterTime = Time.time + scatterIntervalSeconds;
		}

		private void OnDisable() => Clear();

		private void Update()
		{
			bool fast = PlayerStat[fastStat] > 0;
			if (fast != fastBlades)
			{
				Clear();
				fastBlades = fast;
			}
			spin += degreesPerSecond * (fast ? fastMultiplier : 1f) * Time.deltaTime;
			scatterElapsed += Time.deltaTime;
			if (PlayerStat[scatterStat] > 0 && Time.time >= nextScatterTime)
			{
				nextScatterTime = Time.time + scatterIntervalSeconds;
				scatterElapsed = 0f;
			}

			int count = ShurikenStormRules.Count(baseCount, PlayerStat[countStat], 0, maxCount);
			float ringRadius = ShurikenStormRules.ScatterRadius(scatterElapsed, Radius, scatterDistance, scatterOutSeconds, scatterReturnSpeed);
			UpdateRing(0, playerProvider.Current.transform.position, count, ringRadius, 100);
			IReadOnlyList<Vector3> clonePositions = PlayerStat[cloneStat] > 0 ? cloneRunner.Positions : System.Array.Empty<Vector3>();
			for (int i = 0; i < clonePositions.Count; i++)
				UpdateRing(i + 1, clonePositions[i], ShurikenStormRules.CloneCount(count, maxCountPerClone), cloneRadius, cloneRunner.SharePercent);
			for (int i = clonePositions.Count + 1; i < rings.Count; i++)
				Match(rings[i], 0);
		}

		private void UpdateRing(int index, Vector3 center, int count, float ringRadius, int percent)
		{
			while (rings.Count <= index)
				rings.Add(new Ring());
			Ring ring = rings[index];
			Match(ring, count);
			for (int i = 0; i < ring.Blades.Count; i++)
			{
				GameObject blade = ring.Blades[i];
				Vector3 outward = Quaternion.Euler(0f, ShurikenStormRules.Angle(i, count, spin), 0f) * Vector3.forward;
				blade.transform.SetPositionAndRotation(center + outward * ringRadius + Vector3.up * height, Quaternion.LookRotation(outward, Vector3.up));
				if (blade.TryGetComponent(out DamagingObject damagingObject))
					damagingObject.SetDamageBonus(PlayerStat[damageBonusStat]);
				if (blade.TryGetComponent(out ScaledDamage scaledDamage))
					scaledDamage.SetPercent(percent);
			}
		}

		private void Match(Ring ring, int count)
		{
			while (ring.Blades.Count > count)
			{
				ring.Blades[^1].SetActive(false);
				ring.Blades.RemoveAt(ring.Blades.Count - 1);
			}
			while (ring.Blades.Count < count)
			{
				GameObject blade = SkillSpawn.Spawn(objectPoolManager, fastBlades ? fastBladePrefab : bladePrefab, playerProvider.Current.transform.position, Quaternion.identity, context, 0);
				blade.SetActive(true);
				ring.Blades.Add(blade);
			}
		}

		private void Clear()
		{
			foreach (Ring ring in rings)
			{
				foreach (GameObject blade in ring.Blades)
				{
					if (blade != null)
						blade.SetActive(false);
				}
				ring.Blades.Clear();
			}
		}
	}
}
