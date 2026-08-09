namespace WitchMendokusai.Net
{
	/// <summary>세계가 보낸 짧은 말을 화면에 띄우는 자리 — 보이는 쪽이 꽂는다.</summary>
	public interface IWorldNoticeReceiver
	{
		/// <summary>사람에게 그대로 보여 줄 한 줄 (예: 「재료가 모자란다 — 나무 2개가 든다」).</summary>
		void ShowWorldNotice(string message);
	}

	/// <summary>
	/// 세계 → 화면으로 <b>짧은 말</b>을 넘기는 구멍 (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 거절 이유를 웹 창만 보고 게임 창은 못 봤다 — 게임에서는 조용히 실패해
	///   사람이 「고장났나」로 읽는다. 통신 층은 UI 를 볼 수 없으므로(asmdef 단방향)
	///   <see cref="WorldBagBridge"/> 와 같은 모양의 구멍을 둔다.
	/// </summary>
	public static class WorldNoticeBridge
	{
		private static IWorldNoticeReceiver receiver;

		public static void RegisterReceiver(IWorldNoticeReceiver worldNoticeReceiver) => receiver = worldNoticeReceiver;

		/// <summary>보여 줄 곳이 없으면 조용히 흘린다 — 알림 하나 때문에 게임이 죽지 않는다.</summary>
		public static void Deliver(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			receiver?.ShowWorldNotice(message);
		}
	}
}
