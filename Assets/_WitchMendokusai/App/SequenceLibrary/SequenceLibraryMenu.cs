using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.SequenceLibrary
{
    public sealed partial class SequenceLibraryMenu : MonoBehaviour
    {
        private static SequenceLibraryMenu instance;
        private InputManager input;
        private Button launcher;
        private VisualElement panel;
        private ScrollView entries;
        private VisualElement transport;
        private Label status;
        private Button play;
        private Slider timeline;
        private bool opened;
        public bool IsOpen { get { return opened; } }
        public ContentScenePlayer Player { get { return stage == null ? null : stage.Player; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            BootObserver.OnPhaseEntered -= OnBootPhase;
            BootObserver.OnPhaseEntered += OnBootPhase;
            instance = null;
        }

        private static void OnBootPhase(BootPhase phase)
        {
            // 개발용 보관함. 릴리스 로비에 표시 금지 (사용자 2026-11-04)
            if (Debug.isDebugBuild == false) return;
            if (phase != BootPhase.DataReady || instance != null || SceneManager.GetActiveScene().name != "Lobby") return;
            LifetimeScope scope = LifetimeScope.Find<RootLifetimeScope>();
            GameObject host = new GameObject("Sequence library");
            instance = host.AddComponent<SequenceLibraryMenu>();
            DontDestroyOnLoad(host);
            instance.Initialize(scope.Container.Resolve<UIRoot>(), scope.Container.Resolve<InputManager>());
        }

        private void Initialize(UIRoot root, InputManager input)
        {
            this.input = input;
            launcher = new Button(Open) { text = "연출 보관함", name = "sequence-library-open" };
            launcher.style.position = Position.Absolute;
            launcher.style.right = 24;
            launcher.style.top = 24;
            launcher.style.minWidth = 140;
            launcher.style.minHeight = 44;
            root.OverlayLayer.Add(launcher);
            // 배경은 화면 끝까지 (panel), 목록은 안전 영역 (panelContent)
            FullScreenFrame screenFrame = root.CreateFullScreenFrame(root.ScreenLayer, "sequence-library-panel");
            panel = screenFrame.Root;
            VisualElement panelContent = screenFrame.Content;
            panelContent.style.paddingLeft = 24;
            panelContent.style.paddingRight = 24;
            panelContent.style.paddingTop = 24;
            panelContent.style.paddingBottom = 24;
            panel.style.display = DisplayStyle.None;
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.backgroundColor = new Color(.08f, .09f, .12f, .96f);
            header.style.minHeight = 44;
            Label title = new Label("연출 보관함");
            title.style.fontSize = 24;
            title.style.flexGrow = 1;
            title.style.color = Color.white;
            title.style.alignSelf = Align.Center;
            header.Add(title);
            header.Add(new Button(Close) { text = "로비로 돌아가기", name = "sequence-library-close" });
            panelContent.Add(header);
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.color = Color.white;
            status.style.paddingTop = 8;
            status.style.paddingBottom = 8;
            status.style.backgroundColor = new Color(.08f, .09f, .12f, .96f);
            panelContent.Add(status);
            entries = new ScrollView();
            entries.style.flexGrow = 1;
            panelContent.Add(entries);
            transport = new VisualElement();
            transport.style.flexDirection = FlexDirection.Row;
            transport.style.marginTop = StyleKeyword.Auto;
            transport.style.minHeight = 44;
            transport.style.backgroundColor = new Color(.08f, .09f, .12f, .96f);
            play = new Button(TogglePlayback) { text = "재생", name = "sequence-play" };
            transport.Add(play);
            timeline = new Slider(0, 1) { name = "sequence-time" };
            timeline.style.flexGrow = 1;
            timeline.RegisterValueChangedCallback(change =>
            {
                if (Player == null || Player.IsLoaded == false) return;
                Player.Pause();
                try { Player.Seek(change.newValue); }
                catch (Exception exception) { status.text = PlaybackFailure(exception.Message); timeline.SetValueWithoutNotify(Player.Time); }
            });
            transport.Add(timeline);
            transport.Add(new Button(ShowEntries) { text = "목록으로", name = "sequence-list" });
            panelContent.Add(transport);
            input.RegisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, Close, CanClose);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Open()
        {
            if (opened || SceneManager.GetActiveScene().name != "Lobby") return;
            opened = true;
            input.SetInputStrategy(new InputStrategyLoading());
            launcher.style.display = DisplayStyle.None;
            panel.style.display = DisplayStyle.Flex;
            ShowEntries();
            panel.Q<Button>("sequence-library-close").Focus();
        }

        private bool CanClose() { return opened; }

        public void Close()
        {
            if (opened == false) return;
            StopSequence();
            opened = false;
            panel.style.display = DisplayStyle.None;
            bool lobby = SceneManager.GetActiveScene().name == "Lobby";
            launcher.style.display = lobby ? DisplayStyle.Flex : DisplayStyle.None;
            if (lobby && LobbyManager.Instance != null) input.SetInputStrategy(new InputStrategyLobby(LobbyManager.Instance));
            if (lobby) launcher.Focus();
        }

        public void ShowEntries()
        {
            StopSequence();
            entries.Clear();
            entries.style.display = DisplayStyle.Flex;
            transport.style.display = DisplayStyle.None;
            panel.style.backgroundColor = new Color(.08f, .09f, .12f, 1);
            try
            {
                List<string> keys = new List<string>(SequenceCatalog.Keys);
                keys.Reverse();
                status.text = "연출을 선택하면 재생돼요.";
                if (keys.Count == 0) status.text = "아직 연출이 없어요. 장면 편집기에서 '게임 보관함에 내보내기'로 추가하세요.";
                foreach (string key in keys)
                {
                    string selected = SequenceCatalog.FileOf(key);
                    Button entry = new Button(() => LoadSequence(selected)) { text = key, tooltip = Path.GetFileName(selected), name = "sequence-entry" };
                    entry.style.minHeight = 48;
                    entries.Add(entry);
                }
            }
            catch (Exception exception) { status.text = "보관함을 읽지 못했어요: " + exception.Message; }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Close();
            launcher.style.display = scene.name == "Lobby" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDestroy()
        {
            StopSequence();
            if (input != null) input.UnregisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, Close, CanClose);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (launcher != null) launcher.RemoveFromHierarchy();
            if (panel != null) panel.RemoveFromHierarchy();
            if (instance == this) instance = null;
        }
    }
}
