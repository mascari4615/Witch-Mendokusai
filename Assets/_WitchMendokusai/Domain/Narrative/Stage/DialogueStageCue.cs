using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 대사 한 줄의 표시 방식
	public enum DialogueStagePresentation
	{
		World = 0,
		Novel = 1,
		Feature = 2,
		Field = 3,
	}

	public enum DialogueStageSlot
	{
		FarLeft = 0,
		Left = 1,
		Center = 2,
		Right = 3,
		FarRight = 4,
	}

	public enum DialogueStageEntrance
	{
		Slide = 0,
		Fade = 1,
	}

	public enum DialogueStageMotion
	{
		Bob = 0,
		Nod = 1,
		Hop = 2,
		Shake = 3,
	}

	public enum DialogueStageEmote
	{
		Surprise = 0,
		Question = 1,
		Note = 2,
		Heart = 3,
		Sweat = 4,
		Silence = 5,
	}

	public enum DialogueStageShot
	{
		Wide = 0,
		Medium = 1,
		Close = 2,
	}

	// 대사가 나오는 자리. 2D 입상, 별도 3D 무대, 플레이하던 월드
	public enum DialogueStageSpace
	{
		Flat = 0,
		Stage = 1,
		World = 2,
	}

	public enum DialogueStageCueKind
	{
		Presentation = 0,
		Enter = 1,
		Exit = 2,
		Move = 3,
		Highlight = 4,
		Motion = 5,
		Emote = 6,
		Shot = 7,
		ShotEnd = 8,
		Effect = 9,
		Cinema = 10,
		Scene = 11,
		SceneEnd = 12,
		StageSpace = 13,
		Gesture = 14,
	}

	// 무대 신호 한 줄. 보이는 것만 바꿈, 게임 상태 효과 `!`와 분리
	[Serializable]
	public sealed class DialogueStageCue
	{
		public const string ALL_ACTORS = "모두";

		[field: SerializeField] public DialogueStageCueKind Kind { get; private set; }
		[field: SerializeField] public string Actor { get; private set; }
		[field: SerializeField] public DialogueStagePresentation Presentation { get; private set; }
		[field: SerializeField] public DialogueStageSlot Slot { get; private set; }
		[field: SerializeField] public DialogueStageEntrance Entrance { get; private set; }
		[field: SerializeField] public DialogueStageMotion Motion { get; private set; }
		[field: SerializeField] public DialogueStageEmote Emote { get; private set; }
		[field: SerializeField] public DialogueStageShot Shot { get; private set; }
		[field: SerializeField] public string EffectName { get; private set; }
		[field: SerializeField] public bool IsOn { get; private set; }
		[field: SerializeField] public string SceneKey { get; private set; }
		[field: SerializeField] public string GestureName { get; private set; }
		[field: SerializeField] public DialogueStageSpace Space { get; private set; }

		public bool TargetsAllActors => string.Equals(Actor, ALL_ACTORS, StringComparison.Ordinal);

		public static DialogueStageCue ForPresentation(DialogueStagePresentation presentation) =>
			new() { Kind = DialogueStageCueKind.Presentation, Presentation = presentation };

		public static DialogueStageCue Enter(string actor, DialogueStageSlot slot, DialogueStageEntrance entrance) =>
			new() { Kind = DialogueStageCueKind.Enter, Actor = actor, Slot = slot, Entrance = entrance };

		public static DialogueStageCue Exit(string actor, DialogueStageEntrance entrance) =>
			new() { Kind = DialogueStageCueKind.Exit, Actor = actor, Entrance = entrance };

		public static DialogueStageCue Move(string actor, DialogueStageSlot slot) =>
			new() { Kind = DialogueStageCueKind.Move, Actor = actor, Slot = slot };

		public static DialogueStageCue Highlight(string actor) =>
			new() { Kind = DialogueStageCueKind.Highlight, Actor = actor, IsOn = string.IsNullOrEmpty(actor) == false };

		public static DialogueStageCue ForMotion(string actor, DialogueStageMotion motion) =>
			new() { Kind = DialogueStageCueKind.Motion, Actor = actor, Motion = motion };

		public static DialogueStageCue ForEmote(string actor, DialogueStageEmote emote) =>
			new() { Kind = DialogueStageCueKind.Emote, Actor = actor, Emote = emote };

		public static DialogueStageCue ForShot(string actor, DialogueStageShot shot) =>
			new() { Kind = DialogueStageCueKind.Shot, Actor = actor, Shot = shot };

		public static DialogueStageCue ShotEnd() =>
			new() { Kind = DialogueStageCueKind.ShotEnd };

		public static DialogueStageCue Effect(string effectName, string actor) =>
			new() { Kind = DialogueStageCueKind.Effect, EffectName = effectName, Actor = actor };

		public static DialogueStageCue Cinema(bool isOn) =>
			new() { Kind = DialogueStageCueKind.Cinema, IsOn = isOn };

		// 3D 연출 장면 켜기. 키는 장면 편집기에서 내보낸 연출 이름
		public static DialogueStageCue Scene(string sceneKey) =>
			new() { Kind = DialogueStageCueKind.Scene, SceneKey = sceneKey };

		public static DialogueStageCue SceneEnd() =>
			new() { Kind = DialogueStageCueKind.SceneEnd };

		// 무대 바꾸기. 3D 무대와 월드에서는 등장, 이동, 화자, 몸짓, 샷이 3D 인물과 카메라에 적용
		public static DialogueStageCue ForSpace(DialogueStageSpace space) =>
			new() { Kind = DialogueStageCueKind.StageSpace, Space = space };

		// 이름 붙은 한 번짜리 동작. 낱말과 클립의 짝은 3D 무대 설정 자산
		public static DialogueStageCue Gesture(string actor, string gestureName) =>
			new() { Kind = DialogueStageCueKind.Gesture, Actor = actor, GestureName = gestureName };
	}
}
