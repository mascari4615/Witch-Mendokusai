using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적에게 붙는 짧은 표식과 적중 횟수. 표식 이름 (key) 별로 따로 셈
	// 몬스터는 풀에서 다시 쓰여 같은 참조가 새 적이 되므로, 셈은 마지막 적중에서 resetSeconds 가 지나면 처음부터. 판 시작에 전부 비움 (GameManager.InitEquipment)
	// 쓰는 곳: 알리사 완드 검과 빛탄 연계 (MarkOnHit, MarkAmplify), 빛탄 3타 (NthHitBonus)
	public static class HitMarks
	{
		private readonly struct HitCountEntry
		{
			public readonly int Count;
			public readonly float LastTime;

			public HitCountEntry(int count, float lastTime)
			{
				Count = count;
				LastTime = lastTime;
			}
		}

		private static readonly Dictionary<(int key, UnitObject victim), float> expireTimes = new();
		private static readonly Dictionary<(int key, UnitObject victim), HitCountEntry> hitCounts = new();
		private static readonly List<(int key, UnitObject victim)> stale = new();
		private const int CLEANUP_SIZE = 512;
		// 정리 때 셈을 버리는 기준. 쓰는 쪽 resetSeconds 보다 길게
		private const float COUNT_KEEP_SECONDS = 30f;

		public static void Clear()
		{
			expireTimes.Clear();
			hitCounts.Clear();
		}

		public static void Mark(int key, UnitObject victim, float seconds)
		{
			Cleanup();
			expireTimes[(key, victim)] = Time.time + seconds;
		}

		public static bool IsMarked(int key, UnitObject victim) => expireTimes.TryGetValue((key, victim), out float expireTime) && expireTime > Time.time;

		public static int HitCount(int key, UnitObject victim, float resetSeconds)
		{
			if (hitCounts.TryGetValue((key, victim), out HitCountEntry entry) == false || Time.time - entry.LastTime > resetSeconds)
				return 0;
			return entry.Count;
		}

		public static void AddHit(int key, UnitObject victim, float resetSeconds)
		{
			Cleanup();
			hitCounts[(key, victim)] = new HitCountEntry(HitCount(key, victim, resetSeconds) + 1, Time.time);
		}

		// 표가 커지면 지난 표식과 오래된 셈을 걷음
		private static void Cleanup()
		{
			if (expireTimes.Count + hitCounts.Count < CLEANUP_SIZE)
				return;
			float now = Time.time;
			stale.Clear();
			foreach (KeyValuePair<(int key, UnitObject victim), float> pair in expireTimes)
			{
				if (pair.Key.victim == null || pair.Value <= now)
					stale.Add(pair.Key);
			}
			foreach ((int key, UnitObject victim) entry in stale)
				expireTimes.Remove(entry);
			stale.Clear();
			foreach (KeyValuePair<(int key, UnitObject victim), HitCountEntry> pair in hitCounts)
			{
				if (pair.Key.victim == null || now - pair.Value.LastTime > COUNT_KEEP_SECONDS)
					stale.Add(pair.Key);
			}
			foreach ((int key, UnitObject victim) entry in stale)
				hitCounts.Remove(entry);
			stale.Clear();
		}
	}
}
