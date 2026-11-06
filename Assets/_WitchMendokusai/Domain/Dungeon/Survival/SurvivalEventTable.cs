using System;
using System.Collections.Generic;
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
	}

	[Serializable]
	public class EliteProfile
	{
		[field: SerializeField, Min(1f)] public float HealthMultiplier { get; private set; } = 12f;
		[field: SerializeField, Min(1f)] public float ScaleMultiplier { get; private set; } = 1.8f;
		[field: SerializeField] public Color Tint { get; private set; } = new(1f, 0.72f, 0.3f, 1f);
		[field: SerializeField, Min(0)] public int ExpValue { get; private set; } = 150;
		[field: SerializeField] public bool DropHeal { get; private set; } = true;
		[field: SerializeField] public bool DropMagnet { get; private set; } = true;
	}

	// 던전 하나의 시간대 사건표
	[CreateAssetMenu(fileName = nameof(SurvivalEventTable), menuName = "WM/Variable/" + nameof(SurvivalEventTable))]
	public class SurvivalEventTable : ScriptableObject
	{
		[field: SerializeField] public EliteProfile Elite { get; private set; } = new();
		[field: SerializeField, Tooltip("무리 출현 거리"), Min(1f)] public float SwarmRadius { get; private set; } = 11f;
		[field: SerializeField] public List<SurvivalEventEntry> Events { get; private set; } = new();
	}
}
