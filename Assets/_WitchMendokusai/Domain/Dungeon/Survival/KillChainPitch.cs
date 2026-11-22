using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 짧은 시간 안에 연달아 처치하면 처치음이 음계를 따라 한 칸씩 오름. 공백이 생기면 바닥 음으로
	public static class KillChainPitch
	{
		private const int SEMITONES_PER_OCTAVE = 12;

		// 이번 처치가 연쇄의 몇 번째 칸인지. 앞 처치에서 window 초 안이면 한 칸 올림
		// maxStep 을 넘으면 바닥으로 돌아가 다시 오름 (무리를 쓸 때 꼭대기 음에 붙어 귀가 아프지 않게 아르페지오처럼 돎)
		public static int NextStep(int previousStep, float lastKillTime, float now, float window, int maxStep)
		{
			if (window <= 0f || now - lastKillTime > window)
				return 0;
			int next = previousStep + 1;
			return next > maxStep ? 0 : next;
		}

		// 칸 번호를 음계 음 (반음) 으로. 음계 길이를 넘으면 한 옥타브 위에서 다시
		public static int Semitones(int step, IReadOnlyList<int> scale)
		{
			if (scale.Count == 0)
				return 0;
			int octave = step / scale.Count;
			return octave * SEMITONES_PER_OCTAVE + scale[step % scale.Count];
		}

		public static float Multiplier(int step, IReadOnlyList<int> scale)
			=> Mathf.Pow(2f, Semitones(step, scale) / (float)SEMITONES_PER_OCTAVE);
	}
}
