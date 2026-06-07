using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// 팬 마도서 레시피 JSON → UGCRecipeManifest(DomainSDK schema) → List&lt;BrewRecipe&gt; 로딩 + sandbox 검증.
    /// 플랫포머 UGC(비전핏 0) 재조준의 마도서 버전 (TASK-WM-186). 게임 소비(CauldronMap/SOManager 등록) = 후속 증분.
    /// </summary>
    public static class UGCRecipeLoader
    {
        public const int CURRENT_SCHEMA_VERSION = 1;
        public const int MAX_RECIPES = 256; // sandbox: recipe 폭주 방지

        /// <summary>팬 JSON 파싱 + sandbox 검증. 성공 시 recipes 채움, 실패 시 error 사유.</summary>
        public static bool TryLoad(string json, out List<BrewRecipe> recipes, out string error)
        {
            recipes = new List<BrewRecipe>();
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "빈 JSON";
                return false;
            }

            UGCRecipeManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<UGCRecipeManifest>(json);
            }
            catch (Exception e)
            {
                error = "JSON 파싱 실패: " + e.Message;
                return false;
            }

            if (manifest == null)
            {
                error = "manifest null";
                return false;
            }

            if (manifest.recipes != null && manifest.recipes.Count > MAX_RECIPES)
            {
                error = $"recipe 수 {manifest.recipes.Count} > 상한 {MAX_RECIPES} (sandbox)";
                return false;
            }

            foreach (BrewRecipe recipe in manifest.ToBrewRecipes())
            {
                // sandbox: id>0 + radius>0 (도달 불가/무한 반경 방지).
                if (recipe.Id <= 0 || recipe.Target.Radius <= 0f)
                {
                    error = $"부적합 recipe (id={recipe.Id}, radius={recipe.Target.Radius})";
                    return false;
                }
                recipes.Add(recipe);
            }

            return true;
        }
    }
}
