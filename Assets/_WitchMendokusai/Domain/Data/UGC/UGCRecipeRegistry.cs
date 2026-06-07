using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// 팬 마도서 레시피(UGC) 런타임 레지스트리 — 디스크 JSON 스캔 → UGCRecipeLoader(sandbox) → BrewRecipe 수집.
    /// 게임(CauldronMapController)이 조회해 솥 지도에 표시 = 팬 레시피 인게임 등장. ShaderPackManager(데이터 모딩) 동형 패턴.
    /// TASK-WM-186 (UGC 재조준 2/2 게임 소비). 레시피 *선택* UX(여러 팬 레시피 brows·디제틱 솥 트리거)는 비전 후속.
    /// </summary>
    public static class UGCRecipeRegistry
    {
        private static readonly List<BrewRecipe> _recipes = new List<BrewRecipe>();
        public static IReadOnlyList<BrewRecipe> Recipes => _recipes;

        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "ugc", "recipes");

        public static void Register(BrewRecipe recipe) => _recipes.Add(recipe);
        public static void ClearAll() => _recipes.Clear();

        /// <summary>dir 의 *.json 팬 레시피 → sandbox 검증 → 레지스트리. 로드 수 반환.</summary>
        public static int LoadFromDirectory(string dir)
        {
            if (string.IsNullOrEmpty(dir) || Directory.Exists(dir) == false)
                return 0;

            int loaded = 0;
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                string json;
                try
                {
                    json = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                if (UGCRecipeLoader.TryLoad(json, out List<BrewRecipe> recipes, out string error) == false)
                {
                    Debug.LogWarning($"[UGCRecipeRegistry] {Path.GetFileName(file)} 거부 (sandbox): {error}");
                    continue;
                }

                foreach (BrewRecipe recipe in recipes)
                {
                    Register(recipe);
                    loaded++;
                }
            }
            return loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void LoadDefault()
        {
            int n = LoadFromDirectory(DefaultDirectory);
            Debug.Log($"[UGCRecipeRegistry] 팬 마도서 레시피 {n}개 로드 ({DefaultDirectory})");
        }
    }
}
