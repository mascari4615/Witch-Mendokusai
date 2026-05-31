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
			foreach (LifeAgent agent in agents)
			{
				agent.Initialize(profile, BuildDefaultState());
				agent.AttachClock(timeManager);
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

		// 공통 디폴트 욕구 프로필 — 분당 감소·문제 임계·상한(수치노출). INC-7 에서 캐릭터별 SO 로 대체.
		private static NeedProfile BuildDefaultProfile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(0.6f, 30f, 100f) },
				{ NeedKind.Energy, new NeedSpec(0.4f, 25f, 100f) },
				{ NeedKind.Mood, new NeedSpec(0.3f, 20f, 100f) },
				{ NeedKind.Social, new NeedSpec(0.3f, 25f, 100f) },
			};
			return new NeedProfile(specs);
		}

		// 초기엔 적당히 채워진 상태로 시작 — 시간이 흐르며 자연스레 결핍이 생기도록.
		private static NeedState BuildDefaultState()
		{
			return new NeedState(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 80f }, { NeedKind.Energy, 80f }, { NeedKind.Mood, 70f }, { NeedKind.Social, 70f },
			});
		}
	}
}
