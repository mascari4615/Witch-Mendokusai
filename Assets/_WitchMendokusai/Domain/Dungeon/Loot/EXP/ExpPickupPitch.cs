using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 경험치 줍기 소리의 음높이. 바가 찰수록 높아져 레벨업이 가까워짐을 귀로 알림
	public static class ExpPickupPitch
	{
		private const int SEMITONES_PER_OCTAVE = 12;
		// 손으로 그린 곡선이 1.9999 처럼 음 바로 아래에 걸려 한 칸 내려가는 것 방지
		private const float SNAP_TOLERANCE = 0.001f;

		// 진행도 (0~1) 를 FMOD setPitch 배수로. 곡선은 반음 단위, scale 이 있으면 그 음계의 아래쪽 음으로 끊음
		public static float Multiplier(float progress, AnimationCurve semitoneCurve, IReadOnlyList<int> scale)
		{
			float semitones = semitoneCurve.Evaluate(Mathf.Clamp01(progress));
			if (scale != null && scale.Count > 0)
				semitones = SnapDown(semitones, scale);
			return Mathf.Pow(2f, semitones / SEMITONES_PER_OCTAVE);
		}

		// 레벨이 오를수록 줍기 소리를 조금씩 작게 (FMOD setVolume 배수). 후반에 구슬이 쏟아질 때 귀 피로를 줄임
		// dbPerLevel 은 레벨당 더하는 dB (음수), minDb 는 그 아래로 내려가지 않는 하한
		public static float LevelVolume(int level, float dbPerLevel, float minDb)
		{
			float decibels = Mathf.Max(minDb, Mathf.Max(0, level) * dbPerLevel);
			return Mathf.Pow(10f, decibels / 20f);
		}

		// 현재 경험치와 이번 레벨 필요량으로 진행도. 필요량이 없으면 0
		public static float Progress(int currentExp, int requiredExp)
		{
			if (requiredExp <= 0)
				return 0f;
			return Mathf.Clamp01(currentExp / (float)requiredExp);
		}

		// 반음 값을 음계 (옥타브 안 반음 목록, 오름차순) 에서 같거나 낮은 가장 가까운 음으로
		public static float SnapDown(float semitones, IReadOnlyList<int> scale)
		{
			int whole = Mathf.FloorToInt(semitones + SNAP_TOLERANCE);
			int octave = Mathf.FloorToInt(whole / (float)SEMITONES_PER_OCTAVE);
			int inOctave = whole - octave * SEMITONES_PER_OCTAVE;
			int snapped = scale[0] - SEMITONES_PER_OCTAVE;
			for (int i = 0; i < scale.Count; i++)
			{
				if (scale[i] <= inOctave)
					snapped = scale[i];
			}
			return octave * SEMITONES_PER_OCTAVE + snapped;
		}
	}
}
