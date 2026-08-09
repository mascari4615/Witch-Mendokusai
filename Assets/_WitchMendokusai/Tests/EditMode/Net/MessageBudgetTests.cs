using NUnit.Framework;
using WitchMendokusai.Net;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>말 예산 (TASK-WM-218) — 창 하나가 모두의 세계를 느리게 만들지 못하게.</summary>
	public sealed class MessageBudgetTests
	{
		[Test]
		public void 평소에는_그냥_말한다()
		{
			MessageBudget budget = new MessageBudget();

			for (int i = 0; i < 10; i++)
				Assert.That(budget.TrySpend(), Is.True);
		}

		[Test]
		public void 쏟아부으면_막힌다()
		{
			MessageBudget budget = new MessageBudget();

			int allowed = 0;
			for (int i = 0; i < 1000; i++)
			{
				if (budget.TrySpend())
					allowed++;
			}

			// 버그 난 창 하나가 초당 수천 번 보내도 세계는 그만큼 일하지 않는다.
			Assert.That(allowed, Is.EqualTo((int)MessageBudget.BURST));
		}

		[Test]
		public void 시간이_지나면_다시_말할_수_있다()
		{
			MessageBudget budget = new MessageBudget();
			while (budget.TrySpend())
			{
			}

			budget.Refill(1f);

			Assert.That(budget.TrySpend(), Is.True);
			Assert.That(budget.Remaining, Is.LessThanOrEqualTo(MessageBudget.REFILL_PER_SECOND));
		}

		[Test]
		public void 가만히_있었다고_무한히_쌓이지_않는다()
		{
			MessageBudget budget = new MessageBudget();

			budget.Refill(3600f); // 한 시간 가만히 있었다

			int allowed = 0;
			while (budget.TrySpend())
				allowed++;

			// 안 그러면 「가만히 있다가 한꺼번에 쏟기」가 가능해진다.
			Assert.That(allowed, Is.EqualTo((int)MessageBudget.BURST));
		}
	}
}
