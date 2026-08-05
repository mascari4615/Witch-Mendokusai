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
		private CityEconomy cityEconomy; // 노동 생산물이 쌓일 마을 창고(WorldStage 원장). 못 얻으면 경제 off 폴백.

		// init-order-ok: 씬 정적 LifeAgent/WorldClock 을 Start 에서 1회 수집·연결. 없으면 안전 폴백.
		private void Start()
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager) == false)
			{
				return;
			}

			// init-order-ok: StageManager.Start(LoadStage) 가 LifeDirector.Start 보다 먼저 CurStage 세팅(순차 보장).
			// World 씬 전용(LifeWorldBootstrap WORLD_SCENE 게이트)이라 WorldStage 가정 가능. 아니면 경제 off 폴백
			// (생산만 스킵, 욕구·활동·시각은 진행). 로컬 new CityEconomy 폴백은 X(저장 미연결·정본 desync).
			if (StageManager.TryGetExistingInstance(out StageManager stageManager)
				&& stageManager.CurStage is WorldStage worldStage)
			{
				cityEconomy = worldStage.CityEconomy;
			}

			Dictionary<ActivityKind, Vector3> zones = CollectZones();

			// INC-7: 캐릭터별 성격 = LifeProfileSO(Resources/Life/Profiles). 있으면 데이터 주도, 없으면 하드코딩 폴백.
			LifeProfileSO[] profiles = Resources.LoadAll<LifeProfileSO>("Life/Profiles");
			agents = FindObjectsByType<LifeAgent>();
			for (int index = 0; index < agents.Length; index++)
			{
				// 주민마다 다른 성격(미식가/수다쟁이…) — 프로필을 순환 배정. 없으면 공통 디폴트.
				LifeProfileSO profileSO = profiles.Length > 0 ? profiles[index % profiles.Length] : null;
				NeedProfile profile = profileSO != null ? profileSO.ToNeedProfile() : BuildDefaultProfile();

				agents[index].Initialize(profile, BuildDefaultState(index));
				if (profileSO != null)
				{
					agents[index].SetSelfSatisfyPerMinute(profileSO.SelfSatisfyPerMinute);
					agents[index].name = profileSO.DisplayName; // 성격 이름 = 로그·라벨·관계선에 보이게.
					agents[index].SetWorkProfile(profileSO.ToWorkProfile()); // 노동 성격(WM-183) — 한가하면 이 일.
				}
				if (cityEconomy != null)
				{
					agents[index].AttachEconomy(cityEconomy); // 생산물 누적 대상(마을 창고). null=경제 off.
				}
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
			foreach (LifeZone zone in FindObjectsByType<LifeZone>())
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
