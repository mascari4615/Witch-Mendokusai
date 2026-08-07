using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Workshop;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-170 — 듀얼루프(낮의 마계 / 밤의 공방) 단계를 <b>세계 시계에 붙이는</b> 첫 배선.
	///
	/// ★ 왜 이 파일이 생겼나: <see cref="DayNightCycle"/> 은 만들어진 뒤로 <b>게임 어디서도 안 불렸다</b>
	///   (같은 처지의 층이 4개, 타입 27종 — 돌봄·물류·정련·공방). 아무도 안 부르는 코드는 지워져도
	///   컴파일이 안 깨지므로 <b>조용히 사라진다</b> — 2026-08-07 에 실제로 그렇게 105개가 날아갔다.
	///   호출처를 하나 박아 두면 다음번엔 컴파일이 먼저 운다.
	///
	/// ★ 시계를 새로 만들지 않는다: 이 프로젝트의 시간 정본은 <see cref="WorldClock"/> 하나다
	///   (하늘 <c>SkyDirector</c> 도, 생활 <c>LifeDirector</c> 도 거기서 읽는다). 여기서 자체 타이머를
	///   돌리면 「낮인데 밤 하늘」 같은 갈라짐이 난다. 그래서 <b>시각 변화 알림만 받아</b> 단계를 맞춘다.
	///
	/// 씬에 하나 놓는다. 시계가 없으면(전투 씬 등) 조용히 쉰다 — 있는 씬에서만 도는 게 맞다.
	/// </summary>
	public class WorkshopDirector : MonoBehaviour
	{
		[Header("듀얼루프 교대 시각 (세계 시계 기준)")]
		[Tooltip("이 시각부터 낮(마계 채집·전투) — 밤의 공방이 닫히고 하루 인덱스가 +1 된다.")]
		[SerializeField] private int dayStartHour = 6;

		[Tooltip("이 시각부터 밤(공방 운영) — 낮의 수확이 밤의 재료가 된다.")]
		[SerializeField] private int nightStartHour = 18;

		[Header("공방이 파는 것 — 비워 두는 게 기본")]
		[Tooltip("「무엇을 파는 가게인가」는 디자인 결정이라 코드가 정하지 않는다. 상품 에셋을 만들어 여기 넣으면 그날 밤부터 팔린다. 비어 있으면 밤에 아무 일도 안 일어난다.")]
		[SerializeField] private WorkshopProductSO[] products = System.Array.Empty<WorkshopProductSO>();

		[Header("밤 골드 → 다음 낮 채집 효율 (듀얼루프 다리)")]
		[Tooltip("아무것도 투자 안 했을 때의 효율. 1 = 채집량 그대로.")]
		[SerializeField] private float baseEfficiency = 1f;

		[Tooltip("효율이 한 칸 오르는 데 드는 골드.")]
		[SerializeField] private float goldPerEfficiencyStep = 100f;

		[Tooltip("한 칸당 오르는 효율. 0.2 = 칸마다 +20%.")]
		[SerializeField] private float efficiencyPerStep = 0.2f;

		[Tooltip("효율 상한 — 없으면 밤 수익이 무한히 굴러 밸런스가 무너진다.")]
		[SerializeField] private float maxEfficiency = 3f;

		private WorldClock worldClock;
		private DayNightCycle cycle;
		private readonly WorkshopLedger ledger = new WorkshopLedger();
		private readonly List<WorkshopProduct> productValues = new List<WorkshopProduct>();

		/// <summary>지금이 낮인가 밤인가. 시계가 없으면 낮으로 시작한 채 멈춰 있다.</summary>
		public DayNightPhase Phase => cycle == null ? DayNightPhase.Day : cycle.Phase;

		/// <summary>
		/// 듀얼루프가 몇 바퀴 돌았나 (밤 → 낮 전환마다 +1).
		/// <b>달력의 날짜(<see cref="WorldClock.Day"/>)와 일부러 다르다</b> — 한 바퀴는 자정이 아니라
		/// <see cref="dayStartHour"/> 에서 닫힌다. 경영 정산이 걸릴 자리라 이쪽이 기준이다.
		/// </summary>
		public int DayIndex => cycle == null ? 0 : cycle.DayIndex;

		/// <summary>단계가 실제로 바뀐 순간만 알린다(같은 단계 재통보 X).</summary>
		public event Action<DayNightPhase> OnPhaseChanged = delegate { };

		/// <summary>재료 재고·골드 원장. 낮 루프가 여기에 전리품을 넣고, 밤 장사가 여기서 뺀다.</summary>
		public WorkshopLedger Ledger => ledger;

		/// <summary>지금까지 투자한 만큼 오른 낮 채집 효율. 낮 루프가 수확량에 곱해 쓸 값이다.</summary>
		public float DayCollectionEfficiency => DayEfficiencyModel.Evaluate(ledger.GoldInvestedInDayEfficiency, Coefficients);

		private DayEfficiencyCoefficients Coefficients =>
			new DayEfficiencyCoefficients(baseEfficiency, goldPerEfficiencyStep, efficiencyPerStep, maxEfficiency);

		/// <summary>
		/// 시각 하나가 낮인지 밤인지 — 씬·시계 없이 판정 가능한 순수 규칙(시험이 여기를 본다).
		/// 밤이 자정을 넘어가는 경우(예: 18시~6시)를 포함한다.
		/// </summary>
		public static DayNightPhase PhaseAtHour(int hour, int dayStartHour, int nightStartHour)
		{
			if (dayStartHour == nightStartHour)
			{
				return DayNightPhase.Day;
			}

			if (dayStartHour < nightStartHour)
			{
				// 낮 구간이 하루 안에서 안 끊긴다 — 예: 6시~18시가 낮.
				return hour >= dayStartHour && hour < nightStartHour ? DayNightPhase.Day : DayNightPhase.Night;
			}

			// 낮 구간이 자정을 넘는다 — 예: 20시~4시가 낮.
			return hour >= dayStartHour || hour < nightStartHour ? DayNightPhase.Day : DayNightPhase.Night;
		}

		// init-order-ok: WorldClock 은 씬 정적 배치라 Start 시점 존재. 없으면 null-skip 폴백(LifeDirector 와 동일).
		private void Start()
		{
			if (WorldClock.TryGetExistingInstance(out worldClock) == false)
			{
				return;
			}

			cycle = new DayNightCycle(PhaseAtHour(worldClock.Hour, dayStartHour, nightStartHour), worldClock.Day);
			worldClock.OnHourChanged += OnHourChanged;

			// 에셋(유니티) → 순수 값(계산 층). 한 번만 옮겨 두고 밤마다 재사용한다.
			for (int index = 0; index < products.Length; index++)
			{
				if (products[index] != null)
				{
					productValues.Add(products[index].ToProduct());
				}
			}
		}

		private void OnDestroy()
		{
			if (worldClock != null)
			{
				worldClock.OnHourChanged -= OnHourChanged;
			}
		}

		private void OnHourChanged(int hour)
		{
			DayNightPhase wanted = PhaseAtHour(hour, dayStartHour, nightStartHour);
			if (cycle.Phase == wanted)
			{
				return;
			}

			// 상태기는 교대만 안다(낮↔밤). 시각을 건너뛰어도 한 번에 한 단계라 여기서 맞을 때까지 민다.
			// 두 단계뿐이라 최대 1회지만, 단계가 늘어나도 안 깨지게 루프로 둔다.
			int guard = 0;
			while (cycle.Phase != wanted && guard < 4)
			{
				cycle.Advance();
				guard = guard + 1;
			}

			if (cycle.Phase == DayNightPhase.Night)
			{
				// 밤이 왔다 = 낮에 모은 재료로 장사할 시간. 상품이 하나도 없으면 조용히 0으로 끝난다.
				WorkshopNightShift.Run(ledger, productValues);
			}

			OnPhaseChanged.Invoke(cycle.Phase);
		}
	}
}
