using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 무대 위 인물 한 명. 목표값을 부드럽게 따라가고 움직임 곡선을 더함
	public class DialogueStageActorElement : VisualElement
	{
		private const string USS_ROOT = "wm-dlg-actor";
		private const string USS_GLOW = USS_ROOT + "__glow";
		private const string USS_ART = USS_ROOT + "__art";
		private const string USS_ART_LISTENING = USS_ART + "--listening";
		private const string USS_EMOTE = USS_ROOT + "__emote";
		private const string USS_EMOTE_ON = USS_EMOTE + "--on";

		private const float VISIBLE_EPSILON = 0.01f;
		private const float EMOTE_POP_END = 0.18f;
		private const float EMOTE_SETTLE_END = 0.3f;
		private const float EMOTE_FADE_START = 0.85f;

		private readonly VisualElement glow = new() { pickingMode = PickingMode.Ignore };
		private readonly VisualElement art = new() { pickingMode = PickingMode.Ignore };
		private readonly Label emote = new() { pickingMode = PickingMode.Ignore };
		private readonly DialogueStageSettings.CastEntry cast;
		private readonly FilterFunctionDefinition artFilter;

		private float currentX;
		private float currentOpacity;
		private float currentScale = 1f;
		private float currentDim;
		private float currentSlide;
		private float currentOffsetY;
		private float currentGlow;

		private float targetX;
		private float targetOpacity;
		private float targetScale = 1f;
		private float targetDim;
		private float targetSlide;
		private float targetOffsetY;
		private float targetGlow;

		private DialogueStageSettings.MotionTrack motionTrack;
		private float motionElapsed;
		private float emoteElapsed = -1f;
		private float emoteSeconds;
		private float emoteStartScale;
		private float emoteOvershootScale;
		private float lightElapsed = -1f;
		private float lightSeconds;
		private float lightStrength;
		private bool isListening;
		private bool hasInlineFilter;
		private Color tintColor;
		private float tintStrength;

		public string ActorName { get; }
		public bool IsOnStage { get; set; }
		public bool IsVisibleNow => currentOpacity > VISIBLE_EPSILON || targetOpacity > VISIBLE_EPSILON;

		public DialogueStageActorElement(string actorName, DialogueStageSettings.CastEntry cast, Texture2D glowTexture, FilterFunctionDefinition artFilter)
		{
			ActorName = actorName;
			this.cast = cast;
			this.artFilter = artFilter;
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_ROOT);

			glow.AddToClassList(USS_GLOW);
			glow.style.backgroundImage = new StyleBackground(glowTexture);
			glow.style.unityBackgroundImageTintColor = cast.GlowColor;
			Add(glow);

			art.AddToClassList(USS_ART);
			art.style.backgroundImage = new StyleBackground(cast.Bust);
			Add(art);

			emote.AddToClassList(USS_EMOTE);
			Add(emote);

			style.opacity = 0f;
		}

		public void SnapTo(float x)
		{
			currentX = x;
			targetX = x;
			style.left = Length.Percent(x * 100f);
		}

		public void SetTargetX(float x) => targetX = x;

		public void SetTargets(float opacity, float scale, float dim, float glowOpacity, float offsetY)
		{
			targetOpacity = opacity;
			targetScale = scale * cast.Scale;
			targetDim = dim;
			targetGlow = glowOpacity;
			targetOffsetY = offsetY + cast.OffsetY;
			SetListening(dim > 0.5f);
		}

		public void BeginSlide(float fromOffset)
		{
			currentSlide = fromOffset;
			targetSlide = 0f;
		}

		public void EndSlide(float toOffset) => targetSlide = toOffset;

		public void PlayMotion(DialogueStageSettings.MotionTrack track)
		{
			motionTrack = track;
			motionElapsed = 0f;
		}

		public void PlayEmote(DialogueStageSettings.EmoteEntry entry, DialogueStageSettings settings)
		{
			emote.text = entry.Glyph;
			tintColor = entry.ParticleColor;
			tintStrength = entry.TintStrength;
			emoteElapsed = 0f;
			emoteSeconds = settings.EmoteSeconds;
			emoteStartScale = settings.EmoteStartScale;
			emoteOvershootScale = settings.EmoteOvershootScale;
			emote.AddToClassList(USS_EMOTE_ON);
		}

		public void PlayLight(float seconds, float strength)
		{
			lightElapsed = 0f;
			lightSeconds = seconds;
			lightStrength = strength;
		}

		public void Snap()
		{
			currentX = targetX;
			currentOpacity = targetOpacity;
			currentScale = targetScale;
			currentDim = targetDim;
			currentSlide = targetSlide;
			currentOffsetY = targetOffsetY;
			currentGlow = targetGlow;
			motionTrack = null;
			lightElapsed = -1f;
			StopEmote();
		}

		public Vector2 HeadPosition()
		{
			Rect bound = worldBound;
			return new Vector2(bound.center.x, bound.yMin + bound.height * 0.25f);
		}

		public void Tick(float deltaTime, DialogueStageSettings settings, Vector2 stageShake)
		{
			currentX = Approach(currentX, targetX, deltaTime, settings.MoveSmoothSeconds);
			currentScale = Approach(currentScale, targetScale, deltaTime, settings.MoveSmoothSeconds);
			currentSlide = Approach(currentSlide, targetSlide, deltaTime, settings.MoveSmoothSeconds);
			currentOffsetY = Approach(currentOffsetY, targetOffsetY, deltaTime, settings.MoveSmoothSeconds);
			currentOpacity = Approach(currentOpacity, targetOpacity, deltaTime, settings.FadeSmoothSeconds);
			currentDim = Approach(currentDim, targetDim, deltaTime, settings.EmphasisSmoothSeconds);
			currentGlow = Approach(currentGlow, targetGlow, deltaTime, settings.EmphasisSmoothSeconds);

			DialogueMotionKey motion = SampleMotion(deltaTime);
			float lightBoost = SampleLight(deltaTime);
			float tint = SampleTint();
			TickEmote(deltaTime);
			float speaking = settings.SpeakerGlowOpacity <= 0f ? 0f : Mathf.Clamp01(currentGlow / settings.SpeakerGlowOpacity);
			float breath = speaking * BreathWave(settings);

			style.left = Length.Percent(currentX * 100f);
			style.opacity = currentOpacity;
			style.display = currentOpacity <= VISIBLE_EPSILON && targetOpacity <= VISIBLE_EPSILON ? DisplayStyle.None : DisplayStyle.Flex;
			style.translate = new Translate(currentSlide + motion.X + stageShake.x, currentOffsetY + motion.Y + stageShake.y);
			style.scale = new Scale(Vector2.one * (currentScale * motion.Scale * (1f + breath * settings.BreathScale)));
			art.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, settings.ListenerTint, currentDim);
			glow.style.opacity = Mathf.Clamp01(currentGlow + lightBoost);
			ApplyArtFilter(settings, speaking, breath, lightBoost, tint);
		}

		private static float BreathWave(DialogueStageSettings settings)
		{
			if (settings.BreathSeconds <= 0f)
				return 0f;
			return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / settings.BreathSeconds);
		}

		// 효과 끄기면 USS 기본값으로 복귀
		private void ApplyArtFilter(DialogueStageSettings settings, float speaking, float breath, float lightBoost, float tint)
		{
			if (UIEffects.Enabled == false || style.display == DisplayStyle.None)
			{
				if (hasInlineFilter)
				{
					art.style.filter = StyleKeyword.Null;
					hasInlineFilter = false;
				}
				return;
			}

			float sweepPhase = lightElapsed >= 0f && lightSeconds > 0f ? Mathf.Clamp01(lightElapsed / lightSeconds) : -1f;
			float rim = Mathf.Clamp01(speaking * settings.SpeakerRimStrength + lightBoost);
			float light = breath * settings.BreathBrightness - currentDim * settings.ListenerDesaturate;
			Color effectColor = tint > 0f ? tintColor : cast.GlowColor;
			effectColor.a = tint;

			FilterFunction filter = new(artFilter);
			filter.AddParameter(new FilterParameter(sweepPhase));
			filter.AddParameter(new FilterParameter(rim));
			filter.AddParameter(new FilterParameter(light));
			filter.AddParameter(new FilterParameter(effectColor));
			art.style.filter = new StyleList<FilterFunction>(new List<FilterFunction> { filter });
			hasInlineFilter = true;
		}

		private float SampleTint()
		{
			if (emoteElapsed < 0f || emoteSeconds <= 0f)
				return 0f;
			return tintStrength * Mathf.Clamp01(1f - emoteElapsed / emoteSeconds);
		}

		private DialogueMotionKey SampleMotion(float deltaTime)
		{
			if (motionTrack == null)
				return new DialogueMotionKey(0f, 0f, 0f);

			motionElapsed += deltaTime;
			float normalized = motionTrack.Seconds <= 0f ? 1f : motionElapsed / motionTrack.Seconds;
			if (normalized >= 1f)
			{
				motionTrack = null;
				return new DialogueMotionKey(1f, 0f, 0f);
			}
			return DialogueMotionSampler.Sample(motionTrack.Keys, normalized);
		}

		private float SampleLight(float deltaTime)
		{
			if (lightElapsed < 0f)
				return 0f;

			lightElapsed += deltaTime;
			float normalized = lightSeconds <= 0f ? 1f : lightElapsed / lightSeconds;
			if (normalized >= 1f)
			{
				lightElapsed = -1f;
				return 0f;
			}
			return lightStrength * (1f - normalized);
		}

		private void TickEmote(float deltaTime)
		{
			if (emoteElapsed < 0f)
				return;

			emoteElapsed += deltaTime;
			float normalized = emoteSeconds <= 0f ? 1f : emoteElapsed / emoteSeconds;
			if (normalized >= 1f)
			{
				StopEmote();
				return;
			}

			float scale;
			if (normalized < EMOTE_POP_END)
				scale = Mathf.Lerp(emoteStartScale, emoteOvershootScale, normalized / EMOTE_POP_END);
			else if (normalized < EMOTE_SETTLE_END)
				scale = Mathf.Lerp(emoteOvershootScale, 1f, (normalized - EMOTE_POP_END) / (EMOTE_SETTLE_END - EMOTE_POP_END));
			else
				scale = 1f;

			emote.style.scale = new Scale(Vector2.one * scale);
			emote.style.opacity = normalized < EMOTE_FADE_START
				? Mathf.Clamp01(normalized / EMOTE_POP_END)
				: 1f - (normalized - EMOTE_FADE_START) / (1f - EMOTE_FADE_START);
		}

		private void StopEmote()
		{
			emoteElapsed = -1f;
			emote.RemoveFromClassList(USS_EMOTE_ON);
		}

		private void SetListening(bool listening)
		{
			if (isListening == listening)
				return;
			isListening = listening;
			art.EnableInClassList(USS_ART_LISTENING, listening);
		}

		private static float Approach(float current, float target, float deltaTime, float smoothSeconds)
		{
			if (smoothSeconds <= 0f)
				return target;
			return target + (current - target) * Mathf.Exp(-deltaTime / smoothSeconds);
		}
	}
}
