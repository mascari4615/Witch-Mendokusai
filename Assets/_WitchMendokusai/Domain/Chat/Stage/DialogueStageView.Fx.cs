using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 빛, 입자, 플래시, 화면 흔들림. 효과 끄기 설정이면 전부 생략
	public partial class DialogueStageView
	{
		private const string USS_PARTICLE = "wm-dlg-particle";

		private sealed class Particle
		{
			public VisualElement Element;
			public Vector2 Position;
			public Vector2 Velocity;
			public float Life;
		}

		private readonly List<Particle> activeParticles = new();
		private readonly Stack<Particle> particlePool = new();
		private Texture2D glowTexture;
		private Texture2D fadeTexture;

		private float flashElapsed = -1f;
		private float flashSeconds;
		private float flashStrength;
		private float shakeElapsed = -1f;
		private float shakeSeconds;
		private float shakeStrength;

		private void BuildFx()
		{
			flash.style.opacity = 0f;
		}

		private void PlayEffect(string effectName, string actorName)
		{
			DialogueStageSettings.EffectEntry entry = settings.FindEffect(effectName);
			actors.TryGetValue(actorName ?? speaker ?? string.Empty, out DialogueStageActorElement actor);

			switch (entry.Kind)
			{
				case DialogueStageEffectKind.Light:
					actor?.PlayLight(entry.Seconds, entry.Strength);
					break;
				case DialogueStageEffectKind.Sparkle:
					if (actor != null)
						EmitParticles(actor, entry.ParticleCount, entry.Color);
					break;
				case DialogueStageEffectKind.Flash:
					flashElapsed = 0f;
					flashSeconds = entry.Seconds;
					flashStrength = entry.Strength;
					flash.style.backgroundColor = entry.Color;
					break;
				case DialogueStageEffectKind.ScreenShake:
					shakeElapsed = 0f;
					shakeSeconds = entry.Seconds;
					shakeStrength = entry.Strength;
					break;
			}
		}

		private void EmitParticles(DialogueStageActorElement actor, int count, Color color)
		{
			if (UIEffects.Enabled == false)
				return;

			Vector2 origin = fxLayer.WorldToLocal(actor.HeadPosition());
			for (int i = 0; i < count && activeParticles.Count < settings.ParticleCapacity; i++)
			{
				Particle particle = particlePool.Count > 0 ? particlePool.Pop() : CreateParticle();
				float angle = Random.Range(0f, Mathf.PI * 2f);
				float speed = settings.ParticleSpeed * Random.Range(0.4f, 1f);
				particle.Position = origin;
				particle.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - 0.5f) * speed;
				particle.Life = 0f;
				particle.Element.style.backgroundColor = color;
				particle.Element.style.display = DisplayStyle.Flex;
				activeParticles.Add(particle);
			}
		}

		private Particle CreateParticle()
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.AddToClassList(USS_PARTICLE);
			fxLayer.Add(element);
			return new Particle { Element = element };
		}

		private void TickFx(float deltaTime)
		{
			for (int i = activeParticles.Count - 1; i >= 0; i--)
			{
				Particle particle = activeParticles[i];
				particle.Life += deltaTime;
				if (particle.Life >= settings.ParticleLifeSeconds)
				{
					particle.Element.style.display = DisplayStyle.None;
					activeParticles.RemoveAt(i);
					particlePool.Push(particle);
					continue;
				}
				particle.Velocity += Vector2.up * (settings.ParticleGravity * deltaTime);
				particle.Position += particle.Velocity * deltaTime;
				particle.Element.style.translate = new Translate(particle.Position.x, particle.Position.y);
				particle.Element.style.opacity = 1f - particle.Life / settings.ParticleLifeSeconds;
			}

			if (flashElapsed < 0f)
				return;
			flashElapsed += deltaTime;
			float normalized = flashSeconds <= 0f ? 1f : flashElapsed / flashSeconds;
			if (normalized >= 1f)
			{
				flashElapsed = -1f;
				flash.style.opacity = 0f;
				return;
			}
			flash.style.opacity = flashStrength * (1f - normalized);
		}

		private Vector2 SampleScreenShake(float deltaTime)
		{
			if (shakeElapsed < 0f)
				return Vector2.zero;

			shakeElapsed += deltaTime;
			float normalized = shakeSeconds <= 0f ? 1f : shakeElapsed / shakeSeconds;
			if (normalized >= 1f)
			{
				shakeElapsed = -1f;
				return Vector2.zero;
			}
			float amount = shakeStrength * (1f - normalized);
			return new Vector2(
				Mathf.Sin(shakeElapsed * settings.ScreenShakeFrequency) * amount,
				Mathf.Cos(shakeElapsed * settings.ScreenShakeFrequency * 0.7f) * amount * 0.5f);
		}

		private void ClearParticles()
		{
			foreach (Particle particle in activeParticles)
			{
				particle.Element.style.display = DisplayStyle.None;
				particlePool.Push(particle);
			}
			activeParticles.Clear();
			flashElapsed = -1f;
			shakeElapsed = -1f;
			flash.style.opacity = 0f;
		}

		// 등장 빛과 화자 뒤 빛에 쓰는 원형 감쇠 그림. 코드 생성이라 자산 없음
		private Texture2D GetGlowTexture()
		{
			if (glowTexture != null)
				return glowTexture;

			int size = settings.GlowTextureSize;
			glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
			Color32[] pixels = new Color32[size * size];
			float center = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
					float alpha = Mathf.Clamp01(1f - distance);
					pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255f));
				}
			}
			glowTexture.SetPixels32(pixels);
			glowTexture.Apply(false, true);
			return glowTexture;
		}

		// 자막 방식 창 뒤 아래에서 위로 옅어지는 그림
		private Texture2D GetFadeTexture()
		{
			if (fadeTexture != null)
				return fadeTexture;

			int height = settings.FadeTextureHeight;
			fadeTexture = new Texture2D(1, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
			Color32[] pixels = new Color32[height];
			for (int y = 0; y < height; y++)
			{
				float normalized = y / (float)(height - 1);
				float alpha = Mathf.Clamp01((1f - normalized) / 0.55f);
				pixels[y] = new Color32(5, 5, 9, (byte)(alpha * 235f));
			}
			fadeTexture.SetPixels32(pixels);
			fadeTexture.Apply(false, true);
			return fadeTexture;
		}

		private void ReleaseTextures()
		{
			if (glowTexture != null)
				Destroy(glowTexture);
			if (fadeTexture != null)
				Destroy(fadeTexture);
		}
	}
}
