using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public enum SurvivalEventType
	{
		Elite = 0,
		Swarm = 1,
	}

	[Serializable]
	public class SurvivalEventEntry
	{
		[field: SerializeField, Tooltip("던전 경과 초"), Min(0f)] public float Time { get; private set; }
		[field: SerializeField] public SurvivalEventType Type { get; private set; }
		[field: SerializeField] public Monster Monster { get; private set; }
		[field: SerializeField, Min(1)] public int Count { get; private set; } = 1;
		[field: SerializeField, Tooltip("예고 이벤트를 시작 몇 초 전에 낼지"), Min(0f)] public float AnnounceLead { get; private set; } = 10f;
		[field: SerializeField, Tooltip("정예 전용. 처치 상자 확정. 위협 단계 추가 정예 간격 계산에서 제외")] public bool GuaranteedChest { get; private set; }

		public SurvivalEventEntry CopyWith(float time, int count)
		{
			SurvivalEventEntry copy = (SurvivalEventEntry)MemberwiseClone();
			copy.Time = time;
			copy.Count = count;
			copy.GuaranteedChest = false;
			return copy;
		}
	}

	[Serializable]
	public class EliteProfile
	{
		[field: SerializeField, Min(1f)] public float HealthMultiplier { get; private set; } = 12f;
		[field: SerializeField, Min(1f)] public float ScaleMultiplier { get; private set; } = 1.8f;
		[field: SerializeField] public Color Tint { get; private set; } = new(1f, 0.72f, 0.3f, 1f);
		[field: SerializeField, Min(1f)] public float HpBarScale { get; private set; } = 3f;
		[field: SerializeField, Min(1f), Tooltip("정예 체력 바 두께 배율")] public float HpBarThickness { get; private set; } = 2.5f;
		[field: SerializeField, Min(0)] public int ExpValue { get; private set; } = 150;
		[field: SerializeField] public bool DropHeal { get; private set; } = true;
		[field: SerializeField] public bool DropMagnet { get; private set; } = true;
	}

	// 던전 하나의 시간대 사건표
	[CreateAssetMenu(fileName = nameof(SurvivalEventTable), menuName = "WM/Variable/" + nameof(SurvivalEventTable))]
	public class SurvivalEventTable : ScriptableObject
	{
		[field: SerializeField] public SurvivalMapChallengeConfig MapChallenge { get; private set; }
		[field: SerializeField] public EliteProfile Elite { get; private set; } = new();
		[field: SerializeField, Tooltip("무리 출현 거리"), Min(1f)] public float SwarmRadius { get; private set; } = 11f;
		[field: SerializeField] public List<SurvivalEventEntry> Events { get; private set; } = new();

		// 위협 단계를 입힌 이번 판 사건표. 단계 0은 원본 그대로, 시간 안정 정렬
		public List<SurvivalEventEntry> BuildRunEvents(DungeonThreatEffect threat)
		{
			List<SurvivalEventEntry> runEvents = new();
			List<SurvivalEventEntry> elites = new();
			List<float> eliteTimes = new();
			foreach (SurvivalEventEntry entry in Events)
			{
				if (entry.Type == SurvivalEventType.Swarm)
				{
					runEvents.Add(entry.CopyWith(entry.Time, threat.ScaleSwarmCount(entry.Count)));
					continue;
				}
				runEvents.Add(entry);
				if (entry.GuaranteedChest)
					continue;
				elites.Add(entry);
				eliteTimes.Add(entry.Time);
			}

			foreach (ExtraEliteSlot slot in DungeonThreatRules.ExtraEliteSlots(eliteTimes, threat.EliteExtraPerGap))
				runEvents.Add(elites[slot.SourceIndex].CopyWith(slot.Time, 1));

			return runEvents.OrderBy(entry => entry.Time).ToList();
		}
	}
}
