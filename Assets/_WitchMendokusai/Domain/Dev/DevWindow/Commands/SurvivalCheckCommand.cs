using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// survival luck , 행운과 바닥 아이템과 카드 희귀도 실제 계수. survival offscreen , 화면 밖 적 단계별 마릿수.
	/// survival bench [seconds] , 그 시간 동안 프레임 시간과 적 수를 재서 출력. 같은 적 수로 설정을 바꿔 가며 전후 비교.
	/// </summary>
	public class SurvivalCheckCommand : IDevCommand
	{
		private const string LUCK_ARG = "luck";
		private const string OFFSCREEN_ARG = "offscreen";
		private const string BENCH_ARG = "bench";
		private const float DEFAULT_BENCH_SECONDS = 5f;
		private const int SAMPLE_LEVEL = 5;

		public string Name => "survival";
		public string Usage => "survival <luck|offscreen|bench> [seconds]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			if (DevCommandArgs.Is(args[0], LUCK_ARG))
				DescribeLuck(context);
			else if (DevCommandArgs.Is(args[0], OFFSCREEN_ARG))
				DescribeOffscreen(context);
			else if (DevCommandArgs.Is(args[0], BENCH_ARG))
				StartBench(context, args);
			else
				context.LogError(Usage);
		}

		private static void DescribeLuck(DevCommandContext context)
		{
			SurvivalRunRules rules = ResourceManager.Instance.SurvivalRunRules;
			float luck = UpgradeData.Luck;
			float floor = rules.FloorItemLuckMultiplier(luck);
			context.LogInfo($"영구 행운 {luck:0.##}");
			context.LogInfo($"처치 바닥 아이템 x{floor:0.###}: 적 정지 {rules.FreezeDropPercent * floor:0.###}%, 화면 청소 {rules.ClearDropPercent * floor:0.###}%, 켜짐 {rules.KillDropFreezeAndClear}");
			context.LogInfo($"카드 희귀도 가중치 레벨 0: 희귀 {rules.RareCardWeight * rules.RarityLuckMultiplier(luck, 0):0.###}, 영웅 {rules.EpicCardWeight * rules.RarityLuckMultiplier(luck, 0):0.###}, 일반 {rules.CommonCardWeight:0.###}");
			context.LogInfo($"카드 희귀도 가중치 레벨 {SAMPLE_LEVEL}: 희귀 {rules.RareCardWeight * rules.RarityLuckMultiplier(luck, SAMPLE_LEVEL):0.###}, 영웅 {rules.EpicCardWeight * rules.RarityLuckMultiplier(luck, SAMPLE_LEVEL):0.###}");
		}

		private static List<MonsterObject> ActiveMonsters()
		{
			List<MonsterObject> monsters = new();
			foreach (GameObject monsterObject in ObjectBufferManager.GetObjects(ObjectType.Monster))
			{
				if (monsterObject == null || monsterObject.activeInHierarchy == false)
					continue;
				MonsterObject monster = monsterObject.GetComponent<MonsterObject>();
				if (monster != null && monster.IsAlive)
					monsters.Add(monster);
			}
			return monsters;
		}

		private static string OffscreenSummary(List<MonsterObject> monsters)
		{
			int fullRate = 0;
			Dictionary<float, int> byInterval = new();
			float oldestOffscreen = 0f;
			foreach (MonsterObject monster in monsters)
			{
				float interval = monster.UnitMovement.SimulationInterval;
				if (interval <= 0f)
					fullRate++;
				else
					byInterval[interval] = byInterval.TryGetValue(interval, out int count) ? count + 1 : 1;
				if (monster.OffscreenSinceTime >= 0f)
					oldestOffscreen = Mathf.Max(oldestOffscreen, Time.time - monster.OffscreenSinceTime);
			}
			string tiers = string.Join(", ", byInterval.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key:0.##}초 {pair.Value}마리"));
			return $"적 {monsters.Count}마리, 매 틱 {fullRate}마리{(tiers.Length > 0 ? ", " + tiers : string.Empty)}, 화면 밖 최장 {oldestOffscreen:0.#}초";
		}

		private static void DescribeOffscreen(DevCommandContext context)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			SurvivalRunRules rules = ResourceManager.Instance.SurvivalRunRules;
			context.LogInfo($"설정: 가까움 {rules.OffscreenNearInterval}초, {rules.OffscreenMidDistance}m 부터 {rules.OffscreenMidInterval}초, {rules.OffscreenFarDistance}m 부터 {rules.OffscreenFarInterval}초, 자동 제거 {rules.OffscreenRetireSeconds}초");
			context.LogInfo(OffscreenSummary(ActiveMonsters()));
		}

		private static void StartBench(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			float seconds = DEFAULT_BENCH_SECONDS;
			if (args.Length > 1 && (DevCommandArgs.TryFloat(args[1], out seconds) == false || seconds <= 0f))
			{
				context.LogError($"초는 0 초과 숫자: '{args[1]}'");
				return;
			}
			context.LogInfo($"{seconds}초 측정 시작");
			DevWindowController.Instance.StartCoroutine(Bench(context, seconds));
		}

		// 실제 시간 기준 프레임 간격. 시간 배율과 무관
		private static IEnumerator Bench(DevCommandContext context, float seconds)
		{
			List<float> frameMilliseconds = new();
			float elapsed = 0f;
			while (elapsed < seconds)
			{
				yield return null;
				frameMilliseconds.Add(Time.unscaledDeltaTime * 1000f);
				elapsed += Time.unscaledDeltaTime;
			}
			frameMilliseconds.Sort();
			float average = frameMilliseconds.Average();
			float p95 = frameMilliseconds[Mathf.Min(frameMilliseconds.Count - 1, Mathf.FloorToInt(frameMilliseconds.Count * 0.95f))];
			context.LogSuccess($"프레임 평균 {average:0.00}ms, p95 {p95:0.00}ms, {frameMilliseconds.Count}프레임. {OffscreenSummary(ActiveMonsters())}");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return DevCommandArgs.StartsWith(new[] { LUCK_ARG, OFFSCREEN_ARG, BENCH_ARG }, partial[0]);
		}
	}
}
