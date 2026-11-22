using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 단계 상세. 도감이나 벽에서 메달을 고르면 뜨는 가운데 창 (여백은 ui.md 가운데 모달).
	/// 왼쪽 큰 메달과 고리, 오른쪽 분류, 이름, 사연, 단계마다 메달 그림과 조건, 진행 x / y, 날짜. 지금 단계 줄은 밝게.
	/// 숨김 업적은 달성 전 이름과 사연과 단계를 감춤. 벽에 걸기와 닫기 두 버튼만 칸 이동 대상
	/// </summary>
	public class AchievementDetail : VisualElement
	{
		public const string USS_CLASS = "wm-ach-detail";
		public const string USS_SHOWN = "wm-ach-detail--shown";
		private const string USS_STEP = "wm-ach-step";
		private const string USS_STEP_CURRENT = "wm-ach-step--current";
		private const string USS_STEP_TODO = "wm-ach-step--todo";
		private const string HIDDEN_STORY = "아직 모르는 기록";
		private const string HIDDEN_CONDITION = "어떻게 얻는지 아직 모름";

		private readonly AchievementScreenSettings settings;
		private readonly Func<AchievementSO, AchievementWallChange> toggleWall;
		private readonly AchievementMedal medal;
		private readonly Label pinnedChip;
		private readonly VisualElement categoryIcon;
		private readonly Label categoryLabel;
		private readonly Label nameLabel;
		private readonly Label tierLabel;
		private readonly Label storyLabel;
		private readonly VisualElement steps;
		private readonly Button pinButton;
		private readonly Label pinLabel;
		private readonly Label noteLabel;

		private AchievementSO current;

		public VisualElement Window { get; }
		public Button CloseButton { get; }
		public bool IsShown { get; private set; }

		public AchievementDetail(AchievementScreenSettings settings, MenuFocus focus, Func<AchievementSO, AchievementWallChange> toggleWall, Action close)
		{
			this.settings = settings;
			this.toggleWall = toggleWall;
			AddToClassList(USS_CLASS);

			// 뒤 화면을 어둡게 덮는 막. 누르면 닫힘
			VisualElement shade = AchievementElements.Part(this, "wm-ach-detail__shade");
			shade.pickingMode = PickingMode.Position;
			shade.RegisterCallback<ClickEvent>(_ => close());

			Window = AchievementElements.Part(this, "wm-ach-detail__window");
			Window.pickingMode = PickingMode.Position;

			VisualElement left = AchievementElements.Part(Window, "wm-ach-detail__left");
			AchievementElements.Part(left, "wm-ach-detail__glow");
			medal = new AchievementMedal("wm-ach-medal--detail");
			left.Add(medal);
			VisualElement where = AchievementElements.Part(left, "wm-fullmenu-where wm-ach-detail__where");
			for (int i = 1; i <= 3; i++)
			{
				AchievementElements.Part(where, "wm-fullmenu-where__ring wm-fullmenu-where__ring--" + i);
			}
			pinnedChip = AchievementElements.Text(left, "벽에 걸려 있음", "wm-ach-chip wm-ach-detail__pinned");

			VisualElement right = AchievementElements.Part(Window, "wm-ach-detail__right");
			VisualElement category = AchievementElements.Part(right, "wm-ach-chip wm-ach-detail__category");
			categoryIcon = AchievementElements.Part(category, "wm-ach-detail__category-icon");
			categoryLabel = AchievementElements.Text(category, string.Empty, "wm-ach-detail__category-label");
			nameLabel = AchievementElements.Text(right, string.Empty, "wm-ach-detail__name");
			tierLabel = AchievementElements.Text(right, string.Empty, "wm-ach-detail__tier");
			storyLabel = AchievementElements.Text(right, string.Empty, "wm-ach-detail__story");
			steps = AchievementElements.Part(right, "wm-ach-detail__steps");

			VisualElement foot = AchievementElements.Part(Window, "wm-ach-detail__foot");
			noteLabel = AchievementElements.Text(foot, string.Empty, "wm-ach-detail__note");
			pinButton = new Button(TogglePin);
			pinButton.AddToClassList("wm-ach-detail__button");
			pinLabel = AchievementElements.Text(pinButton, string.Empty, "wm-ach-detail__button-label");
			focus.Register(pinButton);
			foot.Add(pinButton);
			CloseButton = KeyHint.KeyButton("닫기", "ESC", close);
			CloseButton.AddToClassList("wm-ach-detail__button");
			focus.Register(CloseButton);
			foot.Add(CloseButton);
		}

		/// <summary>처음 고를 칸. 걸 수 있으면 걸기 버튼, 아니면 닫기</summary>
		public VisualElement FirstFocus => pinButton.resolvedStyle.display == DisplayStyle.None ? CloseButton : pinButton;

		public AchievementSO Current => current;

		public void Show(AchievementSO achievement, AchievementManager manager)
		{
			current = achievement;
			IsShown = true;
			AddToClassList(USS_SHOWN);
			Bind(manager);
		}

		public void Hide()
		{
			current = null;
			IsShown = false;
			RemoveFromClassList(USS_SHOWN);
		}

		/// <summary>열려 있으면 지금 업적으로 다시 그림 (장부나 벽이 바뀜)</summary>
		public void Bind(AchievementManager manager)
		{
			if (current == null)
			{
				return;
			}
			AchievementSO achievement = current;
			AchievementBook book = manager.Book;
			int tier = book.TierOf(achievement.ID);
			bool concealed = achievement.Hidden && tier == 0;
			int value = manager.CurrentValue(achievement);

			medal.Show(achievement, tier);
			pinnedChip.style.display = manager.Wall.IsPinned(achievement.ID) ? DisplayStyle.Flex : DisplayStyle.None;
			categoryIcon.style.backgroundImage = new StyleBackground(achievement.Category.Sprite);
			categoryLabel.text = achievement.Category.Name;
			nameLabel.text = concealed ? AchievementCard.HIDDEN_NAME : achievement.Name;
			storyLabel.text = concealed ? HIDDEN_STORY : achievement.Description;

			string tierName = AchievementTiers.NameOf(tier, achievement.MaxTier);
			if (concealed)
			{
				tierLabel.text = HIDDEN_CONDITION;
			}
			else if (tier >= achievement.MaxTier)
			{
				tierLabel.text = string.IsNullOrEmpty(tierName) ? "달성" : $"{tierName} 단계, 모든 단계 달성";
			}
			else
			{
				string nextName = AchievementTiers.NameOf(tier + 1, achievement.MaxTier);
				string left = $"{achievement.TierTargets[tier] - value}";
				tierLabel.text = tier == 0 ? $"아직, {(string.IsNullOrEmpty(nextName) ? "달성" : nextName)}까지 {left}" : $"{tierName} 단계, {nextName}까지 {left}";
			}

			steps.Clear();
			steps.style.display = concealed ? DisplayStyle.None : DisplayStyle.Flex;
			if (concealed == false)
			{
				for (int stepTier = 1; stepTier <= achievement.MaxTier; stepTier++)
				{
					AddStep(achievement, book, stepTier, tier, value);
				}
			}

			bool canPin = tier > 0;
			pinButton.style.display = canPin ? DisplayStyle.Flex : DisplayStyle.None;
			pinLabel.text = manager.Wall.IsPinned(achievement.ID) ? "벽에서 내리기" : "벽에 걸기";
			noteLabel.text = string.Empty;
		}

		private void AddStep(AchievementSO achievement, AchievementBook book, int stepTier, int tier, int value)
		{
			bool reached = stepTier <= tier;
			int target = achievement.TierTargets[stepTier - 1];
			VisualElement row = AchievementElements.Part(steps, USS_STEP);
			row.EnableInClassList(USS_STEP_CURRENT, stepTier == tier);
			row.EnableInClassList(USS_STEP_TODO, reached == false);

			AchievementMedal stepMedal = new("wm-ach-medal--step");
			stepMedal.ShowStep(achievement, stepTier, reached);
			row.Add(stepMedal);
			AchievementElements.Text(row, AchievementTiers.NameOf(stepTier, achievement.MaxTier), "wm-ach-step__tier wm-ach-tier--" + stepTier);
			VisualElement texts = AchievementElements.Part(row, "wm-ach-step__texts");
			AchievementElements.Text(texts, achievement.Condition, "wm-ach-step__condition");
			string date = book.DateOf(achievement.ID, stepTier);
			string when = reached ? (date == null ? "달성" : $"{date} 달성") : "아직";
			AchievementElements.Text(texts, when, "wm-ach-step__when");

			VisualElement progress = AchievementElements.Part(row, "wm-ach-step__progress");
			int shown = reached ? target : Mathf.Min(value, target);
			AchievementElements.Text(progress, $"{shown} / {target}", "wm-ach-step__value");
			VisualElement fill = AchievementElements.Bar(progress, "wm-ach-step__bar");
			fill.style.width = Length.Percent(100f * shown / target);
		}

		private void TogglePin()
		{
			if (current == null)
			{
				return;
			}
			UiSfx.Play(UiSfxEvent.Confirm);
			AchievementWallChange change = toggleWall(current);
			// 바뀌면 화면이 장부 신호로 다시 그림. 칸이 다 찼을 때만 여기서 알림
			if (change == AchievementWallChange.Full)
			{
				noteLabel.text = $"벽이 다 찼음 ({settings.WallSlotCount}칸). 걸린 메달을 먼저 내리기";
			}
		}
	}
}
