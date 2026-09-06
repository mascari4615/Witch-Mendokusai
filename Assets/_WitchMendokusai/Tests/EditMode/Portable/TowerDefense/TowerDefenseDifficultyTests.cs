using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 난이도 회귀 — 같은 규칙 위에서 *숫자만* 달라야 한다. 규칙을 갈라 쓰면 난이도가 아니라
	/// 다른 게임이 되고, 「어려운 쪽을 배우면 쉬운 쪽도 이해된다」가 깨진다. TASK-WM-194.
	/// </summary>
	public class TowerDefenseDifficultyTests
	{
		[Test]
		public void 보통은_아무것도_안_바꾼다()
		{
			TowerDefenseDifficulty normal = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Normal);

			Assert.AreEqual(1f, normal.PressureScale);
			Assert.AreEqual(1f, normal.StartingResourceScale);
			Assert.AreEqual(1f, normal.LivesScale);
			Assert.AreEqual(1f, normal.NestHealthScale);
			Assert.AreEqual(1f, normal.EnemyCountScale);
		}

		[Test]
		public void 쉬움은_전부_너그럽다()
		{
			TowerDefenseDifficulty easy = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Easy);

			Assert.Less(easy.PressureScale, 1f, "압력이 더 세면 쉬움이 아니다.");
			Assert.Greater(easy.StartingResourceScale, 1f);
			Assert.Greater(easy.LivesScale, 1f);
			Assert.Less(easy.NestHealthScale, 1f, "둥지가 더 단단하면 끝이 더 멀어진다.");
			Assert.Less(easy.EnemyCountScale, 1f);
		}

		[Test]
		public void 어려움은_전부_가혹하다()
		{
			TowerDefenseDifficulty hard = TowerDefenseDifficulty.For(TowerDefenseDifficultyKind.Hard);

			Assert.Greater(hard.PressureScale, 1f);
			Assert.Less(hard.StartingResourceScale, 1f);
			Assert.Less(hard.LivesScale, 1f);
			Assert.Greater(hard.NestHealthScale, 1f);
			Assert.Greater(hard.EnemyCountScale, 1f);
		}

		[Test]
		public void 어느_난이도든_0으로_죽지_않는다()
		{
			// 배수가 0 이면 그 축이 통째로 사라진다 — 난이도가 아니라 규칙 삭제다.
			foreach (TowerDefenseDifficultyKind kind in System.Enum.GetValues(typeof(TowerDefenseDifficultyKind)))
			{
				TowerDefenseDifficulty difficulty = TowerDefenseDifficulty.For(kind);

				Assert.Greater(difficulty.PressureScale, 0f);
				Assert.Greater(difficulty.StartingResourceScale, 0f);
				Assert.Greater(difficulty.LivesScale, 0f);
				Assert.Greater(difficulty.NestHealthScale, 0f);
				Assert.Greater(difficulty.EnemyCountScale, 0f);
			}
		}

		[Test]
		public void 순환은_세_단계를_돈다()
		{
			TowerDefenseDifficultyKind kind = TowerDefenseDifficultyKind.Easy;

			kind = TowerDefenseDifficulty.Next(kind);
			Assert.AreEqual(TowerDefenseDifficultyKind.Normal, kind);
			kind = TowerDefenseDifficulty.Next(kind);
			Assert.AreEqual(TowerDefenseDifficultyKind.Hard, kind);
			kind = TowerDefenseDifficulty.Next(kind);
			Assert.AreEqual(TowerDefenseDifficultyKind.Easy, kind, "끝에서 처음으로 돌아와야 버튼 하나로 고를 수 있다.");
		}

		[Test]
		public void 이름이_비어있지_않다()
		{
			foreach (TowerDefenseDifficultyKind kind in System.Enum.GetValues(typeof(TowerDefenseDifficultyKind)))
				Assert.IsNotEmpty(TowerDefenseDifficulty.NameOf(kind));
		}
	}
}
