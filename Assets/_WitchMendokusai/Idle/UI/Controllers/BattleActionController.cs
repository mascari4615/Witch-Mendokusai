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

		public long? PickFoe(Vector2 position)
		{
			return stage != null && stage.TryPickFoe(position, out long foeIndex)
				? foeIndex
				: (long?)null;
		}

		public bool CastVolleyAt(int handIndex, long foeIndex)
		{
			if (session.TryCastCardAt(handIndex, foeIndex, out IdleCardResult result) == false)
			{
				return false;
			}

			stage?.OnVolley(foeIndex);
			showNote(content.VolleyTargetFeedback, settings.NoteSeconds);
			writeDown();
			requestRender();
			return true;
		}

		public void OnBattleTapped(PointerDownEvent moment)
		{
			if (moment.target is Button ||
				(moment.target is VisualElement element && IsInsideBox(element)))
			{
				return;
			}

			session.Send(new IdleTapIntent());
			stage?.OnTap();
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
				showNote(content.VolleyDragHint, settings.NoteSeconds);
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
