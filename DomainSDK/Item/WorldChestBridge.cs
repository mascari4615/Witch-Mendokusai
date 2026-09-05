using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>상자 안 한 칸 — 무엇이 몇 개.</summary>
	public struct ChestSlot
	{
		public int ItemId;
		public int Amount;
	}

	/// <summary>
	/// 게임 화면이 <b>상자를 여닫는 구멍</b> (TASK-WM-217).
	///
	/// ★ 왜 필요한가 (실측 2026-08-10): 상자는 세계에도 있고 웹 창에도 있는데 <b>게임 창에는
	///   손잡이가 없었다</b> — 게임에서 지은 상자를 게임에서 못 열었다. 그러면 「같이 노는 세계」의
	///   알맹이(나눔)가 한쪽 창에만 있는 셈이다.
	///
	/// 게임 UI 는 통신 층을 직접 못 부른다(asmdef 단방향). 그래서 판정 층에 자리만 두고 통신이 꽂는다.
	/// </summary>
	public interface IWorldChestChannel
	{
		/// <summary>세계에 붙어 있나 — false 면 게임은 상자 손잡이를 감춘다.</summary>
		bool IsActive { get; }

		/// <summary>내가 선 자리에서 가장 가까운 상자를 연다 — 없으면 false.</summary>
		bool TryOpenNearby();

		/// <summary>지금 열어 둔 상자 안 — 아직 못 받았으면 빈 목록.</summary>
		IReadOnlyList<ChestSlot> Contents { get; }

		/// <summary>가방 → 상자. 넣다 남으면 세계가 도로 돌려준다.</summary>
		void Put(int itemId, int amount);

		/// <summary>상자 → 가방. 가방이 좁으면 세계가 남은 만큼 도로 넣는다.</summary>
		void Take(int itemId, int amount);
	}

	/// <summary>상자 구멍을 잇는 자리 — 통신 층이 꽂고, 게임이 부른다.</summary>
	public static class WorldChestBridge
	{
		private static IWorldChestChannel channel;

		public static void Register(IWorldChestChannel worldChestChannel) => channel = worldChestChannel;

		public static void Clear(IWorldChestChannel worldChestChannel)
		{
			if (channel == worldChestChannel)
				channel = null;
		}

		public static bool IsActive => channel != null && channel.IsActive;

		public static IWorldChestChannel Channel => channel;
	}

	/// <summary>
	/// 게임 화면이 <b>이름을 정하는 구멍</b> (TASK-WM-218).
	/// 되나 안 되나는 세계가 본다 — 거절 이유는 알림(WorldNoticeBridge)으로 온다.
	/// </summary>
	public interface IWorldNameChannel
	{
		bool IsActive { get; }

		/// <summary>세계가 지금 나를 뭐라고 부르나 — 아직 모르면 빈 글.</summary>
		string MyName { get; }

		void Rename(string name);
	}

	/// <summary>이름 구멍을 잇는 자리.</summary>
	public static class WorldNameBridge
	{
		private static IWorldNameChannel channel;

		public static void Register(IWorldNameChannel worldNameChannel) => channel = worldNameChannel;

		public static void Clear(IWorldNameChannel worldNameChannel)
		{
			if (channel == worldNameChannel)
				channel = null;
		}

		public static bool IsActive => channel != null && channel.IsActive;

		public static IWorldNameChannel Channel => channel;
	}
}
