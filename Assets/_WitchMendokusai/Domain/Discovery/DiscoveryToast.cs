using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>도감에 새 기록이 적힌 순간의 작은 띠. 업적 띠와 같은 모양 (wm-ach-toast), 자리만 그 위. 조작을 막지 않음</summary>
	public class DiscoveryToast : VisualElement
	{
		private const string USS_SHOWN = "wm-ach-toast--shown";

		private readonly VisualElement picture;
		private readonly Label kindLabel;
		private readonly Label nameLabel;
		private readonly VisualElement pips;

		public DiscoveryToast()
		{
			AchievementElements.AddClasses(this, "wm-ach-toast wm-discovery-toast");
			pickingMode = PickingMode.Ignore;
			picture = AchievementElements.Part(this, "wm-discovery-toast__picture");
			VisualElement text = AchievementElements.Part(this, "wm-ach-toast__text");
			kindLabel = AchievementElements.Text(text, string.Empty, "wm-ach-toast__kind");
			nameLabel = AchievementElements.Text(text, string.Empty, "wm-ach-toast__name");
			pips = AchievementElements.Part(this, "wm-ach-pips wm-discovery-toast__pips");
		}

		public void Show(MonsterLogStageEvent evt)
		{
			picture.style.backgroundImage = new StyleBackground(evt.Monster.Sprite);
			picture.style.unityBackgroundImageTintColor = evt.Monster.Tint;
			kindLabel.text = evt.Stage >= evt.MaxStage ? "기록 완성" : "도감에 새 기록";
			nameLabel.text = evt.Monster.Name;
			DiscoveryScreenCard.FillPips(pips, evt.Stage, evt.MaxStage);
			AddToClassList(USS_SHOWN);
		}

		public void Hide() => RemoveFromClassList(USS_SHOWN);
	}
}
