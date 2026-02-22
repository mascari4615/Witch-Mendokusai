using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class RecipeUtil
	{
		public static string RecipeToString(Recipe recipe)
		{
			string s = recipe.Type + ",";
			
			List<int> ingredientIDs = recipe.Items.Select(ingredient => ingredient.ItemData.ID).ToList();
					ingredientIDs.Sort();
			s += string.Join(',', ingredientIDs);
			
			return s;
		}

		public static string RecipeToString(RecipeType type, List<int> ingredientIDs)
		{
			string s = type + ",";
			
			ingredientIDs.Sort();
			s += string.Join(',', ingredientIDs);

			return s;
		}
	}
}