using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 대본 낱말과 값의 표. 첫 낱말이 되쓰기 표기
	public static class DialogueStageVocabulary
	{
		public const string PRESENTATION = "방식";
		public const string ENTER = "등장";
		public const string EXIT = "퇴장";
		public const string MOVE = "이동";
		public const string HIGHLIGHT = "강조";
		public const string BOB = "들썩임";
		public const string NOD = "끄덕임";
		public const string HOP = "점프";
		public const string SHAKE = "흔들림";
		public const string EMOTE = "아이콘";
		public const string SHOT = "샷";
		public const string EFFECT = "이펙트";
		public const string CINEMA = "띠";
		public const string OFF = "끔";
		public const string ON = "켬";
		public const string END = "끝";

		public const string EFFECT_LIGHT = "빛";
		public const string EFFECT_SPARKLE = "반짝";
		public const string EFFECT_FLASH = "플래시";
		public const string EFFECT_SCREEN_SHAKE = "화면흔들림";

		public static readonly IReadOnlyList<string> EffectNames = new[]
		{
			EFFECT_LIGHT,
			EFFECT_SPARKLE,
			EFFECT_FLASH,
			EFFECT_SCREEN_SHAKE,
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStagePresentation>> Presentations = new[]
		{
			Word("미연시", DialogueStagePresentation.Novel),
			Word("창", DialogueStagePresentation.Novel),
			Word("2d", DialogueStagePresentation.Novel),
			Word("novel", DialogueStagePresentation.Novel),
			Word("자막", DialogueStagePresentation.Feature),
			Word("크게", DialogueStagePresentation.Feature),
			Word("feature", DialogueStagePresentation.Feature),
			Word("필드", DialogueStagePresentation.Field),
			Word("field", DialogueStagePresentation.Field),
			Word("말풍선", DialogueStagePresentation.World),
			Word("bubble", DialogueStagePresentation.World),
			Word("world", DialogueStagePresentation.World),
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStageSlot>> Slots = new[]
		{
			Word("왼쪽끝", DialogueStageSlot.FarLeft),
			Word("farleft", DialogueStageSlot.FarLeft),
			Word("왼쪽", DialogueStageSlot.Left),
			Word("left", DialogueStageSlot.Left),
			Word("가운데", DialogueStageSlot.Center),
			Word("center", DialogueStageSlot.Center),
			Word("오른쪽", DialogueStageSlot.Right),
			Word("right", DialogueStageSlot.Right),
			Word("오른쪽끝", DialogueStageSlot.FarRight),
			Word("farright", DialogueStageSlot.FarRight),
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStageEntrance>> Entrances = new[]
		{
			Word("슬라이드", DialogueStageEntrance.Slide),
			Word("slide", DialogueStageEntrance.Slide),
			Word("페이드", DialogueStageEntrance.Fade),
			Word("fade", DialogueStageEntrance.Fade),
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStageEmote>> Emotes = new[]
		{
			Word("놀람", DialogueStageEmote.Surprise),
			Word("물음", DialogueStageEmote.Question),
			Word("음표", DialogueStageEmote.Note),
			Word("하트", DialogueStageEmote.Heart),
			Word("땀", DialogueStageEmote.Sweat),
			Word("말줄임", DialogueStageEmote.Silence),
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStageShot>> Shots = new[]
		{
			Word("와이드", DialogueStageShot.Wide),
			Word("wide", DialogueStageShot.Wide),
			Word("미디엄", DialogueStageShot.Medium),
			Word("medium", DialogueStageShot.Medium),
			Word("클로즈", DialogueStageShot.Close),
			Word("close", DialogueStageShot.Close),
		};

		public static readonly IReadOnlyList<KeyValuePair<string, DialogueStageMotion>> Motions = new[]
		{
			Word(BOB, DialogueStageMotion.Bob),
			Word(NOD, DialogueStageMotion.Nod),
			Word(HOP, DialogueStageMotion.Hop),
			Word(SHAKE, DialogueStageMotion.Shake),
		};

		public static bool TryRead<T>(IReadOnlyList<KeyValuePair<string, T>> table, string word, out T value)
		{
			for (int i = 0; i < table.Count; i++)
			{
				if (string.Equals(table[i].Key, word, StringComparison.OrdinalIgnoreCase))
				{
					value = table[i].Value;
					return true;
				}
			}
			value = default;
			return false;
		}

		public static string WordOf<T>(IReadOnlyList<KeyValuePair<string, T>> table, T value)
		{
			for (int i = 0; i < table.Count; i++)
			{
				if (EqualityComparer<T>.Default.Equals(table[i].Value, value))
				{
					return table[i].Key;
				}
			}
			throw new ArgumentOutOfRangeException(nameof(value), value, "No script word for this stage value");
		}

		public static string Join<T>(IReadOnlyList<KeyValuePair<string, T>> table)
		{
			List<string> words = new();
			for (int i = 0; i < table.Count; i++)
			{
				words.Add(table[i].Key);
			}
			return string.Join(", ", words);
		}

		private static KeyValuePair<string, T> Word<T>(string word, T value) => new(word, value);
	}
}
