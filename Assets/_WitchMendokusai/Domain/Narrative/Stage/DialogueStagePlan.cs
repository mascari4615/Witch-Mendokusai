using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public sealed class DialogueStageActor
	{
		public string Name { get; }
		public DialogueStageSlot Slot { get; internal set; }

		internal DialogueStageActor(string name, DialogueStageSlot slot)
		{
			Name = name;
			Slot = slot;
		}
	}

	public readonly struct DialogueStageActorChange
	{
		public string Actor { get; }
		public DialogueStageSlot Slot { get; }
		public DialogueStageEntrance Entrance { get; }

		public DialogueStageActorChange(string actor, DialogueStageSlot slot, DialogueStageEntrance entrance)
		{
			Actor = actor;
			Slot = slot;
			Entrance = entrance;
		}
	}

	public readonly struct DialogueStageActorMotion
	{
		public string Actor { get; }
		public DialogueStageMotion Motion { get; }

		public DialogueStageActorMotion(string actor, DialogueStageMotion motion)
		{
			Actor = actor;
			Motion = motion;
		}
	}

	public readonly struct DialogueStageActorEmote
	{
		public string Actor { get; }
		public DialogueStageEmote Emote { get; }

		public DialogueStageActorEmote(string actor, DialogueStageEmote emote)
		{
			Actor = actor;
			Emote = emote;
		}
	}

	public readonly struct DialogueStageEffectRequest
	{
		public string EffectName { get; }
		public string Actor { get; }

		public DialogueStageEffectRequest(string effectName, string actor)
		{
			EffectName = effectName;
			Actor = actor;
		}
	}

	// 이번 줄에서 바뀐 것과 지금 상태
	public sealed class DialogueStageDelta
	{
		public DialogueStagePresentation Presentation { get; internal set; }
		public bool PresentationChanged { get; internal set; }
		public string Speaker { get; internal set; }
		public bool SpeakerChanged { get; internal set; }
		public string Highlighted { get; internal set; }
		public bool Cinema { get; internal set; }
		public bool CinemaChanged { get; internal set; }
		public string ShotTarget { get; internal set; }
		public DialogueStageShot Shot { get; internal set; }
		public bool ShotChanged { get; internal set; }

		public List<DialogueStageActorChange> Entered { get; } = new();
		public List<DialogueStageActorChange> Exited { get; } = new();
		public List<DialogueStageActorChange> Moved { get; } = new();
		public List<DialogueStageActorMotion> Motions { get; } = new();
		public List<DialogueStageActorEmote> Emotes { get; } = new();
		public List<DialogueStageEffectRequest> Effects { get; } = new();
		public List<DialogueStageActor> Actors { get; } = new();
	}

	// 대사 흐름을 따라 StageCue를 누적, 줄마다 StageDelta 발행. 분기는 실제 지나간 길만 누적
	public sealed class DialogueStagePlan
	{
		private static readonly DialogueStageSlot[] AUTO_SLOT_ORDER =
		{
			DialogueStageSlot.Center,
			DialogueStageSlot.Left,
			DialogueStageSlot.Right,
			DialogueStageSlot.FarLeft,
			DialogueStageSlot.FarRight,
		};

		private readonly List<DialogueStageCue> pending = new();
		private readonly List<DialogueStageActor> actors = new();

		private bool hasSpeaker;
		private string speaker;
		private string highlightOverride;
		private bool cinema;
		private string shotTarget;
		private DialogueStageShot shot;

		public DialogueStagePresentation Presentation { get; private set; } = DialogueStagePresentation.World;
		public bool HasPending => pending.Count > 0;
		public IReadOnlyList<DialogueStageActor> Actors => actors;
		public string Speaker => speaker;
		public string ShotTarget => shotTarget;

		public void Queue(IReadOnlyList<DialogueStageCue> cues)
		{
			if (cues == null)
			{
				return;
			}
			for (int i = 0; i < cues.Count; i++)
			{
				if (cues[i] != null)
				{
					pending.Add(cues[i]);
				}
			}
		}

		// 대사 줄. 화자 갱신과 자동 등장 포함. speakerName이 null이면 나레이션
		public DialogueStageDelta FlushForLine(string speakerName)
		{
			DialogueStageDelta delta = ConsumePending();
			string next = string.IsNullOrWhiteSpace(speakerName) ? null : speakerName.Trim();
			delta.SpeakerChanged = hasSpeaker == false || string.Equals(speaker, next, StringComparison.Ordinal) == false;
			hasSpeaker = true;
			speaker = next;

			if (next != null && Presentation != DialogueStagePresentation.World && FindActor(next) == null)
			{
				EnterActor(delta, next, FirstFreeSlot(), DialogueStageEntrance.Slide);
			}
			return FinishDelta(delta);
		}

		// 대사 없는 자리(선택지, 대기). 화자 유지
		public DialogueStageDelta Flush() => FinishDelta(ConsumePending());

		private DialogueStageDelta ConsumePending()
		{
			DialogueStageDelta delta = new();
			for (int i = 0; i < pending.Count; i++)
			{
				ApplyCue(delta, pending[i]);
			}
			pending.Clear();
			return delta;
		}

		private DialogueStageDelta FinishDelta(DialogueStageDelta delta)
		{
			delta.Presentation = Presentation;
			delta.Speaker = speaker;
			delta.Highlighted = highlightOverride ?? speaker;
			delta.Cinema = cinema;
			delta.ShotTarget = shotTarget;
			delta.Shot = shot;
			delta.Actors.AddRange(actors);
			return delta;
		}

		private void ApplyCue(DialogueStageDelta delta, DialogueStageCue cue)
		{
			switch (cue.Kind)
			{
				case DialogueStageCueKind.Presentation:
					if (Presentation != cue.Presentation)
					{
						Presentation = cue.Presentation;
						delta.PresentationChanged = true;
					}
					break;

				case DialogueStageCueKind.Enter:
					if (FindActor(cue.Actor) == null)
					{
						EnterActor(delta, cue.Actor, cue.Slot, cue.Entrance);
					}
					else
					{
						MoveActor(delta, cue.Actor, cue.Slot);
					}
					break;

				case DialogueStageCueKind.Exit:
					ExitActors(delta, cue);
					break;

				case DialogueStageCueKind.Move:
					if (FindActor(cue.Actor) == null)
					{
						EnterActor(delta, cue.Actor, cue.Slot, DialogueStageEntrance.Slide);
					}
					else
					{
						MoveActor(delta, cue.Actor, cue.Slot);
					}
					break;

				case DialogueStageCueKind.Highlight:
					highlightOverride = cue.IsOn ? cue.Actor : null;
					break;

				case DialogueStageCueKind.Motion:
					delta.Motions.Add(new DialogueStageActorMotion(cue.Actor, cue.Motion));
					break;

				case DialogueStageCueKind.Emote:
					delta.Emotes.Add(new DialogueStageActorEmote(cue.Actor, cue.Emote));
					break;

				case DialogueStageCueKind.Shot:
					shotTarget = cue.Actor;
					shot = cue.Shot;
					delta.ShotChanged = true;
					break;

				case DialogueStageCueKind.ShotEnd:
					if (shotTarget != null)
					{
						shotTarget = null;
						delta.ShotChanged = true;
					}
					break;

				case DialogueStageCueKind.Effect:
					delta.Effects.Add(new DialogueStageEffectRequest(cue.EffectName, cue.Actor));
					break;

				case DialogueStageCueKind.Cinema:
					if (cinema != cue.IsOn)
					{
						cinema = cue.IsOn;
						delta.CinemaChanged = true;
					}
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, "Unknown stage cue kind");
			}
		}

		private void EnterActor(DialogueStageDelta delta, string actorName, DialogueStageSlot slot, DialogueStageEntrance entrance)
		{
			actors.Add(new DialogueStageActor(actorName, slot));
			delta.Exited.RemoveAll(change => string.Equals(change.Actor, actorName, StringComparison.Ordinal));
			delta.Entered.Add(new DialogueStageActorChange(actorName, slot, entrance));
		}

		private void MoveActor(DialogueStageDelta delta, string actorName, DialogueStageSlot slot)
		{
			DialogueStageActor actor = FindActor(actorName);
			if (actor.Slot == slot)
			{
				return;
			}
			actor.Slot = slot;
			int entered = delta.Entered.FindIndex(change => string.Equals(change.Actor, actorName, StringComparison.Ordinal));
			if (entered >= 0)
			{
				delta.Entered[entered] = new DialogueStageActorChange(actorName, slot, delta.Entered[entered].Entrance);
				return;
			}
			delta.Moved.RemoveAll(change => string.Equals(change.Actor, actorName, StringComparison.Ordinal));
			delta.Moved.Add(new DialogueStageActorChange(actorName, slot, default));
		}

		private void ExitActors(DialogueStageDelta delta, DialogueStageCue cue)
		{
			for (int i = actors.Count - 1; i >= 0; i--)
			{
				DialogueStageActor actor = actors[i];
				if (cue.TargetsAllActors == false && string.Equals(actor.Name, cue.Actor, StringComparison.Ordinal) == false)
				{
					continue;
				}
				actors.RemoveAt(i);
				delta.Moved.RemoveAll(change => string.Equals(change.Actor, actor.Name, StringComparison.Ordinal));
				bool enteredThisLine = delta.Entered.RemoveAll(change => string.Equals(change.Actor, actor.Name, StringComparison.Ordinal)) > 0;
				if (enteredThisLine == false)
				{
					delta.Exited.Add(new DialogueStageActorChange(actor.Name, actor.Slot, cue.Entrance));
				}
			}
		}

		private DialogueStageActor FindActor(string actorName)
		{
			for (int i = 0; i < actors.Count; i++)
			{
				if (string.Equals(actors[i].Name, actorName, StringComparison.Ordinal))
				{
					return actors[i];
				}
			}
			return null;
		}

		private DialogueStageSlot FirstFreeSlot()
		{
			for (int i = 0; i < AUTO_SLOT_ORDER.Length; i++)
			{
				bool isTaken = false;
				for (int a = 0; a < actors.Count; a++)
				{
					isTaken |= actors[a].Slot == AUTO_SLOT_ORDER[i];
				}
				if (isTaken == false)
				{
					return AUTO_SLOT_ORDER[i];
				}
			}
			return DialogueStageSlot.Center;
		}
	}
}
