using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>한 자리에서 피어올라 퍼지며 사라지는 김이나 연기 (연구소 솥, 굴뚝)</summary>
	[CreateAssetMenu(fileName = "Ambience_0_Steam", menuName = "WM/UI/Ambience/Steam")]
	public sealed class SteamAmbience : UIAmbience
	{
		[Tooltip("시작 자리, 화면 비율 (왼쪽 위 0)")]
		public Vector2 origin = new(0.12f, 0.92f);
		public Color color = new(0.9f, 0.88f, 0.95f, 0.32f);
		public float perSecond = 10f;
		public Vector2 startSize = new(36f, 60f);
		[Tooltip("초당 커지는 크기, 픽셀")]
		public float growth = 60f;
		[Tooltip("오르는 속도와 옆으로 흐르는 속도, 픽셀/초")]
		public Vector2 riseSpeed = new(28f, 44f);
		public Vector2 driftSpeed = new(6f, 16f);
		public Vector2 life = new(2.2f, 3.4f);
		[Tooltip("피어나는 구간 비율 (나머지는 사라지는 구간)")]
		[Range(0.01f, 1f)] public float bloomPart = 0.16f;

		public override void Begin(VisualElement layer)
		{
			List<Particle> puffs = new();
			float budget = 0f;
			Tick(layer, (deltaSeconds, area) =>
			{
				budget += deltaSeconds * perSecond;
				while (budget >= 1f)
				{
					budget -= 1f;
					Particle particle = Spawn(layer, puffs, UIFxTextures.Glow, color);
					particle.Position = new Vector2(origin.x * area.x, origin.y * area.y);
					particle.Velocity = new Vector2(Range(driftSpeed), -Range(riseSpeed));
					particle.Size = Range(startSize);
					particle.Life = Range(life);
				}
				foreach (Particle particle in puffs)
				{
					particle.Age += deltaSeconds;
					particle.Position += particle.Velocity * deltaSeconds;
					particle.Size += growth * deltaSeconds;
					float progress = Mathf.Clamp01(particle.Age / particle.Life);
					Place(particle, Mathf.Min(1f, progress / bloomPart) * (1f - progress));
				}
				Sweep(puffs);
			});
		}
	}
}
