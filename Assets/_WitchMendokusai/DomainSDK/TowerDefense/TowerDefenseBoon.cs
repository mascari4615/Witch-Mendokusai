using System;

namespace WitchMendokusai
{
	/// <summary> 드래프트 카드 한 장의 종류(TASK-WM-194). </summary>
	public enum TowerDefenseBoonKind
	{
		Firepower = 0, // 모든 인형의 피해.
		Income = 1,    // 파도 정산.
		Bounty = 2,    // 격파 보상.
		Life = 3,      // 목숨(즉시).
		Essence = 4,   // 정수(즉시).
		Windfall = 5,  // 자원(즉시).
	}

	/// <summary> 카드 한 장 — 화면은 이 세 값만 읽고 그린다. </summary>
	public readonly struct TowerDefenseBoon
	{
		public readonly TowerDefenseBoonKind Kind;
		public readonly float Magnitude;
		public readonly string DisplayName;
		public readonly string Note;

		public TowerDefenseBoon(TowerDefenseBoonKind kind, float magnitude, string displayName, string note)
		{
			Kind = kind;
			Magnitude = magnitude;
			DisplayName = displayName;
			Note = note;
		}

		public bool IsValid => string.IsNullOrEmpty(DisplayName) == false;
	}

	/// <summary>
	/// 드래프트 수치 노브 — 전부 스테이지 SO 로 노출(수치 노출 룰: 하드코딩 0).
	/// 즉시 효과(목숨·정수·자원)와 지속 효과(피해·정산·보상 배수)가 한 표에 있어야 세기를 나란히 볼 수 있다.
	/// </summary>
	[Serializable]
	public struct TowerDefenseDraftRules
	{
		public int OfferCount;         // 한 번에 내놓는 장수(0 이면 드래프트 없음).
		public float FirepowerBonus;   // 누적 피해 증가 비율(0.15 = 장당 +15%).
		public float IncomeBonus;      // 누적 정산 증가 비율.
		public float BountyBonus;      // 누적 격파 보상 증가 비율.
		public float LivesBonus;       // 즉시 목숨.
		public float EssenceBonus;     // 즉시 정수.
		public float WindfallResource; // 즉시 자원.

		public bool IsEnabled => OfferCount > 0;
	}

	/// <summary>
	/// 이 판에서 고른 것들의 누적(TASK-WM-194). 지속 효과는 여기 쌓이고, 즉시 효과는 셸이 그 자리에서 지급한다
	/// — 「무엇이 쌓였나」와 「무엇을 받았나」를 한 곳에 섞으면 화면이 거짓말을 하게 된다.
	/// </summary>
	public sealed class TowerDefenseBoonState
	{
		private float firepower;
		private float income;
		private float bounty;

		/// <summary> 지금까지 고른 장수 — 화면이 「N번째 선택」을 말할 때 쓴다. </summary>
		public int TakenCount { get; private set; }

		public void Take(TowerDefenseBoon boon)
		{
			TakenCount++;

			switch (boon.Kind)
			{
				case TowerDefenseBoonKind.Firepower:
					firepower += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Income:
					income += boon.Magnitude;
					break;
				case TowerDefenseBoonKind.Bounty:
					bounty += boon.Magnitude;
					break;
				// 즉시 효과는 상태에 안 쌓인다 — 받은 순간 끝.
				default:
					break;
			}
		}

		public float DamageMultiplier => 1f + firepower;
		public float IncomeMultiplier => 1f + income;
		public float BountyMultiplier => 1f + bounty;

		/// <summary> 지금 쌓인 것을 한 줄로 — 판 도중에 「내가 뭘 골랐더라」가 화면에 없으면 선택이 기억에 안 남는다. </summary>
		public string Describe()
		{
			if (TakenCount == 0)
				return string.Empty;

			string text = string.Empty;
			if (firepower > 0f)
				text += "피해 +" + (int)(firepower * 100f) + "% ";
			if (income > 0f)
				text += "정산 +" + (int)(income * 100f) + "% ";
			if (bounty > 0f)
				text += "보상 +" + (int)(bounty * 100f) + "%";
			return text.Trim();
		}

		public void Reset()
		{
			firepower = 0f;
			income = 0f;
			bounty = 0f;
			TakenCount = 0;
		}
	}
}
