using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>사람이 조금 전에 어디 있었나</b> — 세계가 짧은 과거를 기억한다 (TASK-WM-303).
	///
	/// ★ 왜 필요한가 (실측 2026-08-13): 때리기 판정은 <b>지금</b>의 자리로 한다. 그런데 때리는 사람이
	///   본 것은 회선만큼 <b>옛날</b> 화면이고, 그 손짓이 세계에 닿기까지 또 회선만큼 걸린다.
	///   그래서 회선이 나쁜 사람은 자기 화면이 「닿는다」고 해도 세계는 「멀다」고 한다.
	///   같은 싸움에 드는 손짓 — 곧은 회선 46번 · 지연 100ms 58번 · 지연 250ms <b>70번</b>.
	///   회선이 나쁠수록 <b>더</b> 손해다(격차가 회선에 비례해 자란다).
	///
	/// ★ 고치는 자리: 판정을 <b>때린 사람이 보고 있던 순간</b>으로 되감아서 한다.
	///   그러려면 세계가 그 순간의 자리를 알고 있어야 한다 — 그것이 이 기억이다.
	///   되감기는 <see cref="KEEP_MS"/> 까지만 남긴다: 더 옛날은 「지금 싸움」이 아니다.
	///
	/// ★ 왜 사이를 이어 재는가(보간): 자리를 적는 간격(판)과 되감을 순간은 안 맞는다.
	///   가장 가까운 것 하나만 쓰면 판 간격만큼(50ms = 0.15m) 계단이 생긴다 —
	///   그 계단이 바로 판정 오차라서, 두 점 사이를 곧게 이어 읽는다.
	///
	/// 순수 셈이다 — 엔진 밖. 서버가 정본으로 쓰고 유니티도 같은 답을 낸다.
	/// </summary>
	public sealed class PastPlaces
	{
		/// <summary>얼마나 옛날까지 기억하나 (ms) — 이보다 옛 순간은 되감아 주지 않는다.</summary>
		public const long KEEP_MS = 1000;

		private readonly object gate = new object();
		private readonly Dictionary<int, List<Moment>> trails = new Dictionary<int, List<Moment>>();

		private struct Moment
		{
			public long AtMs;
			public Vector3 Where;
		}

		/// <summary>지금 이 사람이 여기 있었다고 적는다.</summary>
		public void Remember(int dollId, long nowMs, Vector3 where)
		{
			lock (gate)
			{
				if (trails.TryGetValue(dollId, out List<Moment> trail) == false)
				{
					trail = new List<Moment>();
					trails[dollId] = trail;
				}

				// 시계가 뒤로 갔으면 앞의 것을 지운다 — 뒤죽박죽인 줄에서 읽으면 없는 자리가 나온다.
				if (trail.Count > 0 && nowMs < trail[trail.Count - 1].AtMs)
					trail.Clear();

				trail.Add(new Moment { AtMs = nowMs, Where = where });

				int drop = 0;
				while (drop < trail.Count - 1 && nowMs - trail[drop].AtMs > KEEP_MS)
					drop += 1;

				if (drop > 0)
					trail.RemoveRange(0, drop);
			}
		}

		/// <summary>이 사람은 <paramref name="atMs"/> 에 어디 있었나. 모르면 <c>false</c>.</summary>
		public bool Where(int dollId, long atMs, out Vector3 place)
		{
			place = Vector3.zero;

			lock (gate)
			{
				if (trails.TryGetValue(dollId, out List<Moment> trail) == false || trail.Count == 0)
					return false;

				// 기억보다 옛날 = 가장 옛 것 · 기억보다 나중 = 가장 최근 것 (없는 자리를 지어내지 않는다).
				if (atMs <= trail[0].AtMs)
				{
					place = trail[0].Where;
					return true;
				}

				Moment last = trail[trail.Count - 1];
				if (atMs >= last.AtMs)
				{
					place = last.Where;
					return true;
				}

				for (int i = 1; i < trail.Count; i += 1)
				{
					Moment after = trail[i];
					if (after.AtMs < atMs)
						continue;

					Moment before = trail[i - 1];
					long span = after.AtMs - before.AtMs;
					if (span <= 0)
					{
						place = after.Where;
						return true;
					}

					float howFar = (atMs - before.AtMs) / (float)span;
					place = before.Where + (after.Where - before.Where) * howFar;
					return true;
				}

				place = last.Where;
				return true;
			}
		}

		/// <summary>세계에서 나간 사람은 기억도 놓는다 (안 놓으면 기억이 무한히 자란다).</summary>
		public void Forget(int dollId)
		{
			lock (gate)
				trails.Remove(dollId);
		}

		/// <summary>지금 몇 사람의 발자국을 들고 있나 (재는 자를 위해).</summary>
		public int Count
		{
			get
			{
				lock (gate)
					return trails.Count;
			}
		}
	}
}
