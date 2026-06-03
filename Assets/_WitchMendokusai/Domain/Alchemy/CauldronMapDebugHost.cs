using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b — 정식 솥 지도 UI(CauldronMapElement) 자가완결 호스트.
    /// GameObject 에 이 컴포넌트만 붙이고 PanelSettings 를 꽂은 뒤 Play → "펼쳐진 마도서" 패널이 화면에 뜬다.
    /// Phase 4 의 OnGUI 디버그뷰(BrewMapDebugView)를 진짜 UI Toolkit Painter2D 패널로 격상한 버전.
    /// 게임 흐름(UIRoot/DI) 통합은 후속 sub-slice — 본 호스트는 메커니즘+표현 즉시 체감용 harness.
    /// 더미 데이터·수치 = [SerializeField] 노출(수치노출 룰) — 인스펙터서 놀이처럼 조정.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CauldronMapDebugHost : MonoBehaviour
    {
        [Header("UI Toolkit")]
        [SerializeField] private PanelSettings panelSettings;

        [Header("더미 레시피 (목표 효과 좌표)")]
        [SerializeField] private Vector2 targetCoord = new Vector2(4f, 0f);
        [SerializeField] private float targetRadius = 0.5f;

        [Header("더미 위험지대 (저주 폭주)")]
        [SerializeField] private Vector2 hazardCenter = new Vector2(2f, 0f);
        [SerializeField] private float hazardRadius = 1f;
        [SerializeField] private float hazardSeverity = 10f;

        [Header("더미 재료 (방향 + 갈기) — 새 재료 추가 = 이 리스트만")]
        [SerializeField]
        private DummyIngredient[] ingredients =
        {
            new DummyIngredient { label = "동(→)", dirX = 1f, dirY = 0f, grind = 1f },
            new DummyIngredient { label = "북(↑)", dirX = 0f, dirY = 1f, grind = 1f },
            new DummyIngredient { label = "남(↓)", dirX = 0f, dirY = -1f, grind = 1f },
            new DummyIngredient { label = "서(←)", dirX = -1f, dirY = 0f, grind = 1f },
        };

        [System.Serializable]
        private struct DummyIngredient
        {
            public string label;
            public float dirX;
            public float dirY;
            public float grind;
        }

        private void OnEnable()
        {
            Rebuild();
        }

        /// <summary>패널 재구성(런타임·에디트모드 공용, MCP/인스펙터서 호출 가능). PanelSettings 세팅 후 1회.</summary>
        public void Rebuild()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (document.panelSettings == null)
            {
                document.panelSettings = panelSettings;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;

            VisualElement frame = new VisualElement();
            frame.style.width = 720f;
            frame.style.height = 400f;

            CauldronMapElement map = new CauldronMapElement();
            frame.Add(map);
            root.Add(frame);

            map.Setup(BuildRecipe(), BuildHazards(), BuildIngredients(), BrewOutcomeRules.Default,
                "재료를 갈아 효과 좌표에 닿게 하라.\n질러가면 강하나 부작용, 돌아가면 안전하나 약하다.");
        }

        private BrewRecipe BuildRecipe()
        {
            return new BrewRecipe
            {
                Id = 1,
                EffectName = "더미-효과",
                Target = new EffectTarget
                {
                    Position = new BrewVector(targetCoord.x, targetCoord.y),
                    Radius = targetRadius,
                },
            };
        }

        private List<HazardZone> BuildHazards()
        {
            return new List<HazardZone>
            {
                new HazardZone
                {
                    Id = 1,
                    Name = "저주-폭주",
                    Center = new BrewVector(hazardCenter.x, hazardCenter.y),
                    Radius = hazardRadius,
                    SeverityPerUnit = hazardSeverity,
                },
            };
        }

        private List<CauldronMapElement.Ingredient> BuildIngredients()
        {
            List<CauldronMapElement.Ingredient> list = new List<CauldronMapElement.Ingredient>();
            for (int i = 0; i < ingredients.Length; i++)
            {
                DummyIngredient dummy = ingredients[i];
                list.Add(new CauldronMapElement.Ingredient
                {
                    Label = dummy.label,
                    Direction = new BrewVector(dummy.dirX, dummy.dirY),
                    Grind = dummy.grind,
                });
            }
            return list;
        }
    }
}
