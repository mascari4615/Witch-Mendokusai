using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class QuestView : MonoBehaviour
	{
		private const string WINDOW_ID = "Quest";

		private WMWindow window;
		private QuestGrid questGrid;
		private QuestDetail questDetail;

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "퀘스트"
			};
			window.style.left = 200;
			window.style.top = 100;
			window.style.width = 600;
			window.style.height = 400;
			UIRoot.Instance.WindowsLayer.Add(window);

			VisualElement body = new();
			body.style.flexDirection = FlexDirection.Row;
			body.style.flexGrow = 1;
			window.Content.Add(body);

			questGrid = new QuestGrid();
			questGrid.style.width = 280;
			questGrid.style.flexShrink = 0;
			questGrid.OnQuestSelected += OnQuestSelected;
			body.Add(questGrid);

			questDetail = new QuestDetail();
			questDetail.style.flexGrow = 1;
			body.Add(questDetail);

			questGrid.Bind(QuestManager.Instance.Quests);

			InputManager.Instance.RegisterInputEvent(InputEventType.QuestToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			questGrid?.Unbind();

			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.QuestToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnQuestSelected(RuntimeQuest quest) => questDetail?.Bind(SOHelper.GetQuestSO(quest.QuestSOID), quest);

		private void OnToggle() => window?.Toggle();
	}
}
