using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적에게 붙는 짧은 표식과 적중 횟수. 표식 이름 (key) 별로 따로 셈. 죽은 적은 다음 정리 때 빠짐
	// 쓰는 곳: 알리사 완드 검과 빛탄 연계 (MarkOnHit, MarkAmplify), 빛탄 3타 (NthHitBonus)
	public static class HitMarks
	{
		private static readonly Dictionary<(int key, UnitObject victim), float> expireTimes = new();
		private static readonly Dictionary<(int key, UnitObject victim), int> hitCounts = new();
		private static readonly List<(int key, UnitObject victim)> stale = new();
		private const int CLEANUP_SIZE = 512;

		public static void Mark(int key, UnitObject victim, float seconds)
		{
			Cleanup();
			expireTimes[(key, victim)] = Time.time + seconds;
		}

		public static bool IsMarked(int key, UnitObject victim) => expireTimes.TryGetValue((key, victim), out float expireTime) && expireTime > Time.time;

		public static int HitCount(int key, UnitObject victim) => hitCounts.TryGetValue((key, victim), out int count) ? count : 0;

		public static void AddHit(int key, UnitObject victim)
		{
			Cleanup();
			hitCounts[(key, victim)] = HitCount(key, victim) + 1;
		}

		// 표가 커지면 죽었거나 사라진 적을 걷음
		private static void Cleanup()
		{
			if (expireTimes.Count + hitCounts.Count < CLEANUP_SIZE)
				return;
			stale.Clear();
			foreach ((int key, UnitObject victim) entry in expireTimes.Keys)
			{
				if (entry.victim == null || entry.victim.IsAlive == false || expireTimes[entry] <= Time.time)
					stale.Add(entry);
			}
			foreach ((int key, UnitObject victim) entry in stale)
				expireTimes.Remove(entry);
			stale.Clear();
			foreach ((int key, UnitObject victim) entry in hitCounts.Keys)
			{
				if (entry.victim == null || entry.victim.IsAlive == false)
					stale.Add(entry);
			}
			foreach ((int key, UnitObject victim) entry in stale)
				hitCounts.Remove(entry);
			stale.Clear();
		}
	}
}
