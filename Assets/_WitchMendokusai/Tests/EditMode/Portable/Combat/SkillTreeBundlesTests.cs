using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	public sealed class SkillTreeBundlesTests
	{
		// 검 묶음: 뿌리, 갈림길 A/B, 마무리. 최종 카드 둘은 묶음 밖 갈림길
		private static readonly SkillTreeBundleNode[] NODES =
		{
			new SkillTreeBundleNode("blade_root", "blade", null),
			new SkillTreeBundleNode("blade_wide", "blade", "blade_choice"),
			new SkillTreeBundleNode("blade_fast", "blade", "blade_choice"),
			new SkillTreeBundleNode("blade_finish", "blade", null),
			new SkillTreeBundleNode("final_sword", null, "final"),
			new SkillTreeBundleNode("final_volley", null, "final"),
			new SkillTreeBundleNode("repeat", null, null),
		};

		private static Func<string, int> Owned(params string[] ids)
		{
			HashSet<string> owned = new HashSet<string>(ids);
			return id => owned.Contains(id) ? 1 : 0;
		}

		[Test]
		public void 갈림길_하나를_받으면_다른_쪽이_잠긴다()
		{
			Func<string, int> owned = Owned("blade_root", "blade_wide");

			Assert.IsTrue(SkillTreeBundles.IsExcluded(NODES, "blade_fast", owned));
			Assert.IsFalse(SkillTreeBundles.IsExcluded(NODES, "blade_wide", owned), "받은 쪽은 잠기지 않음");
		}

		[Test]
		public void 아무것도_안_받은_갈림길은_둘_다_열려_있다()
		{
			Func<string, int> owned = Owned("blade_root");

			Assert.IsFalse(SkillTreeBundles.IsExcluded(NODES, "blade_wide", owned));
			Assert.IsFalse(SkillTreeBundles.IsExcluded(NODES, "blade_fast", owned));
		}

		[Test]
		public void 갈림길이_없는_노드는_잠기지_않는다()
		{
			Assert.IsFalse(SkillTreeBundles.IsExcluded(NODES, "repeat", Owned("final_sword")));
		}

		[Test]
		public void 묶음은_잠긴_쪽을_빼고_전부_받으면_마무리된다()
		{
			Assert.IsTrue(SkillTreeBundles.IsFinished(NODES, "blade", Owned("blade_root", "blade_fast", "blade_finish")));
		}

		[Test]
		public void 갈림길을_안_고르면_마무리가_아니다()
		{
			Assert.IsFalse(SkillTreeBundles.IsFinished(NODES, "blade", Owned("blade_root", "blade_finish")));
		}

		[Test]
		public void 마무리_노드를_안_받으면_마무리가_아니다()
		{
			Assert.IsFalse(SkillTreeBundles.IsFinished(NODES, "blade", Owned("blade_root", "blade_wide")));
		}

		[Test]
		public void 없는_묶음은_마무리될_수_없다()
		{
			Assert.IsFalse(SkillTreeBundles.IsFinished(NODES, "bladd", Owned("blade_root", "blade_wide", "blade_finish")));
		}

		[Test]
		public void 최종_카드_둘은_하나를_고르면_다른_쪽이_영구_잠긴다()
		{
			Assert.IsTrue(SkillTreeBundles.IsExcluded(NODES, "final_volley", Owned("final_sword")));
		}

		[Test]
		public void 진행은_갈림길을_한_칸으로_센다()
		{
			Assert.AreEqual((1, 3), SkillTreeBundles.Progress(NODES, "blade", Owned("blade_root")));
			Assert.AreEqual((2, 3), SkillTreeBundles.Progress(NODES, "blade", Owned("blade_root", "blade_wide")));
			Assert.AreEqual((3, 3), SkillTreeBundles.Progress(NODES, "blade", Owned("blade_root", "blade_wide", "blade_finish")));
		}

		[Test]
		public void 트리에_없는_노드를_물으면_실패한다()
		{
			Assert.Throws<ArgumentException>(() => SkillTreeBundles.IsExcluded(NODES, "nope", Owned()));
		}
	}
}
