using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>화면 진단 표시를 켜고 끄는 탭. 기본은 전부 꺼짐 (2026-11-04).</summary>
	public sealed class DisplayDevMode : IDevMode
	{
		public string Id => "display";
		public string DisplayName => "표시";
		public VisualElement Root { get; } = new VisualElement();

		private readonly Toggle fpsToggle;
		private readonly Toggle buildStampToggle;

		public DisplayDevMode()
		{
			fpsToggle = new Toggle("FPS 숫자");
			fpsToggle.RegisterValueChangedCallback(changed => UIFPS.IsVisible = changed.newValue);
			Root.Add(fpsToggle);

			buildStampToggle = new Toggle("빌드 정보");
			buildStampToggle.RegisterValueChangedCallback(changed => BuildStampOverlay.SetVisible(changed.newValue));
			Root.Add(buildStampToggle);
		}

		public void OnActivate()
		{
			fpsToggle.SetValueWithoutNotify(UIFPS.IsVisible);
			buildStampToggle.SetValueWithoutNotify(BuildStampOverlay.IsVisible);
		}

		public void OnDeactivate() { }
	}
}
