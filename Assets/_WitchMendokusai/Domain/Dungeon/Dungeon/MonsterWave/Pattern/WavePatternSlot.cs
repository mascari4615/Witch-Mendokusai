using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 던전 일정표 한 줄. 어느 패턴을 몇 초에 시작할지
	[Serializable]
	public class WavePatternSlot
	{
		[field: SerializeField] public WavePattern Pattern { get; private set; }
		[field: SerializeField, Tooltip("던전 경과 초"), Min(0f)] public float StartTime { get; private set; }
	}
}
