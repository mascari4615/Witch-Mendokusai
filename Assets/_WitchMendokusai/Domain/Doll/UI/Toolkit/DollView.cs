using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	public class DollView : MonoBehaviour
	{
		private const string WINDOW_ID = "Doll";

		private WMWindow window;
		private DollList dollList;
		private DollDetail dollDetail;

		private UIRoot uiRoot;
		private SOManager soManager;
		private InputManager inputManager;
		private DataManager dataManager;
		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(UIRoot uiRoot, SOManager soManager, InputManager inputManager, DataManager dataManager, PlayerProvider playerProvider)
		{
			this.uiRoot = uiRoot;
			this.soManager = soManager;
			this.inputManager = inputManager;
			this.dataManager = dataManager;
			this.playerProvider = playerProvider;
		}

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "인형"
			};
			window.style.left = 240;
			window.style.top = 120;
			window.style.width = 640;
			window.style.height = 440;
			uiRoot.WindowsLayer.Add(window);

			VisualElement body = new();
			body.style.flexDirection = FlexDirection.Row;
			body.style.flexGrow = 1;
			window.Content.Add(body);

			dollList = new DollList();
			dollList.style.width = 240;
			dollList.style.flexShrink = 0;
			dollList.OnDollSelected += OnDollSelected;
			body.Add(dollList);

			dollDetail = new DollDetail(dataManager, playerProvider);
			dollDetail.style.flexGrow = 1;
			body.Add(dollDetail);

			dollList.Bind(soManager.DollBuffer);
			dollList.SelectFirst();

			inputManager.RegisterInputEvent(InputEventType.DollToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			dollList?.Unbind();

			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.DollToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDollSelected(Doll doll) => dollDetail?.Bind(doll);

		private void OnToggle() => window?.Toggle();
	}
}
