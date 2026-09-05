using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    // CauldronMapElement 의 책 화면 짜기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 CauldronMapElement.cs 를 본다.
    public sealed partial class CauldronMapElement : VisualElement
    {
        // ── 마도서 골격 (두 쪽) ──────────────────────────────────────────
        private void BuildBook()
        {
            VisualElement leftPage = MakePage();
            leftPage.style.maxWidth = 320f;
            leftPage.style.minWidth = 240f;

            Label title = new Label("솥 속의 지도");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 20f;
            title.style.color = InkColor;
            title.style.marginBottom = 6f;
            leftPage.Add(title);

            spellLabel = new Label("재료를 갈아 효과 좌표에 닿게 하라.\n질러가면 강하나 부작용, 돌아가면 안전하나 약하다.");
            spellLabel.style.whiteSpace = WhiteSpace.Normal;
            spellLabel.style.color = InkColor;
            spellLabel.style.fontSize = 12f;
            spellLabel.style.marginBottom = 10f;
            leftPage.Add(spellLabel);

            Label ingredientHeader = new Label("재료를 갈아 넣기");
            ingredientHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            ingredientHeader.style.color = InkColor;
            ingredientHeader.style.fontSize = 13f;
            leftPage.Add(ingredientHeader);

            ingredientRow = new VisualElement();
            ingredientRow.style.flexDirection = FlexDirection.Row;
            ingredientRow.style.flexWrap = Wrap.Wrap;
            ingredientRow.style.marginBottom = 10f;
            leftPage.Add(ingredientRow);

            statusLabel = new Label();
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.color = InkColor;
            statusLabel.style.fontSize = 13f;
            statusLabel.style.marginBottom = 8f;
            leftPage.Add(statusLabel);

            completeButton = new Button(OnCompleteClicked) { text = "✦ 완성 (포션 거두기)" };
            completeButton.style.alignSelf = Align.FlexStart;
            completeButton.style.marginBottom = 4f;
            leftPage.Add(completeButton);

            Button restart = new Button(RestartSession) { text = "↺ 다시 젓기" };
            restart.style.alignSelf = Align.FlexStart;
            leftPage.Add(restart);

            // 책등(spine).
            VisualElement spine = new VisualElement();
            spine.style.width = 3f;
            spine.style.backgroundColor = PageEdgeColor;

            VisualElement rightPage = MakePage();

            Label mapTitle = new Label("효과 지도");
            mapTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mapTitle.style.fontSize = 14f;
            mapTitle.style.color = InkColor;
            mapTitle.style.marginBottom = 4f;
            rightPage.Add(mapTitle);

            mapCanvas = new VisualElement();
            mapCanvas.style.flexGrow = 1f;
            mapCanvas.style.minHeight = 280f;
            mapCanvas.style.borderTopWidth = 1f;
            mapCanvas.style.borderBottomWidth = 1f;
            mapCanvas.style.borderLeftWidth = 1f;
            mapCanvas.style.borderRightWidth = 1f;
            mapCanvas.style.borderTopColor = PageEdgeColor;
            mapCanvas.style.borderBottomColor = PageEdgeColor;
            mapCanvas.style.borderLeftColor = PageEdgeColor;
            mapCanvas.style.borderRightColor = PageEdgeColor;
            mapCanvas.generateVisualContent += OnDrawMap;
            mapCanvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            mapCanvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            mapCanvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            rightPage.Add(mapCanvas);

            Add(leftPage);
            Add(spine);
            Add(rightPage);
        }

        private static VisualElement MakePage()
        {
            VisualElement page = new VisualElement();
            page.style.flexGrow = 1f;
            page.style.backgroundColor = PageColor;
            page.style.paddingLeft = 14f;
            page.style.paddingRight = 14f;
            page.style.paddingTop = 14f;
            page.style.paddingBottom = 14f;
            page.style.borderTopColor = PageEdgeColor;
            page.style.borderBottomColor = PageEdgeColor;
            page.style.borderLeftColor = PageEdgeColor;
            page.style.borderRightColor = PageEdgeColor;
            page.style.borderTopWidth = 2f;
            page.style.borderBottomWidth = 2f;
            page.style.borderLeftWidth = 2f;
            page.style.borderRightWidth = 2f;
            return page;
        }

        private void RebuildIngredientButtons()
        {
            if (ingredientRow == null)
            {
                return;
            }

            ingredientRow.Clear();
            for (int i = 0; i < ingredients.Count; i++)
            {
                Ingredient ingredient = ingredients[i];
                Button button = new Button(() => AddIngredient(ingredient)) { text = ingredient.Label };
                button.style.marginRight = 4f;
                button.style.marginTop = 4f;
                ingredientRow.Add(button);
            }
        }

        // ── 상태 갱신 ────────────────────────────────────────────────────
        private void Refresh()
        {
            if (statusLabel != null)
            {
                // 공유 솥/솔로 공통 소스 = CurrentState (네트워크면 SyncVar, 솔로면 로컬 세션) + 로컬 레시피.
                BrewState state = CurrentState();
                EffectTarget target = ActiveRecipe().Target;
                BrewOutcome outcome = BrewEngine.Evaluate(state, target, rules);
                bool reached = BrewEngine.IsReached(state, target);
                float distance = BrewEngine.DistanceTo(state, target);
                statusLabel.text =
                    (reached ? "✅ 효과 도달" : "… 항해 중") + "\n"
                    + "목표까지 거리: " + distance.ToString("0.00") + "\n"
                    + "누적 부작용: " + state.AccruedSideEffect.ToString("0.0") + "\n"
                    + "강도: " + outcome.Potency.ToString("0.00") + "  품질: " + outcome.Quality.ToString("0.00") + "\n"
                    + "등급: " + GradeText(outcome.Grade) + "\n"
                    + "넣은 재료 수: " + state.StepCount;
            }
            // 이제 누구나 누를 수 있다 — 누가 가져갈지는 세계가 정한다(선착순 한 번).
            completeButton?.SetEnabled(true);
            mapCanvas?.MarkDirtyRepaint();
        }

        private static string GradeText(BrewGrade grade)
        {
            switch (grade)
            {
                case BrewGrade.Masterwork: return "명품 ★★★";
                case BrewGrade.Fine: return "양품 ★★";
                case BrewGrade.Crude: return "조악품 ★";
                default: return "실패";
            }
        }
    }
}
