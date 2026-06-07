using System;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-190 — 멀티 진입 UX (드롭인 헬퍼 인형, Spiritfarer식). 로비에서 「함께 만들기」.
    /// 호스트 = 내 Yon 세계를 열고 친구에게 초대코드 / 참가 = 친구 코드로 그 세계에 헬퍼 인형 합류.
    ///
    /// NetCode 직접참조 0(boundary 게이트 Domain↛Network 준수) — DomainSDK NetworkSessionBridge 경유.
    /// 로직(MultiplayerLobbyLogic)은 MonoBehaviour/UI 와 분리 = EditMode 검증 가능(WM-174 패턴).
    /// 패널 = 코드-스폰 UI Toolkit(CauldronMapController 동형, UIRoot.ScreenLayer) — 씬 아트 불요.
    /// ⚠ 트리거(로비 「함께 만들기」 버튼) + 비주얼 폴리시 + 연결 후 World 동반입장(FishNet SceneManager)
    ///   = 후속(에디터/비전). 본 골격 = 진입(연결) UX.
    /// </summary>
    public sealed class MultiplayerLobbyController : MonoBehaviour
    {
        public static MultiplayerLobbyController Instance { get; private set; }

        private UIRoot uiRoot;
        private MultiplayerLobbyLogic logic;

        private VisualElement container;
        private TextField codeField;
        private Label statusLabel;
        private Button copyButton;
        private bool isOpen;

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
        public void Construct(UIRoot uiRoot)
        {
            this.uiRoot = uiRoot;
        }

        private void Start()
        {
            logic = new MultiplayerLobbyLogic(NetworkSessionBridge.Instance);
            BuildPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 외부 진입점 (로비 「함께 만들기」 버튼이 호출).
        public void Open()
        {
            isOpen = true;
            if (container != null)
            {
                statusLabel.text = string.Empty;
                container.style.display = DisplayStyle.Flex;
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

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void BuildPanel()
        {
            container = new VisualElement { name = nameof(MultiplayerLobbyController) };
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            container.style.display = DisplayStyle.None;

            VisualElement frame = new VisualElement { name = "frame" };
            frame.style.width = 460f;
            frame.style.paddingTop = 20f;
            frame.style.paddingBottom = 20f;
            frame.style.paddingLeft = 24f;
            frame.style.paddingRight = 24f;
            frame.style.backgroundColor = new Color(0.12f, 0.10f, 0.16f, 0.96f);

            Label title = new Label("멀티플레이") { name = "title" };
            title.style.fontSize = 22f;
            title.style.color = Color.white; // 어두운 패널 bg 대비 — 안 주면 기본 어두운 글자라 안 보임.
            title.style.marginBottom = 16f;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            frame.Add(title);

            Button hostButton = new Button(OnHostClicked) { name = "host-button", text = "방 만들기" };
            hostButton.style.height = 40f;
            hostButton.style.marginBottom = 6f;
            frame.Add(hostButton);

            Label hostHint = new Label("초대코드를 친구에게 공유하세요") { name = "host-hint" };
            hostHint.style.fontSize = 11f;
            hostHint.style.color = new Color(0.78f, 0.78f, 0.85f);
            hostHint.style.marginBottom = 8f;
            frame.Add(hostHint);

            // 호스트 성공 시 표시 — 초대코드 클립보드 복사 (친구에게 붙여넣기 편하게).
            copyButton = new Button(OnCopyClicked) { name = "copy-button", text = "📋 초대코드 복사" };
            copyButton.style.height = 32f;
            copyButton.style.marginBottom = 14f;
            copyButton.style.display = DisplayStyle.None;
            frame.Add(copyButton);

            codeField = new TextField("초대코드") { name = "code-field" };
            codeField.style.marginBottom = 6f;
            frame.Add(codeField);

            Button joinButton = new Button(OnJoinClicked) { name = "join-button", text = "방 참가" };
            joinButton.style.height = 40f;
            joinButton.style.marginBottom = 14f;
            frame.Add(joinButton);

            statusLabel = new Label(string.Empty) { name = "status" };
            statusLabel.style.fontSize = 15f;
            statusLabel.style.color = new Color(1f, 0.92f, 0.45f); // 초대코드 = 잘 보이게 노랑(어두운 bg 대비).
            statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusLabel.style.minHeight = 20f;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            frame.Add(statusLabel);

            Button backButton = new Button(Close) { name = "back-button", text = "돌아가기" };
            backButton.style.marginTop = 10f;
            frame.Add(backButton);

            container.Add(frame);
            uiRoot.ScreenLayer.Add(container);
        }

        private void OnHostClicked()
        {
            statusLabel.text = logic.Host();
            if (copyButton != null)
            {
                copyButton.style.display = logic.LastSucceeded ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void OnCopyClicked()
        {
            if (string.IsNullOrEmpty(logic.LastInviteCode))
            {
                return;
            }
            GUIUtility.systemCopyBuffer = logic.LastInviteCode;
            statusLabel.text = $"복사됨! · {logic.LastInviteCode}";
        }

        private void OnJoinClicked()
        {
            statusLabel.text = logic.Join(codeField != null ? codeField.value : string.Empty);
            if (logic.LastSucceeded)
            {
                // 참가 성공 → World 공동입장. 로컬 World 로드(NetworkManager DDOL 유지) → 서버가 스폰한
                // 내 프록시가 이 World 에 등장 + 내 실 플레이어 추종, 호스트는 내 프록시를 봄. TASK-WM-191 step-3.
                Close();
                UISceneLoading.LoadScene("World");
            }
        }
    }

    /// <summary>
    /// 멀티 로비 진입 로직 — MonoBehaviour/UI 무의존 POCO (EditMode 검증 가능). NetCode 는
    /// DomainSDK INetworkSessionControl seam 경유(boundary 준수). 호스트/참가 결과 = 사용자 표시 문자열.
    /// </summary>
    public sealed class MultiplayerLobbyLogic
    {
        private readonly INetworkSessionControl session;

        /// <summary>직전 Host/Join 이 성공했나 (controller 가 성공 시 World 공동입장 트리거).</summary>
        public bool LastSucceeded { get; private set; }

        /// <summary>직전 Host 의 초대코드 (복사 버튼용). Host 성공 시에만 유효.</summary>
        public string LastInviteCode { get; private set; }

        public MultiplayerLobbyLogic(INetworkSessionControl session)
        {
            this.session = session;
        }

        public string Host()
        {
            LastSucceeded = false;
            if (session == null)
            {
                return "네트워크 모듈 미준비";
            }
            if (session.StartHost() == false)
            {
                return "호스트 시작 실패";
            }
            LastSucceeded = true;
            LastInviteCode = session.GetHostInviteCode();
            return $"호스트 중 · 초대코드: {LastInviteCode}";
        }

        public string Join(string inviteCode)
        {
            LastSucceeded = false;
            if (session == null)
            {
                return "네트워크 모듈 미준비";
            }
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                return "초대코드를 입력하세요";
            }
            if (session.JoinByCode(inviteCode) == false)
            {
                return "참가 실패 — 코드 확인";
            }
            LastSucceeded = true;
            return "참가 — World 진입 중…";
        }
    }
}
