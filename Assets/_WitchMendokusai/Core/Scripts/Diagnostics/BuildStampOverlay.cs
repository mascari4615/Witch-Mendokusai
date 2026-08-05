using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 화면 구석에 「이게 어느 빌드인지」를 늘 띄운다.
    ///
    /// 평소엔 반투명 한 줄(`dev #412 · a3f9c21`). 톡 하면 카드가 펼쳐져 가지·구운 때·기기·CI 까지
    /// 보이고, 거기서 「복사」를 누르면 전부 클립보드로 간다 — 폰에서 손으로 옮겨 적는 일을 없애는 게
    /// 이 과제의 목적이므로 표시기도 같은 규칙을 따른다.
    ///
    /// 왜 게임 UI(UIRoot)에 안 붙였나: **UIRoot 가 없는 화면에서야말로 필요하다**(부팅·타이틀·
    /// 조립 실패). 자기 패널을 따로 세워 어느 씬에서나 뜨고, 게임 UI 위에 그린다.
    ///
    /// 스크린샷·영상에 빌드가 같이 찍히는 것도 목적이다(CS2·Godot 이 같은 이유로 워터마크를 박는다).
    /// </summary>
    public sealed class BuildStampOverlay : MonoBehaviour
    {
        private const string SETTINGS_RESOURCE = nameof(BuildStampSettings);
        private const string PANEL_SETTINGS_RESOURCE = "BuildStampPanelSettings";

        private static bool _installed;
        private static BuildStampOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            if (_installed)
            {
                return;
            }

            BuildStampSettings settings = Resources.Load<BuildStampSettings>(SETTINGS_RESOURCE);
            if (settings == null || ShouldShow(settings) == false)
            {
                return;
            }

            PanelSettings panelSettings = Resources.Load<PanelSettings>(PANEL_SETTINGS_RESOURCE);
            if (panelSettings == null)
            {
                Debug.LogWarning($"[BuildStamp] {PANEL_SETTINGS_RESOURCE} 없음 — 빌드 표시기를 못 띄운다.");
                return;
            }

            _installed = true;

            GameObject host = new GameObject(nameof(BuildStampOverlay));
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            BuildStampOverlay overlay = host.AddComponent<BuildStampOverlay>();
            overlay._settings = settings;
            overlay._panelSettings = panelSettings;
        }

        private static bool ShouldShow(BuildStampSettings settings)
        {
#if UNITY_EDITOR
            return settings.ShowInEditor;
#else
            return Debug.isDebugBuild ? settings.ShowInDevelopmentBuild : settings.ShowInReleaseBuild;
#endif
        }

        /// <summary>다른 코드(디버그 메뉴 등)가 껐다 켤 수 있게 — 스크린샷 찍을 때 방해되면 끈다.</summary>
        public static void SetVisible(bool visible)
        {
            if (_instance != null && _instance._root != null)
            {
                _instance._root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private BuildStampSettings _settings;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private VisualElement _collapsed;
        private VisualElement _expanded;
        private Label _relayStatus;

        private void Awake()
        {
            _instance = this;

            UIDocument document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            document.sortingOrder = _settings.SortingOrder;

            VisualElement documentRoot = document.rootVisualElement;
            // 이 패널은 *표시만* 한다 — 접힌 줄과 카드 밖에서는 손가락이 그대로 게임에 닿아야 한다.
            documentRoot.pickingMode = PickingMode.Ignore;

            _root = BuildRoot();
            documentRoot.Add(_root);
        }

        private VisualElement BuildRoot()
        {
            VisualElement root = new VisualElement { name = "build-stamp" };
            root.style.position = Position.Absolute;
            root.pickingMode = PickingMode.Ignore;
            ApplyCorner(root);

            _collapsed = BuildCollapsed();
            _expanded = BuildExpanded();
            _expanded.style.display = DisplayStyle.None;

            root.Add(_collapsed);
            root.Add(_expanded);
            return root;
        }

        /// <summary>노치·제스처바를 피해 안전영역 안으로 들인다 (기기마다 다르므로 계산으로).</summary>
        private void ApplyCorner(VisualElement root)
        {
            Rect safeArea = Screen.safeArea;
            float scale = _panelSettings.referenceResolution.y > 0 && Screen.height > 0
                ? (float)_panelSettings.referenceResolution.y / Screen.height
                : 1f;

            float insetTop = (Screen.height - safeArea.yMax) * scale;
            float insetBottom = safeArea.yMin * scale;
            float insetLeft = safeArea.xMin * scale;
            float insetRight = (Screen.width - safeArea.xMax) * scale;

            float margin = _settings.Margin + _settings.SafeAreaPadding;

            switch (_settings.Corner)
            {
                case BuildStampCorner.RightTop:
                    root.style.right = insetRight + margin;
                    root.style.top = insetTop + margin;
                    root.style.alignItems = Align.FlexEnd;
                    break;
                case BuildStampCorner.RightBottom:
                    root.style.right = insetRight + margin;
                    root.style.bottom = insetBottom + margin;
                    root.style.alignItems = Align.FlexEnd;
                    break;
                case BuildStampCorner.LeftTop:
                    root.style.left = insetLeft + margin;
                    root.style.top = insetTop + margin;
                    root.style.alignItems = Align.FlexStart;
                    break;
                default:
                    root.style.left = insetLeft + margin;
                    root.style.bottom = insetBottom + margin;
                    root.style.alignItems = Align.FlexStart;
                    break;
            }
        }

        private VisualElement BuildCollapsed()
        {
            Label label = new Label(BuildInfo.Current.CollapsedLine()) { name = "build-stamp-collapsed" };
            label.style.color = _settings.TextColor;
            label.style.fontSize = _settings.CollapsedFontSize;
            label.style.opacity = _settings.CollapsedOpacity;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
            // 손가락이 닿아야 펼칠 수 있으므로 이 줄만 입력을 받는다.
            label.pickingMode = PickingMode.Position;
            label.RegisterCallback<PointerDownEvent>(_ => Toggle(true));
            return label;
        }

        private VisualElement BuildExpanded()
        {
            VisualElement card = new VisualElement { name = "build-stamp-card" };
            card.style.backgroundColor = _settings.PanelColor;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.pickingMode = PickingMode.Position;

            foreach (KeyValuePair<string, string> row in BuildInfo.Current.DetailRows())
            {
                card.Add(MakeRowLabel($"{row.Key}  {row.Value}"));
            }

            // 이 장치의 유일한 무음 지점 — 로그가 *안 나가고 있는데* 조용한 상황을 눈에 보이게 한다.
            // 펼칠 때마다 다시 읽으므로 숫자가 안 늘면 그 자리에서 막힌 걸 안다.
            _relayStatus = MakeRowLabel(string.Empty);
            card.Add(_relayStatus);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 6;

            buttons.Add(MakeButton("복사", () =>
            {
                GUIUtility.systemCopyBuffer = BuildInfo.Current.Describe();
                Debug.Log("[BuildStamp] 빌드 정보를 클립보드로 복사했다.");
            }));
            buttons.Add(MakeButton("숨김", () => SetVisible(false)));
            buttons.Add(MakeButton("접기", () => Toggle(false)));

            card.Add(buttons);
            return card;
        }

        private Label MakeRowLabel(string text)
        {
            Label line = new Label(text);
            line.style.color = _settings.TextColor;
            line.style.fontSize = _settings.ExpandedFontSize;
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.maxWidth = 320;
            return line;
        }

        private Button MakeButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick) { text = text };
            button.style.fontSize = _settings.ExpandedFontSize;
            button.style.marginRight = 4;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 4;
            button.style.paddingBottom = 4;
            return button;
        }

        private void Toggle(bool expanded)
        {
            if (expanded && _relayStatus != null)
            {
                // 펼치는 순간의 값을 읽는다 — 카드를 만들 때 한 번 박아두면 영영 옛 숫자를 보여준다.
                _relayStatus.text = $"로그  {DeviceLogRelay.StatusLine()}";
            }
            _collapsed.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
            _expanded.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>화면을 돌리면 안전영역이 바뀐다 — 그때만 다시 계산한다.</summary>
        private ScreenOrientation _lastOrientation;

        private void Update()
        {
            if (_root == null)
            {
                return;
            }
            if (Screen.orientation != _lastOrientation)
            {
                _lastOrientation = Screen.orientation;
                ApplyCorner(_root);
            }
        }
    }
}
