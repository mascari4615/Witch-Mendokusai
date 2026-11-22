using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>마도서 하늘의 별 상태. 선 색과 별 모양이 이것 하나로 정해짐</summary>
	public enum MagicBookStarState
	{
		Locked = 0,
		Doing = 1,
		Ready = 2,
		Done = 3,
	}

	/// <summary>별 이름 자리. 잎 별은 장이 뻗는 바깥쪽, 갈래 별은 아래 (어느 쪽이든 선과 겹치지 않게)</summary>
	public enum MagicBookLabelSide
	{
		Right = 0,
		Left = 1,
		Below = 2,
	}

	/// <summary>
	/// 하늘의 별 하나 (퀘스트 하나). 방향키 칸 이동과 확인 키를 받는 버튼. 모양은 MagicBook.uss 의 상태 클래스.
	/// 별 가운데가 자기 자리 (left, top) 에 오도록 크기의 절반만큼 당겨 둠 (USS)
	/// </summary>
	public sealed class MagicBookStar : Button
	{
		public const string USS_CLASS = "wm-mb-star";
		private const string USS_GLOW = "wm-mb-star__glow";
		private const string USS_RING = "wm-mb-star__ring";
		private const string USS_CORE = "wm-mb-star__core";
		private const string USS_ICON = "wm-mb-star__icon";
		private const string USS_LABEL = "wm-mb-star__label";
		private const string USS_NAME = "wm-mb-star__name";
		private const string USS_SUB = "wm-mb-star__sub";
		private const string USS_GATE = "wm-mb-star--gate";
		private static readonly string[] LABEL_CLASSES =
		{
			"wm-mb-star--label-right",
			"wm-mb-star--label-left",
			"wm-mb-star--label-below",
		};
		private static readonly string[] STATE_CLASSES =
		{
			"wm-mb-star--locked",
			"wm-mb-star--doing",
			"wm-mb-star--ready",
			"wm-mb-star--done",
		};

		private readonly Label subLabel;

		public QuestSO Quest { get; }
		public ChapterSO Chapter { get; }
		public MagicBookStarState State { get; private set; }
		/// <summary>하늘 좌표 (가운데 마도서가 0, 0)</summary>
		public Vector2 SkyPosition { get; }

		public MagicBookStar(QuestSO quest, ChapterSO chapter, Vector2 skyPosition, bool isGate, MagicBookLabelSide labelSide, Texture2D glowTexture)
		{
			Quest = quest;
			Chapter = chapter;
			SkyPosition = skyPosition;
			text = string.Empty;
			AddToClassList(USS_CLASS);
			EnableInClassList(USS_GATE, isGate);
			AddToClassList(LABEL_CLASSES[(int)labelSide]);
			style.left = skyPosition.x;
			style.top = skyPosition.y;

			VisualElement glow = Part(this, USS_GLOW);
			glow.style.backgroundImage = glowTexture;
			Part(this, USS_RING);
			VisualElement core = Part(this, USS_CORE);
			VisualElement icon = Part(core, USS_ICON);
			if (quest.Sprite != null)
			{
				icon.style.backgroundImage = new StyleBackground(quest.Sprite);
			}

			VisualElement label = Part(this, USS_LABEL);
			Label nameLabel = new(quest.Name);
			nameLabel.AddToClassList(USS_NAME);
			nameLabel.pickingMode = PickingMode.Ignore;
			label.Add(nameLabel);
			subLabel = new Label();
			subLabel.AddToClassList(USS_SUB);
			subLabel.pickingMode = PickingMode.Ignore;
			label.Add(subLabel);
		}

		public void SetState(MagicBookStarState state, string subText)
		{
			State = state;
			for (int i = 0; i < STATE_CLASSES.Length; i++)
			{
				EnableInClassList(STATE_CLASSES[i], i == (int)state);
			}
			subLabel.text = subText;
			subLabel.style.display = string.IsNullOrEmpty(subText) ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private static VisualElement Part(VisualElement parent, string className)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(className);
			parent.Add(element);
			return element;
		}
	}
}
