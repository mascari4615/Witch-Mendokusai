using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임이 주운 것을 <b>줄 너머 세계로</b> 옮기는 자리 (TASK-WM-218).
	/// 건설·가마솥 채널과 같은 수법 — 게임은 구멍(<see cref="WorldBagBridge"/>)만 알고,
	/// 그 구멍을 줄이 채운다.
	/// </summary>
	public sealed class WorldBagRelay : IWorldBagSink
	{
		private readonly IWorldLink link;

		public WorldBagRelay(IWorldLink link)
		{
			this.link = link;
		}

		public void Gathered(int itemId, int amount)
		{
			// 가방에 정말 들어갈지는 세계가 본다(꽉 찼으면 덜 넣는다) — 창이 우기지 않는다.
			// ⚠ 「무엇을 몇 개 주웠다」는 이제 세계가 안 받는다 (TASK-WM-217) — 주울 것의 번호로 청한다.
			//   이 자리는 게임 채집 시스템이 노드 번호를 알게 될 때 이어진다.
		}

		public void Consumed(int itemId, int amount)
		{
			link?.RequestConsume(itemId, amount);
		}
	}
}
