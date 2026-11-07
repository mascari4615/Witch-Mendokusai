using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	public class DollView : MonoBehaviour
	{
		private const string WINDOW_ID = "Doll";

		[SerializeField, Tooltip("창 처음 위치와 크기. 높이는 인형 6명이 스크롤 없이 보이는 값")] private Rect windowRect = new(240f, 120f, 640f, 480f);
		[SerializeField] private float listWidth = 240f;

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
			window.style.left = windowRect.x;
			window.style.top = windowRect.y;
			window.style.width = windowRect.width;
			window.style.height = windowRect.height;
			uiRoot.WindowsLayer.Add(window);

			VisualElement body = new();
			body.style.flexDirection = FlexDirection.Row;
			body.style.flexGrow = 1;
			window.Content.Add(body);

			dollList = new DollList();
			dollList.style.width = listWidth;
			dollList.style.flexShrink = 0;
			dollList.OnDollSelected += OnDollSelected;
			body.Add(dollList);

			dollDetail = new DollDetail(dataManager, playerProvider);
			dollDetail.style.flexGrow = 1;
			body.Add(dollDetail);

			dollList.Bind(soManager.DollBuffer);
			dollList.SelectDoll(dataManager.CurDollID);
			window.InitialFocus = () => dollList.SelectedElement;

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
