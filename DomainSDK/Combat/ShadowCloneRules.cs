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
		private const float BEHIND_DEGREES = 180f;

		// 뿌리를 받아야 켜짐. 갈림길 (수) 과 표창 폭풍 그림자 묶음이 더함
		public static int Count(int rootStacks, int bonusCount, int maxCount)
			=> rootStacks <= 0 ? 0 : Math.Max(0, Math.Min(maxCount, 1 + Math.Max(0, bonusCount)));

		public static int SharePercent(bool strong, int basePercent, int strongPercent) => strong ? strongPercent : basePercent;

		// 분신 index 의 자리 각도 (도). 바라보는 쪽이 0, 위에서 볼 때 시계 방향. 등 뒤를 가운데로 고르게 벌림
		public static float SlotAngle(int index, int count, float spreadDegrees) => BEHIND_DEGREES + VolleyGaugeRules.FanAngle(index, count, spreadDegrees);

		// 분신 index 가 처음 사라질 때까지 앞당기는 시간. 여럿이 한 프레임에 같이 터지지 않게 수명 안에서 고르게
		public static float Stagger(int index, int count, float lifeSeconds) => count <= 1 ? 0f : lifeSeconds * index / count;
	}

	// 분신 하나의 수명. 마무리 카드를 받기 전에는 사라지지 않음. 받은 뒤로 수명이 돌고 사라졌다 다시 나타남
	// 수명은 매번 받아 자료를 고치면 바로 반영
	public sealed class ShadowCloneCycle
	{
		private const float MIN_SECONDS = 0.01f;

		private float elapsed;
		private float stagger;
		private bool running;

		public bool IsPresent { get; private set; } = true;

		public void Begin(float staggerSeconds)
		{
			stagger = Math.Max(0f, staggerSeconds);
			elapsed = 0f;
			running = false;
			IsPresent = true;
		}

		// burstsOnVanish 는 마무리 카드. 한 번에 한 사건만. 긴 프레임의 나머지 시간은 다음 단계로 넘김
		public ShadowCloneEvent Tick(float deltaSeconds, bool burstsOnVanish, float lifeSeconds, float reformSeconds)
		{
			if (IsPresent && burstsOnVanish == false)
			{
				running = false;
				return ShadowCloneEvent.None;
			}
			if (IsPresent && running == false)
			{
				running = true;
				elapsed = stagger;
			}
			elapsed += Math.Max(0f, deltaSeconds);
			float limit = Math.Max(MIN_SECONDS, IsPresent ? lifeSeconds : reformSeconds);
			if (elapsed < limit)
				return ShadowCloneEvent.None;
			elapsed -= limit;
			IsPresent = IsPresent == false;
			return IsPresent ? ShadowCloneEvent.Formed : ShadowCloneEvent.Vanished;
		}
	}
}
