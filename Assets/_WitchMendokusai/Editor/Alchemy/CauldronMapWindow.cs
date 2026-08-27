#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b — 솥 지도(CauldronMapElement) 에디트모드 미리보기 창.
    /// `WM > Alchemy > 솥 지도 미리보기` 메뉴 → Play 없이 펼쳐진 마도서 패널을 바로 본다.
    /// (UI Toolkit 은 EditorWindow 안에서 에디트모드에 렌더 — World 부트/다세션 Play 무관.)
    /// 더미 데이터 = placeholder. 재료 버튼·지도 드래그(갈기)로 마커 이동·등급 실시간 확인.
    /// </summary>
    public sealed class CauldronMapWindow : EditorWindow
    {
        [MenuItem("WM/Alchemy/Cauldron Map Preview")]
        public static void Open()
        {
            CauldronMapWindow window = GetWindow<CauldronMapWindow>();
            window.titleContent = new GUIContent("솥 속의 지도");
            window.minSize = new Vector2(760f, 440f);
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            CauldronMapElement map = new CauldronMapElement();
            rootVisualElement.Add(map);

            BrewRecipe recipe = new BrewRecipe
            {
                Id = 1,
                EffectName = "더미-효과",
                Target = new EffectTarget { Position = new BrewVector(4f, 0f), Radius = 0.5f },
            };

            List<HazardZone> hazards = new List<HazardZone>
            {
                new HazardZone
                {
                    Id = 1,
                    Name = "저주-폭주",
                    Center = new BrewVector(2f, 0f),
                    Radius = 1f,
                    SeverityPerUnit = 10f,
                },
            };

            List<CauldronMapElement.Ingredient> palette = new List<CauldronMapElement.Ingredient>
            {
                new CauldronMapElement.Ingredient { Label = "동(→)", Direction = new BrewVector(1f, 0f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "북(↑)", Direction = new BrewVector(0f, 1f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "남(↓)", Direction = new BrewVector(0f, -1f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "서(←)", Direction = new BrewVector(-1f, 0f), Grind = 1f },
            };

            map.Setup(recipe, hazards, palette, BrewOutcomeRules.Default,
                "재료를 갈아 효과 좌표에 닿게 하라.\n질러가면 강하나 부작용, 돌아가면 안전하나 약하다.");
        }
    }
}
#endif
