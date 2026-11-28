using System;
using UnityEngine.UIElements;
using VContainer;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 낚시하는 동안 열리는 빈 화면. 입력만 받음. 집 부엌의 굽기도 같은 화면 (ShowButton)
	// 열린 동안 이동, 상호작용, 전체 메뉴는 화면 규칙이 막음. X 와 Esc 는 이 화면을 닫음 (낚시 그만)
	// 손가락: 이 화면이 전체를 덮어 아래 조작 장치가 안 눌림. 그래서 화면 안에 감기 버튼과 그만두기 버튼 (폰엔 X 키가 없음)
	public sealed class UIFishingPanel : UIToolkitPanel
	{
		private const string BLOCK = "wm-hs-fish";

		[Inject] internal InputManager inputManager;

		private Action onPress = delegate { };
		private Action onRelease = delegate { };
		private Action onClosed = delegate { };
		private Action onStop = delegate { };
		private bool isListening;
		private VisualElement touchColumn;
		private Label reelButton;
		private FishingPhase shownPhase = FishingPhase.Escaped;

		public override string Name => "낚시";

		public void Bind(Action press, Action release, Action closed, Action stop)
		{
			onPress = press;
			onRelease = release;
			onClosed = closed;
			onStop = stop;
		}

		protected override void BuildUI(VisualElement content)
		{
			Root.styleSheets.Add(HomesteadArt.Instance.StyleSheet);
			Root.RegisterCallback<PointerDownEvent>(_ => onPress());
			Root.RegisterCallback<PointerUpEvent>(_ => onRelease());
			Root.RegisterCallback<PointerLeaveEvent>(_ => onRelease());
			BuildTouch(content);
		}

		private void BuildTouch(VisualElement content)
		{
			VisualElement touch = KeyHint.TouchOnly(new VisualElement { pickingMode = PickingMode.Ignore });
			touch.AddToClassList(BLOCK + "__touch");
			content.Add(touch);
			touchColumn = touch;

			Button stop = new(() => onStop()) { focusable = false, text = "그만두기" };
			stop.ClearClassList();
			stop.AddToClassList(BLOCK + "__stop");
			// 그만두기를 누른 손가락이 화면 누름 (감기) 으로 번지지 않게
			stop.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
			touch.Add(stop);

			reelButton = new Label { pickingMode = PickingMode.Position };
			reelButton.AddToClassList(BLOCK + "__reel");
			touch.Add(reelButton);
			// 누르는 동안 감김. 손가락을 붙잡아 두어 버튼 밖으로 미끄러져도 놓지 않음. 붙잡음을 잃으면 뗀 것으로 침
			reelButton.RegisterCallback<PointerDownEvent>(evt =>
			{
				reelButton.CapturePointer(evt.pointerId);
				reelButton.AddToClassList(BLOCK + "__reel--held");
				onPress();
				evt.StopPropagation();
			});
			reelButton.RegisterCallback<PointerUpEvent>(evt =>
			{
				if (reelButton.HasPointerCapture(evt.pointerId))
					reelButton.ReleasePointer(evt.pointerId);
				evt.StopPropagation();
			});
			reelButton.RegisterCallback<PointerCaptureOutEvent>(_ => ReleaseReelButton());
			SetPhase(FishingPhase.Waiting);
		}

		private void ReleaseReelCapture()
		{
			if (reelButton == null)
				return;
			for (int pointerId = 0; pointerId < PointerId.maxPointers; pointerId++)
			{
				if (reelButton.HasPointerCapture(pointerId))
					reelButton.ReleasePointer(pointerId);
			}
			reelButton.RemoveFromClassList(BLOCK + "__reel--held");
		}

		private void ReleaseReelButton()
		{
			reelButton.RemoveFromClassList(BLOCK + "__reel--held");
			onRelease();
		}

		// 단계마다 버튼 글자. 기다림, 채기!, 감기
		public void SetPhase(FishingPhase phase)
		{
			if (reelButton == null || phase == shownPhase)
				return;
			shownPhase = phase;
			reelButton.text = FishingTouch.ButtonText(phase);
			reelButton.EnableInClassList(BLOCK + "__reel--bite", phase == FishingPhase.Bite);
			// 끌 때만 인라인. 켤 때는 USS 에 맡김 (TouchOnly 가 PC 에서 숨김)
			touchColumn.style.display = FishingTouch.ShowsButtons(phase) ? StyleKeyword.Null : DisplayStyle.None;
		}

		// 낚시 단계 없이 버튼 하나만 (집 부엌 굽기). 다음 SetPhase 가 다시 칠하게 보인 단계를 비움
		public void ShowButton(string text)
		{
			if (reelButton == null)
				return;
			shownPhase = (FishingPhase)(-1);
			reelButton.text = text;
			reelButton.RemoveFromClassList(BLOCK + "__reel--bite");
			touchColumn.style.display = StyleKeyword.Null;
		}

		// 열린 동안 평소 손가락 버튼 (공격, 점프, 엎드리기, 기술, 말걸기) 을 숨김. 이 화면이 전체를 덮어 어차피 안 눌리는데 보이기만 해서 감기와 그만두기 위에 겹쳤음
		protected override void OnOpen()
		{
			Listen(true);
			MobileControlsView.Hold(this, true);
		}

		protected override void OnClose()
		{
			// 감기 버튼을 쥔 채 닫히면 (다른 손가락으로 그만두기, 판이 끝남) 붙잡음과 눌림 표시가 다음 낚시까지 남음
			ReleaseReelCapture();
			Listen(false);
			MobileControlsView.Hold(this, false);
			onClosed();
		}

		private void OnDestroy()
		{
			MobileControlsView.Hold(this, false);
			if (inputManager != null)
				Listen(false);
		}

		private void Listen(bool listen)
		{
			if (isListening == listen)
				return;
			isListening = listen;
			if (listen)
			{
				inputManager.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Started, Press);
				inputManager.RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Canceled, Release);
			}
			else
			{
				inputManager.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Started, Press);
				inputManager.UnregisterInputEvent(InputEventType.Submit, InputEventResponseType.Canceled, Release);
			}
		}

		private void Press() => onPress();

		private void Release() => onRelease();

		public override void UpdateUI()
		{
		}
	}
}
