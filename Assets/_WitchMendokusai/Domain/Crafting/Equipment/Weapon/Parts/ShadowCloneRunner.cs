using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 알리사 그림자 분신. 생성기 (Spawner) 가 던질 때마다 분신 자리에서 같은 탄을 낮은 피해로 냄. 수, 자리, 수명 판정은 ShadowCloneRules
	// 분신 몸은 bodyPrefab (주인공 그림을 어둡게 따라 그림). 표창 폭풍은 켜진 분신을 Clones 로 읽음
	public class ShadowCloneRunner : SkillComponent
	{
		[Serializable]
		private class Source
		{
			[field: SerializeField] public Spawner Spawner { get; private set; }
			[field: SerializeField, Tooltip("그 생성기가 내는 탄과 같은 것")] public GameObject Prefab { get; private set; }
			[field: SerializeField] public UnitStatType DamageBonusStat { get; private set; } = UnitStatType.NONE;
		}

		private class Clone
		{
			public ShadowCloneBody Body;
			public readonly ShadowCloneCycle Cycle = new();
		}

		[SerializeField] private List<Source> sources = new();
		[SerializeField] private ShadowCloneBody bodyPrefab;
		[SerializeField] private GameObject burstPrefab;
		[SerializeField, Min(0f)] private float distance = 1.8f;
		[SerializeField, Min(0f)] private float spreadDegrees = 120f;
		[SerializeField, Min(0.1f)] private float followRate = 10f;
		[SerializeField, Min(0)] private int maxCount = 3;
		[SerializeField, Range(0, 200)] private int sharePercent = 40;
		[SerializeField, Range(0, 200)] private int strongSharePercent = 65;
		[SerializeField, Min(0.1f)] private float lifeSeconds = 10f;
		[SerializeField, Min(0.1f)] private float reformSeconds = 2f;

		[Header("Stat")]
		[SerializeField] private UnitStatType rootStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType countStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType strongStat = UnitStatType.NONE;
		[SerializeField] private UnitStatType burstStat = UnitStatType.NONE;

		private readonly List<Clone> clones = new();
		private readonly List<Action<GameObject, Vector3>> handlers = new();
		private readonly List<Vector3> positions = new();
		private SkillContext context;

		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public int SharePercent => ShadowCloneRules.SharePercent(PlayerStat[strongStat] > 0, sharePercent, strongSharePercent);

		// 지금 보이는 분신의 자리
		public IReadOnlyList<Vector3> Positions
		{
			get
			{
				positions.Clear();
				foreach (Clone clone in clones)
				{
					if (clone.Cycle.IsPresent)
						positions.Add(clone.Body.transform.position);
				}
				return positions;
			}
		}

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			handlers.Clear();
			foreach (Source source in sources)
			{
				Source captured = source;
				void Handle(GameObject shot, Vector3 direction) => Mirror(captured, direction);
				handlers.Add(Handle);
				source.Spawner.Shot += Handle;
			}
		}

		private void OnDisable()
		{
			for (int i = 0; i < handlers.Count; i++)
				sources[i].Spawner.Shot -= handlers[i];
			handlers.Clear();
			foreach (Clone clone in clones)
			{
				if (clone.Body != null)
					Destroy(clone.Body.gameObject);
			}
			clones.Clear();
		}

		private void Update()
		{
			MatchCount(ShadowCloneRules.Count(PlayerStat[rootStat], PlayerStat[countStat], maxCount));
			bool bursts = PlayerStat[burstStat] > 0;
			for (int i = 0; i < clones.Count; i++)
			{
				Clone clone = clones[i];
				ShadowCloneEvent cloneEvent = clone.Cycle.Tick(Time.deltaTime, bursts, lifeSeconds, reformSeconds);
				if (cloneEvent == ShadowCloneEvent.Vanished)
				{
					clone.Body.SetShown(false);
					SkillSpawn.Spawn(objectPoolManager, burstPrefab, clone.Body.transform.position, Quaternion.identity, context, 0).SetActive(true);
				}
				else if (cloneEvent == ShadowCloneEvent.Formed)
				{
					clone.Body.transform.position = SlotPosition(i);
					clone.Body.SetShown(true);
				}
				clone.Body.transform.position = Vector3.Lerp(clone.Body.transform.position, SlotPosition(i), 1f - Mathf.Exp(-followRate * Time.deltaTime));
			}
		}

		private void MatchCount(int count)
		{
			if (clones.Count == count)
				return;
			while (clones.Count > count)
			{
				Destroy(clones[^1].Body.gameObject);
				clones.RemoveAt(clones.Count - 1);
			}
			while (clones.Count < count)
			{
				Clone clone = new() { Body = Instantiate(bodyPrefab, playerProvider.Current.transform.position, Quaternion.identity) };
				clone.Body.Init(playerProvider.CurrentObject);
				clones.Add(clone);
			}
			for (int i = 0; i < clones.Count; i++)
			{
				clones[i].Cycle.Begin(ShadowCloneRules.Stagger(i, clones.Count, lifeSeconds));
				clones[i].Body.SetShown(true);
			}
		}

		private Vector3 SlotPosition(int index)
		{
			Vector3 aim = playerProvider.Current.AimDirection;
			aim.y = 0f;
			if (aim.sqrMagnitude < 0.0001f)
				aim = playerProvider.Current.transform.forward;
			Vector3 offset = Quaternion.Euler(0f, ShadowCloneRules.SlotAngle(index, clones.Count, spreadDegrees), 0f) * aim.normalized * distance;
			return playerProvider.Current.transform.position + offset;
		}

		private void Mirror(Source source, Vector3 direction)
		{
			if (direction == Vector3.zero)
				return;
			int bonus = source.DamageBonusStat == UnitStatType.NONE ? 0 : PlayerStat[source.DamageBonusStat];
			foreach (Vector3 position in Positions)
			{
				GameObject spawned = SkillSpawn.Spawn(objectPoolManager, source.Prefab, position, Quaternion.LookRotation(direction, Vector3.up), context, bonus);
				if (spawned.TryGetComponent(out ScaledDamage scaledDamage))
					scaledDamage.SetPercent(SharePercent);
				spawned.SetActive(true);
			}
		}
	}
}
