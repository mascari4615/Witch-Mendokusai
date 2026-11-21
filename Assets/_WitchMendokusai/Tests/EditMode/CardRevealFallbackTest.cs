using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	// 카드 공개 스킨이 비어도 예외 없이 기본 공개로. 2026-11-21 상자 창 NullReferenceException 회귀
	public class CardRevealFallbackTest
	{
		private const string THEME_PATH = "Assets/_WitchMendokusai/Domain/UI/Survival/SurvivalUITheme.asset";
		private static readonly Regex MISSING_SKIN_LOG = new("카드 공개 스킨이 비어 기본 공개로 대체");

		private bool savedEffects;
		private readonly List<GameObject> created = new();

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
			UIEffects.Enabled = savedEffects;
		}

		[Test]
		public void Play_without_skin_returns_false_without_exception()
		{
			GameObject panel = CreateRect("[Test] Panel", null);
			RectTransform content = (RectTransform)CreateRect("[Test] Content", panel.transform).transform;
			RectTransform card = (RectTransform)CreateRect("[Test] Card", content).transform;
			UICardRevealDirector director = UICardRevealDirector.AttachTo(panel, (RectTransform)panel.transform, content);
			List<CardRevealEntry> entries = new() { new CardRevealEntry(card, CardRevealTier.Common) };

			bool played = true;
			LogAssert.Expect(LogType.Error, MISSING_SKIN_LOG);
			Assert.DoesNotThrow(() => played = director.Play(null, entries));
			Assert.IsFalse(played);
			Assert.IsFalse(director.IsRevealing);
			Assert.IsFalse(director.BlocksChoice);
		}

		[Test]
		public void CanPlay_rejects_missing_skin_and_empty_entries()
		{
			RectTransform card = (RectTransform)CreateRect("[Test] Card", null).transform;
			List<CardRevealEntry> entries = new() { new CardRevealEntry(card, CardRevealTier.Rare) };
			Assert.IsFalse(UICardRevealDirector.CanPlay(null, entries));
			Assert.IsFalse(UICardRevealDirector.CanPlay(null, new List<CardRevealEntry>()));
		}

		[Test]
		public void Resolve_passes_missing_skin_through()
		{
			Assert.IsNull(UICardRevealDirector.Resolve(null));
		}

		[Test]
		public void Theme_reveal_slots_are_assigned()
		{
			SurvivalUITheme theme = AssetDatabase.LoadAssetAtPath<SurvivalUITheme>(THEME_PATH);
			Assert.IsNotNull(theme, THEME_PATH);
			Assert.IsNotNull(theme.Reveal.SelectCard, "SelectCard");
			Assert.IsNotNull(theme.Reveal.SelectDeck, "SelectDeck");
			Assert.IsNotNull(theme.Reveal.Chest, "Chest");
		}

		private GameObject CreateRect(string objectName, Transform parent)
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
