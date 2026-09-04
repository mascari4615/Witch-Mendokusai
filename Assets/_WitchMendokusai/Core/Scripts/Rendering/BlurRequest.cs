using System;
using UnityEngine;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	// Frosted glass 사용처 (SettingView, PauseMenu 등) 가 공유하는 enabled flag.
	// count > 0 일 때만 CustomBlurFeature 가 EnqueuePass — Settings 닫혀있을 때 GPU 비용 0.
	// TASK-WM-077.
	public static class BlurRequest
	{
		public static int Count => BlurDemand.Count;

		public static event Action<int> CountChanged = delegate { };

		public static void Add()
		{
			BlurDemand.Add();
			CountChanged.Invoke(BlurDemand.Count);
		}

		public static void Remove()
		{
			BlurDemand.Remove();
			CountChanged.Invoke(BlurDemand.Count);
		}
	}
}
