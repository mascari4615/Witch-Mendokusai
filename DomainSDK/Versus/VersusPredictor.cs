using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 창이 <b>서버를 기다리지 않고</b> 자기 판을 굴리게 한다 (TASK-WM-411, 롤백 넷코드).
	///
	/// ★ 왜 근본인가: 심판이 저쪽에 있으면 내 조작은 왕복 시간만큼 늦게 반응한다. 100ms 면 손맛이 통째로 죽는다.
	///   그렇다고 창이 제멋대로 판정하면 두 화면이 갈린다. 답은 하나 —
	///   <b>창이 미리 굴리고(예측), 서버가 「그 틱은 사실 이랬다」를 보내면 되감아 다시 굴린다(롤백).</b>
	///   판정이 결정론이라(같은 입력 → 같은 답) 이 되감기가 정확히 맞아떨어진다.
	///
	/// 상대의 의도는 알 수 없으므로 <b>마지막에 본 것이 계속된다</b>고 가정한다(격투게임 롤백의 표준).
	/// 틀리면 서버 스냅샷이 오는 순간 바로잡힌다 — 그래서 「틀려도 되는 추측」이다.
	/// </summary>
	public sealed class VersusPredictor
	{
		/// <summary>되감을 수 있는 최대 깊이(틱). 60Hz 기준 0.5초 — 그보다 늦게 온 정정은 못 되감고 그대로 앉힌다.</summary>
		public const int HISTORY = 30;

		private readonly VersusRoundState round;
		private readonly int mySeat;
		private readonly Queue<int> historyTicks = new Queue<int>();
		private readonly Dictionary<int, VersusInputFrame> myInputs = new Dictionary<int, VersusInputFrame>();
		private readonly Dictionary<int, VersusInputFrame> theirInputs = new Dictionary<int, VersusInputFrame>();
		private readonly VersusInputFrame[] frames = new VersusInputFrame[MatchConstants.VERSUS_PLAYER_COUNT];

		private VersusInputFrame lastSeenOpponentFrame;

		public VersusPredictor(VersusRoundState round, int mySeat)
		{
			this.round = round;
			this.mySeat = mySeat;
		}

		/// <summary>지금 창이 그리고 있는 틱.</summary>
		public int CurrentTick { get; private set; }

		/// <summary>지금까지 몇 번 되감았나 — 회선이 나쁜지 화면에 보여 주거나 로그로 잴 때.</summary>
		public int RollbackCount { get; private set; }

		/// <summary>마지막 정정에서 몇 틱을 되감았나.</summary>
		public int LastRollbackDepth { get; private set; }

		/// <summary> 한 틱 굴린다 — 내 의도는 지금 것, 상대 의도는 마지막에 본 것. </summary>
		public void Step(VersusInputFrame mine, float timeLimitSeconds)
		{
			CurrentTick++;
			Remember(myInputs, CurrentTick, mine);
			Remember(theirInputs, CurrentTick, lastSeenOpponentFrame);
			Forget();

			frames[mySeat] = mine;
			frames[1 - mySeat] = lastSeenOpponentFrame;
			round.Step(frames, timeLimitSeconds);
		}

		/// <summary>
		/// 상대가 그 틱에 무엇을 했는지 뒤늦게 알았다 — 기억해 둔다. 되감을 때 이 값이 쓰인다.
		/// </summary>
		public void ObserveOpponent(int tick, VersusInputFrame frame)
		{
			theirInputs[tick] = frame;
			lastSeenOpponentFrame = frame;
		}

		/// <summary>
		/// 서버가 보낸 <b>정본 스냅샷</b>을 얹는다. 그 틱으로 되감고, 기억해 둔 입력으로 지금 틱까지 다시 굴린다.
		/// 되감기가 필요 없을 만큼 오래된 것(또는 미래)이면 아무 일도 안 한다.
		/// </summary>
		public void ApplyAuthoritative(VersusRoundSnapshot snapshot, float timeLimitSeconds)
		{
			if (snapshot == null)
				return;

			// 너무 늦게 온 정정은 되감을 기억이 없다 — 그 자리에 그대로 앉히고 현재를 그 틱으로 삼는다.
			if (snapshot.tick < CurrentTick - HISTORY)
			{
				round.Restore(snapshot);
				CurrentTick = snapshot.tick;
				myInputs.Clear();
				theirInputs.Clear();
				historyTicks.Clear();
				RollbackCount++;
				LastRollbackDepth = HISTORY;
				return;
			}

			int replayTo = CurrentTick;
			LastRollbackDepth = replayTo - snapshot.tick;

			round.Restore(snapshot);
			CurrentTick = snapshot.tick;

			if (LastRollbackDepth <= 0)
				return;

			RollbackCount++;

			for (int tick = snapshot.tick + 1; tick <= replayTo; tick++)
			{
				CurrentTick = tick;
				frames[mySeat] = Recall(myInputs, tick);
				frames[1 - mySeat] = Recall(theirInputs, tick);
				round.Step(frames, timeLimitSeconds);
			}
		}

		private void Remember(Dictionary<int, VersusInputFrame> into, int tick, VersusInputFrame frame)
		{
			into[tick] = frame;
			historyTicks.Enqueue(tick);
		}

		// 오래된 기억은 버린다 — 안 버리면 한 판 내내 쌓인다.
		private void Forget()
		{
			while (historyTicks.Count > HISTORY * MatchConstants.VERSUS_PLAYER_COUNT)
			{
				int old = historyTicks.Dequeue();

				if (old > CurrentTick - HISTORY)
					continue;

				myInputs.Remove(old);
				theirInputs.Remove(old);
			}
		}

		private static VersusInputFrame Recall(Dictionary<int, VersusInputFrame> from, int tick)
		{
			return from.TryGetValue(tick, out VersusInputFrame frame) ? frame : default;
		}
	}
}
