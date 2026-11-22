using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 인물 숙련 막대의 판 하나 상태. 정본 규칙
	// - 막대마다 조각을 모아 문턱에 닿으면 그 갈래 확정 카드가 열림 (준비)
	// - 먼저 찬 쪽을 바로 고를지, 다른 쪽도 채울지는 플레이어 몫. 두 쪽 다 준비될 수 있음
	// - 한 갈래를 확정하면 조각을 더 받지 않음
	public sealed class DollMastery
	{
		public const int NO_TRACK = -1;

		private readonly int[] thresholds;
		private readonly int[] progress;

		public DollMastery(IReadOnlyList<int> trackThresholds)
		{
			if (trackThresholds == null || trackThresholds.Count == 0)
				throw new ArgumentException("숙련 막대가 없음");
			thresholds = new int[trackThresholds.Count];
			for (int i = 0; i < trackThresholds.Count; i++)
			{
				if (trackThresholds[i] < 1)
					throw new ArgumentException($"막대 {i} 문턱 {trackThresholds[i]} 은 1 이상");
				thresholds[i] = trackThresholds[i];
			}
			progress = new int[thresholds.Length];
		}

		public int TrackCount => thresholds.Length;
		public int ConfirmedTrack { get; private set; } = NO_TRACK;
		public bool AcceptsShards => ConfirmedTrack == NO_TRACK;

		public int Progress(int track) => progress[track];
		public int Threshold(int track) => thresholds[track];
		public bool IsReady(int track) => progress[track] >= thresholds[track];

		// 조각 스탯 (고유 효과) 으로 이번에 더할 양. 지금까지 준 양과 합쳐 상한을 넘지 않게 자름. 상한 0 이하는 무제한
		public static int PickupGrant(int alreadyGranted, int amount, int cap)
		{
			if (amount <= 0)
				return 0;
			if (cap <= 0)
				return amount;
			return Math.Max(0, Math.Min(amount, cap - alreadyGranted));
		}

		// 막대가 이번에 처음 찼으면 true. 확정 뒤나 이미 찬 막대는 더 쌓지 않음
		public bool AddShard(int track, int amount)
		{
			if (amount < 1)
				throw new ArgumentException($"조각 양 {amount} 은 1 이상");
			if (AcceptsShards == false || IsReady(track))
				return false;
			progress[track] = Math.Min(thresholds[track], progress[track] + amount);
			return IsReady(track);
		}

		// 준비 안 된 갈래는 확정할 수 없음
		public void Confirm(int track)
		{
			if (IsReady(track) == false)
				throw new InvalidOperationException($"막대 {track} 이 아직 안 참 ({progress[track]}/{thresholds[track]})");
			if (AcceptsShards == false)
				throw new InvalidOperationException($"이미 막대 {ConfirmedTrack} 로 확정");
			ConfirmedTrack = track;
		}
	}
}
