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
	}

	// 인물 숙련 막대 정의이자 고유 효과 (2026-11-22 21:03). 조각을 먹을 때마다 막대 스탯이 오르고, 막대가 차면 확정 카드가 열림
	// 막대 번호는 트리 JSON 의 requires mastery 번호, 조각 맵 오브젝트의 막대 번호와 같음
	// 판정은 DollMastery (DomainSDK), 판 상태는 DollMasteryRun
	[CreateAssetMenu(fileName = nameof(DollMasteryData), menuName = "WM/Variable/" + nameof(DollMasteryData))]
	public class DollMasteryData : ScriptableObject
	{
		[field: SerializeField] public List<DollMasteryTrackData> Tracks { get; private set; } = new();
		[field: SerializeField, Tooltip("조각 배치. 던전 배치 예산과 같은 형식, 이 인물로 들어간 판에만 더해짐")] public MapObjectBudget ShardBudget { get; private set; }
		[field: SerializeField, Tooltip("조각끼리 등장 시각 최소 간격 (초). 맵 오브젝트 공용 간격 대신"), Min(0f)] public float ShardGapSeconds { get; private set; } = 15f;
		[field: SerializeField, Tooltip("막대가 찼을 때 머리 위 글자. {0} 막대 이름")] public string ReadyTextFormat { get; private set; } = "{0} 숙련 완료";

		// 조각 오브젝트가 없는 막대를 가리키면 줍는 순간 터지므로 판 시작에 멈춤
		public void ThrowIfInvalid()
		{
			List<string> errors = new();
			if (Tracks.Count == 0)
				errors.Add($"{name}: 막대 없음");
			if (ShardBudget != null)
			{
				foreach (MapObjectData candidate in ShardBudget.Candidates)
				{
					if (candidate == null)
						continue;
					foreach (MapObjectEffect effect in candidate.Effects)
					{
						if (effect != null && effect.Type == MapObjectEffectType.MasteryShard && effect.MasteryTrack >= Tracks.Count)
							errors.Add($"{name}: 조각 {candidate.name} 이 없는 막대 {effect.MasteryTrack} 를 가리킴 (막대 {Tracks.Count}개)");
					}
				}
			}
			if (errors.Count > 0)
				throw new InvalidOperationException(string.Join("\n", errors));
		}

		public List<int> Thresholds()
		{
			List<int> thresholds = new(Tracks.Count);
			foreach (DollMasteryTrackData track in Tracks)
				thresholds.Add(track.Threshold);
			return thresholds;
		}
	}
}
