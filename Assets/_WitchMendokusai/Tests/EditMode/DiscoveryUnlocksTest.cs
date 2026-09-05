using NUnit.Framework;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 도감 해금 계약 회귀 — 도감은 조건을 정하지 않는다. 밖에서 답을 받고 세기만 한다.
	/// 여기가 새면 잠금이 조용히 뒤집혀 「다 열렸다」나 「다 잠겼다」가 된다.
	/// </summary>
	public class DiscoveryUnlocksTest
	{
		private sealed class FakeSource : IDiscoveryUnlockSource
		{
			private readonly string unlockedEntryId;

			public FakeSource(string catalogId, string unlockedEntryId)
			{
				CatalogId = catalogId;
				this.unlockedEntryId = unlockedEntryId;
			}

			public string CatalogId { get; }

			public bool IsUnlocked(string entryId) => entryId == unlockedEntryId;
		}

		[SetUp]
		public void Reset() => DiscoveryUnlocks.Clear();

		[TearDown]
		public void Cleanup() => DiscoveryUnlocks.Clear();

		[Test]
		public void 출처가_없는_갈래는_열림이다()
		{
			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked("plant", "P_1"),
				"조건을 아는 쪽이 아직 없으면 다 보이는 것이 잠금 층 도입 전 거동이다.");
		}

		[Test]
		public void 출처를_꽂으면_그_답을_따른다()
		{
			DiscoveryUnlocks.Register(new FakeSource("plant", "P_1"));

			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked("plant", "P_1"), "출처가 열렸다고 한 항목.");
			Assert.IsFalse(DiscoveryUnlocks.IsUnlocked("plant", "P_2"), "출처가 안 열렸다고 한 항목.");
		}

		[Test]
		public void 갈래마다_따로_묻는다()
		{
			DiscoveryUnlocks.Register(new FakeSource("plant", "P_1"));

			Assert.IsFalse(DiscoveryUnlocks.IsUnlocked("plant", "I_1"), "식물 출처는 자기 갈래만 답한다.");
			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked("item", "I_1"), "출처 없는 아이템 갈래는 그대로 열림.");
		}

		[Test]
		public void 같은_갈래를_다시_꽂으면_뒤엣것이_이긴다()
		{
			DiscoveryUnlocks.Register(new FakeSource("plant", "P_1"));
			DiscoveryUnlocks.Register(new FakeSource("plant", "P_2"));

			Assert.IsFalse(DiscoveryUnlocks.IsUnlocked("plant", "P_1"), "앞 출처는 밀려난다.");
			Assert.IsTrue(DiscoveryUnlocks.IsUnlocked("plant", "P_2"), "뒤 출처가 답한다.");
		}

		[Test]
		public void 센_결과는_비율과_완성을_말한다()
		{
			DiscoveryProgress partial = new(4, 1);
			Assert.AreEqual(0.25d, partial.Ratio, 0.0001d, "넷 중 하나.");
			Assert.IsFalse(partial.IsComplete);

			DiscoveryProgress full = new(4, 4);
			Assert.IsTrue(full.IsComplete);

			DiscoveryProgress empty = new(0, 0);
			Assert.AreEqual(0d, empty.Ratio, 0.0001d, "항목이 없으면 0 나눗셈 대신 0.");
			Assert.IsFalse(empty.IsComplete, "항목이 하나도 없는 것은 채운 것이 아니다.");
		}
	}
}
