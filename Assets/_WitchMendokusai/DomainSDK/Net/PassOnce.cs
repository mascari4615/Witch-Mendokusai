using System.Collections.Generic;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 이미 쓴 <b>통행증</b>을 기억한다 (TASK-WM-259) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가: 통행증은 결국 <b>글자</b>다. 창은 그걸 복사할 수 있고 남에게 줄 수도 있다.
	///   도장(<see cref="TravelPass"/>)은 「지어낸 것」만 막는다 — 진짜 통행증 한 장으로 <b>두 번</b>
	///   들어오는 것은 못 막는다. 그러면 가방이 두 벌 들어온다(전형적인 복사 버그).
	///
	/// ★ 기한이 지난 것은 안 들고 있는다 — 어차피 통행증 자체가 거절되므로, 여기 쌓아 둘 이유가 없다
	///   (안 버리면 이 표가 곧 세계의 기억을 먹는다).
	/// </summary>
	public sealed class PassOnce
	{
		private readonly Dictionary<string, long> used = new Dictionary<string, long>();
		private readonly object gate = new object();

		/// <summary>지금 기억하고 있는 장수 — 안 늘어나는지 보는 자리다.</summary>
		public int Count
		{
			get
			{
				lock (gate)
				{
					return used.Count;
				}
			}
		}

		/// <summary>이 통행증을 지금 쓴다. <b>두 번째부터는 false</b>.</summary>
		public bool TryUse(string pass, long nowMs)
		{
			if (string.IsNullOrEmpty(pass))
				return false;

			lock (gate)
			{
				ForgetOld(nowMs);

				if (used.ContainsKey(pass))
					return false;

				used[pass] = nowMs;
				return true;
			}
		}

		// ⚠ 이미 자물쇠를 쥔 자리에서 부른다.
		private void ForgetOld(long nowMs)
		{
			List<string> stale = null;
			foreach (KeyValuePair<string, long> one in used)
			{
				if (nowMs - one.Value <= TravelPass.GOOD_FOR_MS)
					continue;

				stale = stale ?? new List<string>();
				stale.Add(one.Key);
			}

			if (stale == null)
				return;

			foreach (string one in stale)
				used.Remove(one);
		}
	}
}
