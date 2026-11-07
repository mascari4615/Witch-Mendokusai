using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	public enum DialogueStageEffectKind
	{
		Light = 0,
		Sparkle = 1,
		Flash = 2,
		ScreenShake = 3,
	}

	// 대화 연출의 수치와 인물 표시 데이터 한 곳. 색과 크기의 나머지는 DialogueStage.uss
	[CreateAssetMenu(fileName = nameof(DialogueStageSettings), menuName = "WM/Narrative/DialogueStageSettings")]
	public class DialogueStageSettings : ScriptableObject
	{
		public const string RESOURCE_NAME = "DialogueStageSettings_0_Main";

		[Serializable]
		public class CastEntry
		{
			[field: SerializeField] public string Name { get; private set; }
			[field: SerializeField] public Sprite Bust { get; private set; }
			[field: SerializeField, Tooltip("이름 띠 색 구분. 켜면 플레이어 쪽 인물")] public bool IsPlayable { get; private set; }
			[field: SerializeField, Tooltip("켜면 월드 대상이 플레이어")] public bool IsPlayerInWorld { get; private set; }
			[field: SerializeField] public float Scale { get; private set; } = 1f;
			[field: SerializeField, Tooltip("하단 정렬 뒤 추가 위치(패널 픽셀, 아래가 양수)")] public float OffsetY { get; private set; }
			[field: SerializeField] public Color GlowColor { get; private set; } = Color.white;
		}

		[Serializable]
		public class MotionTrack
		{
			[field: SerializeField] public DialogueStageMotion Motion { get; private set; }
			[field: SerializeField] public float Seconds { get; private set; } = 0.3f;
			[field: SerializeField] public List<DialogueMotionKey> Keys { get; private set; } = new();
		}

		[Serializable]
		public class EmoteEntry
		{
			[field: SerializeField] public DialogueStageEmote Emote { get; private set; }
			[field: SerializeField] public string Glyph { get; private set; }
			[field: SerializeField] public Color ParticleColor { get; private set; } = Color.white;
			[field: SerializeField] public int ParticleCount { get; private set; } = 8;
			[field: SerializeField, Tooltip("일러에 얹는 감정 색 세기. 색은 입자 색")] public float TintStrength { get; private set; } = 0.25f;
		}

		[Serializable]
		public class PresentationEntry
		{
			[field: SerializeField] public DialogueStagePresentation Presentation { get; private set; }
			[field: SerializeField] public bool PausesTime { get; private set; }
			[field: SerializeField] public bool BlursBackground { get; private set; }
		}

		[Serializable]
		public class EffectEntry
		{
			[field: SerializeField] public string Name { get; private set; }
			[field: SerializeField] public DialogueStageEffectKind Kind { get; private set; }
			[field: SerializeField] public Color Color { get; private set; } = Color.white;
			[field: SerializeField] public int ParticleCount { get; private set; } = 12;
			[field: SerializeField] public float Seconds { get; private set; } = 0.4f;
			[field: SerializeField] public float Strength { get; private set; } = 1f;
		}

		[Serializable]
		public class ShotEntry
		{
			[field: SerializeField] public DialogueStageShot Shot { get; private set; }
			[field: SerializeField] public float Distance { get; private set; } = 6f;
		}

		[field: Header("화면")]
		[field: SerializeField] public VisualTreeAsset Layout { get; private set; }
		[field: SerializeField] public StyleSheet Style { get; private set; }

		[field: Header("표시 방식별 시간 정지와 배경 흐림")]
		[field: SerializeField] public List<PresentationEntry> Presentations { get; private set; } = new();
		[field: SerializeField] public RenderTexture BlurOutput { get; private set; }

		[field: Header("일러 셰이더")]
		[field: SerializeField] public Shader ArtFilterShader { get; private set; }
		[field: SerializeField] public float SweepWidth { get; private set; } = 0.14f;
		[field: SerializeField] public float SweepAngle { get; private set; } = 0.35f;
		[field: SerializeField] public float SweepStrength { get; private set; } = 0.6f;
		[field: SerializeField] public float RimRadiusPixels { get; private set; } = 2.5f;
		[field: SerializeField] public float SpeakerRimStrength { get; private set; } = 0.35f;
		[field: SerializeField] public float BreathSeconds { get; private set; } = 3.2f;
		[field: SerializeField] public float BreathBrightness { get; private set; } = 0.05f;
		[field: SerializeField] public float BreathScale { get; private set; } = 0.004f;
		[field: SerializeField] public float ListenerDesaturate { get; private set; } = 0.25f;

		[field: Header("인물")]
		[field: SerializeField] public List<CastEntry> Cast { get; private set; } = new();

		[field: SerializeField, Tooltip("대본에서 플레이어를 가리키는 이름")] public string PlayerActorName { get; private set; } = "플레이어";

		[field: Header("자리, 가로 비율 0~1. FarLeft Left Center Right FarRight 순")]
		[field: SerializeField] public List<float> SlotPositions { get; private set; } = new() { 0.1667f, 0.25f, 0.5f, 0.75f, 0.8333f };

		[field: SerializeField] public float FieldPosition { get; private set; } = 0.172f;
		[field: SerializeField] public float FieldScale { get; private set; } = 0.8f;
		[field: SerializeField] public float FieldOffsetY { get; private set; } = 15f;
		[field: SerializeField] public float FeatureScale { get; private set; } = 1f;

		[field: Header("인물 움직임")]
		[field: SerializeField] public float MoveSmoothSeconds { get; private set; } = 0.09f;
		[field: SerializeField] public float FadeSmoothSeconds { get; private set; } = 0.07f;
		[field: SerializeField] public float EmphasisSmoothSeconds { get; private set; } = 0.06f;
		[field: SerializeField] public float EnterSlideDistance { get; private set; } = 90f;
		[field: SerializeField] public float SpeakerScale { get; private set; } = 1f;
		[field: SerializeField] public Color ListenerTint { get; private set; } = new(0.5f, 0.5f, 0.52f, 1f);
		[field: SerializeField] public float SpeakerGlowOpacity { get; private set; } = 0.22f;
		[field: SerializeField] public List<MotionTrack> Motions { get; private set; } = new();

		[field: Header("감정 아이콘")]
		[field: SerializeField] public float EmoteSeconds { get; private set; } = 0.9f;
		[field: SerializeField] public float EmoteStartScale { get; private set; } = 0.3f;
		[field: SerializeField] public float EmoteOvershootScale { get; private set; } = 1.1f;
		[field: SerializeField] public List<EmoteEntry> Emotes { get; private set; } = new();

		[field: Header("글자와 진행")]
		[field: SerializeField] public float CharactersPerSecond { get; private set; } = 26f;
		[field: SerializeField] public float AutoDelaySeconds { get; private set; } = 1f;
		[field: SerializeField] public float AutoSecondsPerCharacter { get; private set; } = 0.03f;
		[field: SerializeField] public float NextMarkBlinkSeconds { get; private set; } = 0.4f;
		[field: SerializeField] public float PresentationSwitchSeconds { get; private set; } = 0.16f;
		[field: SerializeField] public float CloseSeconds { get; private set; } = 0.2f;
		[field: SerializeField] public float ChoiceRowDelaySeconds { get; private set; } = 0.05f;
		[field: SerializeField] public float ChoiceAppearDelaySeconds { get; private set; } = 0.016f;
		[field: SerializeField] public float ChoiceFrameSettleSeconds { get; private set; } = 0.16f;

		[field: Header("이펙트")]
		[field: SerializeField, Tooltip("등장 때 자동으로 쓰는 이펙트 이름. 비우면 없음")] public string EnterEffectName { get; private set; } = DialogueStageVocabulary.EFFECT_LIGHT;
		[field: SerializeField] public List<EffectEntry> Effects { get; private set; } = new();
		[field: SerializeField] public float ScreenShakeFrequency { get; private set; } = 40f;
		[field: SerializeField] public int ParticleCapacity { get; private set; } = 64;
		[field: SerializeField] public float ParticleLifeSeconds { get; private set; } = 0.7f;
		[field: SerializeField] public float ParticleSpeed { get; private set; } = 190f;
		[field: SerializeField] public float ParticleGravity { get; private set; } = 260f;
		[field: SerializeField] public int GlowTextureSize { get; private set; } = 64;
		[field: SerializeField] public int FadeTextureHeight { get; private set; } = 64;

		[field: Header("카메라")]
		[field: SerializeField] public List<ShotEntry> Shots { get; private set; } = new();
		[field: SerializeField] public float CameraBlendSeconds { get; private set; } = 0.6f;
		[field: SerializeField] public float CameraDistanceSmoothSeconds { get; private set; } = 0.25f;

		[field: Header("시험")]
		[field: SerializeField] public DialogueScriptSource SampleScript { get; private set; }

		public CastEntry FindCast(string actorName)
		{
			foreach (CastEntry entry in Cast)
			{
				if (string.Equals(entry.Name, actorName, StringComparison.Ordinal))
					return entry;
			}
			return null;
		}

		public PresentationEntry FindPresentation(DialogueStagePresentation presentation)
		{
			foreach (PresentationEntry entry in Presentations)
			{
				if (entry.Presentation == presentation)
					return entry;
			}
			throw new InvalidOperationException($"{nameof(DialogueStageSettings)}: no presentation entry for {presentation}");
		}

		public MotionTrack FindMotion(DialogueStageMotion motion)
		{
			foreach (MotionTrack track in Motions)
			{
				if (track.Motion == motion)
					return track;
			}
			throw new InvalidOperationException($"{nameof(DialogueStageSettings)}: no motion track for {motion}");
		}

		public EmoteEntry FindEmote(DialogueStageEmote emote)
		{
			foreach (EmoteEntry entry in Emotes)
			{
				if (entry.Emote == emote)
					return entry;
			}
			throw new InvalidOperationException($"{nameof(DialogueStageSettings)}: no emote entry for {emote}");
		}

		public EffectEntry FindEffect(string effectName)
		{
			foreach (EffectEntry entry in Effects)
			{
				if (string.Equals(entry.Name, effectName, StringComparison.Ordinal))
					return entry;
			}
			throw new InvalidOperationException($"{nameof(DialogueStageSettings)}: no effect entry for {effectName}");
		}

		public float DistanceFor(DialogueStageShot shot)
		{
			foreach (ShotEntry entry in Shots)
			{
				if (entry.Shot == shot)
					return entry.Distance;
			}
			throw new InvalidOperationException($"{nameof(DialogueStageSettings)}: no distance for shot {shot}");
		}

		public float SlotPosition(DialogueStageSlot slot) => SlotPositions[(int)slot];

		public static DialogueStageSettings Load()
		{
			DialogueStageSettings settings = Resources.Load<DialogueStageSettings>(RESOURCE_NAME);
			if (settings == null)
				throw new InvalidOperationException($"{RESOURCE_NAME} not found in Resources");
			return settings;
		}
	}
}
