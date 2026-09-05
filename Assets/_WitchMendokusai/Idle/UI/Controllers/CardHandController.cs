using System;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class CardHandController
	{
		private readonly VisualElement battle;
		private readonly UIContentSO content;
		private readonly Func<int, bool> canAim;
		private readonly Action<int> clicked;
		private readonly Func<Vector2, long?> pickFoe;
		private readonly Func<int, long, bool> castAt;
		private readonly Button[] buttons;
		private readonly VisualElement[] icons;
		private readonly Label[] costs;
		private readonly Label[] names;
		private readonly Label[] queueChips;
		private readonly VisualElement aim;
		private readonly VisualElement aimOrigin;
		private readonly VisualElement aimLine;
		private readonly VisualElement aimRange;
		private readonly Label aimCaption;
		private int aimedHand = -1;
		private int pointer = -1;
		private int suppressedClick = -1;
		private Vector2 dragOrigin;

		public CardHandController(
			VisualElement battle,
			VisualTreeAsset cardAsset,
			VisualTreeAsset queueChipAsset,
			UIContentSO content,
			Func<int, bool> canAim,
			Action<int> clicked,
			Func<Vector2, long?> pickFoe,
			Func<int, long, bool> castAt)
		{
			this.battle = battle;
			this.content = content;
			this.canAim = canAim;
			this.clicked = clicked;
			this.pickFoe = pickFoe;
			this.castAt = castAt;
			aim = battle.Q<VisualElement>("skill-aim");
			aimOrigin = aim.Q<VisualElement>("skill-aim-origin");
			aimLine = aim.Q<VisualElement>("skill-aim-line");
			aimRange = aim.Q<VisualElement>("skill-aim-range");
			aimCaption = aim.Q<Label>("skill-aim-caption");

			buttons = new Button[IdleCards.HAND_SIZE];
			icons = new VisualElement[IdleCards.HAND_SIZE];
			costs = new Label[IdleCards.HAND_SIZE];
			names = new Label[IdleCards.HAND_SIZE];
			VisualElement cards = battle.Q<VisualElement>("cards");
			for (int index = 0; index < buttons.Length; index++)
			{
				int captured = index;
				TemplateContainer tree = cardAsset.Instantiate();
				Button button = tree.Q<Button>("card");
				icons[index] = button.Q<VisualElement>("card-icon");
				costs[index] = button.Q<Label>("card-cost");
				names[index] = button.Q<Label>("card-name");
				button.RemoveFromHierarchy();
				cards.Add(button);
				buttons[index] = button;
				button.clicked += () => OnClicked(captured);
				button.RegisterCallback<PointerDownEvent>(moment => BeginAim(captured, moment));
				button.RegisterCallback<PointerMoveEvent>(MoveAim);
				button.RegisterCallback<PointerUpEvent>(moment => EndAim(captured, moment, true));
				button.RegisterCallback<PointerCancelEvent>(moment => EndAim(captured, moment, false));
			}

			queueChips = new Label[IdleCards.QUEUE_SIZE];
			VisualElement queue = battle.Q<VisualElement>("card-queue");
			for (int index = 0; index < queueChips.Length; index++)
			{
				TemplateContainer tree = queueChipAsset.Instantiate();
				Label chip = tree.Q<Label>("chip");
				chip.RemoveFromHierarchy();
				chip.EnableInClassList("idle-queue-chip--next", index == 0);
				queue.Add(chip);
				queueChips[index] = chip;
			}
		}

		public void BringAimToFront()
		{
			aim.BringToFront();
		}

		/// <summary>카드를 누른 채 대상을 고르는 중. 세상이 느려지는 구간</summary>
		public bool IsAiming => aimedHand >= 0 && pointer >= 0;

		public void CancelAim()
		{
			aimedHand = -1;
			pointer = -1;
			aim.style.display = DisplayStyle.None;
		}

		public void Render(IdleSnapshot snapshot)
		{
			for (int index = 0; index < buttons.Length; index++)
			{
				IdleCardView card = snapshot.Cards[index];
				costs[index].text = card.Cost.ToString();
				names[index].text = content.CardName(card.Kind);
				SetIconClass(icons[index], card.Kind);
				buttons[index].SetEnabled(card.CanCast);
				buttons[index].EnableInClassList("idle-card--ready", card.CanCast);
			}

			for (int index = 0; index < queueChips.Length; index++)
			{
				queueChips[index].text = content.CardName(snapshot.Queued[index]);
			}
		}

		private void OnClicked(int handIndex)
		{
			if (suppressedClick == handIndex)
			{
				suppressedClick = -1;
				return;
			}
			clicked(handIndex);
		}

		private void BeginAim(int handIndex, PointerDownEvent moment)
		{
			if (canAim(handIndex) == false)
			{
				return;
			}
			aimedHand = handIndex;
			pointer = moment.pointerId;
			dragOrigin = moment.position;
			aim.style.display = DisplayStyle.Flex;
			buttons[handIndex].CapturePointer(moment.pointerId);
			UpdateAim(moment.position);
			moment.StopImmediatePropagation();
		}

		private void MoveAim(PointerMoveEvent moment)
		{
			if (moment.pointerId != pointer) { return; }
			UpdateAim(moment.position);
			moment.StopImmediatePropagation();
		}

		private void EndAim(int handIndex, PointerEventBase<PointerUpEvent> moment, bool commit)
		{
			EndAim(handIndex, moment.pointerId, moment.position, commit);
			moment.StopImmediatePropagation();
		}

		private void EndAim(int handIndex, PointerCancelEvent moment, bool commit)
		{
			EndAim(handIndex, moment.pointerId, moment.position, commit);
			moment.StopImmediatePropagation();
		}

		private void EndAim(int handIndex, int pointerId, Vector2 position, bool commit)
		{
			if (pointerId != pointer || handIndex != aimedHand) { return; }
			if (buttons[handIndex].HasPointerCapture(pointerId))
			{
				buttons[handIndex].ReleasePointer(pointerId);
			}
			suppressedClick = commit ? handIndex : -1;
			pointer = -1;
			aim.style.display = DisplayStyle.None;
			long? foe = commit ? pickFoe(position) : null;
			if (foe.HasValue)
			{
				castAt(handIndex, foe.Value);
			}
			aimedHand = -1;
		}

		private void UpdateAim(Vector2 panelPosition)
		{
			Vector2 origin = battle.WorldToLocal(dragOrigin);
			Vector2 target = battle.WorldToLocal(panelPosition);
			Vector2 delta = target - origin;
			// 중심 맞춤과 설명 띄우기는 USS (translate, margin). 코드는 점 찍기만
			aimOrigin.style.left = origin.x;
			aimOrigin.style.top = origin.y;
			aimRange.style.left = target.x;
			aimRange.style.top = target.y;
			aimLine.style.left = Mathf.Min(origin.x, target.x);
			aimLine.style.top = Mathf.Min(origin.y, target.y);
			aimLine.style.width = Mathf.Abs(delta.x);
			aimLine.style.height = Mathf.Abs(delta.y);
			aimCaption.style.left = target.x;
			aimCaption.style.top = target.y;
		}

		private static void SetIconClass(VisualElement element, IdleCardKind kind)
		{
			element.EnableInClassList("idle-card-icon--volley", kind == IdleCardKind.Volley);
			element.EnableInClassList("idle-card-icon--supply", kind == IdleCardKind.Supply);
			element.EnableInClassList("idle-card-icon--appraise", kind == IdleCardKind.Appraise);
		}
	}
}
