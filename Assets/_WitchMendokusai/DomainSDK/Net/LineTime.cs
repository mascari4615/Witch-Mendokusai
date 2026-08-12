using System.Collections.Generic;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>이 사람의 회선이 얼마나 먼가</b> — 세계가 스스로 잰다 (TASK-WM-303).
	///
	/// ★ 왜 세계가 재는가: 창이 말한 값을 믿으면 「나 300ms 밀려요」라고 우겨 되감기를 늘릴 수 있다.
	///   그건 남을 <b>맞은 걸로 만드는</b> 힘이다. 그래서 회선은 세계만 잰다.
	///
	/// ★ 어떻게: 이미 오가는 말을 쓴다. 세계는 걸음마다 「여기까지 봤다」를 보낸다(stepseen, WM-271).
	///   창이 다음 걸음에 「그거 받았다」를 얹어 보내면, 그 왕복 시간이 곧 회선이다 —
	///   새 말을 안 만들고, 창의 시계를 안 믿는다(양쪽 다 <b>세계의 시계</b>로만 잰다).
	///
	/// ★ 왜 부드럽게(평활): 한 번의 왕복은 흔들린다(지터·밀림). 그대로 쓰면 되감기가 춤춘다.
	///   그래서 새 값을 <see cref="SMOOTHING"/> 만큼만 섞는다.
	///
	/// 되감기는 <see cref="MOST_REWIND_MS"/> 로 묶는다 — 회선이 아무리 멀어도 세계가 너무 옛날로는
	/// 안 돌아간다(맞은 사람이 「난 벌써 피했는데」라고 느끼는 자리라 한도가 필요하다).
	/// </summary>
	public sealed class LineTime
	{
		/// <summary>새 왕복값을 얼마나 섞나 (0~1) — 작을수록 잔잔하고 굼뜨다.</summary>
		public const float SMOOTHING = 0.25f;

		/// <summary>되감아 줄 수 있는 최대 (ms).</summary>
		public const long MOST_REWIND_MS = 250;

		private readonly object gate = new object();
		private readonly Dictionary<int, long> sentAt = new Dictionary<int, long>();
		private readonly Dictionary<int, float> roundTrips = new Dictionary<int, float>();

		/// <summary>이 사람에게 <paramref name="token"/> 표를 이 시각에 보냈다.</summary>
		public void Told(int dollId, int token, long nowMs)
		{
			lock (gate)
				sentAt[Key(dollId, token)] = nowMs;
		}

		/// <summary>그 표를 되받았다 — 왕복이 여기서 나온다. 모르는 표면 <c>false</c>.</summary>
		public bool HeardBack(int dollId, int token, long nowMs)
		{
			lock (gate)
			{
				int key = Key(dollId, token);
				if (sentAt.TryGetValue(key, out long when) == false)
					return false;

				sentAt.Remove(key);

				long roundTrip = nowMs - when;
				if (roundTrip < 0)
					return false;

				if (roundTrips.TryGetValue(dollId, out float smoothed) == false)
					roundTrips[dollId] = roundTrip;
				else
					roundTrips[dollId] = smoothed + (roundTrip - smoothed) * SMOOTHING;

				return true;
			}
		}

		/// <summary>이 사람의 화면이 얼마나 옛것인가 (ms) — 왕복의 절반, 한도까지.</summary>
		public long RewindMsFor(int dollId)
		{
			lock (gate)
			{
				if (roundTrips.TryGetValue(dollId, out float roundTrip) == false)
					return 0;

				long half = (long)(roundTrip / 2f);
				if (half < 0)
					return 0;

				return half > MOST_REWIND_MS ? MOST_REWIND_MS : half;
			}
		}

		/// <summary>세계에서 나간 사람은 놓는다.</summary>
		public void Forget(int dollId)
		{
			lock (gate)
			{
				roundTrips.Remove(dollId);
				List<int> mine = new List<int>();
				foreach (KeyValuePair<int, long> entry in sentAt)
				{
					if (entry.Key >> 8 == dollId)
						mine.Add(entry.Key);
				}

				for (int i = 0; i < mine.Count; i += 1)
					sentAt.Remove(mine[i]);
			}
		}

		private static int Key(int dollId, int token)
		{
			// 표는 걸음 번호다 — 사람마다 따로 세므로 사람 번호와 함께 묶는다.
			return (dollId << 8) | (token & 0xFF);
		}
	}
}
