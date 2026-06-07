using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 팬(UGC)이 작성하는 마도서 레시피 manifest — **DomainSDK 정의 schema**(사용자 데이터는 이걸 채움).
    /// 기존 UGC(플랫포머 점프맵, 비전핏 0)를 마도서 레시피(핵심 루프)로 재조준 — TASK-WM-186.
    /// 순수 POCO(UnityEngine·Newtonsoft 의존 0) → Domain UGCRecipeLoader 가 JSON 역직렬화 + BrewRecipe(런타임 알케미)로 변환.
    /// </summary>
    [Serializable]
    public class UGCRecipeManifest
    {
        public int schemaVersion;
        public string author;
        public List<UGCRecipeEntry> recipes = new List<UGCRecipeEntry>();

        public List<BrewRecipe> ToBrewRecipes()
        {
            List<BrewRecipe> result = new List<BrewRecipe>();
            if (recipes == null)
                return result;

            foreach (UGCRecipeEntry entry in recipes)
            {
                if (entry == null)
                    continue;
                result.Add(entry.ToBrewRecipe());
            }
            return result;
        }
    }

    /// <summary>팬이 채우는 레시피 1개 — 효과명 + 솥 지도 목표 좌표(x,y) + 허용 반경. BrewRecipe 로 변환.</summary>
    [Serializable]
    public class UGCRecipeEntry
    {
        public int id;
        public string effectName;
        public float targetX;
        public float targetY;
        public float radius;

        public BrewRecipe ToBrewRecipe()
        {
            return new BrewRecipe
            {
                Id = id,
                EffectName = effectName,
                Target = new EffectTarget
                {
                    Position = new BrewVector(targetX, targetY),
                    Radius = radius,
                },
            };
        }
    }
}
