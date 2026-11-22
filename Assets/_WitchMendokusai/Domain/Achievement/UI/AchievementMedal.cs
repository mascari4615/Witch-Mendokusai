using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 메달 그림 하나. 업적마다 단계별 그림 (AchievementSO.TierMedals).
	/// 못 모았거나 숨김이면 분류의 빈 칸 메달, 아직 못 닿은 단계를 보여 줄 때는 그 단계 그림을 어둡게 (wm-ach-medal--dim)
	/// </summary>
	public class AchievementMedal : VisualElement
	{
		public const string USS_CLASS = "wm-ach-medal";
		public const string USS_DIM = "wm-ach-medal--dim";
		public const string USS_EMPTY = "wm-ach-medal--empty";

		public AchievementMedal(string sizeClass)
		{
			AddToClassList(USS_CLASS);
			AddToClassList(sizeClass);
			pickingMode = PickingMode.Ignore;
		}

		/// <summary>지금 단계의 메달. 0단계면 빈 칸</summary>
		public void Show(AchievementSO achievement, int tier)
		{
			bool empty = tier <= 0;
			style.backgroundImage = new StyleBackground(empty ? achievement.Category.EmptySlot : achievement.MedalOf(tier));
			EnableInClassList(USS_EMPTY, empty);
			EnableInClassList(USS_DIM, false);
		}

		/// <summary>단계 줄의 메달. 아직 못 닿은 단계도 그 단계 그림을 어둡게</summary>
		public void ShowStep(AchievementSO achievement, int stepTier, bool reached)
		{
			style.backgroundImage = new StyleBackground(achievement.MedalOf(stepTier));
			EnableInClassList(USS_EMPTY, false);
			EnableInClassList(USS_DIM, reached == false);
		}

		/// <summary>벽의 빈 자리. 업적이 없어서 아무 분류의 빈 칸 그림</summary>
		public void ShowSocket(UnityEngine.Sprite emptySlot)
		{
			style.backgroundImage = new StyleBackground(emptySlot);
			EnableInClassList(USS_EMPTY, true);
			EnableInClassList(USS_DIM, false);
		}
	}
}
