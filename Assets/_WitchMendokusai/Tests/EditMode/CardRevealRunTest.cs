using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace WitchMendokusai.Tests
{
	// 공개 시간축 끝 처리, 입력 보호, 카드 층 확대 유지. 2026-11-21 끝 콜백 안에서 자기 Sequence 를 Kill 해 DOTween 활성 목록이 깨지고
	// 공개 뒤 카드 층 확대 복귀 tween 이 돌지 않아 카드가 큰 채로 남았다가 다음 공개에서 갑자기 줄던 회귀
	// - DOTween 깨짐 자체는 편집 모드에서 재현 안 됨. 이 시험은 끝 알림 횟수, 보호 구간, 확대 유지만 지킴
	// - DOTween 회귀는 Play 재현 스크립트 memo wm/design/card-pack-v2-20261120/verify/kill-in-callback-repro.cs (옛 코드 IndexOutOfRange 1, 새 코드 0)
	public class CardRevealRunTest
	{
		private const float STEP = 0.05f;
		private static readonly FieldInfo RUN_FIELD = typeof(UICardRevealDirector).GetField("run", BindingFlags.NonPublic | BindingFlags.Instance);

		private readonly List<GameObject> created = new();
		private readonly List<ScriptableObject> assets = new();
		private bool savedEffects;

		private sealed class StubSkin : CardRevealSkin
		{
			public float Length = 0.2f;

			public override void Build(CardRevealRun run) => run.Mark(Length);
		}

		[SetUp]
		public void SetUp()
		{
			savedEffects = UIEffects.Enabled;
			UIEffects.Enabled = true;
		}

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject createdObject in created)
				Object.DestroyImmediate(createdObject);
			created.Clear();
			foreach (ScriptableObject asset in assets)
				Object.DestroyImmediate(asset);
			assets.Clear();
			UIEffects.Enabled = savedEffects;
			LogAssert.ignoreFailingMessages = false;
		}

		[Test]
		public void Natural_finish_runs_once_without_tween_errors()
		{
			UICardRevealDirector director = NewDirector(out _);
			int finishedCount = 0;
			director.Finished += () => finishedCount++;
			Assert.IsTrue(director.PlayExact(NewSkin(0.4f, 0f, 1f), Entries()));
			CardRevealRun run = Run(director);

			// 끝 알림은 확대 유지, 어둡게 tween 생성과 Sequence Kill 을 함께 함. 콜백 안에서 하면 DOTween 예외
			Assert.DoesNotThrow(() =>
			{
				for (int i = 0; i < 30; i++)
					run.Tick(STEP);
			});
			Assert.AreEqual(1, finishedCount);
			Assert.IsFalse(director.IsRevealing);
			// 깨진 활성 목록은 뒤이은 tween 생성과 Kill 에서 드러남 (옛 코드에서 IndexOutOfRangeException)
			Assert.DoesNotThrow(() =>
			{
				for (int i = 0; i < 10; i++)
				{
					director.PlayExact(NewSkin(0.4f, 0f, 1f), Entries());
					Run(director).Tick(STEP);
					director.Cancel();
				}
			});
			director.Cancel();
		}

		[Test]
		public void Skip_finishes_once()
		{
			UICardRevealDirector director = NewDirector(out _);
			int finishedCount = 0;
			director.Finished += () => finishedCount++;
			Assert.IsTrue(director.PlayExact(NewSkin(1f, 0f, 1f), Entries()));
			CardRevealRun run = Run(director);
			run.Tick(STEP);
			Assert.DoesNotThrow(run.Skip);
			Assert.AreEqual(1, finishedCount);
			Assert.IsFalse(director.IsRevealing);
			director.Cancel();
		}

		[Test]
		public void Input_inside_skip_guard_is_consumed_without_skipping()
		{
			UICardRevealDirector director = NewDirector(out _);
			Assert.IsTrue(director.PlayExact(NewSkin(1f, 0.3f, 1f), Entries()));

			Assert.IsTrue(director.InSkipGuard);
			Assert.IsTrue(director.ConsumeBlockedChoice(), "보호 구간 입력은 소비");
			Assert.IsTrue(director.IsRevealing, "보호 구간 입력은 건너뛰지 않음");

			Run(director).Tick(0.35f);
			Assert.IsFalse(director.InSkipGuard);
			Assert.IsTrue(director.BlocksChoice, "보호 구간 뒤에도 공개 중이면 고르기는 막힘");
			director.Cancel();
		}

		[Test]
		public void Input_never_skips_a_skin_that_is_not_skippable()
		{
			UICardRevealDirector director = NewDirector(out _);
			Assert.IsTrue(director.PlayExact(NewSkin(1f, 0f, 1f), Entries()));
			Assert.IsFalse(director.InSkipGuard);

			Assert.IsTrue(director.ConsumeBlockedChoice(), "입력은 소비");
			director.SkipAll();
			Assert.IsTrue(director.IsRevealing, "건너뛸 수 없는 스킨은 입력으로 끝나지 않음");

			CardRevealRun run = Run(director);
			for (int i = 0; i < 30 && director.IsRevealing; i++)
				run.Tick(STEP);
			Assert.IsFalse(director.IsRevealing, "연출은 끝까지 돌아 자연 종료");
			director.Cancel();
		}

		[Test]
		public void Input_after_skip_guard_skips_a_skippable_skin()
		{
			UICardRevealDirector director = NewDirector(out _);
			Assert.IsTrue(director.PlayExact(NewSkin(1f, 0f, 1f, skippable: true), Entries()));

			Assert.IsTrue(director.ConsumeBlockedChoice());
			Assert.IsFalse(director.IsRevealing, "건너뛸 수 있는 스킨은 기존대로 건너뜀");
			director.Cancel();
		}

		[Test]
		public void Card_layer_zoom_is_kept_after_finish_and_restored_on_cancel()
		{
			UICardRevealDirector director = NewDirector(out RectTransform content);
			Assert.IsTrue(director.PlayExact(NewSkin(0.6f, 0f, 1.2f), Entries()));
			CardRevealRun run = Run(director);
			for (int i = 0; i < 30 && director.IsRevealing; i++)
				run.Tick(STEP);

			Assert.IsFalse(director.IsRevealing);
			Assert.AreEqual(1.2f, content.localScale.x, 0.001f, "고르는 동안 확대 유지");
			director.Cancel();
			Assert.AreEqual(1f, content.localScale.x, 0.001f, "닫히면 원래 크기");
		}

		[TestCase("Full")]
		[TestCase("Quick")]
		public void Pack_art_and_finish_are_connected(string variant)
		{
			const string Directory = "Assets/_WitchMendokusai/Domain/UI/Survival";
			PackOpenV2Skin skin = AssetDatabase.LoadAssetAtPath<PackOpenV2Skin>($"{Directory}/RevealSkin_PackOpen{variant}.asset");
			Assert.IsNotNull(skin);
			Assert.AreEqual(4, skin.PackArt.Length);
			Assert.AreEqual(4, skin.PackBands.Length);
			Assert.AreEqual(4, skin.BackEffects.Length);
			foreach (CardRevealTier tier in System.Enum.GetValues(typeof(CardRevealTier)))
			{
				int index = (int)tier;
				Assert.IsNotNull(skin.BackSprite(tier), $"Back {tier}");
				Assert.IsNotNull(skin.PackArt[index], $"Pack {tier}");
				Assert.IsNotNull(skin.PackBands[index], $"Band {tier}");
				Texture2D frames = skin.BackEffects[index];
				Assert.IsNotNull(frames, $"FX {tier}");
				Assert.AreEqual(0, frames.width % 6, $"FX columns {tier}");
				Assert.AreEqual(0, frames.height % 4, $"FX rows {tier}");
			}

			UICardRevealDirector director = NewDirector(out _);
			Assert.IsTrue(director.PlayExact(skin, new[] { new CardRevealEntry(card, CardRevealTier.Legend) }));
			CardRevealRun run = Run(director);
			Transform back = card.Find("[Reveal] Back");
			RawImage effect = back.GetComponentInChildren<RawImage>();
			Rect firstFrame = effect.uvRect;
			GameObject edge = card.Find("[Reveal] Card stock edge").gameObject;
			bool effectBeforeFront = false;
			bool edgeDuringTurn = false;
			for (float t = 0; director.IsRevealing && t < 16f; t += 0.025f)
			{
				run.Tick(0.025f);
				if (back.gameObject.activeSelf && effect.uvRect != firstFrame)
					effectBeforeFront = true;
				if (edge.activeSelf && Quaternion.Angle(card.localRotation, Quaternion.identity) > 30f)
					edgeDuringTurn = true;
			}
			Assert.IsTrue(effectBeforeFront, "뒷면 FX 다음에 앞면");
			Assert.IsTrue(edgeDuringTurn, "회전 중 옆면 노출");
			Assert.IsFalse(back.gameObject.activeSelf, "앞면으로 전환");
			Assert.IsFalse(director.IsRevealing);
			Assert.Less(Quaternion.Angle(card.localRotation, Quaternion.identity), 0.01f);
			director.Cancel();
		}

		private RectTransform card;

		private List<CardRevealEntry> Entries() => new() { new CardRevealEntry(card, CardRevealTier.Common) };

		private static CardRevealRun Run(UICardRevealDirector director) => (CardRevealRun)RUN_FIELD.GetValue(director);

		private UICardRevealDirector NewDirector(out RectTransform content)
		{
			// 편집 모드에서 카드 꾸밈 정리의 Destroy 가 오류 로그를 냄. 시험 본문에서 켜야 적용. DOTween 깨짐은 Tick 예외로 잡음
			LogAssert.ignoreFailingMessages = true;
			GameObject panel = NewRect("[Test] Panel", null);
			content = (RectTransform)NewRect("[Test] Content", panel.transform).transform;
			card = (RectTransform)NewRect("[Test] Card", content).transform;
			return UICardRevealDirector.AttachTo(panel, (RectTransform)panel.transform, content);
		}

		private StubSkin NewSkin(float length, float guard, float zoom, bool skippable = false)
		{
			StubSkin skin = ScriptableObject.CreateInstance<StubSkin>();
			skin.Length = length;
			SetBacking(skin, "SkipGuard", guard);
			SetBacking(skin, "ContentZoom", zoom);
			SetBacking(skin, "Skippable", skippable);
			assets.Add(skin);
			return skin;
		}

		private static void SetBacking(CardRevealSkin skin, string property, object value) =>
			typeof(CardRevealSkin).GetField($"<{property}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, value);

		private GameObject NewRect(string objectName, Transform parent)
		{
			GameObject rectObject = new(objectName, typeof(RectTransform));
			if (parent != null)
				rectObject.transform.SetParent(parent, false);
			else
				created.Add(rectObject);
			return rectObject;
		}
	}
}
