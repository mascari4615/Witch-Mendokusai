using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 솥 재료(<see cref="BrewIngredientSO"/>)를 <b>가방의 진짜 아이템</b>에 잇는다 (TASK-WM-217).
	///
	/// ★ 왜 필요한가: 세계는 재료를 가방에서 실제로 꺼내 넣는다. 그런데 게임의 재료 에셋은
	///   「동(→)·북(↑)」 같은 방향 표시만 있고 <b>어느 아이템인지</b>가 비어 있었다 — 그 상태로는
	///   게임 창에서 아무것도 못 넣는다(웹 창만 놀 수 있는 세계가 된다).
	///
	/// 잇는 기준 = <b>미는 방향</b>. 세계의 씨앗 재료(나무 → · 나뭇가지 ← · 석탄 ↑ · 철광석 ↓)와
	/// 방향이 같은 것끼리 짝지어 준다. 이미 이어져 있으면 건드리지 않는다(사람 손이 이긴다).
	/// </summary>
	public static class BrewIngredientLinker
	{
		[MenuItem("WM/Link Cauldron Ingredients to Items")]
		public static void Link()
		{
			Debug.Log("[brew-link] " + LinkAndReport());
		}

		public static string LinkAndReport()
		{
			Dictionary<int, ItemData> itemsById = new Dictionary<int, ItemData>();
			foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
			{
				ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
				if (item != null && itemsById.ContainsKey(item.ID) == false)
					itemsById[item.ID] = item;
			}

			IngredientCatalogData seeds = WorldSeeds.Ingredients();
			int linked = 0;
			int already = 0;
			int missed = 0;

			foreach (string guid in AssetDatabase.FindAssets("t:BrewIngredientSO"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				BrewIngredientSO ingredient = AssetDatabase.LoadAssetAtPath<BrewIngredientSO>(path);
				if (ingredient == null)
					continue;

				if (ingredient.Item != null)
				{
					already++;
					continue;
				}

				IngredientCatalogEntry match = FindByDirection(seeds, ingredient.Direction.X, ingredient.Direction.Y);
				if (match == null || itemsById.TryGetValue(match.itemId, out ItemData item) == false)
				{
					missed++;
					continue;
				}

				SerializedObject serialized = new SerializedObject(ingredient);
				serialized.FindProperty("<Item>k__BackingField").objectReferenceValue = item;

				// 이름은 방향 표시(「동(→)」)였다 — 무엇을 넣는지 사람이 알아야 고를 수 있다.
				SerializedProperty name = serialized.FindProperty("<Name>k__BackingField");
				if (name != null)
					name.stringValue = match.name;

				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(ingredient);
				linked++;
			}

			AssetDatabase.SaveAssets();
			return "이음 " + linked + "개 · 이미 이어짐 " + already + "개 · 짝 못 찾음 " + missed + "개";
		}

		/// <summary>미는 방향이 같은 씨앗 재료 — 부호만 본다(세기는 각자 다를 수 있다).</summary>
		private static IngredientCatalogEntry FindByDirection(IngredientCatalogData seeds, float x, float y)
		{
			for (int i = 0; i < seeds.ingredients.Length; i++)
			{
				IngredientCatalogEntry entry = seeds.ingredients[i];
				if (Mathf.Approximately(Mathf.Sign(entry.dx), Mathf.Sign(x)) == false && (entry.dx != 0f || x != 0f))
					continue;

				if (Mathf.Approximately(Mathf.Sign(entry.dy), Mathf.Sign(y)) == false && (entry.dy != 0f || y != 0f))
					continue;

				// 축까지 같아야 한다(가로로 미는 것과 세로로 미는 것은 다른 재료다).
				bool sameAxis = (entry.dx == 0f) == (Mathf.Approximately(x, 0f))
					&& (entry.dy == 0f) == (Mathf.Approximately(y, 0f));
				if (sameAxis)
					return entry;
			}

			return null;
		}
	}
}
