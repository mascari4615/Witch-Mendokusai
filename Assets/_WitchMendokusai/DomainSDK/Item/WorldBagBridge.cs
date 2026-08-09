namespace WitchMendokusai
{
	/// <summary>
	/// 게임에서 뭔가를 주웠을 때 <b>세계에도 알리는 구멍</b> (TASK-WM-218).
	///
	/// ★ 왜 필요한가: 지금 게임의 가방과 세계의 가방이 <b>따로 논다</b>. 게임에서 주운 건
	///   세계가 모르고(그래서 다시 들어오면 없다), 세계가 아는 건 화면에 안 뜬다.
	///   신원이 생겨 「내 것」을 기억할 수 있게 됐으니, 이제 <b>주웠다는 사실이 세계까지</b> 가야 한다.
	///
	/// 판정 층에 구멍만 둔다 — 채우는 쪽은 통신 층(줄), 부르는 쪽은 게임(인벤토리).
	/// 아무도 안 꽂혀 있으면 조용히 아무 일도 안 한다(혼자 놀 때 회귀 0).
	/// </summary>
	public interface IWorldBagSink
	{
		/// <summary>이만큼 주웠다 — 세계도 같이 알아야 다시 왔을 때 있다.</summary>
		void Gathered(int itemId, int amount);
	}

	/// <summary>게임 ↔ 세계 가방을 잇는 자리. 통신 층이 꽂고, 게임이 부른다.</summary>
	public static class WorldBagBridge
	{
		private static IWorldBagSink sink;

		public static void Register(IWorldBagSink worldBagSink) => sink = worldBagSink;

		public static void Clear(IWorldBagSink worldBagSink)
		{
			if (sink == worldBagSink)
				sink = null;
		}

		/// <summary>세계에 알린다. 안 꽂혀 있으면 아무 일도 안 한다.</summary>
		public static void Gathered(int itemId, int amount)
		{
			if (sink == null || amount <= 0)
				return;

			sink.Gathered(itemId, amount);
		}
	}
}
