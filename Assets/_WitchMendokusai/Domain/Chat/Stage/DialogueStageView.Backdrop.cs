using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 배경 흐림과 시간 정지. 표시 방식별 값은 DialogueStageSettings
	public partial class DialogueStageView
	{
		private const string USS_BACKDROP_BLUR = "wm-dlg-backdrop--blur";

		private VisualElement backdrop;
		private bool isBlurRequested;

		public bool IsPausingTime { get; private set; }

		private void BuildBackdrop()
		{
			backdrop = root.Q<VisualElement>("wm-dlg-backdrop");
			backdrop.style.backgroundImage = Background.FromRenderTexture(settings.BlurOutput);
		}

		private void RefreshStageMood()
		{
			DialogueStageSettings.PresentationEntry entry = settings.FindPresentation(presentation);
			SetTimePaused(isSessionOpen && entry.PausesTime);
			SetBlurred((isSessionOpen || isClosing) && entry.BlursBackground && UIEffects.Enabled);
		}

		private void SetTimePaused(bool isPaused)
		{
			if (IsPausingTime == isPaused)
				return;
			IsPausingTime = isPaused;
			if (timeManager == null)
				return;
			if (isPaused)
				timeManager.Pause(gameObject);
			else
				timeManager.Resume(gameObject);
		}

		private void SetBlurred(bool isBlurred)
		{
			if (isBlurRequested == isBlurred)
				return;
			isBlurRequested = isBlurred;
			if (isBlurred)
				BlurRequest.Add();
			else
				BlurRequest.Remove();
			backdrop?.EnableInClassList(USS_BACKDROP_BLUR, isBlurred);
		}

		private void TickBackdrop()
		{
			if (isBlurRequested)
				backdrop.MarkDirtyRepaint();
		}
	}
}
