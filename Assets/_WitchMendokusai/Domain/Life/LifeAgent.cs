using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-168 INC-5a — 한 캐릭터(UnitObject)에 붙어 자율 삶을 구동하는 MonoBehaviour.
	/// 순수 모델(INC-1~2)을 씬에 잇는 한 겹: TimeManager 틱마다 욕구가 줄고(NeedModel.Step),
	/// 가장 급한 욕구·시간대에 맞는 활동을 고른다(ActivitySelector). 활동이 바뀌면 OnActivityChanged 통지.
	///
	/// deps 주입 = TacticDriver 패턴(외부 Initialize) — init-order 룰(Awake Find 금지) 정합.
	/// 욕구·활동만 책임(UnitObject 비의존 → EditMode 직접 테스트 가능). 실제 이동·애니는 INC-5c 에서
	/// UnitObject(UnitMovement) lazy 연동, 관계·개입(INC-3~4 다체 조정)은 LifeDirector 레벨.
	/// </summary>
	public class LifeAgent : MonoBehaviour
	{
		// 틱(TimeManager.TICK=0.05s) 1회당 흐르는 게임 내 분. 수치노출 — 시간 스케일 디자인 손잡이.
		[SerializeField] private float minutesPerTick = 1f;

		private TimeManager timeManager;
		private NeedProfile profile;
		private NeedState needState;
		private TimeOfDay timeOfDay = TimeOfDay.Morning;
		private float accumulatedMinutes;

		/// <summary>지금 이 캐릭터가 하는 활동(가장 최근 선택). 초기 Idle.</summary>
		public ActivityKind CurrentActivity { get; private set; } = ActivityKind.Idle;

		/// <summary>활동이 바뀔 때 통지(가시화·애니 훅). null 방지 초기값.</summary>
		public event Action<ActivityKind> OnActivityChanged = delegate { };

		public NeedState NeedState => needState;

		/// <summary>욕구 프로필·초기 상태 주입(초기 활동 1회 산정). 틱 구동은 AttachClock 으로 분리 — 테스트 가능.</summary>
		public void Initialize(NeedProfile profile, NeedState initialState)
		{
			this.profile = profile;
			this.needState = initialState;
			RefreshActivity();
		}

		/// <summary>TimeManager 틱에 구독해 자율 구동 시작 — 외부(LifeDirector/부트스트랩)가 호출.</summary>
		public void AttachClock(TimeManager timeManager)
		{
			this.timeManager = timeManager;
			this.timeManager.RegisterCallback(OnTick);
		}

		/// <summary>구동 정지 — 틱 구독 해제(좀비 틱 방지, TacticDriver.StopDriving 패턴).</summary>
		public void StopDriving()
		{
			if (timeManager != null)
			{
				timeManager.RemoveCallback(OnTick);
			}
		}

		/// <summary>현재 시간대 갱신 — INC-5b 에서 WorldClock 이 push.</summary>
		public void SetTimeOfDay(TimeOfDay value) => timeOfDay = value;

		private void OnDestroy()
		{
			if (timeManager != null)
			{
				timeManager.RemoveCallback(OnTick);
			}
		}

		// 0.05s 틱마다 게임 분 누적 → 정수 분 쌓일 때만 모델 1스텝(잔여 보존, WorldClock house 패턴).
		private void OnTick()
		{
			accumulatedMinutes += minutesPerTick;
			int wholeMinutes = (int)accumulatedMinutes;
			if (wholeMinutes <= 0)
			{
				return;
			}

			accumulatedMinutes -= wholeMinutes;
			TickMinutes(wholeMinutes);
		}

		/// <summary>게임 내 minutes 만큼 욕구를 진행시키고 활동을 갱신 — 자율 구동·테스트 공통 진입점.</summary>
		public void TickMinutes(int minutes)
		{
			if (profile == null || needState == null || minutes <= 0)
			{
				return;
			}

			NeedModel.Step(needState, profile, minutes);
			RefreshActivity();
		}

		private void RefreshActivity()
		{
			ActivityKind next = ActivitySelector.Select(needState, profile, timeOfDay);
			if (next == CurrentActivity)
			{
				return;
			}

			CurrentActivity = next;
			OnActivityChanged(next);
		}
	}
}
