using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-5c — 씬의 모든 <see cref="LifeAgent"/> 를 발견해 욕구 프로필·초기 상태를 주입하고
	/// TimeManager 틱·WorldClock 시각에 연결하는 주입자(씬 1개). 자율 삶 레이어의 부트스트랩.
	///
	/// 캐릭터별 개성(욘은 Social 느림 등)은 미래 INC-7 LifeProfileSO 로 외부화 — 지금은 공통 코드 디폴트.
	/// (Instance lazy = init-order-ok: LifeAgent·WorldClock 은 씬 정적 배치라 Start 시점 존재, 없으면 null-skip 폴백)
	/// </summary>
	public class LifeDirector : MonoBehaviour
	{
		private LifeAgent[] agents = System.Array.Empty<LifeAgent>();
		private WorldClock worldClock;

		// init-order-ok: 씬 정적 LifeAgent/WorldClock 을 Start 에서 1회 수집·연결. 없으면 안전 폴백.
		private void Start()
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager) == false)
			{
				return;
			}

			NeedProfile profile = BuildDefaultProfile();
			agents = FindObjectsByType<LifeAgent>(FindObjectsSortMode.None);
			for (int index = 0; index < agents.Length; index++)
			{
				// index = 위상 → 같은 순간 서로 다른 활동(색). self-care(LifeAgent) 가 욕구를 채워 자연 순환.
				agents[index].Initialize(profile, BuildDefaultState(index));
				agents[index].AttachClock(timeManager);
			}

			if (WorldClock.TryGetExistingInstance(out worldClock))
			{
				worldClock.OnHourChanged += OnHourChanged;
				PushTimeOfDay(worldClock.Hour);
			}
		}

		private void OnDestroy()
		{
			if (worldClock != null)
			{
				worldClock.OnHourChanged -= OnHourChanged;
			}
		}

		private void OnHourChanged(int hour) => PushTimeOfDay(hour);

		private void PushTimeOfDay(int hour)
		{
			TimeOfDay timeOfDay = TimeOfDayMap.FromHour(hour);
			foreach (LifeAgent agent in agents)
			{
				agent.SetTimeOfDay(timeOfDay);
			}
		}

		// 공통 디폴트 욕구 프로필 — 분당 감소·문제 임계(50 공통)·상한(수치노출). INC-7 에서 캐릭터별 SO 로 대체.
		// decay 1.4/1.2/1.0/0.9 = self-care(LifeAgent.selfSatisfyPerMinute) 와 균형 잡혀 어느 하나 독점 없이 순환.
		private static NeedProfile BuildDefaultProfile()
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

		// 위상차 시작 상태 — phase 만큼 욕구 사다리를 회전(여럿이 같은 순간 다른 활동·색). 시간이 흐르며 결핍 발생.
		private static NeedState BuildDefaultState(int phase)
		{
			float[] ladder = { 40f, 55f, 70f, 85f };
			NeedKind[] order = { NeedKind.Hunger, NeedKind.Energy, NeedKind.Mood, NeedKind.Social };
			Dictionary<NeedKind, float> values = new();
			for (int index = 0; index < order.Length; index++)
			{
				values[order[index]] = ladder[(index + phase) % ladder.Length];
			}
			return new NeedState(values);
		}
	}
}
