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

		public string ActorName { get; }
		public bool IsOnStage { get; set; }
		public bool IsVisibleNow => currentOpacity > VISIBLE_EPSILON || targetOpacity > VISIBLE_EPSILON;

		public DialogueStageActorElement(string actorName, DialogueStageSettings.CastEntry cast, Texture2D glowTexture)
		{
			ActorName = actorName;
			this.cast = cast;
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

		public void PlayEmote(string glyph, DialogueStageSettings settings)
		{
			emote.text = glyph;
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
			TickEmote(deltaTime);

			style.left = Length.Percent(currentX * 100f);
			style.opacity = currentOpacity;
			style.display = currentOpacity <= VISIBLE_EPSILON && targetOpacity <= VISIBLE_EPSILON ? DisplayStyle.None : DisplayStyle.Flex;
			style.translate = new Translate(currentSlide + motion.X + stageShake.x, currentOffsetY + motion.Y + stageShake.y);
			style.scale = new Scale(Vector2.one * (currentScale * motion.Scale));
			art.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, settings.ListenerTint, currentDim);
			glow.style.opacity = Mathf.Clamp01(currentGlow + lightBoost);
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
