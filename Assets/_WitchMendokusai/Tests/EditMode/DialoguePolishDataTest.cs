using System;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 대화 연출 자료와 UI 효과음 표의 자료 계약
	public class DialoguePolishDataTest
	{
		[TestCase(DialogueStagePresentation.World, false, false)]
		[TestCase(DialogueStagePresentation.Novel, true, true)]
		[TestCase(DialogueStagePresentation.Feature, true, true)]
		[TestCase(DialogueStagePresentation.Field, false, false)]
		public void PresentationDefaults(DialogueStagePresentation presentation, bool pauses, bool blurs)
		{
			DialogueStageSettings.PresentationEntry entry = DialogueStageSettings.Load().FindPresentation(presentation);
			Assert.AreEqual(pauses, entry.PausesTime);
			Assert.AreEqual(blurs, entry.BlursBackground);
		}

		[Test]
		public void ArtFilterAndBlurAreAssigned()
		{
			DialogueStageSettings settings = DialogueStageSettings.Load();
			Assert.IsNotNull(settings.ArtFilterShader);
			Assert.IsTrue(settings.ArtFilterShader.isSupported);
			Assert.IsNotNull(settings.BlurOutput);
		}

		[Test]
		public void SfxTableCoversEveryEventWithEventPath()
		{
			UiSfxTable table = Resources.Load<UiSfxTable>(nameof(UiSfxTable));
			foreach (UiSfxEvent sfxEvent in Enum.GetValues(typeof(UiSfxEvent)))
			{
				Assert.IsTrue(table.TryGetEntry(sfxEvent, out UiSfxTable.Entry entry), sfxEvent.ToString());
				StringAssert.StartsWith("event:/SFX/", entry.EventPath, sfxEvent.ToString());
			}
		}
	}
}
