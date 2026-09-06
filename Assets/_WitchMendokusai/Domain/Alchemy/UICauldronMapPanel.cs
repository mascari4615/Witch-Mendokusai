using UnityEngine;
using VContainer;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-5 — NPC 메뉴의 "솥 속의 지도" 항목(공존: 기존 연성 가마솥 Pot 과 별개로 추가).
    /// NPC 대화 → 메뉴에서 선택 → 기존 standalone CauldronMapController(SO 레시피·보상·'n' 키 전부 보유)를 연다.
    /// 얇은 shim — 솥 지도 본체/데이터/보상은 컨트롤러 1곳에 통합(중복 0). UIToolkitPanel 계약(UINPC 자동 버튼).
    /// </summary>
    public sealed class UICauldronMapPanel : UIToolkitPanel
    {
        public override string Name => "솥 속의 지도";

        protected override void BuildUI(VisualElement root)
        {
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            Label hint = new Label("솥 지도를 펼칩니다…");
            hint.style.color = new Color(0.85f, 0.82f, 0.7f, 1f);
            root.Add(hint);
        }

        private IObjectResolver resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        // 메뉴에서 이 항목 선택 → 실제 솥 지도(standalone, SO·보상 통합) 열기.
        // 컨트롤러는 갈래가 심겼을 때만 씬에 있음 (AlchemyMapFeature). 그래서 열 때 물음
        protected override void OnOpen()
        {
            if (resolver != null && resolver.TryResolve(out CauldronMapController controller))
            {
                controller.Open();
            }
        }

        public override void UpdateUI() { }
    }
}
