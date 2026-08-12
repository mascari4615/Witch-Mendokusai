using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 국경 너머에서 <b>비쳐 오는 사람들</b> (TASK-WM-263).
	///
	/// ★ 왜: 세계를 나누면 국경에 선 사람은 1m 옆의 사람을 못 본다 — 저 사람은 옆 세계에 있고
	///   이 세계는 그를 모르기 때문이다. 그러면 한 세계가 아니라 <b>벽으로 갈린 두 게임</b>이다.
	///
	/// ★ 그림자는 <b>보이기만</b> 한다: 번호가 음수라 이 세계의 인형과 절대 안 겹치고,
	///   세계의 인형 표에도 안 들어간다 — 그래서 못 때리고, 저장되지도 않는다.
	///
	/// ★ <b>안 오면 사라진다</b>: 옆 세계가 조용해지면(꺼졌거나 회선이 끊겼거나) 그림자는
	///   <see cref="FADE_AFTER_MS"/> 뒤에 지워진다. 안 지우면 국경에 유령이 영영 서 있다.
	/// </summary>
	public sealed class NeighbourShadows
	{
		/// <summary>이 시간 동안 소식이 없으면 지운다 (ms) — 알려 주는 간격(100ms)의 여러 배.</summary>
		public const long FADE_AFTER_MS = 2000;

		private readonly object gate = new object();
		private readonly Dictionary<int, WorldDoll> byId = new Dictionary<int, WorldDoll>();
		private readonly Dictionary<int, long> seenAt = new Dictionary<int, long>();

		/// <summary>지금 들고 있는 그림자 수 — 안 늘어나는지 보는 자리다.</summary>
		public int Count
		{
			get
			{
				lock (gate)
				{
					return byId.Count;
				}
			}
		}

		/// <summary>옆 세계가 알려 온 한 판 — 그 세계의 그림자를 <b>이것으로 바꾼다</b>.</summary>
		public void TakeFrom(string zoneName, IReadOnlyList<(int DollId, float X, float Z, string Name)> people, long nowMs)
		{
			if (string.IsNullOrEmpty(zoneName) || people == null)
				return;

			lock (gate)
			{
				HashSet<int> stillThere = new HashSet<int>();
				foreach ((int DollId, float X, float Z, string Name) one in people)
				{
					int id = WitchMendokusai.Net.BorderBand.ShadowId(zoneName, one.DollId);
					if (id == 0)
						continue;

					stillThere.Add(id);
					if (byId.TryGetValue(id, out WorldDoll shadow) == false)
					{
						shadow = new WorldDoll(id, new Vector3(one.X, 0f, one.Z));
						byId[id] = shadow;
					}

					shadow.Position = new Vector3(one.X, 0f, one.Z);
					shadow.BorrowedName = one.Name ?? string.Empty;
					seenAt[id] = nowMs;
				}

				// ⚠ 그 세계에서 <b>빠진</b> 사람은 곧바로 지운다 — 기다리면 띠를 벗어난 사람이
				//   국경에 한동안 서 있는다(창 눈에는 「저기 서서 안 움직이는 사람」이다).
				List<int> gone = null;
				foreach (KeyValuePair<int, WorldDoll> entry in byId)
				{
					if (WitchMendokusai.Net.BorderBand.ShadowId(zoneName, ThatWorldsId(entry.Key)) != entry.Key)
						continue;   // 다른 이웃의 그림자다 — 이번 판과 상관없다

					if (stillThere.Contains(entry.Key))
						continue;

					gone = gone ?? new List<int>();
					gone.Add(entry.Key);
				}

				if (gone == null)
					return;

				foreach (int id in gone)
				{
					byId.Remove(id);
					seenAt.Remove(id);
				}
			}
		}

		/// <summary>지금 보이는 그림자들 — 소식이 끊긴 것은 빼고 준다.</summary>
		public WorldDoll[] Alive(long nowMs)
		{
			lock (gate)
			{
				List<int> faded = null;
				foreach (KeyValuePair<int, long> entry in seenAt)
				{
					if (nowMs - entry.Value <= FADE_AFTER_MS)
						continue;

					faded = faded ?? new List<int>();
					faded.Add(entry.Key);
				}

				if (faded != null)
				{
					foreach (int id in faded)
					{
						byId.Remove(id);
						seenAt.Remove(id);
					}
				}

				WorldDoll[] shown = new WorldDoll[byId.Count];
				byId.Values.CopyTo(shown, 0);
				return shown;
			}
		}

		/// <summary>그림자 번호에서 <b>저 세계의 번호</b>를 되뽑는다.</summary>
		private static int ThatWorldsId(int shadowId)
		{
			return (-shadowId) % WitchMendokusai.Net.BorderBand.ROOM_PER_ZONE;
		}
	}
}
