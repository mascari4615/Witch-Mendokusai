using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>위에서 흔들리며 떨어지는 잎이나 꽃잎</summary>
	[CreateAssetMenu(fileName = "Ambience_0_Leaf", menuName = "WM/UI/Ambience/Falling Leaves")]
	public sealed class LeafAmbience : UIAmbience
	{
		public ParticleShape shape = ParticleShape.Petal;
		public Color color = new(1f, 0.78f, 0.38f, 1f);
		[Tooltip("초당 생기는 수")]
		public float perSecond = 3f;
		public Vector2 size = new(16f, 28f);
		[Tooltip("떨어지는 속도, 픽셀/초")]
		public Vector2 fallSpeed = new(25f, 45f);
		[Tooltip("좌우 흔들림 폭 (픽셀) 과 빠르기")]
		public float swayPixels = 18f;
		public float swaySpeed = 1.8f;
		[Tooltip("흔들릴 때 기우는 각도")]
		public float tiltDegrees = 40f;

		public override void Begin(VisualElement layer)
		{
			List<Particle> leaves = new();
			float budget = 0f;
			Tick(layer, (deltaSeconds, area) =>
			{
				budget += deltaSeconds * perSecond;
				while (budget >= 1f)
				{
					budget -= 1f;
					Particle particle = Spawn(layer, leaves, UIFxTextures.Shape(shape), color);
					particle.Size = Range(size);
					particle.Position = new Vector2(UnityEngine.Random.value * area.x, -particle.Size);
					particle.Velocity = new Vector2(0f, Range(fallSpeed));
					particle.Phase = UnityEngine.Random.value * Mathf.PI * 2f;
					particle.Life = (area.y + particle.Size * 2f) / particle.Velocity.y;
				}
				foreach (Particle particle in leaves)
				{
					particle.Age += deltaSeconds;
					particle.Phase += deltaSeconds * swaySpeed;
					particle.Position.y += particle.Velocity.y * deltaSeconds;
					particle.Position.x += Mathf.Cos(particle.Phase) * swayPixels * swaySpeed * deltaSeconds;
					Place(particle, 1f, Mathf.Sin(particle.Phase) * tiltDegrees);
				}
				Sweep(leaves);
			});
		}
	}
}
