using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Sandbox.Demos
{
	// TASK-WM-168 INC-5c 시각 검증 — 자율 삶 두뇌(Need/Activity)를 Play 없이 에디트 모드에서 보여준다.
	// 큐브 3체(작은 마을)가 각자 욕구가 줄어들며(NeedModel.Step) 가장 급한 욕구를 채우는 활동을 스스로 골라
	// (ActivitySelector) 색이 휙휙 바뀐다 = "살아있는 자율 두뇌"의 가시 증거. 활동이 욕구를 채우는 결(activity→need
	// Satisfy)을 데모 글루로 입혀 색이 한 활동에 박히지 않고 순환한다(실제 개입 의미는 INC-4+).
	//
	// LifeDirector 를 안 쓰는 이유: 에디트 모드는 Start/Update 가 안 돈다(LifeDirector.Start·LifeAgent.Update 침묵).
	// 그래서 GreenhouseSandboxDemo 처럼 Build()에서 Initialize 를 직접 호출하고, Tick()에서 TickMinutes·이동을 직접 구동.
	public sealed class LifeSandboxDemo : ISandboxAnimatedDemo
	{
		private const float TICK_INTERVAL = 0.5f;
		private const int MINUTES_PER_TICK = 12;    // 한 틱당 흐르는 게임 분 — 욕구가 눈에 보이게 줄도록.
		private const float SATISFY_PER_TICK = 60f; // 활동이 그 욕구를 채우는 양(데모 글루). 소진(decay×분)을 넘겨 욕구가
		                                            // 임계 위로 회복 → 다음 급한 욕구로 넘어가야 색이 순환(검증: 24틱 5색 전환).
		private const float WANDER_STEP = 0.6f;     // 틱당 무작위 보행 거리(에디트 모드엔 Update 가 없어 여기서 이동).
		private const float WANDER_RADIUS = 4f;     // 마을 광장 반경 — 너무 멀리 못 가게 중심으로 당김.

		private readonly List<Resident> residents = new();
		private int randomSeed = 1;

		public string Title => "자율 삶";
		public string Category => "Life";
		public float TickInterval => TICK_INTERVAL;

		// 큐브 3체를 광장에 깔고 각자 위상이 어긋난 욕구 상태로 시작 → 첫 프레임부터 색이 제각각(주황/파랑/초록),
		// 이후 셋 다 자율로 활동을 순환(색이 휙휙). 위상차 = 같은 순간에 셋이 다른 일을 한다는 "각자 산다"가 한눈에.
		public GameObject Build()
		{
			residents.Clear();
			randomSeed = 1;

			GameObject root = new("자율 삶 마을 (Sandbox)");

			// phase 0/1/2 = 시작 시 가장 급한 욕구를 회전 → t0 색 = 주황(A=Eat)·분홍(B=Socialize)·초록(C=Hobby). (라이브 검증)
			SpawnResident(root, "주민 A", new Vector3(-2.5f, 0.5f, 0f), phase: 0);
			SpawnResident(root, "주민 B", new Vector3(0f, 0.5f, 0f), phase: 1);
			SpawnResident(root, "주민 C", new Vector3(2.5f, 0.5f, 0f), phase: 2);

			return root;
		}

		public void Tick()
		{
			foreach (Resident resident in residents)
			{
				if (resident.Agent == null)
				{
					continue;
				}

				// ① 시간 경과 — 욕구가 줄고 자율로 활동(색)이 갱신된다.
				resident.Agent.TickMinutes(MINUTES_PER_TICK);

				// ② 활동이 그 욕구를 채우는 결을 흉내(데모 글루) — 채워지면 다음 급한 욕구로 넘어가 색이 순환.
				NeedKind? target = NeedForActivity(resident.Agent.CurrentActivity);
				if (target.HasValue)
				{
					NeedModel.Satisfy(resident.Agent.NeedState, resident.Profile, target.Value, SATISFY_PER_TICK);
				}

				// ③ 살아있음의 결 — 광장을 어슬렁(에디트 모드엔 LifeAgent.Update 가 안 도므로 여기서 이동).
				Wander(resident.Agent.transform);
			}
		}

		private void SpawnResident(GameObject root, string name, Vector3 position, int phase)
		{
			GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			cube.name = name;
			cube.transform.SetParent(root.transform);
			cube.transform.position = position;

			LifeAgent agent = cube.AddComponent<LifeAgent>();
			NeedProfile profile = BuildProfile();
			agent.Initialize(profile, BuildState(phase));

			residents.Add(new Resident { Agent = agent, Profile = profile });
		}

		// 광장 중심으로 당기는 무작위 보행 — Random.insideUnitCircle 대신 결정적 LCG(데모 재현성).
		private void Wander(Transform target)
		{
			float angle = NextRandom01() * Mathf.PI * 2f;
			Vector3 step = new(Mathf.Cos(angle) * WANDER_STEP, 0f, Mathf.Sin(angle) * WANDER_STEP);

			Vector3 next = target.position + step;
			Vector3 flat = new(next.x, 0f, next.z);
			if (flat.magnitude > WANDER_RADIUS)
			{
				next -= flat.normalized * (flat.magnitude - WANDER_RADIUS); // 광장 밖이면 안쪽으로 되당김.
			}

			target.position = new Vector3(next.x, target.position.y, next.z);
		}

		// 활동이 채우는 욕구(ActivitySelector.ActivityForNeed 역) — Idle 은 채울 욕구 없음.
		private static NeedKind? NeedForActivity(ActivityKind activity) => activity switch
		{
			ActivityKind.Eat => NeedKind.Hunger,
			ActivityKind.Sleep => NeedKind.Energy,
			ActivityKind.Hobby => NeedKind.Mood,
			ActivityKind.Socialize => NeedKind.Social,
			_ => null,
		};

		// 데모용 욕구 프로필 — 4 욕구가 비슷한 속도로 감소(임계 50 공통)해 어느 하나에 막히지 않고 순환.
		// SATISFY_PER_TICK(60) > decay×분(최대 1.4×12≈17)이라 활동 시 욕구가 임계 위로 회복 → 다음 욕구로 넘어감.
		// 검증(24틱 트레이스): Eat>Sleep>Hobby>Eat>Socialize>… 5 활동 23 전환. 캐릭터별 개성은 미래 INC-7 LifeProfileSO.
		private static NeedProfile BuildProfile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(1.4f, 50f, 100f) },
				{ NeedKind.Energy, new NeedSpec(1.2f, 50f, 100f) },
				{ NeedKind.Mood, new NeedSpec(1.0f, 50f, 100f) },
				{ NeedKind.Social, new NeedSpec(0.9f, 50f, 100f) },
			};
			return new NeedProfile(specs);
		}

		// 위상차 시작 상태 — phase 만큼 욕구 사다리를 회전시켜 셋이 같은 순간 다른 활동(색)을 하게.
		// phase 0 = Hunger 가장 낮음(Eat 주황) / 1 = Social(Socialize 분홍) / 2 = Mood(Hobby 초록). (라이브 t0 검증)
		private static NeedState BuildState(int phase)
		{
			float[] ladder = { 40f, 55f, 70f, 85f }; // 낮을수록 먼저 급함 — 가장 낮은 게 첫 활동을 정함.
			NeedKind[] order = { NeedKind.Hunger, NeedKind.Energy, NeedKind.Mood, NeedKind.Social };
			Dictionary<NeedKind, float> values = new();
			for (int index = 0; index < order.Length; index++)
			{
				values[order[index]] = ladder[(index + phase) % ladder.Length];
			}
			return new NeedState(values);
		}

		// 결정적 난수(LCG) — Math.Random/Date 금지 정합 + 데모 재현성. 0~1.
		private float NextRandom01()
		{
			randomSeed = (randomSeed * 1103515245 + 12345) & int.MaxValue;
			return (randomSeed % 10000) / 10000f;
		}

		private sealed class Resident
		{
			public LifeAgent Agent;
			public NeedProfile Profile;
		}
	}
}
