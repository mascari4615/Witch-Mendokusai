using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public partial class MonsterSpawner : MonoBehaviour
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
		[SerializeField, Min(0f), Tooltip("보스 웨이브 시작 몇 초 전에 예고 이벤트를 낼지")] private float bossAnnounceLeadSeconds = 5f;
		private readonly Plane[] visibilityPlanes = new Plane[6];
		// 아직 예고하지 않은 보스 웨이브
		private readonly List<MonsterWave> unannouncedBossWaves = new();
		private SurvivalEventTable eventTable;
		private readonly List<SurvivalEventEntry> runEvents = new();
		private int nextAnnounceIndex;
		private int nextEventIndex;
		// 생성 원 표시 중이라 아직 버퍼에 없는 적. 상한과 최소 적 수 판정에 합산
		private int pendingSpawns;

		private DungeonManager dungeonManager;
		private PlayerProvider playerProvider;
		private ObjectPoolManager objectPoolManager;
		private GameLogic gameLogic;

		[Inject]
		public void Construct(PlayerProvider playerProvider, ObjectPoolManager objectPoolManager, GameLogic gameLogic)
		{
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			this.gameLogic = gameLogic;
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

			unannouncedBossWaves.Clear();

			foreach (MonsterWave monsterWave in curDungeon.MonsterWaves)
			{
				waves.Add(new MonsterWaveInstance(monsterWave, 0f));
				if (FindBoss(monsterWave) != null)
					unannouncedBossWaves.Add(monsterWave);
			}

			eventTable = curDungeon.EventTable;
			BuildRunEvents(dungeonManager.CurThreat);
			nextAnnounceIndex = 0;
			nextEventIndex = 0;
			InitPatterns(curDungeon);
			InitThreat(curDungeon);
			InitMapObjects(curDungeon);
		}

		// 위협 단계를 입힌 이번 판 사건표. 단계 0은 원본 그대로
		private void BuildRunEvents(DungeonThreatEffect threat)
		{
			runEvents.Clear();
			if (eventTable == null)
				return;

			runEvents.AddRange(eventTable.BuildRunEvents(threat));
		}

		public void UpdateWaves()
		{
			UpdateThreat();
			RetireDistantMonsters();
			UpdatePatterns();
			for (int i = waves.Count - 1; i >= 0; i--)
				UpdateWave(i);
			UpdateEvents();
			UpdateBossAnnounces();
			UpdateMapObjects();
		}

		private static Monster FindBoss(MonsterWave wave) => Array.Find(wave.Monsters, monster => monster.Type == MonsterType.Boss);

		// 보스는 사건표가 아니라 웨이브로 나옴. 웨이브 시작 전에 예고를 한 번 발행
		private void UpdateBossAnnounces()
		{
			float elapsed = (float)dungeonManager.Context.ElapsedTime.TotalSeconds;
			for (int i = unannouncedBossWaves.Count - 1; i >= 0; i--)
			{
				MonsterWave wave = unannouncedBossWaves[i];
				if (elapsed < wave.StartTime - bossAnnounceLeadSeconds)
					continue;

				unannouncedBossWaves.RemoveAt(i);
				float leadSeconds = wave.StartTime - elapsed;
				// 판 시작과 함께 나오는 보스는 예고할 틈이 없음. 등장 띠만
				if (leadSeconds <= 0f)
					continue;
				EventBusBridge.Publish(new BossAnnouncedEvent { Monster = FindBoss(wave), StartTime = wave.StartTime, LeadSeconds = leadSeconds });
			}
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
				StartCoroutine(SpawnMonsterAt(entry.Monster, position, profile, entry.GuaranteedChest));
			}
			if (entry.Type == SurvivalEventType.Swarm && count > 0)
				SurvivalFeel.Instance.OnSwarmStarted(center);
			EventBusBridge.Publish(new SurvivalEventStartedEvent { Type = entry.Type, Monster = entry.Monster, Count = count });
		}

		private IEnumerator SpawnMonsterAt(Monster monster, Vector3 position, EliteProfile eliteProfile, bool guaranteedChest, WaveMoveOrder moveOrder = null)
		{
			// 경계 밖 자리. 패턴 이동 (벽, 포위) 은 끌어오면 대형이 벽에 뭉쳐서 생략, 나머지는 경계 안쪽으로
			if (IsInsideArena(position, 0f) == false)
			{
				if (moveOrder != null)
					yield break;
				position = ClampToArena(position);
			}
			// 경계 안 자리도 장애물 속이면 가장자리 밖으로
			position = ClearOfObstacles(position);
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
				monsterObjectComponent.MakeElite(eliteProfile, guaranteedChest);
			if (moveOrder != null)
				WaveMoveOverride.Apply(monsterObject, moveOrder);
		}

		// 화면 밖 일반 적은 거리 단계별로 이동 갱신을 낮추고, 오래 화면 밖이면 드롭 없이 반납 또는 앞으로 재배치 (mc36, th4). 보스와 정예는 매 틱, 반납 없음
		private void RetireDistantMonsters()
		{
			bool useVisibility = dungeonManager.CurDungeon.MapType == DungeonMapType.SurvivorsLike && RefreshVisibilityPlanes();
			SurvivalRunRules rules = dungeonManager.RunRules;
			Vector3 playerPosition = playerProvider.Current.transform.position;
			float maxDistanceSquared = despawnDistance * despawnDistance;
			float now = Time.time;
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = monsters.Count - 1; i >= 0; i--)
			{
				GameObject obj = monsters[i];
				if (obj == null || obj.TryGetComponent(out MonsterObject monster) == false || monster.UnitData == null)
					continue;
				if (monster.UnitData.Type == MonsterType.Boss || monster.IsElite)
				{
					monster.UnitMovement.SetSimulationInterval(0f);
					continue;
				}
				float distanceSquared = (obj.transform.position - playerPosition).sqrMagnitude;
				if (distanceSquared > maxDistanceSquared)
				{
					RetireOrRelocate(obj, monster);
					continue;
				}

				bool onScreen = useVisibility == false || IsOnScreen(monster.SpriteRenderer.bounds);
				if (onScreen)
				{
					monster.OffscreenSinceTime = -1f;
					monster.UnitMovement.SetSimulationInterval(0f);
					continue;
				}

				if (monster.OffscreenSinceTime < 0f)
					monster.OffscreenSinceTime = now;
				else if (rules.OffscreenRetireSeconds > 0f && now - monster.OffscreenSinceTime >= rules.OffscreenRetireSeconds)
				{
					RetireOrRelocate(obj, monster);
					continue;
				}
				monster.UnitMovement.SetSimulationInterval(rules.OffscreenUpdateInterval(Mathf.Sqrt(distanceSquared)));
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
			// 숨 고르기 패턴과 최종 보스 뒤 배율. 0 이면 주기 생성 멈춤
			if (waveInstance.Data.Once == false)
			{
				// 멈춘 동안 누적을 버림. 재개 직후 몰아서 생성 방지
				if (PeriodicRateMultiplier <= 0f)
				{
					waveInstance.SpawnT = 0f;
					return;
				}
				spawnInterval /= PeriodicRateMultiplier;
			}
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
			// 자리는 생성 정책 (DungeonThreatPolicy). 기존 기본은 이동 중 앞 ±30도, 멈추면 둘레. 도망가지마 맞서싸워 - KarmoDDrine 2025-12-27
			Vector3 spawnPos = PickPeriodicSpawnPosition();

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
			ResetPatterns();
			StopThreat();
			StopMapObjects();
			foreach (GameObject obj in ObjectBufferManager.GetObjects(ObjectType.Monster))
				if (obj != null && obj.TryGetComponent(out MonsterObject monster))
					monster.UnitMovement.SetSimulationInterval(0f);
		}
	}
}
