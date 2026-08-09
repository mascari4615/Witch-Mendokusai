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

        /// <summary>"완성" 클릭 시 현재 채점 결과 통지 — 호스트가 보상(인벤토리)·이벤트 처리. UI 는 채점만, 보상은 호스트.</summary>
        /// <summary>혼자 노는 솥의 채점 결과 — 보상도 게임이 준다.</summary>
        public System.Action<BrewOutcome> BrewCompleted;

        /// <summary>
        /// 세계가 내준 완성 (TASK-WM-217) — 무엇이 나왔는지까지 정해져 온다.
        /// 보상은 <b>이미 세계가 가방에 넣었다</b>. 받는 쪽은 보여 주기만 한다.
        /// </summary>
        public System.Action<BrewCompletion> WorldGranted;

        private VisualElement mapCanvas;
        private VisualElement ingredientRow;
        private Label spellLabel;
        private Label statusLabel;

        // 드래그(갈기) 상태.
        private bool dragging;
        private Vector2 dragStartLocal;
        private Vector2 dragCurrentLocal;

        // 공유 솥(네트워크) 폴링 — 원격 피어의 투입이 SyncVar 로 도달하면 재드로. 솔로면 no-op.
        private IVisualElementScheduledItem networkPoll;

        // 공유 솥 경로선 렌더 버퍼(매 draw 마다 채널에서 동기 step 복사 — 매 프레임 alloc 회피).
        private readonly List<BrewStep> networkStepsBuffer = new List<BrewStep>();

        // 「완성」 버튼 — 공유 솥에선 host 만 enabled(보상 host-권위, 이중지급 방지).
        private Button completeButton;

        /// <summary>재료 한 종(효과공간 방향 + 기본 갈기량 + 라벨).</summary>
        public struct Ingredient
        {
            public string Label;
            public BrewVector Direction;
            public float Grind;

            /// <summary>가방에서 꺼낼 아이템 번호 (TASK-WM-217). 0 이면 세계의 솥엔 못 넣는다.</summary>
            public int ItemId;
        }

        public CauldronMapElement()
        {
            style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Row;
            style.minHeight = 360f;
            BuildBook();
            // 공유 가마솥(네트워크) = 서버 권위 마커가 SyncVar 로 도달 → 폴링으로 재드로(원격 피어 투입 반영).
            // WorldClock 동기 패턴(폴링, OnChange 불요). 솔로면 PollShared 가 즉시 return(비용 0).
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (networkPoll == null)
            {
                networkPoll = schedule.Execute(PollShared).Every(100);
            }
            networkPoll.Resume();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            networkPoll?.Pause();
        }

        // 네트워크 공유 솥일 때만 재드로(서버 권위 마커 변화 반영). 솔로 = no-op(입력 시 즉시 Refresh 로 충분).
        private void PollShared()
        {
            if (SharedBrewChannelBridge.IsActive == false)
            {
                return;
            }

            // 세계가 완성을 내줬으면 그 상태로 채점한다 (TASK-WM-217). 못 받았으면 아무 일도 없다
            // (남이 먼저 가져갔거나 빈 솥이었다) — 그래도 화면은 계속 갱신된다.
            // ★ 채점도 세계가 한 것을 그대로 쓴다 (TASK-WM-217). 여기서 다시 채점하면 게임과 웹이
            //   같은 솥에서 다른 등급을 보고, 게임은 세계가 이미 넣어 준 물건을 한 번 더 넣는다.
            if (SharedBrewChannelBridge.Channel.TryTakeCompletionResult(out BrewCompletion given))
            {
                WorldGranted?.Invoke(given);
            }

            Refresh();
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

        private void AddIngredient(Ingredient ingredient)
        {
            // 세계에 붙어 있으면 <b>가방에서 꺼내</b> 넣는다 — 방향은 세계가 재료에서 읽는다.
            if (SharedBrewChannelBridge.IsActive)
            {
                SharedBrewChannelBridge.Channel.AddIngredient(ingredient.ItemId);
                Refresh();
                return;
            }

            AddStepRouted(new BrewStep { Direction = ingredient.Direction, Grind = ingredient.Grind });
        }

        // 공유 솥(네트워크)이면 ServerRpc 라우팅(서버 권위 brew 전진 → SyncVar → 폴링 재드로), 솔로면 로컬 세션.
        private void AddStepRouted(BrewStep step)
        {
            if (SharedBrewChannelBridge.IsActive)
            {
                // 둘 다 같은 솥에 넣음 — 서버가 누적, 마커는 round-trip 후 SyncVar 로 도달(PollShared 가 반영).
                SharedBrewChannelBridge.Channel.AddStep(step);
            }
            else
            {
                session.AddStep(step);
                Refresh();
            }
        }

        // 공유 가마솥(네트워크) 활성 여부 — 경로 렌더·소스 분기 근거.
        private static bool IsNetworked => SharedBrewChannelBridge.IsActive;

        // 현재 마커 상태 = 공유 솥이면 SyncVar 수신값, 솔로면 로컬 세션. 채점·렌더 공통 소스(레시피는 항상 로컬).
        private BrewState CurrentState()
        {
            if (SharedBrewChannelBridge.IsActive
                && SharedBrewChannelBridge.Channel.TryGetState(out BrewVector position, out int stepCount, out float sideEffect))
            {
                return new BrewState { Position = position, StepCount = stepCount, AccruedSideEffect = sideEffect };
            }
            return session.State;
        }

        // 현재 경로 step 열 = 공유 솥이면 동기된 SyncList(buffer 복사), 솔로면 로컬 세션. 경로선 렌더 소스.
        private IReadOnlyList<BrewStep> CurrentSteps()
        {
            if (SharedBrewChannelBridge.IsActive)
            {
                SharedBrewChannelBridge.Channel.ReadSteps(networkStepsBuffer);
                return networkStepsBuffer;
            }
            return session.Steps;
        }

        private void RestartSession()
        {
            if (SharedBrewChannelBridge.IsActive)
            {
                SharedBrewChannelBridge.Channel.ResetBrew(); // 같은 솥 비우기(서버 권위 → 양 피어 반영)
            }
            else
            {
                session.Start(session.Recipe, hazards);
                Refresh();
            }
        }

        // "완성" = 현재 채점 결과를 호스트에 통지(보상/이벤트는 호스트 책임 — UI 는 제조 행위만) + 솥 리셋(중복 수확 방지).
        // 공유 솥: 보상 host-권위(아래 가드 + Refresh 의 버튼 disable)로 이중지급 차단. 단 *완전 권위*(비-host 가
        // host 에게 수확 요청 RPC)는 후속 — 현재 = host 만 수확(비-host 「완성」 버튼 비활성).
        private void OnCompleteClicked()
        {
            // 공유 솥: 「달라」고 말하고 기다린다 (TASK-WM-217). 세계가 선착순 한 사람에게만 내주므로
            // 이중지급은 구조적으로 막힌다 — 옛 규칙(host 만 누름)은 호스트가 없어져 기능이 죽었었다.
            if (IsNetworked)
            {
                SharedBrewChannelBridge.Channel.RequestCompletion();
                return;
            }
            // 채점 = 현재 마커(공유 솥이면 SyncVar 수신값) + 로컬 레시피·규칙(양 피어 동일 SO).
            BrewState state = CurrentState();
            BrewCompleted?.Invoke(BrewEngine.Evaluate(state, session.Recipe.Target, rules));
            RestartSession();
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

            // 누적 경로 (원점 → 각 step 끝점, 보랏빛 잉크 선). 공유 솥이면 동기된 경로(SyncList), 솔로면 로컬
            // = 둘이 같은 *경로*까지 본다. 재료 0개 = 경로 없음(빈 stroke 아티팩트 회피).
            IReadOnlyList<BrewStep> steps = CurrentSteps();
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

            // 현재 마커 (●) — 공유 솥이면 SyncVar 수신 위치, 솔로면 로컬 세션.
            FillCircle(painter, EffectToPixels(CurrentState().Position), 5f, MarkerColor);
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
                // 드래그 방향 단위벡터 × 거리 = 한 번의 갈기(손맛). 공유 솥이면 ServerRpc 라우팅.
                BrewVector direction = new BrewVector(delta.X / grind, delta.Y / grind);
                AddStepRouted(new BrewStep { Direction = direction, Grind = grind });
            }
            Refresh(); // 드래그 미리보기 해제(+ 솔로 상태 반영). 네트워크 마커는 PollShared 가 갱신.
        }

        // ── 상태 갱신 ────────────────────────────────────────────────────
        private void Refresh()
        {
            if (statusLabel != null)
            {
                // 공유 솥/솔로 공통 소스 = CurrentState (네트워크면 SyncVar, 솔로면 로컬 세션) + 로컬 레시피.
                BrewState state = CurrentState();
                EffectTarget target = session.Recipe.Target;
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
