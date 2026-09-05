using System;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary> 판을 시작할 때 고르는 결. append-only(저장된 기록이 번호를 쓴다). </summary>
	public enum TowerDefenseDifficultyKind
	{
		Easy = 0,
		Normal = 1,
		Hard = 2,
	}

	/// <summary>
	/// 시작 난이도(TASK-WM-194) — 같은 판을 *다른 조건*으로 시작한다.
	///
	/// ★ 왜 필요한가: 지금은 모두가 같은 조건으로 시작한다. 처음 하는 사람은 첫 무리에 쓸리고,
	///   익숙한 사람은 초반이 지루하다. 하나의 곡선으로 둘을 다 만족시킬 수는 없다.
	/// ★ 왜 *배수*로만 두나: 규칙을 난이도마다 갈라 쓰면(예: 쉬움엔 둥지 없음) 난이도가 아니라 다른
	///   게임이 된다. 같은 규칙 위에서 숫자만 달라야 「어려운 쪽을 배우면 쉬운 쪽도 이해된다」가 성립한다.
	///
	/// 순수 정적 — 씬·RNG 0.
	/// </summary>
	[Serializable]
	public struct TowerDefenseDifficulty
	{
		[Tooltip("마수 강화 속도 배수 — 시간이 올리는 압력에 곱해진다.")]
		public float PressureScale;

		[Tooltip("시작 자원 배수.")]
		public float StartingResourceScale;

		[Tooltip("시작 목숨 배수.")]
		public float LivesScale;

		[Tooltip("둥지 체력 배수 — 낮으면 밀어내기가 빨라진다(끝이 가까워진다).")]
		public float NestHealthScale;

		[Tooltip("마수 수 배수.")]
		public float EnemyCountScale;

		public static TowerDefenseDifficulty For(TowerDefenseDifficultyKind kind)
		{
			return kind switch
			{
				// 쉬움 = 배우는 판. 압력이 천천히 오르고 밑천이 넉넉하며 둥지가 빨리 무너진다
				//        — 「이길 수 있다」를 한 번 겪어봐야 규칙이 몸에 남는다.
				TowerDefenseDifficultyKind.Easy => new TowerDefenseDifficulty
				{
					PressureScale = 0.6f,
					StartingResourceScale = 1.6f,
					LivesScale = 1.8f,
					NestHealthScale = 0.7f,
					EnemyCountScale = 0.75f,
				},

				// 어려움 = 이미 아는 사람의 판. 초반부터 몰아치고 둥지가 단단해 *밀어내는 데 오래 걸린다*.
				TowerDefenseDifficultyKind.Hard => new TowerDefenseDifficulty
				{
					PressureScale = 1.5f,
					StartingResourceScale = 0.7f,
					LivesScale = 0.6f,
					NestHealthScale = 1.5f,
					EnemyCountScale = 1.4f,
				},

				_ => new TowerDefenseDifficulty
				{
					PressureScale = 1f,
					StartingResourceScale = 1f,
					LivesScale = 1f,
					NestHealthScale = 1f,
					EnemyCountScale = 1f,
				},
			};
		}

		public static string NameOf(TowerDefenseDifficultyKind kind)
		{
			return kind switch
			{
				TowerDefenseDifficultyKind.Easy => "쉬움",
				TowerDefenseDifficultyKind.Hard => "어려움",
				_ => "보통",
			};
		}

		/// <summary> 다음 난이도로 순환 — 화면 버튼 하나로 고르게 한다(창을 새로 세우지 않는다). </summary>
		public static TowerDefenseDifficultyKind Next(TowerDefenseDifficultyKind kind)
		{
			return kind switch
			{
				TowerDefenseDifficultyKind.Easy => TowerDefenseDifficultyKind.Normal,
				TowerDefenseDifficultyKind.Normal => TowerDefenseDifficultyKind.Hard,
				_ => TowerDefenseDifficultyKind.Easy,
			};
		}
	}
}
