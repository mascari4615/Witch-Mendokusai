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

		/// <summary>
		/// 이만큼 썼다(제작 재료 등) — 안 알리면 <b>쓴 게 다시 생긴다</b>.
		/// 줍기만 알리고 쓰기를 안 알리면 세계의 가방은 영영 불어나기만 한다.
		/// </summary>
		void Consumed(int itemId, int amount);
	}

	/// <summary>
	/// 세계가 알려준 가방을 <b>화면에 반영하는 쪽</b> (TASK-WM-218).
	/// 게임이 구현하고, 통신 층이 부른다 — 방향이 반대인 구멍이다.
	/// </summary>
	public interface IWorldBagReceiver
	{
		/// <summary>세계가 아는 내 가방은 이렇다 — 화면(인벤토리)을 여기에 맞춘다.</summary>
		void ApplyWorldBag(int[] itemIds, int[] amounts);
	}

	/// <summary>게임 ↔ 세계 가방을 잇는 자리. 통신 층이 꽂고, 게임이 부른다.</summary>
	public static class WorldBagBridge
	{
		private static IWorldBagSink sink;
		private static IWorldBagReceiver receiver;

		public static void Register(IWorldBagSink worldBagSink) => sink = worldBagSink;

		public static void Clear(IWorldBagSink worldBagSink)
		{
			if (sink == worldBagSink)
				sink = null;
		}

		/// <summary>화면 쪽을 꽂는다(게임이 부른다).</summary>
		public static void RegisterReceiver(IWorldBagReceiver worldBagReceiver) => receiver = worldBagReceiver;

		public static void ClearReceiver(IWorldBagReceiver worldBagReceiver)
		{
			if (receiver == worldBagReceiver)
				receiver = null;
		}

		/// <summary>
		/// 세계가 알려준 가방을 화면에 넘긴다 (TASK-WM-218).
		/// ⚠ 받는 쪽은 이걸 반영하는 동안 <b>다시 세계에 알리면 안 된다</b> — 그러면 무한히 오간다.
		/// </summary>
		public static void DeliverBag(int[] itemIds, int[] amounts)
		{
			if (receiver == null || itemIds == null || amounts == null)
				return;

			receiver.ApplyWorldBag(itemIds, amounts);
		}

		/// <summary>세계에 알린다. 안 꽂혀 있으면 아무 일도 안 한다.</summary>
		public static void Gathered(int itemId, int amount)
		{
			if (sink == null || amount <= 0)
				return;

			sink.Gathered(itemId, amount);
		}

		/// <summary>썼다고 알린다.</summary>
		public static void Consumed(int itemId, int amount)
		{
			if (sink == null || amount <= 0)
				return;

			sink.Consumed(itemId, amount);
		}
	}
}
