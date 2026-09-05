using System.Collections.Generic;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 통행증 한 장으로 <b>짐은 한 번만</b> 건네준다 (TASK-WM-259 → 309) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가: 통행증은 결국 <b>글자</b>다. 창은 그걸 복사할 수 있고 남에게 줄 수도 있다.
	///   도장(<see cref="TravelPass"/>)은 「지어낸 것」만 막는다 — 진짜 통행증 한 장으로 <b>두 번</b>
	///   들어오는 것은 못 막는다. 그러면 가방이 두 벌 들어온다(전형적인 복사 버그).
	///
	/// ★ 그런데 「내밀면 곧 쓴 것」으로 세면 <b>더 나쁜 일</b>이 생긴다 (실측 2026-08-13):
	///   통행증을 내밀다 줄이 끊기면 짐은 아직 안 건너왔는데 통행증만 타 버린다.
	///   그 사람이 다시 붙으면 세계는 그를 처음 보는 손님으로 맞는다 — <b>가방도 자리도 없이</b>.
	///
	/// ★ 그래서 두 단계다:
	///   ① <see cref="TryClaim"/> — 「지금 이 통행증으로 들어가는 중」이라고 <b>맡아 둔다</b>.
	///      같은 순간에 둘이 같은 통행증을 내밀면 뒤엣것은 거절한다(그게 진짜 복사 시도다).
	///   ② <see cref="MarkDelivered"/> — 짐을 <b>실제로 건넨 뒤에야</b> 「썼다」고 적는다.
	///      그 뒤 같은 통행증으로 다시 들어오면 <b>받아 주되 짐은 다시 안 준다</b>(이미 그 세계에 있다).
	///
	/// ★ 맡아 둔 것은 <see cref="CLAIM_GOOD_FOR_MS"/> 뒤 풀린다 — 반쪽으로 죽은 시도가 통행증을
	///   영영 묶어 두면, 그것도 사람을 가두는 것이다.
	/// </summary>
	public sealed class PassOnce
	{
		/// <summary>맡아 둔 것이 풀리기까지 (ms) — 도착이 이보다 오래 걸리면 그건 실패한 도착이다.</summary>
		public const long CLAIM_GOOD_FOR_MS = 10000;

		/// <summary>맡아 둔 통행증 — 언제, <b>누가</b>. 주인을 같이 적어야 「같은 사람의 재시도」와
		/// 「남의 복사 시도」를 가를 수 있다 (TASK-WM-337).</summary>
		private readonly Dictionary<string, (long When, string Owner)> claimed =
			new Dictionary<string, (long When, string Owner)>();
		private readonly Dictionary<string, long> delivered = new Dictionary<string, long>();
		private readonly object gate = new object();

		/// <summary>지금 기억하고 있는 장수 — 안 늘어나는지 보는 자리다.</summary>
		public int Count
		{
			get
			{
				lock (gate)
				{
					return delivered.Count + claimed.Count;
				}
			}
		}

		/// <summary>
		/// 이 통행증으로 지금 들어가도 되나. <paramref name="needsLuggage"/> 는
		/// <b>짐을 건네야 하는가</b> — 이미 건넨 통행증이면 <c>false</c>(그 사람 짐은 이 세계에 있다).
		/// </summary>
		public bool TryClaim(string pass, long nowMs, out bool needsLuggage)
		{
			return TryClaim(pass, nowMs, string.Empty, out needsLuggage);
		}

		/// <summary>
		/// 주인을 밝히고 맡는다 (TASK-WM-337).
		///
		/// ★ 왜 주인이 필요한가 (실측 2026-08-14): 통행증을 집은 <b>뒤</b> 짐을 받기 전에 줄이 끊기면,
		///   그 사람은 <see cref="CLAIM_GOOD_FOR_MS"/> 동안 <b>자기 통행증에서 쫓겨난다</b> —
		///   다시 붙어도 거절당해 <b>가방 없는 손님</b>이 된다(관문 실측: 가방 3 → 0).
		///   막으려던 것은 「남이 같은 종이를 동시에 내미는 것」이지 <b>그 사람의 재시도</b>가 아니다.
		///   그래서 맡아 둔 주인과 같으면 <b>다시 맡는다</b>(시각만 새로 적는다).
		/// </summary>
		public bool TryClaim(string pass, long nowMs, string owner, out bool needsLuggage)
		{
			needsLuggage = false;
			if (string.IsNullOrEmpty(pass))
				return false;

			lock (gate)
			{
				ForgetOld(nowMs);

				if (delivered.ContainsKey(pass))
					return true;

				if (claimed.TryGetValue(pass, out (long When, string Owner) held) && nowMs - held.When <= CLAIM_GOOD_FOR_MS)
				{
					// 남이 같은 순간에 같은 종이를 내밀었다 = 진짜 복사 시도.
					bool sameHand = string.IsNullOrEmpty(owner) == false && owner == held.Owner;
					if (sameHand == false)
						return false;
				}

				claimed[pass] = (nowMs, owner);
				needsLuggage = true;
				return true;
			}
		}

		/// <summary>
		/// 「썼다」고 적힌 것들 — <b>세계가 껐다 켜져도</b> 이어지게 밖에 적어 두려고 꺼낸다 (TASK-WM-382).
		/// 맡아 둔 것(<see cref="TryClaim"/>)은 안 준다: 그건 <b>지금 들어가는 중</b>이라는 짧은 표라
		/// 재시작을 넘겨 살리면 그 사람이 자기 통행증에서 쫓겨난다.
		/// </summary>
		public List<(string Pass, long WhenMs)> Delivered()
		{
			lock (gate)
			{
				List<(string, long)> rows = new List<(string, long)>(delivered.Count);
				foreach (KeyValuePair<string, long> one in delivered)
					rows.Add((one.Key, one.Value));

				return rows;
			}
		}

		/// <summary>적어 둔 「썼다」를 되살린다 — 이미 지난 것은 어차피 통행증 자체가 죽어 있다.</summary>
		public void RestoreDelivered(string pass, long whenMs)
		{
			if (string.IsNullOrEmpty(pass))
				return;

			lock (gate)
			{
				delivered[pass] = whenMs;
			}
		}

		/// <summary>짐을 건넸다 — 이제부터 이 통행증으로는 <b>짐 없이</b>만 들어올 수 있다.</summary>
		public void MarkDelivered(string pass, long nowMs)
		{
			if (string.IsNullOrEmpty(pass))
				return;

			lock (gate)
			{
				claimed.Remove(pass);
				delivered[pass] = nowMs;
			}
		}

		// ⚠ 이미 자물쇠를 쥔 자리에서 부른다.
		private void ForgetOld(long nowMs)
		{
			Sweep(delivered, nowMs, TravelPass.GOOD_FOR_MS);
			SweepClaims(nowMs);
		}

		private void SweepClaims(long nowMs)
		{
			List<string> stale = null;
			foreach (KeyValuePair<string, (long When, string Owner)> one in claimed)
			{
				if (nowMs - one.Value.When <= CLAIM_GOOD_FOR_MS)
					continue;

				stale = stale ?? new List<string>();
				stale.Add(one.Key);
			}

			if (stale == null)
				return;

			for (int i = 0; i < stale.Count; i++)
				claimed.Remove(stale[i]);
		}

		private static void Sweep(Dictionary<string, long> book, long nowMs, long keepMs)
		{
			List<string> stale = null;
			foreach (KeyValuePair<string, long> one in book)
			{
				if (nowMs - one.Value <= keepMs)
					continue;

				stale = stale ?? new List<string>();
				stale.Add(one.Key);
			}

			if (stale == null)
				return;

			for (int i = 0; i < stale.Count; i++)
				book.Remove(stale[i]);
		}
	}
}
