using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Alchemy;
using System.Collections.Generic;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-2 — 솥 지도 제조 UI 인게임 진입점.
    /// World 씬 진입 시 Resources/Singletons/CauldronMapController.prefab 에서 자동 스폰 (CodexWindowController 와 같은 모양).
    /// 단축키(InputEventType.CauldronMapToggle, 기본 B) 로 펼쳐진 마도서 패널을 열고 닫는다.
    /// 패널 본체 = CauldronMapElement (Painter2D, EditorWindow 미리보기와 동일). 데이터 = placeholder(후속 SO/레시피 연동).
    /// ⚠ 트리거(키)는 임시 — 디제틱 트리거(솥 오브젝트 상호작용)는 후속 사용자 결정.
    /// </summary>
    public sealed class CauldronMapController : MonoBehaviour
    {
        private InputManager inputManager;
        private UIRoot uiRoot;

        private VisualElement container;
        private CauldronMapElement map;
        private bool isOpen;

        [Inject]
        public void Construct(InputManager inputManager, UIRoot uiRoot)
        {
            this.inputManager = inputManager;
            this.uiRoot = uiRoot;
        }

        private void Start()
        {
            BuildPanel();
            inputManager.RegisterInputEvent(InputEventType.CauldronMapToggle, InputEventResponseType.Performed, OnToggle);
        }

        private void OnDestroy()
        {
            if (inputManager != null)
            {
                inputManager.UnregisterInputEvent(InputEventType.CauldronMapToggle, InputEventResponseType.Performed, OnToggle);
            }
        }

        private void BuildPanel()
        {
            container = new VisualElement { name = nameof(CauldronMapController) };
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f); // 뒤 게임을 살짝 가리는 막
            container.style.display = DisplayStyle.None;

            VisualElement frame = new VisualElement();
            frame.style.width = 760f;
            frame.style.height = 420f;

            map = new CauldronMapElement();
            frame.Add(map);
            container.Add(frame);

            uiRoot.ScreenLayer.Add(container);

            map.Setup(BuildRecipe(), BuildHazards(), BuildIngredients(), BrewOutcomeRules.Default,
                "재료를 갈아 효과 좌표에 닿게 하라.\n질러가면 강하나 부작용, 돌아가면 안전하나 약하다.");
        }

        private void OnToggle()
        {
            isOpen = isOpen == false;
            container.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 후속에 SO/레시피로 대체될 placeholder 더미.
        private static BrewRecipe BuildRecipe()
        {
            return new BrewRecipe
            {
                Id = 1,
                EffectName = "더미-효과",
                Target = new EffectTarget { Position = new BrewVector(4f, 0f), Radius = 0.5f },
            };
        }

        private static List<HazardZone> BuildHazards()
        {
            return new List<HazardZone>
            {
                new HazardZone { Id = 1, Name = "저주-폭주", Center = new BrewVector(2f, 0f), Radius = 1f, SeverityPerUnit = 10f },
            };
        }

        private static List<CauldronMapElement.Ingredient> BuildIngredients()
        {
            return new List<CauldronMapElement.Ingredient>
            {
                new CauldronMapElement.Ingredient { Label = "동(→)", Direction = new BrewVector(1f, 0f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "북(↑)", Direction = new BrewVector(0f, 1f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "남(↓)", Direction = new BrewVector(0f, -1f), Grind = 1f },
                new CauldronMapElement.Ingredient { Label = "서(←)", Direction = new BrewVector(-1f, 0f), Grind = 1f },
            };
        }
    }
}
