using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public class MonsterSpawner : MonoBehaviour
	{
		public class MonsterWaveInstance
		{
			public MonsterWave Data { get; set; }
			public float SpawnT { get; set; }

			public MonsterWaveInstance(MonsterWave wave, float spawnT)
			{
				Data = wave;
				SpawnT = spawnT;
			}
		}

		[field: Header("_" + nameof(MonsterSpawner))]
		[SerializeField] private GameObject spawnCirclePrefab;
		private readonly List<MonsterWaveInstance> waves = new();
		[SerializeField] private float spawnDelay = .2f;
		[SerializeField] private Vector2 spawnDistanceRange = new(10f, 15f);
		[SerializeField] private float despawnDistance = 50f;
		[SerializeField, Min(0f)] private float visibilityPadding = 2f;
		[SerializeField, Min(0f)] private float offscreenMovementInterval = 0.1f;
		private readonly Plane[] visibilityPlanes = new Plane[6];
		private SurvivalEventTable eventTable;
		private readonly List<SurvivalEventEntry> runEvents = new();
		private int nextAnnounceIndex;
		private int nextEventIndex;
		// 생성 원 표시 중이라 아직 버퍼에 없는 적. 상한과 최소 적 수 판정에 합산
		private int pendingSpawns;

		private DungeonManager dungeonManager;
		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
		}

		// dungeonManager 는 owner DungeonManager 가 직접 주입 (DungeonManager.Construct).
		// 과거 [Inject] Construct(DungeonManager,...) 는 DungeonManager ⇄ MonsterSpawner 순환
		// → SceneLifetimeScope Build 중 Lazy 재진입. 비-container 경로로 순환 엣지 제거
		// (fix#1 SettingView 미러 원리, TASK-WM-078, 2026-05-16).
		public void SetDungeonManager(DungeonManager dungeonManager) => this.dungeonManager = dungeonManager;

		public void InitWaves(Dungeon curDungeon)
		{
			// Debug.Log(nameof(InitWaves));
			waves.Clear();
			pendingSpawns = 0;

			foreach (MonsterWave monsterWave in curDungeon.MonsterWaves)
				waves.Add(new MonsterWaveInstance(monsterWave, 0f));

			eventTable = curDungeon.EventTable;
			BuildRunEvents(dungeonManager.CurThreat);
			nextAnnounceIndex = 0;
			nextEventIndex = 0;
		}

		// 위협 단계를 입힌 이번 판 사건표. 단계 0은 원본 그대로
		private void BuildRunEvents(DungeonThreatEffect threat)
		{
			runEvents.Clear();
			if (eventTable == null)
				return;

			List<SurvivalEventEntry> elites = new();
			List<float> eliteTimes = new();
			foreach (SurvivalEventEntry entry in eventTable.Events)
			{
				if (entry.Type == SurvivalEventType.Swarm)
				{
					runEvents.Add(entry.CopyWith(entry.Time, threat.ScaleSwarmCount(entry.Count)));
					continue;
				}
				runEvents.Add(entry);
				elites.Add(entry);
				eliteTimes.Add(entry.Time);
			}

			foreach (ExtraEliteSlot slot in DungeonThreatRules.ExtraEliteSlots(eliteTimes, threat.EliteExtraPerGap))
				runEvents.Add(elites[slot.SourceIndex].CopyWith(slot.Time, 1));

			// 안정 정렬, 원본 순서 유지
			List<SurvivalEventEntry> sorted = runEvents.OrderBy(entry => entry.Time).ToList();
			runEvents.Clear();
			runEvents.AddRange(sorted);
		}

		public void UpdateWaves()
		{
			RetireDistantMonsters();
			for (int i = waves.Count - 1; i >= 0; i--)
				UpdateWave(i);
			UpdateEvents();
		}

		private int CurrentMonsterCount => ObjectBufferManager.GetObjects(ObjectType.Monster).Count + pendingSpawns;

		public SurvivalEventEntry NextEvent => nextEventIndex < runEvents.Count ? runEvents[nextEventIndex] : null;

		private void UpdateEvents()
		{
			if (eventTable == null)
				return;
			float elapsed = (float)dungeonManager.Context.ElapsedTime.TotalSeconds;
			List<SurvivalEventEntry> entries = runEvents;
			while (nextAnnounceIndex < entries.Count && elapsed >= entries[nextAnnounceIndex].Time - entries[nextAnnounceIndex].AnnounceLead)
			{
				SurvivalEventEntry entry = entries[nextAnnounceIndex++];
				EventBusBridge.Publish(new SurvivalEventAnnouncedEvent { Type = entry.Type, StartTime = entry.Time, LeadSeconds = entry.AnnounceLead, Monster = entry.Monster, Count = entry.Count });
			}
			while (nextEventIndex < entries.Count && elapsed >= entries[nextEventIndex].Time)
				StartEvent(entries[nextEventIndex++]);
		}

		private void StartEvent(SurvivalEventEntry entry)
		{
			Vector3 center = playerProvider.Current.transform.position;
			EliteProfile profile = entry.Type == SurvivalEventType.Elite ? eventTable.Elite : null;
			SurvivalRunRules rules = dungeonManager.RunRules;
			SpawnClass spawnClass = entry.Type == SurvivalEventType.Elite ? SpawnClass.Priority : SpawnClass.Event;
			int count = WaveDensityRules.AllowedCount(spawnClass, CurrentMonsterCount, entry.Count, rules.EnemySoftCap, rules.EnemyHardCap);
			for (int i = 0; i < count; i++)
			{
				float angle = (360f / count * i + Random.Range(0f, 30f)) * Mathf.Deg2Rad;
				Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * eventTable.SwarmRadius;
				StartCoroutine(SpawnMonsterAt(entry.Monster, position, profile));
			}
			if (entry.Type == SurvivalEventType.Swarm && count > 0)
				SurvivalFeel.Instance.OnSwarmStarted(center);
			EventBusBridge.Publish(new SurvivalEventStartedEvent { Type = entry.Type, Monster = entry.Monster, Count = count });
		}

		private IEnumerator SpawnMonsterAt(Monster monster, Vector3 position, EliteProfile eliteProfile)
		{
			pendingSpawns++;
			GameObject spawnCircle = objectPoolManager.Spawn(spawnCirclePrefab);
			spawnCircle.transform.position = position;
			spawnCircle.SetActive(true);
			ObjectBufferManager.AddObject(ObjectType.SpawnCircle, spawnCircle);

			yield return new WaitForSeconds(spawnDelay);
			pendingSpawns = Mathf.Max(0, pendingSpawns - 1);

			GameObject monsterObject = objectPoolManager.Spawn(monster.Prefab);
			MonsterObject monsterObjectComponent = monsterObject.GetComponent<MonsterObject>();
			monsterObject.transform.position = position;
			monsterObjectComponent.SetThreat(dungeonManager.CurThreat);
			monsterObjectComponent.Init(monster);
			monsterObject.SetActive(true);
			if (eliteProfile != null)
				monsterObjectComponent.MakeElite(eliteProfile);
		}

		private void RetireDistantMonsters()
		{
			Camera camera = Camera.main;
			bool useVisibility = camera != null && dungeonManager.CurDungeon.MapType == DungeonMapType.SurvivorsLike;
			if (useVisibility)
				GeometryUtility.CalculateFrustumPlanes(camera, visibilityPlanes);
			Vector3 playerPosition = playerProvider.Current.transform.position;
			float maxDistanceSquared = despawnDistance * despawnDistance;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = monsters.Count - 1; i >= 0; i--)
			{
				GameObject obj = monsters[i];
				if (obj == null || obj.TryGetComponent(out MonsterObject monster) == false || monster.UnitData == null)
					continue;
				if (monster.UnitData.Type == MonsterType.Boss)
				{
					monster.UnitMovement.SetSimulationInterval(0f);
					continue;
				}
				if ((obj.transform.position - playerPosition).sqrMagnitude > maxDistanceSquared)
				{
					obj.SetActive(false);
					continue;
				}

				Bounds bounds = monster.SpriteRenderer.bounds;
				bounds.Expand(visibilityPadding * 2f);
				bool fullRate = useVisibility == false || GeometryUtility.TestPlanesAABB(visibilityPlanes, bounds);
				monster.UnitMovement.SetSimulationInterval(fullRate ? 0f : offscreenMovementInterval);
			}
		}

		private void UpdateWave(int waveIndex)
		{
			// waveInstance 대신 index를 전달받아 쓰는 이유

			// 이 함수 내에서 특정 조건이 충족하면 waves에서 waveInstance를 지워야 하는데,
			// 이때, 요소를 찾기 위해 리스트를 탐색하는 Remove보다 RemoveAt이 더 빠르기 때문.
			// (웨이브 수가 많지는 않을 것 같아서 효과는 미미하겠지만..)

			// 요소가 삭제되면 waves.Count가 줄어들기 때문에 UpdateWaves에서
			// 그냥 waves.Count를 기준으로 i++ for문을 돌리면 예외가 발생한다.
			// 그래서 waves.Count - 1부터 0까지 역순으로 for문을 돈다.

			// 어차피 index만 알고 있으면 현재 단계에서 처리할 waveInstance에 접근할 수 있으므로
			// 굳이 처리할 waveInstance까지는 전달받지 않아도 된다.
			MonsterWaveInstance waveInstance = waves[waveIndex];

			TimeSpan dungeonTime = dungeonManager.Context.ElapsedTime;
			DungeonDifficulty curDifficulty = dungeonManager.Context.CurDifficulty;

			if (dungeonTime < TimeSpan.FromSeconds(waveInstance.Data.StartTime))
				return;

			if (dungeonTime > TimeSpan.FromSeconds(waveInstance.Data.EndTime))
			{
				waves.RemoveAt(waveIndex);
				return;
			}

			waveInstance.SpawnT += DungeonContext.TimeUpdateInterval.Milliseconds / 1000f;
			// 보스 같은 1회 웨이브는 물량 배율 제외
			float spawnInterval = waveInstance.Data.Once ? waveInstance.Data.SpawnDelay : dungeonManager.CurThreat.ScaleSpawnDelay(waveInstance.Data.SpawnDelay);
			if (waveInstance.SpawnT > spawnInterval)
			{
				SpawnWaveTick(waveInstance.Data, curDifficulty);
				waveInstance.SpawnT = 0;

				if (waveInstance.Data.Once)
					waves.RemoveAt(waveIndex);
			}
		}

		// 한 번의 생성 기회. 보스 웨이브는 상한 예외, 그 외는 최소 적 수 보충과 상한 적용
		private void SpawnWaveTick(MonsterWave wave, DungeonDifficulty difficulty)
		{
			SurvivalRunRules rules = dungeonManager.RunRules;
			int current = CurrentMonsterCount;
			Monster first = wave.Monsters[Random.Range(0, wave.Monsters.Length)];
			if (wave.Once || first.Type == MonsterType.Boss)
			{
				StartCoroutine(SpawnMonster(first, spawnDelay, difficulty));
				return;
			}

			int count = WaveDensityRules.PeriodicCount(current, wave.MinOnScreen, rules.RefillBurstMax, rules.EnemySoftCap, rules.EnemyHardCap);
			for (int i = 0; i < count; i++)
			{
				Monster monster = i == 0 ? first : wave.Monsters[Random.Range(0, wave.Monsters.Length)];
				StartCoroutine(SpawnMonster(monster, spawnDelay, difficulty));
			}
		}

		private IEnumerator SpawnMonster(Monster monster, float spawnDelay, DungeonDifficulty difficulty)
		{
			pendingSpawns++;
			// 플레이어가 이동하는 방향 쪽으로 스폰 위치를 랜덤으로 선택 - KarmoDDrine 2025-12-27
			Vector3 randomOffset;
			Vector3 playerPosition = playerProvider.Current.transform.position;
			Vector3 playerForward = playerProvider.CurrentObject.UnitMovement.MoveDirectionLocal;

			// 플레이어가 이동 중이면, 플레이어 앞에 스폰. 도망가지마 맞서싸워.
			bool spawnInFrontOfPlayer = playerForward != Vector3.zero;
			if (spawnInFrontOfPlayer)
			{
				// 플레이어가 이동하는 방향 쪽으로 '일정거리 떨어진' 범위 내에서 스폰 위치를 랜덤으로 선택 - KarmoDDrine 2025-12-27
				float angleOffset = Random.Range(-30f, 30f); // 플레이어 이동 방향 기준 ±30도 이내
				Quaternion rotation = Quaternion.Euler(0, angleOffset, 0);
				Vector3 direction = rotation * playerForward;
				float randomDistance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
				randomOffset = direction.normalized * randomDistance;
			}
			else
			{
				// 플레이어 주변 '일정거리 떨어진' 범위 내에서 스폰 위치를 랜덤으로 선택 - KarmoDDrine 2025-12-27
				Vector2 randomCircle = Random.insideUnitCircle.normalized;
				float randomDistance = Random.Range(spawnDistanceRange.x, spawnDistanceRange.y);
				randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y) * randomDistance;
			}

			Vector3 spawnPos = playerPosition + randomOffset;

			GameObject spawnCircle = objectPoolManager.Spawn(spawnCirclePrefab);
			spawnCircle.transform.position = spawnPos;
			spawnCircle.SetActive(true);
			ObjectBufferManager.AddObject(ObjectType.SpawnCircle, spawnCircle);

			yield return new WaitForSeconds(spawnDelay);
			pendingSpawns = Mathf.Max(0, pendingSpawns - 1);

			GameObject monsterObject = objectPoolManager.Spawn(monster.Prefab);
			MonsterObject monsterObjectComponent = monsterObject.GetComponent<MonsterObject>();
			monsterObject.transform.position = spawnPos;
			monsterObjectComponent.SetThreat(dungeonManager.CurThreat);
			monsterObjectComponent.Init(monster);
			monsterObject.SetActive(true);
		}

		public void StopWave()
		{
			Debug.Log(nameof(StopWave));
			StopAllCoroutines();
			pendingSpawns = 0;
			foreach (GameObject obj in ObjectBufferManager.GetObjects(ObjectType.Monster))
				if (obj != null && obj.TryGetComponent(out MonsterObject monster))
					monster.UnitMovement.SetSimulationInterval(0f);
		}
	}
}
