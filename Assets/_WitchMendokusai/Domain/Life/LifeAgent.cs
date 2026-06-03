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
		// 배회(Idle) 시 새 방향을 고르는 주기(초). 수치노출.
		[SerializeField] private float wanderInterval = 2f;
		// 이동 속도(유닛/초). 자체 transform 이동 — 큐브든 캐릭터든 의존 없이 어슬렁. 수치노출.
		[SerializeField] private float moveSpeed = 2f;
		// 지금 하는 활동이 그 욕구를 분당 채우는 양(자율 self-care). 소진보다 살짝 커서 활동이 한동안 지속(게임-시간 페이싱).
		// 0 = self-care 끔(욕구는 4호 개입(INC-4)으로만 채워짐). 수치노출 — 자율 일상 리듬 손잡이.
		[SerializeField] private float selfSatisfyPerMinute = 0.8f;
		// 활동을 그만두는 충족 수준(이력현상) — 현재 활동의 욕구가 이만큼 차야 다른 활동으로. 임계 근처 깜빡임(strobe) 방지.
		[SerializeField] private float contentLevel = 85f;
		// 어슬렁 반경(집/스폰 기준 유닛). 이 밖으론 안 나감 — 마을 주민답게 떠돌지 않고 광장 근처를 맴돈다. 수치노출.
		[SerializeField] private float wanderRadius = 6f;

		private TimeManager timeManager;
		private float wanderTimer;
		private Vector3 currentMoveDirection;
		private Vector3 home;
		private bool homeSet;
		private MeshRenderer bodyRenderer;
		private NeedProfile profile;
		private NeedState needState;
		private TimeOfDay timeOfDay = TimeOfDay.Morning;
		private float accumulatedMinutes;

		/// <summary>지금 이 캐릭터가 하는 활동(가장 최근 선택). 초기 Idle.</summary>
		public ActivityKind CurrentActivity { get; private set; } = ActivityKind.Idle;

		/// <summary>활동이 바뀔 때 통지(가시화·애니 훅). null 방지 초기값.</summary>
		public event Action<ActivityKind> OnActivityChanged = delegate { };

		public NeedState NeedState => needState;

		/// <summary>true 면 활동 전환마다 `[Life]` 로그(헤드리스 검증·디버그). 더미/프리뷰에서만 켬(런타임 set).</summary>
		public bool LogActivityChanges { get; set; }

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

			// 자율 self-care — 지금 고른 활동이 그 욕구를 스스로 채운다(밥 먹으면 배부름). 기본 일상 회복.
			// 소진(Step)보다 회복이 크면 욕구가 임계 위로 올라 다음 급한 욕구로 넘어가 활동이 순환(살아있음).
			// 4호 개입(INC-4)과 별개: 여긴 캐릭터가 혼자 푸는 기본 욕구, 개입은 못 푸는 문제·관계 도약.
			if (selfSatisfyPerMinute > 0f)
			{
				NeedKind? selfCare = ActivitySelector.NeedForActivity(CurrentActivity);
				if (selfCare.HasValue)
				{
					NeedModel.Satisfy(needState, profile, selfCare.Value, selfSatisfyPerMinute * minutes);
				}
			}
		}

		private void RefreshActivity()
		{
			// 이력현상 — 현재 활동을 그 욕구가 contentLevel 에 찰 때까지 유지(매 틱 깜빡임 방지, 자연스러운 리듬).
			ActivityKind next = ActivitySelector.SelectWithCommitment(needState, profile, timeOfDay, CurrentActivity, contentLevel);
			if (next == CurrentActivity)
			{
				return;
			}

			CurrentActivity = next;
			ApplyActivityVisual(next);
			if (LogActivityChanges)
			{
				// 자율 두뇌 heartbeat — 전환마다 1줄(greppable `[Life]`). 헤드리스 검증·디버그용(Play 시 Editor.log).
				Debug.Log($"[Life] {name} → {next}");
			}
			OnActivityChanged(next);
		}

		// 활동의 색 표현(INC-5c 시연) — 자율 두뇌가 고른 활동을 한눈에. 가구·애니 연동 전 임시 가시화.
		// (Bootstrap 후 1회 lazy 캐싱 — 에셋 없는 큐브엔 sharedMaterial 인스턴스가 생긴다.)
		private void ApplyActivityVisual(ActivityKind activity)
		{
			if (bodyRenderer == null)
			{
				bodyRenderer = GetComponent<MeshRenderer>();
				if (bodyRenderer == null)
				{
					return;
				}
			}

			bodyRenderer.material.color = ColorForActivity(activity);
		}

		// 활동별 식별 색 — 수치노출(시연 손잡이). Idle=흰 / 먹기=주황 / 자기=파랑 / 취미=초록 / 사교=분홍.
		private static Color ColorForActivity(ActivityKind activity) => activity switch
		{
			ActivityKind.Eat => new Color(1f, 0.6f, 0.2f),
			ActivityKind.Sleep => new Color(0.3f, 0.4f, 0.9f),
			ActivityKind.Hobby => new Color(0.4f, 0.8f, 0.4f),
			ActivityKind.Socialize => new Color(0.95f, 0.5f, 0.7f),
			_ => Color.white,
		};

		// 시각 이동(INC-5c) — 활동 무관 늘 마을을 어슬렁(주기적 새 방향 XZ 평면). 활동은 색으로 구분.
		// 자체 transform 이동 — UnitObject/물리 의존 0이라 큐브든 캐릭터든 붙이면 움직인다(시연 우선).
		// (활동별 목적지·가구 이동, 자는 동안 정지 등 의미적 이동은 후속 — 지금은 "살아있음"을 보이는 게 우선.)
		private void Update()
		{
			if (homeSet == false)
			{
				home = transform.position; // 첫 프레임 위치 = 집(스폰 지점). 이후 이 반경 안에서만 어슬렁.
				homeSet = true;
			}

			wanderTimer -= Time.deltaTime;
			if (wanderTimer <= 0f)
			{
				wanderTimer = wanderInterval;
				Vector2 random = UnityEngine.Random.insideUnitCircle.normalized;
				currentMoveDirection = new Vector3(random.x, 0f, random.y);
			}

			Vector3 next = transform.position + currentMoveDirection * (moveSpeed * Time.deltaTime);
			Vector3 fromHome = next - home;
			fromHome.y = 0f;
			if (fromHome.magnitude > wanderRadius)
			{
				// 집 반경 밖 = 경계로 되돌리고 다음 방향을 집 쪽으로(떠돌지 않게).
				currentMoveDirection = -fromHome.normalized;
				next = new Vector3(home.x, next.y, home.z) + fromHome.normalized * wanderRadius;
			}

			transform.position = next;
		}
	}
}
