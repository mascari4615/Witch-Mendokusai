using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// WorldSim.cs 의 Action 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 채집, 소비, 타격, 이동.
	public sealed partial class WorldSim
	{
		/// <summary>
		/// 줍기 — 서버가 가방 규칙으로 넣는다. <b>못 넣고 남은 개수</b>를 돌려준다(가방이 꽉 찼을 때).
		/// </summary>
		public int TryGather(int dollId, IItemData itemData, int amount)
		{
			if (itemData == null)
				return amount;

			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return amount;

				return doll.Bag.Add(itemData, amount);
			}
		}

		/// <summary>그 인형이 이만큼을 받을 자리가 있나 — 되돌릴 수 없는 보상 전에 묻는다.</summary>
		public bool CanReceive(int dollId, IItemData itemData, int amount)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) && doll.Bag.CanReceive(itemData, amount);
			}
		}

		/// <summary>그 인형이 그 아이템을 몇 개 가졌나.</summary>
		public int BagCount(int dollId, int itemId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Bag.CountById(itemId) : 0;
			}
		}

		/// <summary>
		/// 그 인형의 가방 전부 (TASK-WM-217 — 창이 진짜 가방을 보이려면 필요하다).
		/// ⚠ 전에는 서버가 <b>아는 아이템 두 종류만</b> 물어 봤다 — 나머지는 가방에 있어도 창에 안 보였다.
		/// </summary>
		/// <summary>그 사람의 몸 — 없으면 0 (TASK-WM-258).</summary>
		public int HealthOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Health : 0;
			}
		}

		public List<BagSaveEntry> BagOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.SaveBag() : new List<BagSaveEntry>();
			}
		}

		/// <summary>제작 등으로 재료를 쓴다. 못 쓰고 남은 개수를 돌려준다.</summary>
		public int TryConsume(int dollId, int itemId, int amount)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Bag.Consume(itemId, amount) : amount;
			}
		}

		/// <summary>
		/// 때린다 (TASK-WM-251) — 판정은 <see cref="Net.StrikeRule"/> 이 한다.
		/// 되는 경우에만 몸이 줄고, 0 이 되면 그 사람은 <b>다시 세워진다</b>(원점·가득 찬 몸).
		/// </summary>
		public Net.StrikeRule.Denial TryStrike(int attackerId, int targetId, long nowMs,
			out int healthLeft, out bool wentDown)
		{
			return TryStrike(attackerId, targetId, nowMs, null, 0, out healthLeft, out wentDown);
		}

		/// <summary>
		/// 때린다 — 다만 <b>때린 사람이 보고 있던 순간</b>으로 되감아 판정한다 (TASK-WM-303).
		///
		/// ★ 왜: 회선이 먼 사람의 화면은 <paramref name="rewindMs"/> 만큼 옛것이다. 그 사람이 화면에서
		///   닿는 것을 보고 휘둘렀는데 세계가 <b>지금</b> 자리로 재면, 그 사이 움직인 만큼 늘 헛친다 —
		///   손해가 회선에 비례해 자란다(실측: 곧은 46번 · 100ms 58번 · 250ms 70번).
		///
		/// ★ 무엇이 안 흔들리나: 되감는 것은 <b>남의 자리</b>뿐이다. 거리·간격·대상 규칙과
		///   때린 사람 자리는 그대로다 — 창이 우겨서 얻는 것은 여전히 없다
		///   (회선은 세계가 재고, 되감기는 <see cref="Net.LineTime.MOST_REWIND_MS"/> 로 묶인다).
		/// </summary>
		public Net.StrikeRule.Denial TryStrike(int attackerId, int targetId, long nowMs,
			Net.PastPlaces past, long rewindMs, out int healthLeft, out bool wentDown)
		{
			healthLeft = 0;
			wentDown = false;

			lock (gate)
			{
				if (dolls.TryGetValue(attackerId, out WorldDoll attacker) == false)
					return Net.StrikeRule.Denial.NoSuchOne;

				bool targetExists = dolls.TryGetValue(targetId, out WorldDoll target);
				Vector3 to = targetExists ? target.Position : Vector3.zero;

				// 되감을 것이 있고 그 순간을 기억하고 있으면, <b>그때</b>의 자리로 잰다.
				if (targetExists && past != null && rewindMs > 0
					&& past.Where(targetId, nowMs - rewindMs, out Vector3 wasAt))
				{
					to = wasAt;
				}
				int health = targetExists ? target.Health : 0;

				Net.StrikeRule.Denial why = Net.StrikeRule.CanStrike(attackerId, targetId, targetExists,
					attacker.Position, to, health, attacker.LastStruckMs, nowMs);
				if (why != Net.StrikeRule.Denial.None)
					return why;

				attacker.LastStruckMs = nowMs;
				target.Health = Net.StrikeRule.HealthAfterHit(target.Health);
				healthLeft = target.Health;

				if (target.Health <= 0)
				{
					// ⚠ 쓰러진 채로 두면 그 사람은 <b>게임에서 나간 것</b>이 된다. 뼈대에서는 곧바로 세운다 —
					//   어떻게 되살아날지(자리·기다림·잃는 것)는 게임이 정할 몫이다.
					target.Health = Net.StrikeRule.FULL_HEALTH;
					target.Position = Vector3.zero;
					healthLeft = target.Health;
					wentDown = true;
				}

				return Net.StrikeRule.Denial.None;
			}
		}

		/// <summary>
		/// 이 세계가 <b>맡은 땅</b> (TASK-WM-252). 기본은 온 세상 — 안 나눈 세계는 그대로 돈다.
		/// 나뉜 세계에서는 사람이 이 밖으로 못 나간다: 남의 땅을 내가 굴리면 두 세계가 갈라진다.
		/// </summary>
		public Net.ZonePatch Patch { get; set; } = Net.ZonePatch.Everywhere;

		public bool TryMove(int dollId, Vector3 delta)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return false;

				Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
				doll.Position = Patch.Clamp(doll.Position + clamped);
				return true;
			}
		}
	}
}


