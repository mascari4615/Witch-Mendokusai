using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 갈래 저장 조각 계약. 공용 저장은 글자만 옮기므로 조각이 스스로 왕복해야 함
	/// 옛 세이브 (조각 없음, 갈래 필드가 GameData 에 직접) 도 잃지 않아야 함
	/// </summary>
	public sealed class FeatureSaveSliceTests
	{
		private static TowerDefenseSaveSlice Filled()
		{
			TowerDefenseSaveSlice slice = new();
			slice.BestWave[3] = 120;
			slice.BestWave[7] = 45;
			slice.Relics = 17;
			slice.UnlockedTowers.Add(2);
			slice.UnlockedTowers.Add(5);
			slice.Resume = new TowerDefenseSaveData { StageId = "TDS_0", Lives = 3 };
			return slice;
		}

		[Test]
		public void TowerDefense_RoundTrips_ThroughText()
		{
			TowerDefenseSaveSlice source = Filled();
			TowerDefenseSaveSlice loaded = new();

			loaded.Restore(source.Capture());

			Assert.AreEqual(120, loaded.BestWave[3]);
			Assert.AreEqual(45, loaded.BestWave[7]);
			Assert.AreEqual(17, loaded.Relics);
			CollectionAssert.AreEqual(new List<int> { 2, 5 }, loaded.UnlockedTowers);
		}

		[Test]
		public void TowerDefense_Resume_StaysOutOfTheSave()
		{
			string json = Filled().Capture();

			StringAssert.DoesNotContain("Resume", json);
			StringAssert.DoesNotContain("TDS_0", json);
			StringAssert.DoesNotContain("Key", json);
		}

		[Test]
		public void TowerDefense_ReadsOldSave_WithoutSlice()
		{
			GameData old = new()
			{
				towerDefenseBestWave = new Dictionary<int, int> { { 1, 9 } },
				towerDefenseRelics = 4,
				towerDefenseUnlockedTowers = new List<int> { 0 },
			};
			TowerDefenseSaveSlice slice = new();

			slice.RestoreLegacy(old);

			Assert.AreEqual(9, slice.BestWave[1]);
			Assert.AreEqual(4, slice.Relics);
			CollectionAssert.AreEqual(new List<int> { 0 }, slice.UnlockedTowers);
		}

		[Test]
		public void TowerDefense_OldSave_WithoutTheFields_IsEmpty_NotNull()
		{
			GameData old = new() { towerDefenseBestWave = null, towerDefenseUnlockedTowers = null };
			TowerDefenseSaveSlice slice = new();

			slice.RestoreLegacy(old);

			Assert.IsNotNull(slice.BestWave);
			Assert.IsNotNull(slice.UnlockedTowers);
			Assert.AreEqual(0, slice.BestWave.Count);
		}

		[Test]
		public void Reset_ClearsEverything()
		{
			TowerDefenseSaveSlice slice = Filled();

			slice.Reset();

			Assert.AreEqual(0, slice.BestWave.Count);
			Assert.AreEqual(0, slice.Relics);
			Assert.AreEqual(0, slice.UnlockedTowers.Count);
			Assert.IsNull(slice.Resume);
		}

		/// <summary>열쇠가 겹치면 뒤의 갈래가 앞의 조각을 덮어씀. 목록을 늘릴 때 여기서 걸림</summary>
		[Test]
		public void Manifest_Slices_HaveDistinctKeys()
		{
			FeatureRegistry.Install(FeatureManifest.Installers);
			List<IFeatureSaveSlice> slices = FeatureRegistry.CreateSaveSlices();
			HashSet<string> keys = new();

			foreach (IFeatureSaveSlice slice in slices)
			{
				Assert.IsFalse(string.IsNullOrEmpty(slice.Key), slice.GetType().Name + " 의 Key 가 비었다");
				Assert.IsTrue(keys.Add(slice.Key), "열쇠 겹침: " + slice.Key);
			}

			Assert.IsTrue(keys.Contains(TowerDefenseSaveSlice.KEY), "개척 조각이 목록에 없다");
		}
	}
}
