using System;

namespace WitchMendokusai
{
	public enum ShadowCloneEvent
	{
		None = 0,
		// 이번에 사라짐 (그 자리에서 폭발)
		Vanished = 1,
		// 다시 나타남
		Formed = 2,
	}

	// 알리사 그림자 분신의 정본. 분신은 내 투척을 약하게 따라 함
	public static class ShadowCloneRules
	{
		// 뿌리를 받아야 켜짐. 갈림길 (수) 과 표창 폭풍 그림자 묶음이 더함
		public static int Count(int rootStacks, int bonusCount, int maxCount)
			=> rootStacks <= 0 ? 0 : Math.Max(1, Math.Min(maxCount, 1 + Math.Max(0, bonusCount)));

		public static int SharePercent(bool strong, int basePercent, int strongPercent) => strong ? strongPercent : basePercent;

		// 분신 index 의 자리 각도 (도). 바라보는 쪽이 0, 등 뒤 (180) 를 가운데로 고르게 벌림
		public static float SlotAngle(int index, int count, float spreadDegrees)
		{
			if (count <= 1)
				return 180f;
			return 180f - spreadDegrees / 2f + spreadDegrees * index / (count - 1);
		}
	}

	// 분신 하나의 수명. 마무리 카드를 받기 전에는 사라지지 않음. 받으면 수명이 돌고 사라졌다 다시 나타남
	public sealed class ShadowCloneCycle
	{
		private readonly float lifeSeconds;
		private readonly float reformSeconds;
		private float elapsed;

		public ShadowCloneCycle(float lifeSeconds, float reformSeconds)
		{
			this.lifeSeconds = Math.Max(0.01f, lifeSeconds);
			this.reformSeconds = Math.Max(0.01f, reformSeconds);
		}

		public bool IsPresent { get; private set; } = true;

		// burstsOnVanish 는 마무리 카드. 한 번에 한 사건만. 긴 프레임의 나머지 시간은 다음 단계로 넘김
		public ShadowCloneEvent Tick(float deltaSeconds, bool burstsOnVanish)
		{
			if (IsPresent && burstsOnVanish == false)
				return ShadowCloneEvent.None;
			elapsed += Math.Max(0f, deltaSeconds);
			float limit = IsPresent ? lifeSeconds : reformSeconds;
			if (elapsed < limit)
				return ShadowCloneEvent.None;
			elapsed -= limit;
			IsPresent = IsPresent == false;
			return IsPresent ? ShadowCloneEvent.Formed : ShadowCloneEvent.Vanished;
		}
	}
}
