using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 숙련 조각을 언제, 어느 막대 것으로 놓을지의 정본 (사용자 결정 2026-11-22 22:07: 종류별 고정 수, 일정 간격)
	// 막대를 번갈아 하나씩. 한쪽이 다 나오면 남은 쪽만. 시각은 판 경과 초
	public sealed class DollMasteryShardSchedule
	{
		private readonly List<int> tracks = new List<int>();
		private readonly float firstSeconds;
		private readonly float gapSeconds;

		public DollMasteryShardSchedule(IReadOnlyList<int> countPerTrack, float firstSeconds, float gapSeconds)
		{
			if (countPerTrack == null || countPerTrack.Count == 0)
				throw new ArgumentException("막대가 없음");
			if (firstSeconds < 0f || gapSeconds <= 0f)
				throw new ArgumentException($"첫 등장 {firstSeconds} 는 0 이상, 간격 {gapSeconds} 는 0 보다 커야 함");
			int[] remaining = new int[countPerTrack.Count];
			for (int i = 0; i < countPerTrack.Count; i++)
			{
				if (countPerTrack[i] < 0)
					throw new ArgumentException($"막대 {i} 조각 수 {countPerTrack[i]} 은 0 이상");
				remaining[i] = countPerTrack[i];
			}
			bool placed = true;
			while (placed)
			{
				placed = false;
				for (int track = 0; track < remaining.Length; track++)
				{
					if (remaining[track] == 0)
						continue;
					tracks.Add(track);
					remaining[track]--;
					placed = true;
				}
			}
			this.firstSeconds = firstSeconds;
			this.gapSeconds = gapSeconds;
		}

		public int Count => tracks.Count;
		public int TrackAt(int index) => tracks[index];
		public float TimeAt(int index) => firstSeconds + gapSeconds * index;

		// 경과 초까지 놓였어야 할 조각 수
		public int DueCount(float elapsedSeconds)
		{
			if (elapsedSeconds < firstSeconds)
				return 0;
			int due = (int)Math.Floor((elapsedSeconds - firstSeconds) / gapSeconds) + 1;
			return Math.Min(due, tracks.Count);
		}
	}
}
