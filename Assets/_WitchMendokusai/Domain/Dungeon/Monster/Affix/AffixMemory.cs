using UnityEngine;

namespace WitchMendokusai
{
	// 유닛 하나, 특징 하나의 기억. 특징마다 뜻을 정해 씀 (쓰는 법은 각 특징의 주석). 켜질 때마다 Reset
	public sealed class AffixMemory
	{
		// 다음 사건까지 남은 초
		public float Timer;
		// 한 번만 일어나는 일이 이미 일어남 (분노)
		public bool IsOn;
		// 마지막으로 무엇을 남긴 자리
		public Vector3 LastSpot;

		public void Reset(Vector3 position)
		{
			Timer = 0f;
			IsOn = false;
			LastSpot = position;
		}
	}
}
