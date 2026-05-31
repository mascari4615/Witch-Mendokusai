using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 매치 오케스트레이터 — 맵 생성 → 유닛 스폰(기존 풀, 자동 DI) → ArenaCombatant/TacticDriver 부착
	/// → TargetingSystem 등록 → TimeManager 틱으로 ArenaMatchCore 폴 → 종료 시 MatchEnded.
	/// 스코프 불요: ObjectPoolManager/TimeManager static Instance 사용(World 부팅 후 가용).
	/// ⚠ init-order: 스폰 활성화 후 1프레임 양보(UnitObject.Start 의 Init 정착) 뒤 드라이버 부착 —
	///   Start 재-Init 이 SkillHandler 재생성하므로 그 후 AutoCastEnabled=false 가 안정적으로 적용(트랩#1).
	/// ⚠ 라이브 거동(스폰/전투/종료)은 PlayMode 검증 필요(콘텐츠 로스터 + 관전). 코드코어 = ArenaMatchCore(검증됨).
	/// </summary>
	public class ArenaMatch : MonoBehaviour
	{
		[field: Header("_" + nameof(ArenaMatch))]
		[SerializeField] private ArenaMatchConfig config;
		[SerializeField] private Transform arenaRoot;

		private TargetingSystem targeting;
		private ArenaMatchCore core;
		private readonly List<TacticDriver> drivers = new();
		private bool ticking;

		public event System.Action<int> MatchEnded = delegate { };

		public bool IsConcluded => core != null && core.IsConcluded;
		public int WinnerTeamId => core != null ? core.WinnerTeamId : ArenaModeSO.NO_WINNER;

		public void Begin()
		{
			if (config == null || arenaRoot == null)
			{
				Debug.LogError($"{nameof(ArenaMatch)}: config/arenaRoot 미할당 — 시작 불가.");
				return;
			}
			StartCoroutine(BeginRoutine());
		}

		private IEnumerator BeginRoutine()
		{
			config.Map.Build(arenaRoot);
			targeting = new TargetingSystem();

			ObjectPoolManager pool = ObjectPoolManager.Instance;
			if (pool == null)
			{
				Debug.LogError($"{nameof(ArenaMatch)}: ObjectPoolManager.Instance null — World 부팅 후 호출 필요.");
				yield break;
			}

			// 1) 스폰 + 위치 + Init + 활성화 (MonsterSpawner 패턴).
			List<UnitObject> spawnedUnits = new();
			List<ArenaMatchConfig.ArenaUnitEntry> spawnedEntries = new();
			Dictionary<int, int> teamSpawnIndex = new();

			foreach (ArenaMatchConfig.ArenaUnitEntry entry in config.Roster)
			{
				if (entry.UnitData == null || entry.UnitData.Prefab == null)
					continue;

				GameObject unitGameObject = pool.Spawn(entry.UnitData.Prefab);
				UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
				if (unitObject == null)
					continue;

				int memberIndex = teamSpawnIndex.TryGetValue(entry.TeamId, out int existing) ? existing : 0;
				teamSpawnIndex[entry.TeamId] = memberIndex + 1;

				IReadOnlyList<Vector3> teamSpawns = config.Map.GetSpawns(entry.TeamId);
				Vector3 localSpawn = teamSpawns.Count > 0 ? teamSpawns[memberIndex % teamSpawns.Count] : Vector3.zero;
				unitGameObject.transform.position = arenaRoot.TransformPoint(localSpawn);

				unitObject.Init(entry.UnitData);
				unitGameObject.SetActive(true);

				spawnedUnits.Add(unitObject);
				spawnedEntries.Add(entry);
			}

			// 2) UnitObject.Start(자동 Init) 정착 대기 — 드라이버를 안정된 SkillHandler 에 부착.
			yield return null;

			// 3) 전투 래핑 + 전술 드라이버 + 타겟팅 등록 + 팀 구성.
			Dictionary<int, List<ICombatant>> teamMembers = new();
			int combatantId = 0;

			for (int i = 0; i < spawnedUnits.Count; i++)
			{
				UnitObject unitObject = spawnedUnits[i];
				ArenaMatchConfig.ArenaUnitEntry entry = spawnedEntries[i];
				if (unitObject == null)
					continue;

				ArenaCombatant combatant = unitObject.GetComponent<ArenaCombatant>();
				if (combatant == null)
					combatant = unitObject.gameObject.AddComponent<ArenaCombatant>();
				combatant.SetTeam(entry.TeamId, combatantId);
				combatantId++;
				targeting.Register(combatant);

				TacticDriver driver = unitObject.GetComponent<TacticDriver>();
				if (driver == null)
					driver = unitObject.gameObject.AddComponent<TacticDriver>();
				driver.Initialize(entry.Tactic, targeting, TimeManager.Instance);
				drivers.Add(driver);

				if (teamMembers.ContainsKey(entry.TeamId) == false)
					teamMembers[entry.TeamId] = new List<ICombatant>();
				teamMembers[entry.TeamId].Add(combatant);
			}

			List<ArenaTeam> teams = new();
			foreach (KeyValuePair<int, List<ICombatant>> pair in teamMembers)
				teams.Add(new ArenaTeam(pair.Key, pair.Value));

			core = new ArenaMatchCore(teams, config.Mode);

			if (TimeManager.Instance != null)
				TimeManager.Instance.RegisterCallback(Tick);
			ticking = true;
		}

		private void Tick()
		{
			if (ticking == false || core == null)
				return;

			if (core.Poll())
			{
				ticking = false;
				if (TimeManager.Instance != null)
					TimeManager.Instance.RemoveCallback(Tick);
				MatchEnded(core.WinnerTeamId);
			}
		}

		private void OnDestroy()
		{
			if (ticking && TimeManager.TryGetExistingInstance(out TimeManager timeManager))
				timeManager.RemoveCallback(Tick);
		}
	}
}
