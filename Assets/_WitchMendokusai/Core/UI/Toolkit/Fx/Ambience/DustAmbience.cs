using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>비스듬한 빛줄기와 그 안을 떠도는 먼지 (숲, 창가)</summary>
	[CreateAssetMenu(fileName = "Ambience_0_Dust", menuName = "WM/UI/Ambience/Dust Shaft")]
	public sealed class DustAmbience : UIAmbience
	{
		public Color shaftColor = new(1f, 0.94f, 0.75f, 0.3f);
		[Tooltip("빛줄기 가운데 가로 위치와 너비, 화면 비율")]
		[Range(0f, 1f)] public float shaftCenter = 0.45f;
		[Range(0.05f, 1f)] public float shaftWidth = 0.3f;
		public float shaftAngle = 20f;
		[Tooltip("빛줄기 밝기 숨쉬기 시간과 깊이")]
		public float breatheSeconds = 5f;
		[Range(0f, 1f)] public float breatheDepth = 0.3f;
		public Color dustColor = new(1f, 0.96f, 0.85f, 1f);
		public int dustCount = 60;
		public Vector2 dustSize = new(5f, 12f);
		public Vector2 dustLife = new(4f, 8f);
		[Tooltip("떠도는 속도, 픽셀/초")]
		public float dustSpeed = 6f;

		public override void Begin(VisualElement layer)
		{
			VisualElement shaft = new() { pickingMode = PickingMode.Ignore };
			shaft.style.position = Position.Absolute;
			shaft.style.top = Length.Percent(-30f);
			shaft.style.bottom = Length.Percent(-30f);
			shaft.style.left = Length.Percent((shaftCenter - shaftWidth * 0.5f) * 100f);
			shaft.style.width = Length.Percent(shaftWidth * 100f);
			shaft.style.backgroundImage = new StyleBackground(UIFxTextures.Band);
			shaft.style.unityBackgroundImageTintColor = shaftColor;
			shaft.style.rotate = UIFxTween.Degrees(shaftAngle);
			layer.Add(shaft);

			List<Particle> dust = new();
			float elapsed = 0f;
			Tick(layer, (deltaSeconds, size) =>
			{
				elapsed += deltaSeconds;
				shaft.style.opacity = 1f - breatheDepth * (0.5f + 0.5f * Mathf.Sin(elapsed / UIFxTween.Duration(breatheSeconds) * Mathf.PI * 2f));
				while (dust.Count < dustCount)
				{
					Particle particle = Spawn(layer, dust, UIFxTextures.Glow, dustColor);
					float y = UnityEngine.Random.value * size.y;
					// 기울어진 빛줄기 안쪽에 놓음
					float slant = Mathf.Tan(shaftAngle * Mathf.Deg2Rad) * (y - size.y * 0.5f);
					particle.Position = new Vector2(size.x * (shaftCenter + (UnityEngine.Random.value - 0.5f) * shaftWidth) - slant, y);
					particle.Velocity = UnityEngine.Random.insideUnitCircle * dustSpeed;
					particle.Size = Range(dustSize);
					particle.Life = Range(dustLife);
				}
				foreach (Particle particle in dust)
				{
					particle.Age += deltaSeconds;
					particle.Position += particle.Velocity * deltaSeconds;
					Place(particle, Mathf.Sin(Mathf.Clamp01(particle.Age / particle.Life) * Mathf.PI));
				}
				Sweep(dust);
			});
		}
	}
}
