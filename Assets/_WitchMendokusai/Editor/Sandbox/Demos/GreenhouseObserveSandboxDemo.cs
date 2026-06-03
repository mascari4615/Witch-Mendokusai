using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.Sandbox.Demos
{
	// 마도 온실 2호 데모(WM-167 Phase 1f) — 테마 페이오프 「봐줘야 진짜가 된다」를 *눈으로* 보여준다.
	// 4칸 전부 코지(안 시듦) 작물 → 모두 개화한다. 단, Build 직후 칸 1·3만 Fourth 가 관찰(Observe)한다.
	// 시간이 흘러 모두 개화하면: 봐준 1·3 = 밝은 금색(영구 표본=진짜), 안 봐준 0·2 = 평범한 칙칙한 노랑.
	// = "같은 작물이라도 누군가 봐준 것만 진짜가 된다" 가 색으로 드러남(관찰만이 변수, 시듦은 통제).
	//
	// 1호(GreenhouseSandboxDemo)는 "마도작물은 방치하면 시든다"(상실 톤)를 보여줌. 본 데모는 그 위 레이어 —
	// "살아남은 것 중에도 봐준 것만 진짜가 된다"(증언 톤). 둘이 절충 톤의 두 축.
	public sealed class GreenhouseObserveSandboxDemo : ISandboxAnimatedDemo
	{
		private const float TICK_INTERVAL = 1.2f;

		private WitchGreenhouseObject house;

		public string Title => "마도 온실 — 봐줘야 진짜";
		public string Category => "Farming";
		public float TickInterval => TICK_INTERVAL;

		public GameObject Build()
		{
			GameObject go = new("마도 온실 봐줘야진짜 (Sandbox)");
			house = go.AddComponent<WitchGreenhouseObject>();
			house.CoerceDefaults(); // Start 안 도는 에디트 모드 — 수치 보장

			// 4칸 전부 코지·빠른개화(Drain 0=안 시듦, 1단계=한 틱이면 개화) → 시듦 변수 제거, 관찰만 변수.
			List<WitchPlantSO> plants = new() { FastCozy(), FastCozy(), FastCozy(), FastCozy() };
			house.Initialize(() => System.Array.Empty<int>()); // 인형 0 — 코지라 안 시듦
			house.BuildSelfContained(plants, true);

			// Fourth 가 칸 1·3만 봐준다(관찰=진짜화 자격). 0·2 는 외면.
			house.Observe(1);
			house.Observe(3);

			return go;
		}

		public void Tick()
		{
			if (house != null)
			{
				house.TickDay(); // 한 틱이면 개화 — 봐준 1·3 은 금색, 안 봐준 0·2 는 칙칙한 노랑
			}
		}

		// 코지(Drain 0=안 시듦) + 빠른 개화(MinutesPerStage 30, MaxStage 1 = 하루치 틱 한 번에 개화). asset 불요.
		private static WitchPlantSO FastCozy()
		{
			WitchPlantSO plant = ScriptableObject.CreateInstance<WitchPlantSO>();
			plant.ApplyDefaults();
			UnityEditor.SerializedObject serialized = new(plant);
			serialized.FindProperty("<DrainPerMinute>k__BackingField").floatValue = 0f;
			serialized.FindProperty("<MinutesPerStage>k__BackingField").intValue = 30;
			serialized.FindProperty("<MaxStage>k__BackingField").intValue = 1;
			serialized.ApplyModifiedProperties();
			return plant;
		}
	}
}
