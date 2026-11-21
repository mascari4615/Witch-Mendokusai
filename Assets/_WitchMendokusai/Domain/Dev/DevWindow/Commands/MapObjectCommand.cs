using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// mapobj list , Resources/MapObjects 의 맵 오브젝트 자산 목록과 현재 던전 예산 후보.
	/// mapobj budget [seed] , 예산 배치 시뮬레이션. 시드 생략 시 현재 판 시드. 판 밖이면 설정의 기본 예산.
	/// mapobj reveal , 판 안에 놓인 맵 오브젝트를 전부 작은 지도와 화면 밖 화살표에 드러냄.
	/// mapobj &lt;이름&gt; [near] , 판 안 (진열장 포함) 에 바로 배치. near 는 주인공 앞, 생략 시 배치 규칙 (거리, 간격, 지면, 길).
	/// </summary>
	public class MapObjectCommand : IDevCommand
	{
		private const string LIST_ARG = "list";
		private const string BUDGET_ARG = "budget";
		private const string NEAR_ARG = "near";
		private const string REVEAL_ARG = "reveal";

		public string Name => "mapobj";
		public string Usage => "mapobj <list|budget [seed]|reveal|이름 [near]>";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}
			if (DevCommandArgs.Is(args[0], LIST_ARG))
			{
				List(context);
				return;
			}
			if (DevCommandArgs.Is(args[0], BUDGET_ARG))
			{
				Budget(context, args);
				return;
			}
			if (DevCommandArgs.Is(args[0], REVEAL_ARG))
			{
				if (DevCommandArgs.RequireDungeon(context) == false)
					return;
				context.LogSuccess($"맵 오브젝트 {DungeonManager.Instance.MonsterSpawner.RevealAllMapObjects()}개 드러냄");
				return;
			}
			Place(context, args);
		}

		private static void List(DevCommandContext context)
		{
			MapObjectData[] all = MapObjectSettings.LoadAllData();
			foreach (MapObjectData data in all)
				context.LogInfo($"{data.name} | {data.DisplayName} | {data.Category} | 비용 {data.CostType} {data.CostValue} x{data.CostGrowth} | 머무름 {data.HoldSeconds}초 | 가격 {data.PlacementCost} 가중치 {data.Weight} 개수 {data.CountRange}");
			MapObjectBudget budget = CurrentBudget();
			string candidates = budget != null ? string.Join(", ", budget.Candidates.Select(candidate => candidate != null ? candidate.name : "(빈 칸)")) : "없음";
			context.LogSuccess($"자산 {all.Length}개. 예산 {(budget != null ? budget.name : "없음")} 후보: {candidates}");
		}

		private static void Budget(DevCommandContext context, string[] args)
		{
			MapObjectBudget budget = CurrentBudget();
			if (budget == null)
			{
				context.LogError("예산 없음. 현재 던전과 설정의 기본 예산이 비어 있음");
				return;
			}
			int runSeed = RunSeed.Current;
			if (args.Length > 1 && int.TryParse(args[1], out runSeed) == false)
			{
				context.LogError($"시드는 정수: '{args[1]}'");
				return;
			}
			List<string> errors = new();
			budget.Validate(errors);
			foreach (string error in errors)
				context.LogError(error);
			if (errors.Count > 0)
				return;
			List<MapObjectPlan> plans = MapObjectPlanner.Plan(budget, RunSeed.Derive(runSeed, budget.SeedSalt), MapObjectSettings.Load().Pacing, out int remaining);
			foreach (MapObjectPlan plan in plans)
				context.LogInfo($"{plan.Order + 1}. {plan.Data.name} ({plan.Data.DisplayName}) {plan.SpawnTime:0.#}초, 가격 {plan.Data.PlacementCost}");
			context.LogSuccess($"{budget.name} 시드 {runSeed}: {plans.Count}개, 크레딧 {budget.Credits} 중 남음 {remaining}");
		}

		private static void Place(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			MapObjectData data = Find(args[0]);
			if (data == null)
			{
				context.LogError($"모르는 맵 오브젝트: '{args[0]}'. mapobj list");
				return;
			}
			MonsterSpawner spawner = DungeonManager.Instance.MonsterSpawner;
			bool near = args.Length > 1 && DevCommandArgs.Is(args[1], NEAR_ARG);
			if (near)
			{
				Transform player = PlayerProvider.Instance.CurrentObject.transform;
				Vector3 position = player.position + player.forward * MapObjectSettings.Load().NearDistance;
				spawner.PlaceMapObjectAt(data, position);
				context.LogSuccess($"{data.DisplayName} 주인공 앞 배치");
				return;
			}
			if (spawner.TryPlaceMapObject(data, new System.Random(), out _, out string failure) == false)
			{
				context.LogError($"{data.DisplayName} 배치 실패: {failure}. near 로 바로 놓기");
				return;
			}
			context.LogSuccess($"{data.DisplayName} 배치 규칙으로 배치");
		}

		private static MapObjectBudget CurrentBudget()
		{
			Dungeon dungeon = DungeonManagerBridge.IsDungeon ? DungeonManager.Instance.CurDungeon : null;
			if (dungeon != null && dungeon.MapObjectBudget != null)
				return dungeon.MapObjectBudget;
			return MapObjectSettings.Load().DefaultBudget;
		}

		private static MapObjectData Find(string text) =>
			MapObjectSettings.LoadAllData().FirstOrDefault(data => DevCommandArgs.Is(data.name, text) || DevCommandArgs.Is(data.DisplayName, text));

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length == 1)
				return DevCommandArgs.StartsWith(new[] { LIST_ARG, BUDGET_ARG, REVEAL_ARG }.Concat(MapObjectSettings.LoadAllData().Select(data => data.name)), partial[0]);
			if (partial.Length == 2 && DevCommandArgs.Is(partial[0], LIST_ARG) == false && DevCommandArgs.Is(partial[0], BUDGET_ARG) == false && DevCommandArgs.Is(partial[0], REVEAL_ARG) == false)
				return DevCommandArgs.StartsWith(new[] { NEAR_ARG }, partial[1]);
			return Enumerable.Empty<string>();
		}
	}
}
