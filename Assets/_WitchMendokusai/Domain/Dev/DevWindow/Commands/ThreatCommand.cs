using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// threat , 지금 판의 생성 정책, 반납 정책, 경계.
	/// threat spawn &lt;forwardcone|offscreenring&gt; , 주기 생성 자리 정책 (th4).
	/// threat retire &lt;retire|relocate&gt; , 화면 밖 일반 적 처리 정책 (th4).
	/// threat arena none , 경계 끔. threat arena &lt;circle|square&gt; &lt;반경&gt; [wall|hazard] , 지금 자리 중심으로 경계 (th2).
	/// threat pattern &lt;이름&gt; , 현재 던전 일정표의 웨이브 패턴 하나를 지금 발동 (th5, th8).
	/// 판 중 비교용. 던전 자산 값은 바꾸지 않음, 다음 판은 자산 값으로 시작
	/// </summary>
	public class ThreatCommand : IDevCommand
	{
		private const string SPAWN_ARG = "spawn";
		private const string RETIRE_ARG = "retire";
		private const string ARENA_ARG = "arena";
		private const string PATTERN_ARG = "pattern";

		public string Name => "threat";
		public string Usage => "threat [spawn <정책>|retire <정책>|arena <none|circle|square> [반경] [wall|hazard]|pattern <이름>]";

		public void Execute(DevCommandContext context, string[] args)
		{
			if (DevCommandArgs.RequireDungeon(context) == false)
				return;
			MonsterSpawner spawner = DungeonManager.Instance.MonsterSpawner;
			if (args.Length == 0)
			{
				Report(context, spawner);
				return;
			}
			if (DevCommandArgs.Is(args[0], SPAWN_ARG) && args.Length > 1 && DevCommandArgs.TryEnum(args[1], out SpawnPlacementPolicy spawnPolicy))
			{
				spawner.SetThreatPolicy(spawner.ThreatPolicy.With(spawnPolicy, spawner.ThreatPolicy.RetirePolicy));
				Report(context, spawner);
				return;
			}
			if (DevCommandArgs.Is(args[0], RETIRE_ARG) && args.Length > 1 && DevCommandArgs.TryEnum(args[1], out OffscreenRetirePolicy retirePolicy))
			{
				spawner.SetThreatPolicy(spawner.ThreatPolicy.With(spawner.ThreatPolicy.SpawnPolicy, retirePolicy));
				Report(context, spawner);
				return;
			}
			if (DevCommandArgs.Is(args[0], ARENA_ARG) && args.Length > 1)
			{
				Arena(context, spawner, args);
				return;
			}
			if (DevCommandArgs.Is(args[0], PATTERN_ARG) && args.Length > 1)
			{
				Pattern(context, args[1]);
				return;
			}
			context.LogError(Usage);
		}

		private void Arena(DevCommandContext context, MonsterSpawner spawner, string[] args)
		{
			if (DevCommandArgs.TryEnum(args[1], out ArenaBoundaryShape shape) == false)
			{
				context.LogError($"모양은 {string.Join(", ", DevCommandArgs.EnumNames<ArenaBoundaryShape>())}");
				return;
			}
			float halfSize = spawner.Arena.HalfSize;
			if (args.Length > 2 && DevCommandArgs.TryFloat(args[2], out halfSize) == false)
			{
				context.LogError($"반경은 숫자: '{args[2]}'");
				return;
			}
			ArenaBoundaryMode mode = spawner.Arena.Mode;
			if (args.Length > 3 && DevCommandArgs.TryEnum(args[3], out mode) == false)
			{
				context.LogError($"방식은 {string.Join(", ", DevCommandArgs.EnumNames<ArenaBoundaryMode>())}");
				return;
			}
			spawner.SetArena(spawner.Arena.With(shape, mode, halfSize));
			Report(context, spawner);
		}

		private static void Pattern(DevCommandContext context, string text)
		{
			WavePattern pattern = DungeonManager.Instance.CurDungeon.WavePatterns
				.Select(slot => slot.Pattern)
				.FirstOrDefault(candidate => DevCommandArgs.Is(candidate.name, text));
			if (pattern == null)
			{
				context.LogError($"현재 던전 일정표에 없는 패턴: '{text}'");
				return;
			}
			DungeonManager.Instance.TriggerWavePattern(pattern);
			context.LogSuccess($"{pattern.name} 발동");
		}

		private static void Report(DevCommandContext context, MonsterSpawner spawner)
		{
			DungeonThreatPolicy policy = spawner.ThreatPolicy;
			DungeonArenaOptions arena = spawner.Arena;
			string arenaText = arena.Enabled ? $"{arena.Shape} {arena.Mode} {arena.HalfSize}m" : "없음";
			context.LogSuccess($"생성 {policy.SpawnPolicy} (앞 {policy.ForwardHalfAngle}도, 앞 확률 {policy.ForwardWeight}), 반납 {policy.RetirePolicy}, 경계 {arenaText}");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length == 1)
				return DevCommandArgs.StartsWith(new[] { SPAWN_ARG, RETIRE_ARG, ARENA_ARG, PATTERN_ARG }, partial[0]);
			if (partial.Length == 2 && DevCommandArgs.Is(partial[0], SPAWN_ARG))
				return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<SpawnPlacementPolicy>(), partial[1]);
			if (partial.Length == 2 && DevCommandArgs.Is(partial[0], RETIRE_ARG))
				return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<OffscreenRetirePolicy>(), partial[1]);
			if (partial.Length == 2 && DevCommandArgs.Is(partial[0], ARENA_ARG))
				return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<ArenaBoundaryShape>(), partial[1]);
			if (partial.Length == 4 && DevCommandArgs.Is(partial[0], ARENA_ARG))
				return DevCommandArgs.StartsWith(DevCommandArgs.EnumNames<ArenaBoundaryMode>(), partial[3]);
			if (partial.Length == 2 && DevCommandArgs.Is(partial[0], PATTERN_ARG) && DungeonManagerBridge.IsDungeon)
				return DevCommandArgs.StartsWith(DungeonManager.Instance.CurDungeon.WavePatterns.Select(slot => slot.Pattern.name), partial[1]);
			return Enumerable.Empty<string>();
		}
	}
}
