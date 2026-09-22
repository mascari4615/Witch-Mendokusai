using System;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 손패 카드 셋과 대상 지정 표시.
	///
	/// ★ 대상 지정은 두 길이 하나의 표시를 쓴다. 카드를 끌든 (여기서 포인터를 잡음), 카드를 탭한 뒤 적을 탭하든
	///   (<see cref="ShowAimFor"/> 와 <see cref="MoveAimTo"/> 를 밖에서 부름). 표시는 참고 실측 (2026-09-22) 대로:
	///   화면 어둡게 + 커서 아래 바닥 타원만 밝게, 고른 카드 들림 + CANCEL 탭, 좌상단 한 줄 설명. 카드와 바닥 사이 선 없음
	/// </summary>
	internal sealed class CardHandController
	{
		private readonly VisualElement battle;
		private readonly UIContentSO content;
		private readonly Func<int, bool> canAim;
		private readonly Action<int> clicked;
		private readonly Func<IPanel, Vector2, long?> pickFoe;
		private readonly Func<int, long, bool> castAt;
		private readonly Action<long?> aimAt;
		private readonly Action aimMissed;
		private readonly DollVisualPresenter dollVisualPresenter;
		private readonly Button[] buttons;
		private readonly VisualElement[] icons;
		private readonly VisualElement[] faces;
		private readonly Label[] costs;
		private readonly Label[] names;
		private readonly int[] shownOwners;
		private readonly VisualElement dimLayer;
		private readonly AimHoleElement hole;
		private readonly VisualElement briefLayer;
		private readonly Label brief;
		private int aimedHand = -1;
		private int pointer = -1;
		private int suppressedClick = -1;

		public CardHandController(
			VisualElement battle,
			VisualTreeAsset cardAsset,
			DollVisualPresenter dollVisualPresenter,
			UIContentSO content,
			Func<int, bool> canAim,
			Action<int> clicked,
			Func<IPanel, Vector2, long?> pickFoe,
			Func<int, long, bool> castAt,
			Action<long?> aimAt,
			Action aimMissed)
		{
			this.battle = battle;
			this.dollVisualPresenter = dollVisualPresenter;
			this.content = content;
			this.canAim = canAim;
			this.clicked = clicked;
			this.pickFoe = pickFoe;
			this.castAt = castAt;
			this.aimAt = aimAt;
			this.aimMissed = aimMissed;
			dimLayer = battle.RequireQ<VisualElement>("skill-dim");
			hole = new AimHoleElement { name = "skill-hole" };
			hole.AddToClassList("idle-skill-hole");
			dimLayer.Add(hole);
			briefLayer = battle.RequireQ<VisualElement>("skill-aim");
			brief = briefLayer.RequireQ<Label>("skill-brief");

			buttons = new Button[IdleCards.HAND_SIZE];
			icons = new VisualElement[IdleCards.HAND_SIZE];
			faces = new VisualElement[IdleCards.HAND_SIZE];
			costs = new Label[IdleCards.HAND_SIZE];
			names = new Label[IdleCards.HAND_SIZE];
			shownOwners = new int[IdleCards.HAND_SIZE];
			VisualElement cards = battle.RequireQ<VisualElement>("cards");
			for (int index = 0; index < buttons.Length; index++)
			{
				int captured = index;
				TemplateContainer tree = cardAsset.Instantiate();
				Button button = tree.RequireQ<Button>("card");
				icons[index] = button.RequireQ<VisualElement>("card-icon");
				faces[index] = button.RequireQ<VisualElement>("card-face");
				costs[index] = button.RequireQ<Label>("card-cost");
				names[index] = button.RequireQ<Label>("card-name");
				// 자리 번호는 편성 자리이자 단축키 자리 (시안 C)
				button.RequireQ<Label>("card-slot").text = (index + 1).ToString();
				shownOwners[index] = int.MinValue;
				button.RemoveFromHierarchy();
				cards.Add(button);
				buttons[index] = button;
				button.clicked += () => OnClicked(captured);
				button.RegisterCallback<PointerDownEvent>(moment => BeginAim(captured, moment));
				button.RegisterCallback<PointerMoveEvent>(MoveAim);
				button.RegisterCallback<PointerUpEvent>(moment => EndAim(captured, moment, true));
				button.RegisterCallback<PointerCancelEvent>(moment => EndAim(captured, moment, false));
			}
		}

		public void BringAimToFront()
		{
			briefLayer.BringToFront();
		}

		/// <summary>카드를 누른 채 대상을 고르는 중 (끌기). 탭 조준은 <see cref="ShownHand"/> 로 본다</summary>
		public bool IsAiming => aimedHand >= 0 && pointer >= 0;

		/// <summary>지정 표시가 켜진 손패 자리. 끌기든 탭이든. 없으면 -1</summary>
		public int ShownHand { get; private set; } = -1;

		public void CancelAim()
		{
			aimedHand = -1;
			pointer = -1;
			HideAim();
			aimAt(null);
		}

		/// <summary>탭 조준 시작. 카드를 들고 설명을 띄우고 화면을 어둡게. 타원은 전투 창 가운데에서 시작</summary>
		public void ShowAimFor(int handIndex)
		{
			if (handIndex < 0 || handIndex >= buttons.Length)
			{
				return;
			}

			ShowAim(handIndex);
			Rect box = battle.contentRect;
			PlaceHole(new Vector2(box.center.x, box.center.y), false);
		}

		/// <summary>탭 조준 중 포인터가 전투 창 위를 지난다. 타원과 대상 강조를 따라가게</summary>
		public void MoveAimTo(Vector2 panelPosition)
		{
			if (ShownHand < 0)
			{
				return;
			}

			TrackTarget(panelPosition);
		}

		public void Render(IdleSnapshot snapshot)
		{
			for (int index = 0; index < buttons.Length; index++)
			{
				IdleCardView card = snapshot.Cards[index];
				// 카드는 편성 인형의 스킬. 자리가 비면 카드도 없다 (2026-09-21)
				buttons[index].EnableInClassList("idle-card--empty", card.Empty);
				if (card.Empty)
				{
					continue;
				}

				costs[index].text = card.Cost.ToString();
				names[index].text = content.CardName(card.Kind);
				SetIconClass(icons[index], card.Kind);
				SetKindClass(buttons[index], card.Kind);
				if (shownOwners[index] != card.OwnerDollId)
				{
					shownOwners[index] = card.OwnerDollId;
					dollVisualPresenter.SetFace(faces[index], card.OwnerDollId);
				}
				buttons[index].SetEnabled(card.CanCast);
				buttons[index].EnableInClassList("idle-card--ready", card.CanCast);
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
			ShowAim(handIndex);
			buttons[handIndex].CapturePointer(moment.pointerId);
			TrackTarget(moment.position);
			moment.StopImmediatePropagation();
		}

		private void MoveAim(PointerMoveEvent moment)
		{
			if (moment.pointerId != pointer) { return; }
			TrackTarget(moment.position);
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
			HideAim();
			long? foe = commit ? pickFoe(battle.panel, position) : null;
			if (foe.HasValue)
			{
				castAt(handIndex, foe.Value);
			}
			else if (commit)
			{
				aimMissed();
			}

			aimAt(null);
			aimedHand = -1;
		}

		/// <summary>지정 표시 켜기. 카드 들림 + CANCEL, 설명 한 줄, 덮개</summary>
		private void ShowAim(int handIndex)
		{
			if (ShownHand >= 0 && ShownHand != handIndex)
			{
				buttons[ShownHand].RemoveFromClassList("idle-card--aiming");
			}

			ShownHand = handIndex;
			buttons[handIndex].AddToClassList("idle-card--aiming");
			brief.text = content.VolleyBrief;
			dimLayer.style.display = DisplayStyle.Flex;
			briefLayer.style.display = DisplayStyle.Flex;
		}

		private void HideAim()
		{
			if (ShownHand >= 0)
			{
				buttons[ShownHand].RemoveFromClassList("idle-card--aiming");
			}

			ShownHand = -1;
			dimLayer.style.display = DisplayStyle.None;
			briefLayer.style.display = DisplayStyle.None;
			hole.SetReady(false);
		}

		/// <summary>커서 아래 적을 무대와 타원에 알린다</summary>
		private void TrackTarget(Vector2 panelPosition)
		{
			long? foe = pickFoe(battle.panel, panelPosition);
			aimAt(foe);
			PlaceHole(dimLayer.WorldToLocal(panelPosition), foe.HasValue);
		}

		private void PlaceHole(Vector2 local, bool ready)
		{
			hole.SetCenter(local);
			hole.SetReady(ready);
		}

		private static void SetIconClass(VisualElement element, IdleCardKind kind)
		{
			element.EnableInClassList("idle-card-icon--volley", kind == IdleCardKind.Volley);
			element.EnableInClassList("idle-card-icon--supply", kind == IdleCardKind.Supply);
			element.EnableInClassList("idle-card-icon--appraise", kind == IdleCardKind.Appraise);
			element.EnableInClassList("idle-card-icon--haste", kind == IdleCardKind.Haste);
		}

		/// <summary>종류 색은 카드 전체가 아니라 왼쪽 띠와 이름 색 (시안 C)</summary>
		private static void SetKindClass(VisualElement element, IdleCardKind kind)
		{
			element.EnableInClassList("idle-card--volley", kind == IdleCardKind.Volley);
			element.EnableInClassList("idle-card--supply", kind == IdleCardKind.Supply);
			element.EnableInClassList("idle-card--appraise", kind == IdleCardKind.Appraise);
			element.EnableInClassList("idle-card--haste", kind == IdleCardKind.Haste);
		}
	}
}
