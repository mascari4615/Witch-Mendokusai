using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 카드 한 개. 일러스트 + 이름 + grade 테두리 (USS class).
	/// hover/click 이벤트 + active(현재 선택된 카드) 시각 표시.
	/// 시각은 `CodexWindow.uss` (단계 B) 에서 정의 — 본 단계는 USS class 만 부여.
	/// </summary>
	public class CodexCard : VisualElement
	{
		public const string USS_CLASS = "wm-codex-card";
		public const string USS_ICON = "wm-codex-card__icon";
		public const string USS_NAME = "wm-codex-card__name";
		public const string USS_GRADE_PREFIX = "wm-codex-card--grade-";
		public const string USS_LOCKED = "wm-codex-card--locked";
		public const string USS_HOVER = "wm-codex-card--hover";
		public const string USS_ACTIVE = "wm-codex-card--active";

		public event Action OnClicked = delegate { };

		public CodexEntry Entry { get; }

		private readonly Image iconImage;
		private readonly Label nameLabel;

		public CodexCard(CodexEntry entry)
		{
			Entry = entry;

			AddToClassList(USS_CLASS);
			if (string.IsNullOrEmpty(entry.GradeKey) == false)
				AddToClassList(USS_GRADE_PREFIX + entry.GradeKey);
			if (entry.IsUnlocked == false)
				AddToClassList(USS_LOCKED);

			focusable = true;

			iconImage = new Image
			{
				scaleMode = ScaleMode.ScaleToFit,
			};
			iconImage.AddToClassList(USS_ICON);
			iconImage.pickingMode = PickingMode.Ignore;
			if (entry.Icon != null)
				iconImage.sprite = entry.Icon;
			Add(iconImage);

			nameLabel = new Label(entry.IsUnlocked ? entry.DisplayName : "???");
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			Add(nameLabel);

			RegisterCallback<MouseEnterEvent>(_ => AddToClassList(USS_HOVER));
			RegisterCallback<MouseLeaveEvent>(_ => RemoveFromClassList(USS_HOVER));
			RegisterCallback<ClickEvent>(_ => OnClicked.Invoke());
		}

		public void SetActive(bool active)
		{
			if (active)
				AddToClassList(USS_ACTIVE);
			else
				RemoveFromClassList(USS_ACTIVE);
		}
	}
}
