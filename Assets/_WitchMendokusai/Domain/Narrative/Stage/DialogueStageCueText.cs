using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// `@명령 인자` 읽기와 되쓰기. 모르는 낱말은 줄 번호 알림 대상
	public static class DialogueStageCueText
	{
		public static bool TryRead(string body, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;

			string[] parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
			{
				problem = "무대 신호가 비었다";
				return false;
			}

			string command = parts[0];
			string[] args = parts.Skip(1).ToArray();
			switch (command)
			{
				case DialogueStageVocabulary.PRESENTATION:
					return ReadPresentation(args, out cue, out problem);
				case DialogueStageVocabulary.ENTER:
					return ReadEnter(args, out cue, out problem);
				case DialogueStageVocabulary.EXIT:
					return ReadExit(args, out cue, out problem);
				case DialogueStageVocabulary.MOVE:
					return ReadMove(args, out cue, out problem);
				case DialogueStageVocabulary.HIGHLIGHT:
					return ReadHighlight(args, out cue, out problem);
				case DialogueStageVocabulary.EMOTE:
					return ReadEmote(args, out cue, out problem);
				case DialogueStageVocabulary.SHOT:
					return ReadShot(args, out cue, out problem);
				case DialogueStageVocabulary.EFFECT:
					return ReadEffect(args, out cue, out problem);
				case DialogueStageVocabulary.CINEMA:
					return ReadCinema(args, out cue, out problem);
			}

			if (DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Motions, command, out DialogueStageMotion motion))
			{
				if (args.Length != 1)
				{
					problem = $"@{command} 에 인물 하나가 필요하다";
					return false;
				}
				cue = DialogueStageCue.ForMotion(args[0], motion);
				return true;
			}

			problem = $"모르는 무대 신호: \"{command}\"";
			return false;
		}

		public static string Write(DialogueStageCue cue)
		{
			switch (cue.Kind)
			{
				case DialogueStageCueKind.Presentation:
					return Line(DialogueStageVocabulary.PRESENTATION, DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Presentations, cue.Presentation));
				case DialogueStageCueKind.Enter:
					return Line(DialogueStageVocabulary.ENTER, cue.Actor,
						DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Slots, cue.Slot),
						DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Entrances, cue.Entrance));
				case DialogueStageCueKind.Exit:
					return Line(DialogueStageVocabulary.EXIT, cue.Actor, DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Entrances, cue.Entrance));
				case DialogueStageCueKind.Move:
					return Line(DialogueStageVocabulary.MOVE, cue.Actor, DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Slots, cue.Slot));
				case DialogueStageCueKind.Highlight:
					return Line(DialogueStageVocabulary.HIGHLIGHT, cue.IsOn ? cue.Actor : DialogueStageVocabulary.OFF);
				case DialogueStageCueKind.Motion:
					return Line(DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Motions, cue.Motion), cue.Actor);
				case DialogueStageCueKind.Emote:
					return Line(DialogueStageVocabulary.EMOTE, cue.Actor, DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Emotes, cue.Emote));
				case DialogueStageCueKind.Shot:
					return Line(DialogueStageVocabulary.SHOT, cue.Actor, DialogueStageVocabulary.WordOf(DialogueStageVocabulary.Shots, cue.Shot));
				case DialogueStageCueKind.ShotEnd:
					return Line(DialogueStageVocabulary.SHOT, DialogueStageVocabulary.END);
				case DialogueStageCueKind.Effect:
					return string.IsNullOrEmpty(cue.Actor)
						? Line(DialogueStageVocabulary.EFFECT, cue.EffectName)
						: Line(DialogueStageVocabulary.EFFECT, cue.EffectName, cue.Actor);
				case DialogueStageCueKind.Cinema:
					return Line(DialogueStageVocabulary.CINEMA, cue.IsOn ? DialogueStageVocabulary.ON : DialogueStageVocabulary.OFF);
				default:
					throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, "Unknown stage cue kind");
			}
		}

		private static string Line(params string[] words) => string.Join(" ", words);

		private static bool ReadPresentation(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length != 1 || DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Presentations, args[0], out DialogueStagePresentation presentation) == false)
			{
				problem = $"@{DialogueStageVocabulary.PRESENTATION} 는 다음 중 하나: {DialogueStageVocabulary.Join(DialogueStageVocabulary.Presentations)}";
				return false;
			}
			cue = DialogueStageCue.ForPresentation(presentation);
			return true;
		}

		private static bool ReadEnter(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length < 2 || args.Length > 3)
			{
				problem = $"@{DialogueStageVocabulary.ENTER} <인물> <자리> [슬라이드|페이드]";
				return false;
			}
			if (TryReadSlot(args[1], out DialogueStageSlot slot, out problem) == false)
			{
				return false;
			}
			DialogueStageEntrance entrance = DialogueStageEntrance.Slide;
			if (args.Length == 3 && TryReadEntrance(args[2], out entrance, out problem) == false)
			{
				return false;
			}
			cue = DialogueStageCue.Enter(args[0], slot, entrance);
			return true;
		}

		private static bool ReadExit(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length < 1 || args.Length > 2)
			{
				problem = $"@{DialogueStageVocabulary.EXIT} <인물|{DialogueStageCue.ALL_ACTORS}> [슬라이드|페이드]";
				return false;
			}
			DialogueStageEntrance entrance = DialogueStageEntrance.Slide;
			if (args.Length == 2 && TryReadEntrance(args[1], out entrance, out problem) == false)
			{
				return false;
			}
			cue = DialogueStageCue.Exit(args[0], entrance);
			return true;
		}

		private static bool ReadMove(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length != 2)
			{
				problem = $"@{DialogueStageVocabulary.MOVE} <인물> <자리>";
				return false;
			}
			if (TryReadSlot(args[1], out DialogueStageSlot slot, out problem) == false)
			{
				return false;
			}
			cue = DialogueStageCue.Move(args[0], slot);
			return true;
		}

		private static bool ReadHighlight(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length != 1)
			{
				problem = $"@{DialogueStageVocabulary.HIGHLIGHT} <인물|{DialogueStageVocabulary.OFF}>";
				return false;
			}
			cue = DialogueStageCue.Highlight(args[0] == DialogueStageVocabulary.OFF ? null : args[0]);
			return true;
		}

		private static bool ReadEmote(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length != 2 || DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Emotes, args[1], out DialogueStageEmote emote) == false)
			{
				problem = $"@{DialogueStageVocabulary.EMOTE} <인물> <{DialogueStageVocabulary.Join(DialogueStageVocabulary.Emotes)}>";
				return false;
			}
			cue = DialogueStageCue.ForEmote(args[0], emote);
			return true;
		}

		private static bool ReadShot(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length == 1 && args[0] == DialogueStageVocabulary.END)
			{
				cue = DialogueStageCue.ShotEnd();
				return true;
			}
			if (args.Length != 2 || DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Shots, args[1], out DialogueStageShot shot) == false)
			{
				problem = $"@{DialogueStageVocabulary.SHOT} <대상> <{DialogueStageVocabulary.Join(DialogueStageVocabulary.Shots)}> 또는 @{DialogueStageVocabulary.SHOT} {DialogueStageVocabulary.END}";
				return false;
			}
			cue = DialogueStageCue.ForShot(args[0], shot);
			return true;
		}

		private static bool ReadEffect(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length < 1 || args.Length > 2 || DialogueStageVocabulary.EffectNames.Contains(args[0]) == false)
			{
				problem = $"@{DialogueStageVocabulary.EFFECT} <{string.Join(", ", DialogueStageVocabulary.EffectNames)}> [인물]";
				return false;
			}
			cue = DialogueStageCue.Effect(args[0], args.Length == 2 ? args[1] : null);
			return true;
		}

		private static bool ReadCinema(string[] args, out DialogueStageCue cue, out string problem)
		{
			cue = null;
			problem = null;
			if (args.Length != 1 || (args[0] != DialogueStageVocabulary.ON && args[0] != DialogueStageVocabulary.OFF))
			{
				problem = $"@{DialogueStageVocabulary.CINEMA} <{DialogueStageVocabulary.ON}|{DialogueStageVocabulary.OFF}>";
				return false;
			}
			cue = DialogueStageCue.Cinema(args[0] == DialogueStageVocabulary.ON);
			return true;
		}

		private static bool TryReadSlot(string word, out DialogueStageSlot slot, out string problem)
		{
			problem = null;
			if (DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Slots, word, out slot))
			{
				return true;
			}
			problem = $"모르는 자리: \"{word}\" ({DialogueStageVocabulary.Join(DialogueStageVocabulary.Slots)})";
			return false;
		}

		private static bool TryReadEntrance(string word, out DialogueStageEntrance entrance, out string problem)
		{
			problem = null;
			if (DialogueStageVocabulary.TryRead(DialogueStageVocabulary.Entrances, word, out entrance))
			{
				return true;
			}
			problem = $"모르는 전환: \"{word}\" ({DialogueStageVocabulary.Join(DialogueStageVocabulary.Entrances)})";
			return false;
		}
	}
}
