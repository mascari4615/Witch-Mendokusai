using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Act;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	// 이 세계에서 행동이 걸리는 자리 (TASK-WM-410) — 몸 하나, 하늘 하나, 시간을 타는 것들.
	//
	// ★ 왜 이 자리가 필요한가: 원장(ActLedger)은 「무엇을 얼마나 먹나」만 판정하고 세계를 안 갖는다.
	//   그래서 누군가는 <b>몸·창고·하늘</b>을 들고 있어야 한다. 밭도 온실도 이 하나를 함께 쓰면
	//   「같은 세계에서 하루를 태워 쓴다」가 성립한다 — 각자 시계를 가지면 그 순간 갈라진다.
	//
	// ★ 하늘의 정본은 벽시계(WorldClock)다. 여기 든 WorldCalendar 는 <b>따라 읽는 사본</b>이라
	//   시계가 움직일 때마다 그 값으로 맞춘다(WM-315 의 SetTotalMinutesHard 가 그 용도).
	//   행동이 시간을 먹으면 사본이 먼저 흐르고, rider 가 같은 분을 벽시계에 밀고,
	//   벽시계가 알려 오면 사본을 그 값으로 다시 맞춘다 — 두 하늘이 어긋날 자리가 없다.
	public sealed class WorldActSite : MonoBehaviour
	{
		private const float DEFAULT_NEED_MAX = 100f;
		private const float DEFAULT_LOW = 30f;
		private const float DEFAULT_HUNGER_DECAY = 0.03f;
		private const float DEFAULT_ENERGY_DECAY = 0.02f;
		private const float DEFAULT_MOOD_DECAY = 0.01f;
		private const float DEFAULT_SOCIAL_DECAY = 0.01f;

		[Header("창고 (행동이 씨앗·재료를 꺼내 쓰는 가방)")]
		[SerializeField] private Inventory satchel;

		[Header("몸 (수치노출 룰 — 분당 감소·문제 임계·상한)")]
		[SerializeField, Min(0f)] private float needMax = DEFAULT_NEED_MAX;
		[SerializeField, Min(0f)] private float lowThreshold = DEFAULT_LOW;
		[SerializeField, Min(0f)] private float hungerDecayPerMinute = DEFAULT_HUNGER_DECAY;
		[SerializeField, Min(0f)] private float energyDecayPerMinute = DEFAULT_ENERGY_DECAY;
		[SerializeField, Min(0f)] private float moodDecayPerMinute = DEFAULT_MOOD_DECAY;
		[SerializeField, Min(0f)] private float socialDecayPerMinute = DEFAULT_SOCIAL_DECAY;

		private readonly ActTimeRiders riders = new();
		private NeedState body;
		private NeedProfile profile;
		private WorldCalendar calendar;
		private WorldClock clock;

		/// <summary>이 세계 — 밭·온실·무엇이든 이걸 받아 행동을 건다.</summary>
		public ActContext World { get; private set; }

		public NeedState Body => body;

		public WorldCalendar Calendar => calendar;

		private void Reset()
		{
			needMax = DEFAULT_NEED_MAX;
			lowThreshold = DEFAULT_LOW;
			hungerDecayPerMinute = DEFAULT_HUNGER_DECAY;
			energyDecayPerMinute = DEFAULT_ENERGY_DECAY;
			moodDecayPerMinute = DEFAULT_MOOD_DECAY;
			socialDecayPerMinute = DEFAULT_SOCIAL_DECAY;
		}

		private void Awake()
		{
			// 인스펙터 직렬화 디폴트(0)로 덮여도 세계가 죽지 않게 자가보정 (온실 선례).
			if (needMax <= 0f)
			{
				needMax = DEFAULT_NEED_MAX;
				lowThreshold = DEFAULT_LOW;
				hungerDecayPerMinute = DEFAULT_HUNGER_DECAY;
				energyDecayPerMinute = DEFAULT_ENERGY_DECAY;
				moodDecayPerMinute = DEFAULT_MOOD_DECAY;
				socialDecayPerMinute = DEFAULT_SOCIAL_DECAY;
			}

			Initialize();
		}

		/// <summary>세계를 세운다(몸·하늘·타는 것들). 멱등 — Awake 가 부르고, 검증도 직접 부른다.</summary>
		public void Initialize()
		{
			if (World != null)
			{
				return;
			}

			BuildWorld();
		}

		private void Start()
		{
			// 내 아래 달린 밭들에게 세계를 준다 + 그것들을 시간에 태운다.
			// (자식 스코프라 cross-ref Find 아님 — init-order 규약 안쪽.)
			FarmGroundObject[] farms = GetComponentsInChildren<FarmGroundObject>(true);
			for (int i = 0; i < farms.Length; i++)
			{
				// 밭의 정본은 스테이지다 - 씬 오브젝트가 아니라(나갔다 오면 심은 것이 사라진다).
				if (StageManager.TryGetExistingInstance(out StageManager stageManager)
					&& stageManager.CurStage is WorldStage stage)
				{
					farms[i].UseModel(stage.Farm);
				}

				farms[i].World = World;
				riders.Add(farms[i].TimeRider);
			}

			AttachClock(WorldClock.TryGetExistingInstance(out WorldClock existing) ? existing : null);
		}

		private void OnDestroy()
		{
			DetachClock();
		}

		/// <summary>창고를 갈아 끼운다 — 가방이 아직 없을 때(검증)나 다른 창고를 쓸 때.</summary>
		public void UseResources(IActResourcePool pool)
		{
			Initialize();
			World = new ActContext(body, profile, pool, calendar, riders);
		}

		/// <summary>시간을 타는 것을 하나 더 태운다(나중에 생긴 밭·가마솥 등).</summary>
		public void Ride(IActTimeRider rider)
		{
			riders.Add(rider);
		}

		/// <summary>이 세계에 행동 하나를 건다 — 대가 판정은 전부 원장.</summary>
		public bool Do(ActSpec spec, out ActOutcome outcome)
		{
			return ActLedger.TryApply(spec, World, out outcome);
		}

		public void AttachClock(WorldClock worldClock)
		{
			DetachClock();
			clock = worldClock;

			if (clock == null)
			{
				return;
			}

			clock.OnMinuteChanged += HandleClockMinute;
			SyncCalendarFromClock();
		}

		public void DetachClock()
		{
			if (clock != null)
			{
				clock.OnMinuteChanged -= HandleClockMinute;
			}

			clock = null;
		}

		private void BuildWorld()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(hungerDecayPerMinute, lowThreshold, needMax) },
				{ NeedKind.Energy, new NeedSpec(energyDecayPerMinute, lowThreshold, needMax) },
				{ NeedKind.Mood, new NeedSpec(moodDecayPerMinute, lowThreshold, needMax) },
				{ NeedKind.Social, new NeedSpec(socialDecayPerMinute, lowThreshold, needMax) },
			};
			profile = new NeedProfile(specs);
			body = new NeedState(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, needMax }, { NeedKind.Energy, needMax }, { NeedKind.Mood, needMax }, { NeedKind.Social, needMax },
			});

			calendar = BuildCalendar();
			riders.Add(new NeedDecayTimeRider(body, profile));
			riders.Add(new WorldClockTimeRider(() => clock));

			World = new ActContext(body, profile, satchel == null ? null : new InventoryActPool(satchel), calendar, riders);
		}

		// 자릿수(하루 몇 시간·한 계절 며칠)는 WorldClockSO 가 정본 — 같은 수를 두 곳에 안 적는다.
		private WorldCalendar BuildCalendar()
		{
			WorldClockSO config = WorldClock.TryGetExistingInstance(out WorldClock existing) && existing.Config != null
				? existing.Config
				: null;

			if (config == null)
			{
				return new WorldCalendar(0, 0, 0);
			}

			return new WorldCalendar(config.HoursPerDay, config.DaysPerSeason, config.SeasonsPerYear, config.StartHour, config.StartMinute);
		}

		private void HandleClockMinute(int minute)
		{
			SyncCalendarFromClock();
		}

		// 벽시계가 정본 — 사본을 그 값으로 맞춘다(앞뒤 어느 쪽이든).
		private void SyncCalendarFromClock()
		{
			if (clock == null || clock.Config == null || calendar == null)
			{
				return;
			}

			long totalDays = ((long)(clock.Year - 1) * clock.Config.SeasonsPerYear + clock.Season) * clock.Config.DaysPerSeason + (clock.Day - 1);
			long totalMinutes = (totalDays * clock.Config.HoursPerDay + clock.Hour) * WorldCalendar.MINUTES_PER_HOUR + clock.Minute;
			calendar.SetTotalMinutesHard(totalMinutes);
		}
	}
}
