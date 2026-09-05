using System;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	internal sealed class BattleActionController
	{
		private readonly IdleSession session;
		private readonly BattleStage stage;
		private readonly UIContentSO content;
		private readonly RuntimeSettingsSO settings;
		private readonly Action cancelCardAim;
		/// <summary>일제 사격을 누른 뒤 대상을 기다리는 손패 자리</summary>
		private int armedHand = -1;
		private readonly Action closeMap;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showNote;

		public BattleActionController(
			IdleSession session,
			BattleStage stage,
			UIContentSO content,
			RuntimeSettingsSO settings,
			Action cancelCardAim,
			Action closeMap,
			Action writeDown,
			Action requestRender,
			Action<string, float> showNote)
		{
			this.session = session;
			this.stage = stage;
			this.content = content;
			this.settings = settings;
			this.cancelCardAim = cancelCardAim;
			this.closeMap = closeMap;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showNote = showNote;
		}

		public bool CanAimCard(int handIndex)
		{
			if (handIndex < 0)
			{
				return false;
			}

			IdleSnapshot snapshot = session.Capture();
			return handIndex < snapshot.Cards.Length
				&& snapshot.Cards[handIndex].Kind == IdleCardKind.Volley
				&& snapshot.Cards[handIndex].CanCast;
		}

		public long? PickFoe(IPanel panel, Vector2 position)
		{
			return stage != null && stage.TryPickFoe(panel, position, out long foeIndex)
				? foeIndex
				: (long?)null;
		}

		/// <summary>조준 중 커서 아래 적을 무대에 알린다. 사람이 누가 맞는지 보게</summary>
		public void AimAt(long? foeIndex)
		{
			stage?.SetAimTarget(foeIndex ?? -1L);
		}

		/// <summary>적을 못 짚고 놓았다. 조용히 사라지면 고장으로 보임 (사용자 2026-09-05)</summary>
		public void VolleyMissed()
		{
			showNote(content.VolleyMissFeedback, settings.NoteSeconds);
		}

		public bool CastVolleyAt(int handIndex, long foeIndex)
		{
			if (session.TryCastCardAt(handIndex, foeIndex, out IdleCardResult result) == false)
			{
				return false;
			}

			armedHand = -1;

			stage?.OnVolley(foeIndex);
			showNote(content.VolleyTargetFeedback, settings.NoteSeconds);
			writeDown();
			requestRender();
			return true;
		}

		/// <summary>
		/// 전투 창을 눌렀다. 일제 사격을 겨눈 상태면 <b>그 자리의 적</b>에게 쏘고, 아니면 응원 한 대
		///
		/// ★ 끌어 놓기만으로는 안 되는 자리가 있었다 (사용자 2026-09-05: 스킬 사용이 전혀 불가능).
		///   그래서 카드를 누르면 겨눔 상태가 되고 다음 누름이 대상이 됨. 끌어 놓기도 그대로 둠
		/// </summary>
		public void OnBattleTapped(PointerDownEvent moment)
		{
			if (moment.target is Button ||
				(moment.target is VisualElement element && IsInsideBox(element)))
			{
				return;
			}

			if (armedHand >= 0)
			{
				IPanel panel = moment.target is VisualElement spot ? spot.panel : null;
				long? foe = panel != null ? PickFoe(panel, moment.position) : null;
				int hand = armedHand;
				Disarm();

				if (foe.HasValue)
				{
					CastVolleyAt(hand, foe.Value);
				}
				else
				{
					showNote(content.VolleyMissFeedback, settings.NoteSeconds);
				}

				return;
			}

			session.Send(new IdleTapIntent());
			stage?.OnTap();
			requestRender();
		}

		/// <summary>지금 겨누고 있는 손패 자리. 없으면 -1</summary>
		public int ArmedHand => armedHand;

		/// <summary>겨눔 풀기. 조준을 끌어서 마쳤거나 Esc 를 눌렀을 때</summary>
		public void Disarm()
		{
			armedHand = -1;
			stage?.SetAimTarget(-1L);
			requestRender();
		}

		public void Cast(int handIndex)
		{
			IdleSnapshot beforeCast = session.Capture();
			if (handIndex < 0 || handIndex >= beforeCast.Cards.Length)
			{
				return;
			}

			IdleCardKind selected = beforeCast.Cards[handIndex].Kind;
			if (selected == IdleCardKind.Volley)
			{
				cancelCardAim();

				if (armedHand == handIndex)
				{
					Disarm();
					return;
				}

				armedHand = beforeCast.Cards[handIndex].CanCast ? handIndex : -1;
				showNote(armedHand >= 0 ? content.VolleyTapHint : content.VolleyDragHint, settings.NoteSeconds);
				requestRender();
				return;
			}

			if (session.TryCastCard(handIndex, out IdleCardResult result) == false)
			{
				return;
			}

			switch (result.Kind)
			{
				case IdleCardKind.Volley:
					showNote(content.VolleyResolvedFeedback, settings.NoteSeconds);
					break;

				case IdleCardKind.Supply:
					stage?.OnSupply((float)result.EffectSeconds);
					showNote(content.SupplyFeedbackText(
						result.EffectSeconds, result.EffectMultiplier), settings.NoteSeconds);
					break;

				default:
					stage?.OnAppraise();
					showNote(result.HasRoll
						? content.AppraiseCardFeedbackText(
							result.Roll.Tier, result.Roll.Value, result.Roll.Replaced)
						: content.AppraiseCardEmptyFeedback, settings.NoteSeconds);
					break;
			}

			writeDown();
			requestRender();
		}

		public void StepStage(int delta)
		{
			GoToStage(session.Capture().Stage + delta);
		}

		public void GoToStage(int target)
		{
			closeMap();
			if (session.Send(new IdleGoToStageIntent(target)))
			{
				writeDown();
			}

			requestRender();
		}

		public void ToggleHold()
		{
			session.Send(new IdleHoldStageIntent(session.Capture().HoldingStage == false));
			writeDown();
			requestRender();
		}

		public void ToggleAutoCast()
		{
			session.ToggleAutoCast();
			requestRender();
		}

		private static bool IsInsideBox(VisualElement element)
		{
			for (VisualElement at = element; at != null; at = at.parent)
			{
				if (at.ClassListContains("idle-box"))
				{
					return true;
				}
			}

			return false;
		}
	}
}
