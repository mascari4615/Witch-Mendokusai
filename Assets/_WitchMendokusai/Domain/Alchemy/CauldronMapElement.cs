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
    public sealed partial class CauldronMapElement : VisualElement
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

        /// <summary>
        /// 지금 화면이 그리고 채점하는 <b>목표</b> — 세계에 붙어 있으면 <b>세계의 마도서</b>다 (TASK-WM-217).
        ///
        /// ★ 왜: 완성 보상은 이미 세계가 정하는데 목표·등급만 자기 자산(SO)으로 그렸다. 둘이 어긋나면
        ///   사람은 「여기까지 저으면 된다」는 표시를 보고 저은 뒤 딴 것을 받는다 —
        ///   화면은 「최상급」인데 세계는 「조잡」인 상태도 만들어진다. 그건 같은 세계가 아니다.
        ///   세계가 마도서를 아직 안 줬으면 자기 것으로 그린다(빈 화면보다 낫다).
        /// </summary>
        private BrewRecipe ActiveRecipe()
        {
            if (SharedBrewChannelBridge.IsActive
                && WorldSpellbookView.TryAim(SharedBrewChannelBridge.Channel.Spellbook, CurrentState(), out BrewRecipe aimed))
            {
                return aimed;
            }

            return session.Recipe;
        }

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
            BrewCompleted?.Invoke(BrewEngine.Evaluate(state, ActiveRecipe().Target, rules));
            RestartSession();
        }
    }
}
