using System.Text;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 가챠가 <b>곡선을 어떻게 바꿨나</b> (TASK-WM-406).
	///
	/// ★ 영웅 배수를 공격·속도·기지·떨구기 넷에 물렸다. 그 순간 <b>기존 밸런스는 옛 것</b>이 됐다.
	///   재미가 늘었는지는 사람이 켜 봐야 알지만, <b>망가졌는지</b>는 여기서 잰다.
	///
	/// ★ 여기서 지키는 셋:
	///   ① 뽑기가 실제로 판을 앞당긴다 (안 그러면 환생석을 쓸 이유가 없다)
	///   ② 그런데 <b>폭주하지 않는다</b> (같은 갈래 합·다른 갈래 곱이 제 일을 하나)
	///   ③ 환생석을 뽑기에 다 써도 <b>판이 멎지 않는다</b> (환생 배수를 포기한 값이 있나)
	/// </summary>
	public sealed class IdleGachaCurveTests
	{
		private const double TICK = 10d;
		private const double SEVEN_DAYS = 7d * 24d * 3600d;

		/// <summary>
		/// ★ 뽑는 판이 <b>같은 시각에 더 깊이</b> 가 있다 — 아니면 가챠는 장식이다.
		///
		/// ★ <b>두 시간</b>에서 잰다. 이레 끝값으로는 못 잰다 — 지금 판은 반나절이면
		///   1619단계에서 멎어서(별도 결함, 개발 중) 양쪽 끝값이 같아진다.
		///   「멎기 전에 누가 앞서 있나」가 뽑기의 값어치다.
		/// </summary>
		[Test]
		public void PullingGetsYouDeeper()
		{
			IdleTuning tuning = new IdleTuning();

			IdleState hoarding = RunFor(tuning, false, 2d * 3600d);
			IdleState pulling = RunFor(tuning, true, 2d * 3600d);

			TestContext.WriteLine("[가챠곡선] 두 시간 — 안 뽑음: " + hoarding.BestStage + "단계 · 돌 "
				+ hoarding.Stones + "  ||  뽑음: " + pulling.BestStage + "단계 · " + pulling.PullsDone
				+ "번 뽑음 · 영웅 " + pulling.Heroes.Count + "종 · 도감 " + IdleHeroes.CodexScoreOf(pulling));

			Assert.Greater(pulling.BestStage, hoarding.BestStage,
				"뽑아도 더 깊이 못 간다 — 환생석을 쓸 이유가 없다");
		}

		/// <summary>
		/// ★ <b>폭주하지 않는다</b>. 같은 갈래끼리 곱했다면 여기서 터진다.
		///
		/// 이레에 천 단계를 넘으면 그건 성장이 아니라 되먹임 사고다 —
		/// 프레스티지 조율에서 이미 한 번 겪었다(점수가 깊이를, 깊이가 점수를 밀었다).
		/// </summary>
		[Test]
		public void PullingDoesNotExplode()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState pulling = Run(tuning, true);

			Assert.Less(pulling.BestStage, 5000,
				"이레에 5000단계를 넘었다 — 배수가 서로를 밀고 있다(같은 갈래를 곱하고 있나)");
		}

		/// <summary>
		/// ★ 환생석을 <b>다 뽑기에 써도</b> 판이 멎지 않는다.
		///
		/// 환생석은 환생 배수의 재료이기도 하다. 뽑기에 다 쓰면 배수를 포기하는 셈인데,
		/// 그래도 굴러가야 「뽑을까 아낄까」가 <b>결정</b>이 된다 — 한쪽이 정답이면 결정이 아니다.
		/// </summary>
		[Test]
		public void SpendingEverythingOnPulls_StillRuns()
		{
			IdleTuning tuning = new IdleTuning();
			IdleState pulling = Run(tuning, true);

			Assert.Greater(pulling.Kills, 0L, "판이 아예 안 돌았다");
			Assert.Greater(pulling.Ascensions, 0, "이레 동안 한 번도 환생을 못 했다");
			Assert.Greater(pulling.PullsDone, 0L, "한 번도 못 뽑았다 — 값이 너무 비싸다");
		}

		/// <summary>
		/// 이른 시간대 표 — <b>성장이 살아 있는 구간</b>을 본다 (실패하지 않는다).
		///
		/// ★ 왜 이 표가 필요한가: 이레 표는 <b>하루 만에 멎어</b> 아무것도 못 가른다.
		///   뽑기의 값어치는 「끝값」이 아니라 「같은 시각에 얼마나 앞서 있나」로 재야 한다.
		/// </summary>
		[Test]
		public void PrintEarlyHours()
		{
			IdleTuning tuning = new IdleTuning();
			StringBuilder table = new StringBuilder();
			table.AppendLine("[가챠곡선] 이른 시간 — 같은 시각의 깊이");
			table.AppendLine("시간 | 안뽑음 | 뽑음 | 뽑은횟수 | 영웅종류");

			IdleState hoarding = new IdleState();
			hoarding.EnsureProducerRoom(tuning.ProducerCount);

			IdleState pulling = new IdleState();
			pulling.EnsureProducerRoom(tuning.ProducerCount);

			double[] marks = { 0.5d, 1d, 2d, 4d, 8d, 12d, 24d };
			double at = 0d;

			for (int index = 0; index < marks.Length; index++)
			{
				double span = (marks[index] - at) * 3600d;
				at = marks[index];

				PlayFor(hoarding, tuning, span, false);
				PlayFor(pulling, tuning, span, true);

				table.AppendLine(marks[index] + "h | " + hoarding.BestStage + " | " + pulling.BestStage
					+ " | " + pulling.PullsDone + " | " + pulling.Heroes.Count);
			}

			TestContext.WriteLine(table.ToString());
			Assert.Pass();
		}

		/// <summary>이레 표 — 사람이 눈으로 보는 용도(실패하지 않는다).</summary>
		[Test]
		public void PrintSevenDayTable()
		{
			IdleTuning tuning = new IdleTuning();
			StringBuilder table = new StringBuilder();
			table.AppendLine("[가챠곡선] 하루별 — 뽑는 판");
			table.AppendLine("일 | 최고단계 | 영웅종류 | 도감점수 | 환생 | 남은돌 | 뽑은횟수");

			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);

			for (int day = 1; day <= 7; day++)
			{
				PlayFor(state, tuning, 24d * 3600d, true);

				table.AppendLine(day + " | " + state.BestStage + " | " + state.Heroes.Count
					+ " | " + IdleHeroes.CodexScoreOf(state) + " | " + state.Ascensions
					+ " | " + state.Stones + " | " + state.PullsDone);
			}

			TestContext.WriteLine(table.ToString());
			Assert.Pass();
		}

		private static IdleState Run(IdleTuning tuning, bool pulling)
		{
			return RunFor(tuning, pulling, SEVEN_DAYS);
		}

		private static IdleState RunFor(IdleTuning tuning, bool pulling, double seconds)
		{
			IdleState state = new IdleState();
			state.EnsureProducerRoom(tuning.ProducerCount);
			PlayFor(state, tuning, seconds, pulling);
			return state;
		}

		/// <summary>
		/// 사람 흉내 — 사고, 막히면 환생하고, (고르면) 환생석을 <b>다 뽑는다</b>.
		/// </summary>
		private static void PlayFor(IdleState state, IdleTuning tuning, double seconds, bool pulling)
		{
			for (double elapsed = 0d; elapsed < seconds; elapsed += TICK)
			{
				IdleModel.Step(state, tuning, TICK);
				IdlePlay.BuyEverything(state, tuning);

				// 천장에 닿았으면 환생한다 — 더 내려가도 등급이 안 열리는 자리다.
				if (IdleModel.PrestigeAwardFor(state, tuning) > 0L
					&& IdleDrops.MaxTierAt(state.Stage, state.Ascensions, tuning)
						>= IdleDrops.CeilingFor(state.Ascensions, tuning))
				{
					IdleModel.TryPrestige(state, tuning, out long _);
				}

				if (pulling == false)
				{
					continue;
				}

				while (IdleGacha.CanPull(state, tuning))
				{
					IdleGacha.TryPull(state, tuning, out IdleHeroPull _);
				}
			}
		}
	}
}
