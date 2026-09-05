using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum PortraitMood
	{
		Neutral,
		Happy,
		Sad,
		Surprised,
		Angry,
		Sleepy,
		Embarrassed,
	}

	[Serializable]
	public struct PortraitMoodEntry
	{
		public PortraitMood Mood;
		public Sprite Sprite;
	}

	/// <summary>
	/// 캐릭터의 mood 별 표정 sprite 매핑. DialogueLine.Speaker 가 이걸 가리키고,
	/// 라인별 PortraitMood 또는 Portrait sprite override 로 표현.
	/// Sprite lookup helper 는 Phase 1.3 (DialogueRunner) 에서 추가 예정.
	/// </summary>
	[CreateAssetMenu(fileName = "PortraitData_", menuName = "WM/Narrative/PortraitData")]
	public class PortraitData : DataSO
	{
		[field: Header("_" + nameof(PortraitData))]
		[field: SerializeField] public List<PortraitMoodEntry> Moods { get; private set; } = new();
	}
}
