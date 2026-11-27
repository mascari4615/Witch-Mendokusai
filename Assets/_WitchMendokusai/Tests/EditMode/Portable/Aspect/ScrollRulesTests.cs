using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai.Tests
{
	public sealed class ScrollRulesTests
	{
		private static List<ScrollEntry> Entries() => new List<ScrollEntry>
		{
			new ScrollEntry { Id = 1, Kind = ScrollResultKind.Spell, Target = 10, Forms = 2 },
			new ScrollEntry { Id = 2, Kind = ScrollResultKind.Recipe, Target = 20, Forms = 1 },
			new ScrollEntry { Id = 3, Kind = ScrollResultKind.Spell, Target = 30, Forms = 3 },
		};

		private static int First(int count) => 0;

		private static ScrollDraw DrawAndApply(List<ScrollEntry> entries, ScrollKnowledge knowledge)
		{
			Assert.That(ScrollRules.TryDraw(entries, knowledge, First, out ScrollDraw draw), Is.True);
			ScrollRules.Apply(knowledge, draw);
			return draw;
		}

		[Test]
		public void 모르는_것이_있으면_모르는_것만_나온다()
		{
			List<ScrollEntry> entries = Entries();
			ScrollKnowledge knowledge = new ScrollKnowledge();
			knowledge.OpenedForms[1] = 1;

			for (int i = 0; i < 2; i++)
			{
				int index = i;
				Assert.That(ScrollRules.TryDraw(entries, knowledge, count => index, out ScrollDraw draw), Is.True);
				Assert.That(draw.Entry.Id, Is.Not.EqualTo(1), "배운 1 은 다른 것을 다 배우기 전에는 안 나옴");
				Assert.That(draw.IsNew, Is.True);
			}
		}

		[Test]
		public void 다_배우면_안_열린_모습이_나온다()
		{
			List<ScrollEntry> entries = Entries();
			ScrollKnowledge knowledge = new ScrollKnowledge();
			for (int i = 0; i < 3; i++)
			{
				DrawAndApply(entries, knowledge);
			}

			ScrollDraw draw = DrawAndApply(entries, knowledge);

			Assert.That(draw.Entry.Id, Is.EqualTo(1), "모습이 하나뿐인 2 는 후보가 아님. 첫 후보가 1");
			Assert.That(draw.Form, Is.EqualTo(2));
			Assert.That(draw.IsNew, Is.False);
		}

		[Test]
		public void 다_모으면_풀_수_없다()
		{
			List<ScrollEntry> entries = Entries();
			ScrollKnowledge knowledge = new ScrollKnowledge();
			int total = 2 + 1 + 3;
			for (int i = 0; i < total; i++)
			{
				Assert.That(ScrollRules.CanInterpret(entries, knowledge), Is.True, $"{i} 번째");
				DrawAndApply(entries, knowledge);
			}

			Assert.That(ScrollRules.CanInterpret(entries, knowledge), Is.False);
			Assert.That(ScrollRules.TryDraw(entries, knowledge, First, out _), Is.False);
			Assert.That(knowledge.Opened(3), Is.EqualTo(3));
		}

		[Test]
		public void 같은_뽑기를_두_번_기록하면_막는다()
		{
			List<ScrollEntry> entries = Entries();
			ScrollKnowledge knowledge = new ScrollKnowledge();
			ScrollRules.TryDraw(entries, knowledge, First, out ScrollDraw draw);
			ScrollRules.Apply(knowledge, draw);

			Assert.Throws<System.InvalidOperationException>(() => ScrollRules.Apply(knowledge, draw));
		}

		[Test]
		public void 무작위가_범위를_넘으면_막는다()
		{
			Assert.Throws<System.ArgumentOutOfRangeException>(() => ScrollRules.TryDraw(Entries(), new ScrollKnowledge(), count => count, out _));
		}

		[Test]
		public void 빈_목록은_풀_수_없다()
		{
			List<ScrollEntry> none = new List<ScrollEntry>();

			Assert.That(ScrollRules.CanInterpret(none, new ScrollKnowledge()), Is.False);
			Assert.That(ScrollRules.TryDraw(none, new ScrollKnowledge(), First, out _), Is.False);
		}
	}
}
