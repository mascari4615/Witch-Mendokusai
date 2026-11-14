using System;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
    public class WorldClockView : MonoBehaviour
    {
        private const string USS_CONTAINER = "wm-world-clock";
        private const string USS_TIME = "wm-world-clock__time";
        private const string USS_DATE = "wm-world-clock__date";
        // 시작 화면에서는 세계 시계를 숨김 (로고 화면 위 개발용 글자로 보임)
        private const string LOBBY_SCENE = "Lobby";

        private VisualElement container;
        private Label timeLabel;
        private Label dateLabel;

        private WorldClockViewModel viewModel;
        private IDisposable timeSub;
        private IDisposable dateSub;

        [Inject]
        public void Construct(WorldClockViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        private void Start()
        {
            UIRoot uiRoot = GetComponent<UIRoot>();

            container = new VisualElement();
            container.AddToClassList(USS_CONTAINER);

            timeLabel = new Label();
            timeLabel.AddToClassList(USS_TIME);
            container.Add(timeLabel);

            dateLabel = new Label();
            dateLabel.AddToClassList(USS_DATE);
            container.Add(dateLabel);

            uiRoot.HudLayer.Add(container);

            timeSub = viewModel.TimeText.Subscribe(text => timeLabel.text = text);
            dateSub = viewModel.DateText.Subscribe(text => dateLabel.text = text);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        private void OnActiveSceneChanged(Scene previous, Scene next) => ApplySceneVisibility(next);

        private void ApplySceneVisibility(Scene scene)
        {
            container.style.display = scene.name == LOBBY_SCENE ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            timeSub?.Dispose();
            dateSub?.Dispose();
            container?.RemoveFromHierarchy();
        }
    }
}
