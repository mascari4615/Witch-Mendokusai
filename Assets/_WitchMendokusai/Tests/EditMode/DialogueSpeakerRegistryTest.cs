using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-052 — 원고에 쓴 이름과 실제 캐릭터를 잇는 표의 회귀 잠금.
	///
	/// 잠그는 것: ① 이름으로 찾힌다 ② **사라진 캐릭터를 살아있다고 하지 않는다**(씬 전환 뒤 죽은 참조)
	/// ③ 남의 등록을 뺏지 않는다(재등장 순서 뒤엉킴) ④ 표가 없어도 대화가 안 죽는다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class DialogueSpeakerRegistryTest
	{
		private static Transform NewAnchor(string name) => new GameObject(name).transform;

		[Test]
		public void RegisteredName_IsFound()
		{
			DialogueSpeakerRegistry registry = new();
			Transform anchor = NewAnchor("욘");
			registry.Register("욘", anchor);

			Assert.That(registry.TryGetAnchor("욘", out Transform found), Is.True);
			Assert.That(found, Is.SameAs(anchor));
		}

		[Test]
		public void SurroundingSpacesAreIgnored()
		{
			DialogueSpeakerRegistry registry = new();
			registry.Register("  링 ", NewAnchor("링"));

			Assert.That(registry.TryGetAnchor("링", out Transform _), Is.True,
				"원고에 공백이 섞여도 같은 사람이다");
		}

		[Test]
		public void UnknownName_IsNotFound()
		{
			DialogueSpeakerRegistry registry = new();

			Assert.That(registry.TryGetAnchor("아무도아님", out Transform _), Is.False,
				"모르는 이름은 없다고 답한다 — 터뜨리면 이름 오타 하나로 대화가 죽는다");
		}

		[Test]
		public void DestroyedAnchor_IsTreatedAsMissing()
		{
			DialogueSpeakerRegistry registry = new();
			Transform anchor = NewAnchor("알리사");
			registry.Register("알리사", anchor);

			Object.DestroyImmediate(anchor.gameObject);

			Assert.That(registry.TryGetAnchor("알리사", out Transform _), Is.False,
				"씬이 바뀌면 등록만 남고 대상은 사라진다 — 그걸 살아있다고 하면 그 뒤가 전부 어긋난다");
			Assert.That(registry.Count, Is.Zero, "죽은 자리는 표에서도 치운다");
		}

		[Test]
		public void Unregister_OnlyRemovesOwnEntry()
		{
			DialogueSpeakerRegistry registry = new();
			Transform first = NewAnchor("욘1");
			Transform second = NewAnchor("욘2");
			registry.Register("욘", first);
			registry.Register("욘", second);

			registry.Unregister("욘", first);

			Assert.That(registry.TryGetAnchor("욘", out Transform found), Is.True);
			Assert.That(found, Is.SameAs(second), "먼저 있던 쪽이 나가면서 나중 것을 지우면 안 된다");
		}

		[Test]
		public void Bridge_WithoutRegistry_AnswersFalse()
		{
			DialogueSpeakerBridge.Clear(DialogueSpeakerBridge.Current);

			Assert.That(DialogueSpeakerBridge.TryGetAnchor("욘", out Transform _), Is.False,
				"캐릭터 배선이 아직이어도 대화는 돌아야 한다");
		}

		[Test]
		public void Bridge_FindsThroughRegisteredRegistry()
		{
			DialogueSpeakerRegistry registry = new();
			Transform anchor = NewAnchor("욘");
			registry.Register("욘", anchor);
			DialogueSpeakerBridge.Register(registry);
			try
			{
				Assert.That(DialogueSpeakerBridge.TryGetAnchor("욘", out Transform found), Is.True);
				Assert.That(found, Is.SameAs(anchor));
			}
			finally
			{
				DialogueSpeakerBridge.Clear(registry);
			}
		}
	}
}
