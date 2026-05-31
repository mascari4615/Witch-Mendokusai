using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.Sandbox.Demos
{
	// 마도 온실 1호 데모(WM-167/177) — 4칸/인형 2이 에디트 모드에서 라이브로 자라고(초록) 개화(노랑)하거나 시든다(갈색).
	// 인형 2 < 칸 4 → 일부는 못 돌봐 시든다(마도작물=봐주지 않으면 시듦, 절충 톤). WitchGreenhouseObject 의
	// 결합분리 진입점(CoerceDefaults/Initialize/BuildSelfContained/TickDay)을 Play 없이 에디터 틱으로 그대로 구동.
	public sealed class GreenhouseSandboxDemo : ISandboxAnimatedDemo
	{
		private const int PLOT_COUNT = 4;
		private const int CARER_COUNT = 2;
		private const float TICK_INTERVAL = 1.2f;

		private WitchGreenhouseObject house;

		public string Title => "마도 온실";
		public string Category => "Farming";
		public float TickInterval => TICK_INTERVAL;

		public GameObject Build()
		{
			GameObject go = new("마도 온실 (Sandbox)");
			house = go.AddComponent<WitchGreenhouseObject>();
			house.CoerceDefaults(); // Start 안 도는 에디트 모드 — 수치(분/하루 등) 보장

			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults(); // asset 불요 — 기본 마도작물(시듦 있음)

			house.Initialize(() => CarerIds(CARER_COUNT));
			house.BuildSelfContained(PLOT_COUNT, plant, true);

			return go;
		}

		public void Tick()
		{
			if (house != null)
			{
				house.TickDay();
			}
		}

		private static IReadOnlyList<int> CarerIds(int count)
		{
			List<int> ids = new(count);
			for (int index = 0; index < count; index++)
			{
				ids.Add(index);
			}

			return ids;
		}
	}
}
