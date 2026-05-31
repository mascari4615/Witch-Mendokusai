using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.Sandbox.Demos
{
	// 마도 온실 1호 데모(WM-167/177) — 4칸/인형 2이 에디트 모드에서 라이브로 자라고(초록) 개화(노랑)하거나 시든다(갈색).
	// 인형 2 < 칸 4 → 일부는 못 돌봐 시든다(마도작물=봐주지 않으면 시듦, 절충 톤). WitchGreenhouseObject 의
	// 결합분리 진입점(CoerceDefaults/Initialize/BuildSelfContained/TickDay)을 Play 없이 에디터 틱으로 그대로 구동.
	public sealed class GreenhouseSandboxDemo : ISandboxAnimatedDemo
	{
		private const float TICK_INTERVAL = 1.2f;

		private WitchGreenhouseObject house;

		public string Title => "마도 온실";
		public string Category => "Farming";
		public float TickInterval => TICK_INTERVAL;

		// 절충 톤 가시화: 일반(코지) 작물 = 안 시듦 / 마도 작물 = 돌봄 없으면 시듦.
		// 인형 0(방치) + 칸 교차(코지·마도·코지·마도) → 코지 2 개화(노랑), 마도 2 시듦(갈색) = "마도작물만 상실"이 눈에.
		public GameObject Build()
		{
			GameObject go = new("마도 온실 (Sandbox)");
			house = go.AddComponent<WitchGreenhouseObject>();
			house.CoerceDefaults(); // Start 안 도는 에디트 모드 — 수치(분/하루 등) 보장

			WitchPlantSO cozy = MakePlant(true);
			WitchPlantSO magical = MakePlant(false);

			house.Initialize(() => System.Array.Empty<int>()); // 인형 0 = 방치
			house.BuildSelfContained(new List<WitchPlantSO> { cozy, magical, cozy, magical }, true);

			return go;
		}

		public void Tick()
		{
			if (house != null)
			{
				house.TickDay();
			}
		}

		// 코지=DrainPerMinute 0(안 시듦), 마도=ApplyDefaults 기본(시듦 있음). asset 불요.
		private static WitchPlantSO MakePlant(bool cozy)
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults();
			if (cozy)
			{
				UnityEditor.SerializedObject serialized = new(plant);
				serialized.FindProperty("<DrainPerMinute>k__BackingField").floatValue = 0f;
				serialized.ApplyModifiedProperties();
			}

			return plant;
		}
	}
}
