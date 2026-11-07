using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 인물 무대 부분
	public partial class DialogueStageView
	{
		private void ApplyDelta(DialogueStageDelta delta)
		{
			bool showOneShots = isSkipping == false && UIEffects.Enabled;

			if (delta.PresentationChanged)
				SetPresentation(delta.Presentation);
			speaker = delta.Speaker;
			highlighted = delta.Highlighted;

			foreach (DialogueStageActorChange change in delta.Entered)
				EnterActor(change, showOneShots);
			foreach (DialogueStageActorChange change in delta.Moved)
				MoveActor(change);
			foreach (DialogueStageActorChange change in delta.Exited)
				ExitActor(change);

			RestoreSlotTargets(delta.Actors);
			RefreshActorTargets();

			if (delta.CinemaChanged)
				root.EnableInClassList(USS_CINEMA, delta.Cinema);

			if (delta.ShotChanged)
				cameraDirector.Apply(delta.ShotTarget, delta.Shot);

			if (showOneShots)
				PlayOneShots(delta);

			if (showOneShots == false)
				SnapIfEffectsOff();

			RefreshOpenState();
		}

		private void PlayOneShots(DialogueStageDelta delta)
		{
			foreach (DialogueStageActorMotion motion in delta.Motions)
			{
				if (actors.TryGetValue(motion.Actor, out DialogueStageActorElement actor))
					actor.PlayMotion(settings.FindMotion(motion.Motion));
			}

			foreach (DialogueStageActorEmote emote in delta.Emotes)
			{
				if (actors.TryGetValue(emote.Actor, out DialogueStageActorElement actor) == false)
					continue;
				DialogueStageSettings.EmoteEntry entry = settings.FindEmote(emote.Emote);
				actor.PlayEmote(entry, settings);
				EmitParticles(actor, entry.ParticleCount, entry.ParticleColor);
			}

			foreach (DialogueStageEffectRequest effect in delta.Effects)
				PlayEffect(effect.EffectName, effect.Actor);
		}

		private void SnapIfEffectsOff()
		{
			if (UIEffects.Enabled)
				return;
			foreach (DialogueStageActorElement actor in actors.Values)
				actor.Snap();
		}

		private void EnterActor(DialogueStageActorChange change, bool showOneShots)
		{
			DialogueStageActorElement actor = GetOrCreateActor(change.Actor);
			if (actor == null)
				return;

			actor.IsOnStage = true;
			actor.SnapTo(presentation == DialogueStagePresentation.Field ? settings.FieldPosition : TargetXFor(change.Slot));
			bool slides = showOneShots && change.Entrance == DialogueStageEntrance.Slide;
			actor.BeginSlide(slides ? SlideOffsetFor(change.Slot) : 0f);
			if (showOneShots && string.IsNullOrEmpty(settings.EnterEffectName) == false)
				PlayEffect(settings.EnterEffectName, change.Actor);
		}

		private void MoveActor(DialogueStageActorChange change)
		{
			if (actors.TryGetValue(change.Actor, out DialogueStageActorElement actor))
				actor.SetTargetX(TargetXFor(change.Slot));
		}

		private void ExitActor(DialogueStageActorChange change)
		{
			if (actors.TryGetValue(change.Actor, out DialogueStageActorElement actor) == false)
				return;
			actor.IsOnStage = false;
			if (change.Entrance == DialogueStageEntrance.Slide)
				actor.EndSlide(SlideOffsetFor(change.Slot));
		}

		private DialogueStageActorElement GetOrCreateActor(string actorName)
		{
			if (actors.TryGetValue(actorName, out DialogueStageActorElement existing))
				return existing;

			DialogueStageSettings.CastEntry entry = settings.FindCast(actorName);
			if (entry == null)
			{
				WarnOnce(actorName, "표시 데이터 없는 인물, 일러 없이 진행");
				return null;
			}

			DialogueStageActorElement actor = new(actorName, entry, GetGlowTexture(), GetArtFilter());
			actors.Add(actorName, actor);
			cast.Add(actor);
			return actor;
		}

		private float TargetXFor(DialogueStageSlot slot) => settings.SlotPosition(slot);

		private float SlideOffsetFor(DialogueStageSlot slot) =>
			settings.SlotPosition(slot) < 0.5f ? -settings.EnterSlideDistance : settings.EnterSlideDistance;

		// 표시 방식과 화자에 따른 목표값. 방식 전환 트윈도 이 값을 따라감
		private void RefreshActorTargets()
		{
			foreach (DialogueStageActorElement actor in actors.Values)
			{
				bool isSpeaking = actor.ActorName == speaker;
				bool isEmphasized = highlighted == null || actor.ActorName == highlighted;
				float offsetY = 0f;
				float scale = settings.SpeakerScale;
				float opacity = actor.IsOnStage ? 1f : 0f;

				switch (presentation)
				{
					case DialogueStagePresentation.World:
						opacity = 0f;
						break;
					case DialogueStagePresentation.Feature:
						scale *= settings.FeatureScale;
						break;
					case DialogueStagePresentation.Field:
						scale *= settings.FieldScale;
						offsetY = settings.FieldOffsetY;
						opacity = actor.IsOnStage && isSpeaking ? 1f : 0f;
						actor.SetTargetX(settings.FieldPosition);
						break;
				}

				actor.SetTargets(opacity, scale, isEmphasized ? 0f : 1f, actor.ActorName == highlighted ? settings.SpeakerGlowOpacity : 0f, offsetY);
			}
		}

		// 슬롯 자리 기준값. 필드 방식이면 RefreshActorTargets가 덮음
		private void RestoreSlotTargets(IReadOnlyList<DialogueStageActor> stageActors)
		{
			foreach (DialogueStageActor stageActor in stageActors)
			{
				if (actors.TryGetValue(stageActor.Name, out DialogueStageActorElement actor))
					actor.SetTargetX(TargetXFor(stageActor.Slot));
			}
		}

		private void TickActors(float deltaTime)
		{
			Vector2 shake = SampleScreenShake(deltaTime);
			foreach (DialogueStageActorElement actor in actors.Values)
				actor.Tick(deltaTime, settings, shake);
		}

		private void ClearActors()
		{
			foreach (DialogueStageActorElement actor in actors.Values)
				actor.RemoveFromHierarchy();
			actors.Clear();
		}
	}
}
