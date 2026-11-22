using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 효과음 표 두 개가 모든 사건을 덮고, 적힌 경로가 FMOD 프로젝트에 실제로 있는지
	public class SfxTableTest
	{
		[Test]
		public void CombatTableCoversEveryEventWithRealPath()
		{
			CombatSfxTable table = Resources.Load<CombatSfxTable>(nameof(CombatSfxTable));
			Assert.IsNotNull(table, nameof(CombatSfxTable));
			HashSet<string> paths = FmodProjectPaths.Load();
			foreach (CombatSfxEvent sfxEvent in Enum.GetValues(typeof(CombatSfxEvent)))
			{
				Assert.IsTrue(table.TryGetEntry(sfxEvent, out CombatSfxTable.Entry entry), sfxEvent.ToString());
				// 경로가 빈 칸은 소리 없음 (낚시처럼 아직 이벤트가 없는 사건)
				if (string.IsNullOrEmpty(entry.EventPath))
					continue;
				Assert.IsTrue(paths.Contains(entry.EventPath), sfxEvent + ": " + entry.EventPath);
			}
		}

		// 동시 발음 상한은 위치 재생에서만 셈. 위치 없이 켜 두면 상한이 조용히 안 먹음
		[Test]
		public void VoiceLimitOnlyOnPositionalEntries()
		{
			CombatSfxTable table = Resources.Load<CombatSfxTable>(nameof(CombatSfxTable));
			Assert.IsNotNull(table, nameof(CombatSfxTable));
			foreach (CombatSfxEvent sfxEvent in Enum.GetValues(typeof(CombatSfxEvent)))
			{
				Assert.IsTrue(table.TryGetEntry(sfxEvent, out CombatSfxTable.Entry entry), sfxEvent.ToString());
				if (entry.MaxVoices > 0)
					Assert.IsTrue(entry.PlayAtPosition, sfxEvent.ToString());
			}
		}

		[Test]
		public void UiTableCoversEveryEventWithRealPath()
		{
			UiSfxTable table = Resources.Load<UiSfxTable>(nameof(UiSfxTable));
			Assert.IsNotNull(table, nameof(UiSfxTable));
			HashSet<string> paths = FmodProjectPaths.Load();
			foreach (UiSfxEvent sfxEvent in Enum.GetValues(typeof(UiSfxEvent)))
			{
				Assert.IsTrue(table.TryGetEntry(sfxEvent, out UiSfxTable.Entry entry), sfxEvent.ToString());
				Assert.IsTrue(paths.Contains(entry.EventPath), sfxEvent + ": " + entry.EventPath);
			}
		}
	}
}
