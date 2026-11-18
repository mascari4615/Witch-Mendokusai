using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 웨이브 패턴 일정표 실행과 던전 선택 규칙 (최종 보스 클리어, 보스 등장 잡몹 정리)
	public partial class MonsterSpawner : IWavePatternHost
	{
		private class ActivePattern
		{
			public WavePatternRun Run { get; }
			public float StartTime { get; }

			public ActivePattern(WavePatternRun run, float startTime)
			{
				Run = run;
				StartTime = startTime;
			}
		}

		private readonly List<WavePatternSlot> pendingPatternSlots = new();
		private readonly List<ActivePattern> activePatterns = new();
		private readonly List<float> activeRates = new();
		private DungeonWaveOptions waveOptions;
		private Monster finalBoss;
		private bool finalBossSpawned;
		private bool patternsRunning;

		public float PeriodicRateMultiplier { get; private set; } = 1f;
		public float FinalBossRateMultiplier => finalBossSpawned ? waveOptions.AfterFinalBossPeriodicRate : 1f;

		public Vector3 PlayerPosition => playerProvider.Current.transform.position;
		public Transform PlayerTransform => playerProvider.Current.transform;

		private float ElapsedSeconds => (float)dungeonManager.Context.ElapsedTime.TotalSeconds;

		private void OnEnable()
		{
			EventBusBridge.Subscribe<BossSpawnedEvent>(OnBossSpawned);
			EventBusBridge.Subscribe<BossKilledEvent>(OnBossKilled);
		}

		private void OnDisable()
		{
			EventBusBridge.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
			EventBusBridge.Unsubscribe<BossKilledEvent>(OnBossKilled);
		}

		private void InitPatterns(Dungeon curDungeon)
		{
			ResetPatterns();
			pendingPatternSlots.AddRange(curDungeon.WavePatterns);
			pendingPatternSlots.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
			waveOptions = curDungeon.WaveOptions;
			finalBoss = waveOptions.ClearOnFinalBossKill ? waveOptions.ResolveFinalBoss(curDungeon.MonsterWaves) : null;
			patternsRunning = true;
		}

		private void ResetPatterns()
		{
			pendingPatternSlots.Clear();
			activePatterns.Clear();
			waveOptions = null;
			finalBoss = null;
			finalBossSpawned = false;
			patternsRunning = false;
			PeriodicRateMultiplier = 1f;
		}

		// 개발 확인용. 진행 중인 판에 패턴 하나를 지금 시작
		public void TriggerPattern(WavePattern pattern)
		{
			if (patternsRunning == false)
				throw new InvalidOperationException("던전 판이 진행 중일 때만 패턴 발동 가능");
			pattern.Validate();
			activePatterns.Add(new ActivePattern(pattern.CreateRun(), ElapsedSeconds));
		}

		private void UpdatePatterns()
		{
			float elapsed = ElapsedSeconds;
			while (pendingPatternSlots.Count > 0 && elapsed >= pendingPatternSlots[0].StartTime)
			{
				WavePatternSlot slot = pendingPatternSlots[0];
				pendingPatternSlots.RemoveAt(0);
				activePatterns.Add(new ActivePattern(slot.Pattern.CreateRun(), slot.StartTime));
			}

			// 배율을 먼저 정해 같은 틱의 짝 패턴과 주기 웨이브에 적용
			activeRates.Clear();
			foreach (ActivePattern active in activePatterns)
				activeRates.Add(active.Run.PeriodicRateMultiplier(elapsed - active.StartTime));
			activeRates.Add(FinalBossRateMultiplier);
			PeriodicRateMultiplier = WavePatternRules.CombineRates(activeRates);

			float deltaSeconds = (float)DungeonContext.TimeUpdateInterval.TotalSeconds;
			for (int i = activePatterns.Count - 1; i >= 0; i--)
			{
				ActivePattern active = activePatterns[i];
				if (active.Run.Tick(this, elapsed - active.StartTime, deltaSeconds))
					activePatterns.RemoveAt(i);
			}
		}

		public float ScaleSpawnRate(float perSecond) => dungeonManager.CurThreat.ScaleSpawnRate(perSecond);

		public int ScaleSwarmCount(int count) => dungeonManager.CurThreat.ScaleSwarmCount(count);

		public int AllowedCount(SpawnClass spawnClass, int requested)
		{
			SurvivalRunRules rules = dungeonManager.RunRules;
			return WaveDensityRules.AllowedCount(spawnClass, CurrentMonsterCount, requested, rules.EnemySoftCap, rules.EnemyHardCap);
		}

		public void SpawnAroundPlayer(Monster monster) => StartCoroutine(SpawnMonster(monster, spawnDelay, dungeonManager.Context.CurDifficulty));

		public void SpawnAt(Monster monster, Vector3 position, WaveMoveOrder moveOrder) => StartCoroutine(SpawnMonsterAt(monster, position, null, false, moveOrder));

		private void OnBossSpawned(BossSpawnedEvent spawned)
		{
			if (patternsRunning == false)
				return;
			if (finalBoss != null && spawned.Monster == finalBoss)
				finalBossSpawned = true;
			if (waveOptions.ClearMobsOnBossSpawn)
				StartCoroutine(ClearMobsAfter(waveOptions.ClearMobsDelay));
		}

		private void OnBossKilled(BossKilledEvent killed)
		{
			if (patternsRunning == false || finalBoss == null || killed.Monster != finalBoss)
				return;
			dungeonManager.CompleteObjective();
		}

		// 잡몹만 드롭 없이 정리. 보스와 정예는 남김
		private IEnumerator ClearMobsAfter(float delaySeconds)
		{
			yield return new WaitForSeconds(delaySeconds);
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			for (int i = monsters.Count - 1; i >= 0; i--)
			{
				GameObject monsterObject = monsters[i];
				if (monsterObject == null || monsterObject.TryGetComponent(out MonsterObject monster) == false || monster.UnitData == null)
					continue;
				if (monster.UnitData.Type == MonsterType.Boss || monster.IsElite)
					continue;
				monsterObject.SetActive(false);
			}
		}
	}
}
