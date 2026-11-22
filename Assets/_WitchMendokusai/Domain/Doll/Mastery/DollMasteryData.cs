using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct DollMasteryTrackData
	{
		[Tooltip("막대 이름 (빛, 그림자)")] public string DisplayName;
		[Tooltip("확정 카드가 열리는 조각 수"), Min(1)] public int Threshold;
		public Color Color;
		[Tooltip("조각을 먹을 때마다 판 안에서 오르는 스탯 (인물 고유 효과)")] public UnitStatType PickupStat;
		[Tooltip("조각 하나에 오르는 양")] public int PickupAmount;
		[Tooltip("조각으로 오르는 양의 판 안 상한. 0 이면 무제한")] public int PickupCap;
		[Tooltip("이 막대 조각 (MasteryShardObject 프리팹)")] public MasteryShardObject ShardPrefab;
		[Tooltip("판당 이 막대 조각 수"), Min(0)] public int ShardCount;
	}

	// 인물 숙련 막대 정의이자 고유 효과 (2026-11-22 21:03). 조각을 먹을 때마다 막대 스탯이 오르고, 막대가 차면 확정 카드가 열림
	// 막대 번호는 트리 JSON 의 requires mastery 번호, 조각 맵 오브젝트의 막대 번호와 같음
	// 판정은 DollMastery (DomainSDK), 판 상태는 DollMasteryRun
	[CreateAssetMenu(fileName = nameof(DollMasteryData), menuName = "WM/Variable/" + nameof(DollMasteryData))]
	public class DollMasteryData : ScriptableObject
	{
		[field: SerializeField] public List<DollMasteryTrackData> Tracks { get; private set; } = new();
		[field: SerializeField, Tooltip("첫 조각 등장 (판 경과 초)"), Min(0f)] public float FirstShardSeconds { get; private set; } = 10f;
		[field: SerializeField, Tooltip("조각 사이 간격 (초). 막대를 번갈아 하나씩"), Min(0.1f)] public float ShardGapSeconds { get; private set; } = 20f;
		[field: SerializeField, Tooltip("주인공과 거리 범위 (m)")] public Vector2 ShardDistance { get; private set; } = new(8f, 16f);
		[field: SerializeField, Tooltip("조각 자리 난수 갈래 값. 다른 판 시드 소비자와 겹치지 않게")] public int ShardSeedSalt { get; private set; } = 7411;
		[field: SerializeField, Tooltip("막대가 찼을 때 머리 위 글자. {0} 막대 이름")] public string ReadyTextFormat { get; private set; } = "{0} 숙련 완료";

		// 조각이 있는 막대에 프리팹이 없으면 판 중에 터지므로 판 시작에 멈춤
		public void ThrowIfInvalid()
		{
			List<string> errors = new();
			if (Tracks.Count == 0)
				errors.Add($"{name}: 막대 없음");
			for (int i = 0; i < Tracks.Count; i++)
			{
				if (Tracks[i].ShardCount > 0 && Tracks[i].ShardPrefab == null)
					errors.Add($"{name}: 막대 {i} 조각 {Tracks[i].ShardCount}개인데 ShardPrefab 없음");
			}
			if (ShardDistance.x <= 0f || ShardDistance.y < ShardDistance.x)
				errors.Add($"{name}: {nameof(ShardDistance)} 범위 오류 {ShardDistance}");
			if (errors.Count > 0)
				throw new InvalidOperationException(string.Join("\n", errors));
		}

		public List<int> ShardCounts()
		{
			List<int> counts = new(Tracks.Count);
			foreach (DollMasteryTrackData track in Tracks)
				counts.Add(track.ShardCount);
			return counts;
		}

		public DollMasteryShardSchedule CreateSchedule() => new(ShardCounts(), FirstShardSeconds, ShardGapSeconds);

		public List<int> Thresholds()
		{
			List<int> thresholds = new(Tracks.Count);
			foreach (DollMasteryTrackData track in Tracks)
				thresholds.Add(track.Threshold);
			return thresholds;
		}
	}
}
