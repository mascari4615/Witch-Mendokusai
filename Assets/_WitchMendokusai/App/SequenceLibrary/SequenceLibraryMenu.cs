using System;
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
        public ContentScenePlayer Player { get { return player; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            BootObserver.OnPhaseEntered -= OnBootPhase;
            BootObserver.OnPhaseEntered += OnBootPhase;
            instance = null;
        }

        private static void OnBootPhase(BootPhase phase)
        {
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
            panel = new VisualElement { name = "sequence-library-panel" };
            panel.style.position = Position.Absolute;
            panel.style.left = 0;
            panel.style.right = 0;
            panel.style.top = 0;
            panel.style.bottom = 0;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 24;
            panel.style.display = DisplayStyle.None;
            root.ScreenLayer.Add(panel);
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
            panel.Add(header);
            status = new Label();
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.color = Color.white;
            status.style.paddingTop = 8;
            status.style.paddingBottom = 8;
            status.style.backgroundColor = new Color(.08f, .09f, .12f, .96f);
            panel.Add(status);
            entries = new ScrollView();
            entries.style.flexGrow = 1;
            panel.Add(entries);
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
                if (player == null || player.IsLoaded == false) return;
                player.Pause();
                try { player.Seek(change.newValue); }
                catch (Exception exception) { status.text = PlaybackFailure(exception.Message); timeline.SetValueWithoutNotify(player.Time); }
            });
            transport.Add(timeline);
            transport.Add(new Button(ShowEntries) { text = "목록으로", name = "sequence-list" });
            panel.Add(transport);
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
            string directory = SequenceLibraryLocation.Directory;
            try
            {
                string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.sequence.json", SearchOption.TopDirectoryOnly) : new string[0];
                Array.Sort(files, StringComparer.Ordinal);
                Array.Reverse(files);
                status.text = "연출을 선택하면 재생돼요.";
                if (files.Length == 0) status.text = "아직 연출이 없어요. 장면 편집기에서 '게임 보관함에 내보내기'로 추가하세요.";
                foreach (string file in files)
                {
                    string selected = file;
                    Button entry = new Button(() => LoadSequence(selected)) { text = Path.GetFileName(file).Replace(".sequence.json", ""), tooltip = Path.GetFileName(file), name = "sequence-entry" };
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
