using System;
using UnityEngine;

namespace WitchMendokusai
{
	// Frosted glass 사용처 (SettingView, PauseMenu 등) 가 공유하는 enabled flag.
	// count > 0 일 때만 CustomBlurFeature 가 EnqueuePass — Settings 닫혀있을 때 GPU 비용 0.
	// TASK-WM-077.
	public static class BlurRequest
	{
		private static int count;

		public static int Count => count;

		public static event Action<int> CountChanged = delegate { };

		public static void Add()
		{
			count++;
			CountChanged.Invoke(count);
		}

		public static void Remove()
		{
			count = Mathf.Max(0, count - 1);
			CountChanged.Invoke(count);
		}
	}
}
