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
        [Header("레시피 SO — 비우면 더미 placeholder 로 동작")]
        [SerializeField] private BrewRecipeSO recipeSO;

        private InputManager inputManager;
        private UIRoot uiRoot;

        private VisualElement container;
        private CauldronMapElement map;
        private bool isOpen;

        // 디제틱 트리거(솥 오브젝트 상호작용)가 외부에서 Open 호출하는 진입점 (CodexWindowController 패턴).
        public static CauldronMapController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

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
            if (Instance == this)
            {
                Instance = null;
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

            string spell = "재료를 갈아 효과 좌표에 닿게 하라.\n질러가면 강하나 부작용, 돌아가면 안전하나 약하다.";
            if (recipeSO != null)
            {
                map.Setup(recipeSO.ToRecipe(), recipeSO.Hazards, BuildPalette(recipeSO), BrewOutcomeRules.Default, spell);
            }
            else if (UGCRecipeRegistry.Recipes.Count > 0)
            {
                // 팬 마도서 레시피(UGC) 인게임 등장 — SO 미설정 시 로드된 첫 팬 레시피 표시 (TASK-WM-186 소비).
                // 재료/위험은 기본값(팬 schema 는 효과 좌표만 — 재료/위험 schema 확장은 후속).
                map.Setup(UGCRecipeRegistry.Recipes[0], BuildHazards(), BuildIngredients(), BrewOutcomeRules.Default, spell);
            }
            else
            {
                map.Setup(BuildRecipe(), BuildHazards(), BuildIngredients(), BrewOutcomeRules.Default, spell);
            }

            map.BrewCompleted = HandleBrewComplete;
            map.WorldGranted = HandleWorldGranted;
        }

        /// <summary>
        /// 세계가 내준 완성 (TASK-WM-217) — <b>인벤토리에 넣지 않는다</b>.
        /// 물건은 이미 세계의 가방에 들어갔고, 그 가방이 화면으로 내려온다(PlayerBagSync).
        /// 여기서 또 넣으면 같은 한 판으로 두 번 받는다.
        /// </summary>
        private void HandleWorldGranted(BrewCompletion given)
        {
            EventBusBridge.Publish(new PotionBrewedEvent(
                given.RecipeId,
                given.Grade,
                given.Quality,
                given.State.AccruedSideEffect,
                given.ResultItemId,
                given.Amount));

            Close();
        }

        // 제조 "완성" = 보상 루프 닫기: 마을 벌크 재료 소비(INC-W7) → 등급별 포션 인벤토리 투입 + PotionBrewedEvent 발행 + 패널 닫기.
        private void HandleBrewComplete(BrewOutcome outcome)
        {
            int recipeId = recipeSO != null ? recipeSO.ID : -1;
            int resultId = (recipeSO != null && recipeSO.ResultItem != null) ? recipeSO.ResultItem.ID : -1;

            int baseAmount = (recipeSO != null && recipeSO.BaseAmount > 0) ? recipeSO.BaseAmount : 1;
            int produced = outcome.Reached ? baseAmount * AmountByGrade(outcome.Grade) : 0;

            // INC-W7: 마을 창고(CityEconomy)에서 레시피 벌크 재료를 *확인 후 1회 차감*. 못 대면 산출 0(루프엔 자원 필수).
            // 비용 비면(placeholder/현 레시피 전부) Consume=true → 기존 동작 그대로. 차감은 여기 한 곳 = 이중차감 X.
            if (produced > 0 && TryConsumeMaterials() == false)
            {
                produced = 0;
            }

            if (produced > 0 && recipeSO != null && recipeSO.ResultItem != null)
            {
                SOManager.Instance.ItemInventory.Add(recipeSO.ResultItem, produced);
            }

            EventBusBridge.Publish(new PotionBrewedEvent(recipeId, outcome.Grade, outcome.Potency, outcome.SideEffect, resultId, produced));

            Close();
        }

        // 레시피 벌크 재료를 마을 창고에서 차감(확인 후 충분하면). 비용 0/경제 미해소 = true(소비 0, 기존 무료 제조 유지).
        // 마을 창고 = WorldStage.CityEconomy 단일 정본(주민 노동이 쌓은 그 원장) — 사용 시점 lazy resolve(init-order-ok).
        private bool TryConsumeMaterials()
        {
            if (recipeSO == null)
            {
                return true;
            }

            IReadOnlyList<ResourceFlow> costs = recipeSO.ToMaterialCosts();
            if (costs.Count == 0)
            {
                return true;
            }

            CityEconomy economy = ResolveCityEconomy();
            if (economy == null)
            {
                return true; // 경제 핸들 못 얻음(World 밖 등) = 무료 제조 폴백(생산만, 차감 스킵). 로컬 new X(정본 desync 방지).
            }

            return BrewConsumptionModel.Consume(economy, costs);
        }

        // 주민 노동이 쌓는 그 마을 창고 핸들 — LifeDirector 와 동일 경로(StageManager→WorldStage.CityEconomy). 없으면 null.
        private static CityEconomy ResolveCityEconomy()
        {
            if (StageManager.TryGetExistingInstance(out StageManager stageManager)
                && stageManager.CurStage is WorldStage worldStage)
            {
                return worldStage.CityEconomy;
            }
            return null;
        }

        // 등급 → 생산 배수 (Masterwork 명품일수록 더 많이, 실패 = 0).
        private static int AmountByGrade(BrewGrade grade)
        {
            switch (grade)
            {
                case BrewGrade.Masterwork: return 3;
                case BrewGrade.Fine: return 2;
                case BrewGrade.Crude: return 1;
                default: return 0;
            }
        }

        // 외부 진입점(디제틱 솥 상호작용 / 단축키 공용) — CodexWindowController 패턴.
        public void Open()
        {
            isOpen = true;
            if (container != null)
            {
                container.style.display = DisplayStyle.Flex;
            }
        }

        public void Toggle()
        {
            isOpen = isOpen == false;
            if (container != null)
            {
                container.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Close()
        {
            isOpen = false;
            if (container != null)
            {
                container.style.display = DisplayStyle.None;
            }
        }

        // SO 레시피의 재료 목록 → UI 팔레트(코드 변경 0 으로 새 재료 반영).
        private static List<CauldronMapElement.Ingredient> BuildPalette(BrewRecipeSO recipe)
        {
            List<CauldronMapElement.Ingredient> list = new List<CauldronMapElement.Ingredient>();
            if (recipe.Ingredients == null)
            {
                return list;
            }
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                BrewIngredientSO ingredientSO = recipe.Ingredients[i];
                if (ingredientSO == null)
                {
                    continue;
                }
                BrewIngredient ingredient = ingredientSO.ToRuntime();
                list.Add(new CauldronMapElement.Ingredient
                {
                    Label = ingredient.Name,
                    Direction = ingredient.Direction,
                    Grind = ingredient.DefaultGrind,
                    ItemId = ingredientSO.Item != null ? ingredientSO.Item.ID : 0,
                });
            }
            return list;
        }

        // 'n' 단축키(dev/fallback) → 공용 Toggle. 디제틱 트리거(솥)는 Open() 직접 호출.
        private void OnToggle() => Toggle();

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
