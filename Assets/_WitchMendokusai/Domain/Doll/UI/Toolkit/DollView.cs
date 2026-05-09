using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public class DollView : MonoBehaviour
	{
		private const string WINDOW_ID = "Doll";

		private WMWindow window;
		private DollList dollList;
		private DollDetail dollDetail;

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
			UIRoot.Instance.WindowsLayer.Add(window);

			VisualElement body = new();
			body.style.flexDirection = FlexDirection.Row;
			body.style.flexGrow = 1;
			window.Content.Add(body);

			dollList = new DollList();
			dollList.style.width = 240;
			dollList.style.flexShrink = 0;
			dollList.OnDollSelected += OnDollSelected;
			body.Add(dollList);

			dollDetail = new DollDetail();
			dollDetail.style.flexGrow = 1;
			body.Add(dollDetail);

			dollList.Bind(SOManager.Instance.DollBuffer);
			dollList.SelectFirst();

			InputManager.Instance.RegisterInputEvent(InputEventType.DollToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			dollList?.Unbind();

			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.DollToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDollSelected(Doll doll) => dollDetail?.Bind(doll);

		private void OnToggle() => window?.Toggle();
	}
}
