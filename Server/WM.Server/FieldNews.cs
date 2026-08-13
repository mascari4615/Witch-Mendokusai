using System.Collections.Generic;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// <b>들판 소식을 창마다 옳게 고르는 셈</b> — 순수 셈, 엔진 밖 (TASK-WM-343).
	///
	/// ★ 왜 떼어내나: 지금은 이 셈이 알림 루프 안에 있고, <b>칸마다 장부 하나</b>다.
	///   그래서 그 칸의 「없어졌다」는 <b>한 판만</b> 실리고 장부가 갱신된다 —
	///   그 판을 건너뛴 창(밀려서 덜 받는 창)은 다음 판에 아무것도 못 받고 그 자리를 <b>영영</b> 그린다.
	///   브라우저 관문으로만 재다 보니 한 판에 2~3분이 걸리고 <b>판마다 결과가 흔들려</b>
	///   같은 코드로 3/3·2/3·1/3 을 다 봤다(그 흔들림에 두 번 속았다).
	///   그래서 이 셈만 떼어 <b>밀리초</b>에 가른다.
	///
	/// 규칙:
	///   ① 처음 보는 창에는 <b>통째로</b>
	///   ② 바로 앞 판까지 받은 창에는 <b>델타</b>(바뀐 것·없어진 것)
	///   ③ 한 판이라도 건너뛴 창에는 <b>통째로</b> — 놓친 판의 「없어졌다」는 되살릴 수 없다
	///   ④ 어떤 창이 통째로 받아 갔다고 해서 <b>다른 창의 델타가 빈손이 되면 안 된다</b>
	/// </summary>
	public sealed class FieldNews
	{
		/// <summary>
		/// 이 칸의 <b>지금</b>과 <b>바로 앞</b> 모습 — 둘을 다 들고 있어야 같은 판의 창들이 서로를 굶기지 않는다.
		///
		/// ★ 하나만 들고 있으면: 밀린 창이 통째로 받아 가며 장부를 갱신하고, 뒤이어 델타를 받을 창은
		///   「바뀐 게 없다」로 빈손이 된다(오늘 실제로 그랬다 — 그래서 없어진 자리가 영영 남았다).
		/// </summary>
		private readonly Dictionary<string, (long Version, Dictionary<int, int> Amounts, long WasVersion, Dictionary<int, int> Was)> byCell =
			new Dictionary<string, (long, Dictionary<int, int>, long, Dictionary<int, int>)>();

		/// <summary>한 창에게 이번 판에 무엇을 줄까.</summary>
		public readonly struct Choice
		{
			public Choice(bool whole, IReadOnlyList<int> changed, IReadOnlyList<int> gone)
			{
				Whole = whole;
				Changed = changed;
				Gone = gone;
			}

			/// <summary>통째로 주나 — 그러면 <see cref="Changed"/> 가 지금 있는 전부다.</summary>
			public bool Whole { get; }

			public IReadOnlyList<int> Changed { get; }

			public IReadOnlyList<int> Gone { get; }
		}

		/// <summary>
		/// 이 칸의 지금 모습을 적어 두고, <paramref name="windowSawVersion"/> 까지 받은 창에게 줄 것을 고른다.
		/// </summary>
		/// <param name="cell">칸 이름</param>
		/// <param name="nowVersion">이번 판 번호 (세계의 들판 판번호)</param>
		/// <param name="amounts">지금 이 칸에 있는 것 — 자리 번호 → 개수</param>
		/// <param name="windowSawVersion">그 창이 <b>마지막으로 받은</b> 판번호 (0 = 아직 아무것도 못 받음)</param>
		public Choice PickFor(string cell, long nowVersion, IReadOnlyDictionary<int, int> amounts, long windowSawVersion)
		{
			Dictionary<int, int> now = new Dictionary<int, int>(amounts.Count);
			foreach (KeyValuePair<int, int> one in amounts)
				now[one.Key] = one.Value;

			byCell.TryGetValue(cell, out (long Version, Dictionary<int, int> Amounts, long WasVersion, Dictionary<int, int> Was) last);

			// 판이 넘어갔으면 「바로 앞」을 한 칸 밀어 둔다 — 같은 판 안에서는 안 민다.
			if (last.Amounts == null)
				byCell[cell] = (nowVersion, now, 0, null);
			else if (last.Version != nowVersion)
				byCell[cell] = (nowVersion, now, last.Version, last.Amounts);

			byCell.TryGetValue(cell, out (long Version, Dictionary<int, int> Amounts, long WasVersion, Dictionary<int, int> Was) book);

			// 이미 이번 판까지 받은 창 — 줄 것이 없다.
			if (windowSawVersion == book.Version)
				return new Choice(false, new List<int>(), new List<int>());

			// 바로 앞 판까지 받은 창 → 그 사이의 델타.
			if (windowSawVersion > 0 && windowSawVersion == book.WasVersion && book.Was != null)
			{
				List<int> changed = new List<int>();
				List<int> gone = new List<int>();

				foreach (KeyValuePair<int, int> one in book.Amounts)
				{
					if (book.Was.TryGetValue(one.Key, out int was) && was == one.Value)
						continue;

					changed.Add(one.Key);
				}

				foreach (int id in book.Was.Keys)
				{
					if (book.Amounts.ContainsKey(id) == false)
						gone.Add(id);
				}

				return new Choice(false, changed, gone);
			}

			// 그 밖 — 처음 보거나 한 판이라도 건너뛴 창 → 통째로.
			return new Choice(true, new List<int>(book.Amounts.Keys), new List<int>());
		}
	}
}
