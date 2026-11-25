using System;
using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
	/// <summary>자취 기록과 조합판 판정. 연구 중인 재료만 오름, 칸은 필요 횟수에서 밝혀짐, 빈칸은 귀찮음</summary>
	public class TraceRulesTest
	{
		private const int COTTON = 92;
		private const int DANDELION = 91;
		private const int PILLOW = 20000018;
		private const int BROOM = 20000017;
		private const int SWORD = 20000009;

		private static readonly TraceMaterial[] MATERIALS = { new TraceMaterial(DANDELION, 2), new TraceMaterial(COTTON, 1) };
		private static readonly TraceCombo[] COMBOS = { new TraceCombo(COTTON, TraceType.Calm), new TraceCombo(DANDELION, TraceType.Clean) };

		private static TraceType TraceOf(int tool) => tool switch
		{
			PILLOW => TraceType.Calm,
			BROOM => TraceType.Clean,
			_ => TraceType.None,
		};

		[Test]
		public void NoStudy_RecordsNothing()
		{
			TraceNotebook notebook = new TraceNotebook();

			Assert.IsFalse(notebook.Record(COTTON, TraceType.Calm));
			Assert.AreEqual(0, notebook.CountOf(COTTON, TraceType.Calm));
		}

		[Test]
		public void OnlyTheStudiedItem_WithATrace_IsRecorded()
		{
			TraceNotebook notebook = new TraceNotebook { StudyItemId = COTTON };

			Assert.IsFalse(notebook.Record(DANDELION, TraceType.Clean));
			Assert.IsFalse(notebook.Record(COTTON, TraceType.None));
			Assert.IsTrue(notebook.Record(COTTON, TraceType.Calm));
			Assert.AreEqual(0, notebook.CountOf(DANDELION, TraceType.Clean));
			Assert.AreEqual(1, notebook.CountOf(COTTON, TraceType.Calm));
		}

		[Test]
		public void SwitchingStudy_KeepsOldRecords()
		{
			TraceNotebook notebook = new TraceNotebook { StudyItemId = COTTON };
			notebook.Record(COTTON, TraceType.Calm);

			notebook.StudyItemId = DANDELION;
			notebook.Record(DANDELION, TraceType.Clean);

			Assert.AreEqual(1, notebook.CountOf(COTTON, TraceType.Calm));
			Assert.AreEqual(1, notebook.CountOf(DANDELION, TraceType.Clean));
		}

		[Test]
		public void Cell_StaysUnknown_UntilNeedIsMet()
		{
			TraceNotebook notebook = new TraceNotebook { StudyItemId = DANDELION };

			notebook.Record(DANDELION, TraceType.Clean);
			Assert.AreEqual(TraceCellState.Unknown, TraceRules.CellOf(notebook, MATERIALS[0], TraceType.Clean, COMBOS));

			notebook.Record(DANDELION, TraceType.Clean);
			Assert.AreEqual(TraceCellState.Magic, TraceRules.CellOf(notebook, MATERIALS[0], TraceType.Clean, COMBOS));
		}

		[Test]
		public void FilledCell_WithoutMagic_IsHassle_AndHasslesBecomeWarped()
		{
			TraceNotebook notebook = new TraceNotebook { StudyItemId = COTTON };
			notebook.Record(COTTON, TraceType.Calm);
			notebook.Record(COTTON, TraceType.Clean);
			notebook.Record(COTTON, TraceType.Bond);

			TraceProgress progress = TraceRules.ProgressOf(notebook, MATERIALS, COMBOS, 2);

			Assert.AreEqual(1, progress.Magics);
			Assert.AreEqual(2, progress.Hassles);
			Assert.AreEqual(1, progress.Warped);
		}

		[Test]
		public void WarpedOff_WhenThresholdIsZero()
		{
			TraceNotebook notebook = new TraceNotebook { StudyItemId = COTTON };
			notebook.Record(COTTON, TraceType.Clean);

			Assert.AreEqual(0, TraceRules.ProgressOf(notebook, MATERIALS, COMBOS, 0).Warped);
		}

		[Test]
		public void NeedBelowOne_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				TraceRules.CellOf(new TraceNotebook(), new TraceMaterial(COTTON, 0), TraceType.Calm, COMBOS));
		}

		[Test]
		public void MainTrace_IgnoresTracelessTools_EvenWhenTheyKillMore()
		{
			Dictionary<int, int> kills = new Dictionary<int, int> { { SWORD, 900 }, { PILLOW, 10 }, { BROOM, 30 } };

			Assert.AreEqual(TraceType.Clean, TraceRules.MainTrace(kills, TraceOf));
		}

		[Test]
		public void MainTrace_Tie_TakesTheSmallerId_WhateverTheOrder()
		{
			Dictionary<int, int> forward = new Dictionary<int, int> { { BROOM, 5 }, { PILLOW, 5 } };
			Dictionary<int, int> backward = new Dictionary<int, int> { { PILLOW, 5 }, { BROOM, 5 } };

			Assert.AreEqual(TraceType.Clean, TraceRules.MainTrace(forward, TraceOf));
			Assert.AreEqual(TraceType.Clean, TraceRules.MainTrace(backward, TraceOf));
		}

		[Test]
		public void MainTrace_None_WhenNothingLeavesATrace()
		{
			Assert.AreEqual(TraceType.None, TraceRules.MainTrace(new Dictionary<int, int>(), TraceOf));
			Assert.AreEqual(TraceType.None, TraceRules.MainTrace(new Dictionary<int, int> { { SWORD, 3 } }, TraceOf));
		}
	}
}
