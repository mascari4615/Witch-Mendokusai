using System.Collections.Generic;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// <b>같은 일을 두 번 하지 않는다</b> (TASK-WM-305) — 순수 셈, 엔진 밖.
	///
	/// ★ 무엇이 빠져 있었나 (실측 2026-08-13): 줍기를 보낸 <b>그 순간</b> 회선이 끊기면
	///   그 줍기는 <b>조용히 사라진다</b>. 가방은 그대로, 들판도 그대로, 창은 아무 말도 안 한다
	///   (같은 순서로 회선을 안 끊으면 잘 주워진다 — 재는 자가 아니라 진짜 구멍이었다).
	///   사람은 「눌렀는데 안 됐다」만 남는다.
	///
	/// ★ 고치는 길: 창이 <b>답이 올 때까지 들고 있다가 다시 붙으면 또 보낸다</b>.
	///   그러면 이번엔 반대 위험이 생긴다 — 세계가 첫 번째도 받았고 다시 보낸 것도 받으면
	///   <b>두 번</b> 주워진다. 그래서 세계가 「이 번호는 이미 했다」를 기억해야 한다.
	///
	/// ★ 왜 사람(신원)마다인가: 줄이 끊기면 인형 번호는 그대로여도 <b>줄</b>은 새것이다.
	///   줄에 매달아 두면 다시 붙는 순간 기억이 없어져 두 번 하게 된다. 그래서 세계를 건너가도
	///   남는 <b>신원</b>에 매단다(같은 이유로 통행증도 신원에 매달았다 — TravelPass).
	///
	/// ★ 얼마나 기억하나: 사람마다 최근 <see cref="REMEMBER"/>개. 창은 답 못 받은 것만 다시 보내므로
	///   그 폭은 몇 개면 넉넉하다. 무한히 기억하면 그것도 새는 것이다.
	/// </summary>
	public sealed class ActionOnce
	{
		/// <summary>한 사람의 최근 몇 개를 기억하나.</summary>
		public const int REMEMBER = 64;

		private readonly object gate = new object();
		private readonly Dictionary<int, Queue<long>> doneBy = new Dictionary<int, Queue<long>>();
		private readonly Dictionary<int, HashSet<long>> quickLook = new Dictionary<int, HashSet<long>>();

		/// <summary>
		/// 이 사람의 이 번호를 <b>처음 보는가</b>. 처음이면 적어 두고 <c>true</c>,
		/// 이미 했던 것이면 <c>false</c>(그 일은 다시 하면 안 된다).
		///
		/// 번호가 0 이하 = 번호를 안 붙인 옛 창이다. 막지 않는다(하던 대로 한 번 한다).
		/// </summary>
		public bool FirstTime(int identityId, long actionId)
		{
			if (actionId <= 0)
				return true;

			lock (gate)
			{
				if (quickLook.TryGetValue(identityId, out HashSet<long> seen) == false)
				{
					seen = new HashSet<long>();
					quickLook[identityId] = seen;
					doneBy[identityId] = new Queue<long>();
				}

				if (seen.Add(actionId) == false)
					return false;

				Queue<long> order = doneBy[identityId];
				order.Enqueue(actionId);
				while (order.Count > REMEMBER)
					seen.Remove(order.Dequeue());

				return true;
			}
		}

		/// <summary>이 사람의 기억을 놓는다 (세계를 떠났다).</summary>
		public void Forget(int identityId)
		{
			lock (gate)
			{
				doneBy.Remove(identityId);
				quickLook.Remove(identityId);
			}
		}

		/// <summary>지금 몇 사람의 기억을 들고 있나 (재는 자를 위해).</summary>
		public int Count
		{
			get
			{
				lock (gate)
					return quickLook.Count;
			}
		}
	}
}
