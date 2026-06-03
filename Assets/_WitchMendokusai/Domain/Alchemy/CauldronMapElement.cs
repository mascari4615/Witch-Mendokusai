using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b — 정식 솥 지도 UI (UI Toolkit, "펼쳐진 마도서").
    /// 두 쪽: 왼쪽 = 주문 글자·재료·상태(현재 등급) / 오른쪽 = 효과 지도(벡터 항해).
    /// 오른쪽 지도 = generateVisualContent + Painter2D 로 목표 원 / 위험지대 원 / 누적 경로 선 / 현재 마커 그림.
    /// 입력: ① 재료 버튼 클릭 = 그 재료를 갈아 넣음 ② 지도 위 드래그 = 그 방향·거리만큼 갈기(손맛).
    /// 메커니즘은 BrewSession/BrewEngine(DomainSDK, EditMode 31+8 테스트 GREEN) — 본 element 는 "표현만".
    /// 데이터(재료/목표/위험지대)는 Setup 으로 주입 = placeholder 더미든 SO 든 무관(데이터 주도).
    /// </summary>
    public sealed class CauldronMapElement : VisualElement
    {
        // 양피지 톤(펼쳐진 마도서).
        private static readonly Color PageColor = new Color(0.93f, 0.89f, 0.78f, 1f);
        private static readonly Color PageEdgeColor = new Color(0.78f, 0.71f, 0.55f, 1f);
        private static readonly Color InkColor = new Color(0.20f, 0.16f, 0.12f, 1f);
        private static readonly Color TargetColor = new Color(0.20f, 0.45f, 0.30f, 1f);   // 효과 목표 = 청록 잉크
        private static readonly Color HazardColor = new Color(0.62f, 0.16f, 0.16f, 0.85f); // 저주 폭주 = 붉은 얼룩
        private static readonly Color PathColor = new Color(0.30f, 0.22f, 0.55f, 1f);      // 항해 경로 = 보랏빛 잉크
        private static readonly Color MarkerColor = new Color(0.10f, 0.10f, 0.10f, 1f);    // 현재 마커

        private readonly BrewSession session = new BrewSession();
        private readonly List<HazardZone> hazards = new List<HazardZone>();
        private readonly List<Ingredient> ingredients = new List<Ingredient>();
        private BrewOutcomeRules rules = BrewOutcomeRules.Default;
        private float pixelsPerUnit = 56f;

        private VisualElement mapCanvas;
        private VisualElement ingredientRow;
        private Label spellLabel;
        private Label statusLabel;

        // 드래그(갈기) 상태.
        private bool dragging;
        private Vector2 dragStartLocal;
        private Vector2 dragCurrentLocal;

        /// <summary>재료 한 종(효과공간 방향 + 기본 갈기량 + 라벨).</summary>
        public struct Ingredient
        {
            public string Label;
            public BrewVector Direction;
            public float Grind;
        }

        public CauldronMapElement()
        {
            style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Row;
            style.minHeight = 360f;
            BuildBook();
        }

        /// <summary>제조 한 판 셋업 — 목표 레시피 + 위험지대 + 고를 재료 + 채점 규칙(placeholder/SO 무관).</summary>
        public void Setup(BrewRecipe recipe, IReadOnlyList<HazardZone> hazardZones, IReadOnlyList<Ingredient> palette, BrewOutcomeRules outcomeRules, string spellText)
        {
            hazards.Clear();
            if (hazardZones != null)
            {
                hazards.AddRange(hazardZones);
            }

            ingredients.Clear();
            if (palette != null)
            {
                ingredients.AddRange(palette);
            }

            rules = outcomeRules;
            session.Start(recipe, hazards);

            if (spellLabel != null)
            {
                spellLabel.text = spellText;
            }

            RebuildIngredientButtons();
            Refresh();
        }

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

        private void AddIngredient(Ingredient ingredient)
        {
            session.AddStep(new BrewStep { Direction = ingredient.Direction, Grind = ingredient.Grind });
            Refresh();
        }

        private void RestartSession()
        {
            session.Start(session.Recipe, hazards);
            Refresh();
        }

        // ── 좌표 변환 (효과공간 ↔ 캔버스 픽셀) ───────────────────────────
        // 캔버스 중앙 = 효과공간 원점. y 위로 = 화면 위로.
        private Vector2 EffectToPixels(BrewVector coord)
        {
            Rect rect = mapCanvas.contentRect;
            Vector2 center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            return new Vector2(
                center.x + coord.X * pixelsPerUnit,
                center.y - coord.Y * pixelsPerUnit);
        }

        private BrewVector PixelsToEffect(Vector2 localPixels)
        {
            Rect rect = mapCanvas.contentRect;
            Vector2 center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            return new BrewVector(
                (localPixels.x - center.x) / pixelsPerUnit,
                (center.y - localPixels.y) / pixelsPerUnit);
        }

        // ── 지도 렌더 (Painter2D) ────────────────────────────────────────
        private void OnDrawMap(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;

            // 위험지대 (붉은 얼룩 — 채움).
            for (int i = 0; i < hazards.Count; i++)
            {
                if (hazards[i].Radius <= 0f)
                {
                    continue;
                }
                FillCircle(painter, EffectToPixels(hazards[i].Center), hazards[i].Radius * pixelsPerUnit, HazardColor);
            }

            // 목표 효과 좌표 (청록 원 + 중심점).
            EffectTarget target = session.Recipe.Target;
            Vector2 targetPixels = EffectToPixels(target.Position);
            StrokeCircle(painter, targetPixels, Mathf.Max(target.Radius * pixelsPerUnit, 6f), TargetColor, 2f);
            FillCircle(painter, targetPixels, 3f, TargetColor);

            // 누적 경로 (원점 → 각 step 끝점, 보랏빛 잉크 선). 재료 0개 = 경로 없음(빈 stroke 아티팩트 회피).
            IReadOnlyList<BrewStep> steps = session.Steps;
            if (steps.Count > 0)
            {
                BrewVector cursor = BrewVector.Zero;
                painter.strokeColor = PathColor;
                painter.lineWidth = 2.5f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(EffectToPixels(cursor));
                for (int i = 0; i < steps.Count; i++)
                {
                    cursor = cursor + steps[i].Direction * steps[i].Grind;
                    painter.LineTo(EffectToPixels(cursor));
                }
                painter.Stroke();
            }

            // 드래그 미리보기 (갈기 방향 점선 느낌 = 실선 회색).
            if (dragging)
            {
                painter.strokeColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(dragStartLocal);
                painter.LineTo(dragCurrentLocal);
                painter.Stroke();
            }

            // 현재 마커 (●).
            FillCircle(painter, EffectToPixels(session.State.Position), 5f, MarkerColor);
        }

        private static void StrokeCircle(Painter2D painter, Vector2 center, float radius, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();
        }

        private static void FillCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Fill();
        }

        // ── 드래그(갈기) 입력 ────────────────────────────────────────────
        private void OnPointerDown(PointerDownEvent evt)
        {
            dragging = true;
            dragStartLocal = evt.localPosition;
            dragCurrentLocal = dragStartLocal;
            mapCanvas.CapturePointer(evt.pointerId);
            mapCanvas.MarkDirtyRepaint();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (dragging == false)
            {
                return;
            }
            dragCurrentLocal = evt.localPosition;
            mapCanvas.MarkDirtyRepaint();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (dragging == false)
            {
                return;
            }
            dragging = false;
            mapCanvas.ReleasePointer(evt.pointerId);

            BrewVector from = PixelsToEffect(dragStartLocal);
            BrewVector to = PixelsToEffect(dragCurrentLocal);
            BrewVector delta = to - from;
            float grind = delta.Magnitude;
            if (grind > 0.05f)
            {
                // 드래그 방향 단위벡터 × 거리 = 한 번의 갈기(손맛).
                BrewVector direction = new BrewVector(delta.X / grind, delta.Y / grind);
                session.AddStep(new BrewStep { Direction = direction, Grind = grind });
            }
            Refresh();
        }

        // ── 상태 갱신 ────────────────────────────────────────────────────
        private void Refresh()
        {
            if (statusLabel != null)
            {
                BrewOutcome outcome = session.Evaluate(rules);
                statusLabel.text =
                    (session.IsComplete ? "✅ 효과 도달" : "… 항해 중") + "\n"
                    + "목표까지 거리: " + session.DistanceToTarget.ToString("0.00") + "\n"
                    + "누적 부작용: " + session.AccruedSideEffect.ToString("0.0") + "\n"
                    + "강도: " + outcome.Potency.ToString("0.00") + "  품질: " + outcome.Quality.ToString("0.00") + "\n"
                    + "등급: " + GradeText(outcome.Grade) + "\n"
                    + "넣은 재료 수: " + session.StepCount;
            }
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
