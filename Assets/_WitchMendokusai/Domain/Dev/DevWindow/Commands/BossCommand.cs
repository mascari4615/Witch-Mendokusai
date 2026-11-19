using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// boss &lt;pattern&gt; [loop] , 슬라임 왕 패턴을 바로 실행. 진열장 (showcase) 이나 판 안에서
	/// - 살아 있는 슬라임 왕이 없으면 플레이어 앞에 하나 꺼냄
	/// - pattern: projectile, dash, launch, pull. loop 면 boss stop 전까지 그 패턴만 반복
	/// - boss stop: 지정 해제, 원래 순서로
	/// </summary>
	public class BossCommand : IDevCommand
	{
		public string Name => "boss";
		public string Usage => "boss <projectile|dash|launch|pull|stop> [loop]";

		private const string LOOP_ARG = "loop";
		private const string STOP_ARG = "stop";
		// 새로 꺼낼 때 플레이어에서 떨어뜨리는 거리 (월드 단위). 패턴 사거리 안
		private const float SPAWN_DISTANCE = 7f;

		public void Execute(DevCommandContext context, string[] args)
		{
			if (args.Length < 1)
			{
				context.LogError(Usage);
				return;
			}

			if (DungeonManagerBridge.IsDungeon == false)
			{
				context.LogError("판 안에서만. showcase 로 진열장에 들어간 뒤");
				return;
			}

			if (string.Equals(args[0], STOP_ARG, StringComparison.OrdinalIgnoreCase))
			{
				int cleared = 0;
				foreach (FSMSlimeKing king in FindKings())
				{
					king.ClearForcedPattern();
					cleared++;
				}
				context.LogSuccess($"지정 해제 {cleared}마리");
				return;
			}

			if (Enum.TryParse(args[0], true, out SlimeKingPattern pattern) == false || Enum.IsDefined(typeof(SlimeKingPattern), pattern) == false)
			{
				context.LogError($"모르는 패턴: '{args[0]}'. {Usage}");
				return;
			}
			bool loop = args.Length > 1 && string.Equals(args[1], LOOP_ARG, StringComparison.OrdinalIgnoreCase);

			FSMSlimeKing target = FindKings().FirstOrDefault();
			if (target == null)
			{
				target = SpawnKing(context);
				if (target == null)
					return;
			}

			target.ForcePattern(pattern, loop);
			context.LogSuccess($"슬라임 왕 {pattern}{(loop ? " 반복" : "")}");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length == 1)
			{
				return Enum.GetNames(typeof(SlimeKingPattern))
					.Select(name => name.ToLowerInvariant())
					.Append(STOP_ARG)
					.Where(name => name.StartsWith(partial[0], StringComparison.OrdinalIgnoreCase));
			}
			if (partial.Length == 2)
				return new[] { LOOP_ARG }.Where(name => name.StartsWith(partial[1], StringComparison.OrdinalIgnoreCase));
			return Enumerable.Empty<string>();
		}

		private static IEnumerable<FSMSlimeKing> FindKings() =>
			UnityEngine.Object.FindObjectsByType<FSMSlimeKing>(FindObjectsSortMode.None)
				.Where(king => king.isActiveAndEnabled);

		private static FSMSlimeKing SpawnKing(DevCommandContext context)
		{
			Monster kingData = null;
			SOHelper.ForEach<Monster>(monster =>
			{
				if (kingData == null && monster != null && monster.Prefab != null && monster.Prefab.GetComponent<FSMSlimeKing>() != null)
					kingData = monster;
			});
			if (kingData == null)
			{
				context.LogError("슬라임 왕 프리팹을 쓰는 몬스터 자료 없음");
				return null;
			}

			Transform player = PlayerProvider.Instance.Current != null ? PlayerProvider.Instance.Current.transform : null;
			Vector3 origin = player != null ? player.position : Vector3.zero;
			Vector3 spawnPosition = origin + Vector3.forward * SPAWN_DISTANCE;

			GameObject spawned = ObjectPoolManager.Instance.Spawn(kingData.Prefab);
			spawned.transform.position = spawnPosition;
			spawned.GetComponent<MonsterObject>().Init(kingData);
			spawned.SetActive(true);
			context.LogInfo($"{kingData.Name} 꺼냄 (ID {kingData.ID}, pos={spawnPosition:F1})");
			return spawned.GetComponent<FSMSlimeKing>();
		}
	}
}
