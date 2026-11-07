using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 조작 인형 자산 계약. 값 자체는 임시라 잠그지 않음
	public class DollRosterDataTest
	{
		private static readonly int[] PLAYABLE_IDS = { 0, 4, 5, 6, 7, 8 };
		private static readonly int[] TEST_ONLY_IDS = { 1, 2, 3 };

		private static Dictionary<int, Doll> LoadDolls() =>
			AssetDatabase.FindAssets("t:Doll")
				.Select(guid => AssetDatabase.LoadAssetAtPath<Doll>(AssetDatabase.GUIDToAssetPath(guid)))
				.ToDictionary(doll => doll.ID);

		[Test]
		public void PlayableDolls_AreControllable_WithSharedPrefab()
		{
			Dictionary<int, Doll> dolls = LoadDolls();
			GameObject sharedPrefab = dolls[0].Prefab;
			foreach (int id in PLAYABLE_IDS)
			{
				Doll doll = dolls[id];
				Assert.That(doll.IsControllable, Is.True, doll.name);
				Assert.That(doll.Prefab, Is.SameAs(sharedPrefab), doll.name);
				Assert.That(doll.Sprite, Is.Not.Null, doll.name);
				Assert.That(doll.DefaultEquipments.Count, Is.InRange(1, Doll.EQUIPMENT_SLOT_COUNT), doll.name);
				Assert.That(doll.DefaultEquipments, Has.No.Member(doll.SignatureEquipment), doll.name);
			}
		}

		[Test]
		public void PlayableDolls_HaveDistinctSignature_WithCards()
		{
			Dictionary<int, Doll> dolls = LoadDolls();
			List<EquipmentData> signatures = PLAYABLE_IDS.Select(id => dolls[id].SignatureEquipment).ToList();
			Assert.That(signatures, Is.Unique);
			foreach (EquipmentData signature in signatures)
				Assert.That(signature.EffectCards.Any(card => card != null), signature.name);
		}

		[Test]
		public void TestOnlyDolls_AreNotControllable_ButAssetsRemain()
		{
			Dictionary<int, Doll> dolls = LoadDolls();
			foreach (int id in TEST_ONLY_IDS)
			{
				Assert.That(dolls[id].IsTestOnly, Is.True, dolls[id].name);
				Assert.That(dolls[id].IsControllable, Is.False, dolls[id].name);
			}
		}

		[Test]
		public void DummyAndBodilessDolls_AreNotControllable()
		{
			Dictionary<int, Doll> dolls = LoadDolls();
			Assert.That(dolls[Doll.DUMMY_ID].IsControllable, Is.False);
			Assert.That(dolls[700].IsControllable, Is.False);
		}
	}
}
