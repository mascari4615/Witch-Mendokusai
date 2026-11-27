using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 봉인된 두루마리 자산의 이음새. ResourceManager 가 표를 가리키고, 표가 두루마리 두 아이템, 마법 목록, 든 것을 가리키는지.
	/// 든 것의 이름과 수는 기획이 고치는 임시값이라 잠그지 않음
	/// </summary>
	public sealed class ScrollTableAssetTest
	{
		private const string RESOURCE_MANAGER_PATH = "Assets/_WitchMendokusai/Core/Resources/ResourceManager.asset";
		private const string ITEM_GROUP_PATH = "Assets/AddressableAssetsData/AssetGroups/ItemData.asset";

		private static ScrollTableSO Table()
		{
			ResourceManager resources = AssetDatabase.LoadAssetAtPath<ResourceManager>(RESOURCE_MANAGER_PATH);
			Assert.That(resources, Is.Not.Null, "ResourceManager 자산이 없음");
			Assert.That(resources.Scrolls, Is.Not.Null, "ResourceManager 가 두루마리 표를 안 가리킴. 성질 놀이 창의 두루마리 버튼과 처치 떨구기에서 멈춤");
			Assert.That(resources.Scrolls.Spells, Is.Not.Null, "두루마리 표가 하찮은 마법 목록을 안 가리킴");
			return resources.Scrolls;
		}

		[Test]
		public void Resources_PointToTable_WithBothScrollItems()
		{
			ScrollTableSO table = Table();
			Assert.That(table.PaperScroll, Is.Not.Null);
			Assert.That(table.TurnScroll, Is.Not.Null);
			Assert.That(table.PaperScroll, Is.Not.EqualTo(table.TurnScroll), "두 종류가 같은 아이템이면 가방에서 구분이 안 됨");
		}

		[Test]
		public void Rows_HaveDistinctIds_AndRecipeRowsPointToItem()
		{
			ScrollTableSO table = Table();
			List<ScrollTableRow> rows = table.Rows;
			Assert.That(rows.Count, Is.GreaterThan(0));
			Assert.That(rows.Select(row => row.Id).Distinct().Count(), Is.EqualTo(rows.Count), "항목 번호가 겹치면 세이브에서 둘이 한 칸을 씀");
			foreach (ScrollTableRow row in rows)
			{
				Assert.That(row.Forms, Is.GreaterThanOrEqualTo(1), $"항목 {row.Id}");
				// 만드는 법이면 아이템 이름, 마법이면 목록의 이름. 아이템이 비거나 목록에 번호가 없으면 여기서 멈춤
				Assert.That(string.IsNullOrEmpty(table.DisplayName(row)), Is.False, $"항목 {row.Id} 의 이름이나 아이템이 비었음");
			}
		}

		// 런타임 자료는 Addressables 의 ItemData 묶음에서 번호로 읽음. 빠지면 가방 세이브를 불러올 때 두루마리가 사라짐
		[Test]
		public void ScrollItems_AreInItemDataAddressables()
		{
			ScrollTableSO table = Table();
			string group = System.IO.File.ReadAllText(ITEM_GROUP_PATH);
			foreach (ItemData item in new[] { table.PaperScroll, table.TurnScroll })
			{
				string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
				Assert.That(group.Contains($"m_GUID: {guid}"), Is.True, $"{item.name} 이 Addressables 에 없음");
				Assert.That(group.Contains($"m_Address: ItemData/{item.ID}"), Is.True, $"{item.name} 의 주소가 번호와 다름");
			}
		}
	}
}
