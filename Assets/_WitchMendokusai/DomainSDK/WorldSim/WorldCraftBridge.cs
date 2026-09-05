using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임의 제작 화면이 <b>세계에 물어보는 구멍</b> (TASK-WM-217).
	///
	/// ★ 왜 구멍인가: 제작 UI 는 Domain 층이고 통신은 Network 층이라 서로 직접 못 부른다
	///   (asmdef 단방향). 그래서 판정 층에 자리만 두고, 통신 층이 꽂는다.
	///   아무도 안 꽂혀 있으면 <see cref="IsActive"/> 가 false — 게임은 옛 경로(자기 판정)로 돈다.
	/// </summary>
	public interface IWorldCraftChannel
	{
		/// <summary>세계에 붙어 있나. false 면 게임이 자기 규칙으로 만든다.</summary>
		bool IsActive { get; }

		/// <summary>세계가 아는 제작표 — 못 받았으면 빈 목록.</summary>
		IReadOnlyList<CraftRecipeEntry> Recipes { get; }

		/// <summary>이 줄대로 만들겠다 — 재료도 주사위도 세계가 본다.</summary>
		void Request(int recipeId);

		/// <summary>세계가 돌려준 결과. 아직 없으면 false. 한 번 받으면 비운다.</summary>
		bool TryTakeResult(out CraftResult result);
	}

	/// <summary>제작 구멍을 잇는 자리 — 통신 층이 꽂고, 게임이 부른다.</summary>
	public static class WorldCraftBridge
	{
		private static IWorldCraftChannel channel;

		public static void Register(IWorldCraftChannel worldCraftChannel) => channel = worldCraftChannel;

		public static void Clear(IWorldCraftChannel worldCraftChannel)
		{
			if (channel == worldCraftChannel)
				channel = null;
		}

		/// <summary>세계가 제작을 판정하고 있나.</summary>
		public static bool IsActive => channel != null && channel.IsActive;

		public static IWorldCraftChannel Channel => channel;
	}
}
