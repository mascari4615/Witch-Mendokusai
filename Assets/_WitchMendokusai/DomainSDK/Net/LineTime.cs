using System.Collections.Generic;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>이 사람의 회선이 얼마나 먼가</b> — 세계가 스스로 잰다 (TASK-WM-303).
	///
	/// ★ 왜 세계가 재는가: 창이 말한 값을 믿으면 「나 300ms 밀려요」라고 우겨 되감기를 늘릴 수 있다.
	///   그건 남을 <b>맞은 걸로 만드는</b> 힘이다. 그래서 회선은 세계만 잰다.
	///
	/// ★ 어떻게: 세계가 <b>제 시계 도장</b>을 그림마다 찍는다(`at`). 창은 마지막으로 본 도장을
	///   자기가 보내는 말(걸음·때리기)에 그대로 얹는다. 왕복 = 지금 − 그 도장이다.
	///   양쪽 다 <b>세계의 시계</b>다 — 창의 시계는 한 번도 안 쓴다.
	///
	/// ★ 왜 걸음(stepseen)이 아니라 그림인가 (실측 2026-08-13): 걸음 답장은 <b>걷는 사람에게만</b> 간다.
	///   가만히 서서 때리는 사람은 회선이 영영 0으로 남아 되감기를 못 받았다 — 배선해 놓고도
	///   격차가 그대로였다(58 → 57). 그림은 <b>모두에게</b> 가므로 서 있는 사람도 재어진다.
	///
	/// ★ 우기면 얻는 것 (솔직히): 창이 <b>일부러 옛 도장</b>을 얹으면 되감기를 늘릴 수 있다.
	///   다만 그 이득은 <see cref="MOST_REWIND_MS"/> 까지다 — 회선이 500ms 인 <b>정직한</b> 사람이
	///   받는 것과 똑같다. 즉 우겨서 갈 수 있는 가장 먼 곳이 「나쁜 회선인 척」이다.
	///   더 조이려면 세계가 낸 도장을 통째로 기억해야 하는데, 그건 사람 수 × 판만큼 자란다.
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

		/// <summary>이보다 긴 왕복은 안 믿는다 (ms) — 한참 전 도장을 얹어도 회선으로 안 쳐 준다.</summary>
		public const long MOST_BELIEVABLE_MS = 5000;

		private readonly object gate = new object();
		private readonly Dictionary<int, float> roundTrips = new Dictionary<int, float>();

		/// <summary>
		/// 창이 <paramref name="stampMs"/> 도장을 되돌려 줬다 — 왕복이 여기서 나온다.
		/// 말이 안 되는 도장(미래·너무 옛것)은 안 받는다.
		/// </summary>
		public bool HeardStamp(int dollId, long stampMs, long nowMs)
		{
			long roundTrip = nowMs - stampMs;
			if (roundTrip < 0 || roundTrip > MOST_BELIEVABLE_MS)
				return false;

			lock (gate)
			{
				if (roundTrips.TryGetValue(dollId, out float smoothed) == false)
					roundTrips[dollId] = roundTrip;
				else
					roundTrips[dollId] = smoothed + (roundTrip - smoothed) * SMOOTHING;
			}

			return true;
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
				roundTrips.Remove(dollId);
		}

	}
}
