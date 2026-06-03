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

			Dictionary<ActivityKind, Vector3> zones = CollectZones();

			NeedProfile profile = BuildDefaultProfile();
			agents = FindObjectsByType<LifeAgent>(FindObjectsSortMode.None);
			for (int index = 0; index < agents.Length; index++)
			{
				// index = 위상 → 같은 순간 서로 다른 활동(색). self-care(LifeAgent) 가 욕구를 채워 자연 순환.
				agents[index].Initialize(profile, BuildDefaultState(index));
				agents[index].SetActivityZones(zones); // 활동별 장소 → 목적 이동(랜덤 어슬렁 대신).
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

		// 씬의 모든 LifeZone → 활동별 위치 맵. 같은 활동 장소가 여럿이면 마지막 것(드묾). 없으면 빈 맵(주민은 어슬렁 폴백).
		private static Dictionary<ActivityKind, Vector3> CollectZones()
		{
			Dictionary<ActivityKind, Vector3> map = new();
			foreach (LifeZone zone in FindObjectsByType<LifeZone>(FindObjectsSortMode.None))
			{
				map[zone.Activity] = zone.Position;
			}
			return map;
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

		// 공통 디폴트 욕구 프로필 — 분당 감소(게임-시간 페이싱: 욕구는 게임-시간 단위로 천천히 닳음)·문제 임계 40·상한.
		// LifeAgent.selfSatisfyPerMinute(0.8) 이력현상(contentLevel) 과 균형 — 활동이 매 틱 안 튀고 한동안 지속. 수치노출.
		private static NeedProfile BuildDefaultProfile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(0.13f, 40f, 100f) },
				{ NeedKind.Energy, new NeedSpec(0.11f, 40f, 100f) },
				{ NeedKind.Mood, new NeedSpec(0.09f, 40f, 100f) },
				{ NeedKind.Social, new NeedSpec(0.08f, 40f, 100f) },
			};
			return new NeedProfile(specs);
		}

		// 위상차 시작 상태 — phase 만큼 욕구 사다리를 회전(여럿이 같은 순간 다른 활동·색). 시간이 흐르며 결핍 발생.
		private static NeedState BuildDefaultState(int phase)
		{
			float[] ladder = { 45f, 65f, 80f, 95f };
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
