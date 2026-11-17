using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 장소에 맞는 공기 (먼지 빛줄기, 낙엽, 김, 등불). UIFxProfile 의 ambience 칸, 패널 맨 뒤 층에 그림
	/// - 새 종류: 상속 클래스에 Begin 하나. 입자는 Particles 도구로
	/// - 이름 규격 Ambience_{ID}_{이름}.asset
	/// </summary>
	public abstract class UIAmbience : ScriptableObject
	{
		public const string LAYER_NAME = "fx-ambience";

		/// <summary>root 맨 뒤에 층을 깔고 시작. 층을 돌려줌 (떼면 멈춤)</summary>
		public VisualElement Attach(VisualElement root)
		{
			VisualElement layer = UIFxTween.MakeLayer(LAYER_NAME);
			root.Insert(0, layer);
			Begin(layer);
			return layer;
		}

		/// <summary>층 안에서 매 프레임 그리기 시작. 층이 패널에서 떨어지면 멈출 것</summary>
		public abstract void Begin(VisualElement layer);

		/// <summary>입자 하나. 위치는 층 안 픽셀</summary>
		protected sealed class Particle
		{
			public VisualElement Element;
			public Vector2 Position;
			public Vector2 Velocity;
			public float Size;
			public float Age;
			public float Life;
			public float Phase;
		}

		/// <summary>층 크기를 받아 매 프레임 update. 층이 떨어지면 멈춤</summary>
		protected static void Tick(VisualElement layer, Action<float, Vector2> update)
		{
			layer.schedule.Execute(timer =>
			{
				Vector2 size = new(layer.layout.width, layer.layout.height);
				// 패널이 숨은 동안은 입자를 만들지도 움직이지도 않음
				if (float.IsNaN(size.x) || size.x <= 0f || UIIdleFx.IsShown(layer) == false)
					return;
				update(UIFxTween.Seconds(timer), size);
			}).Every(0).Until(() => layer.panel == null);
		}

		protected static Particle Spawn(VisualElement layer, List<Particle> particles, Texture2D texture, Color color)
		{
			VisualElement element = new() { pickingMode = PickingMode.Ignore };
			element.style.position = Position.Absolute;
			element.style.backgroundImage = new StyleBackground(texture);
			element.style.unityBackgroundImageTintColor = color;
			layer.Add(element);
			Particle particle = new() { Element = element };
			particles.Add(particle);
			return particle;
		}

		protected static void Place(Particle particle, float opacity, float rotateDegrees = 0f)
		{
			VisualElement element = particle.Element;
			element.style.width = particle.Size;
			element.style.height = particle.Size;
			element.style.translate = new Translate(particle.Position.x - particle.Size * 0.5f, particle.Position.y - particle.Size * 0.5f);
			element.style.opacity = opacity;
			if (rotateDegrees != 0f)
				element.style.rotate = UIFxTween.Degrees(rotateDegrees);
		}

		/// <summary>수명이 다한 입자 제거</summary>
		protected static void Sweep(List<Particle> particles)
		{
			for (int i = particles.Count - 1; i >= 0; i--)
			{
				if (particles[i].Age < particles[i].Life)
					continue;
				particles[i].Element.RemoveFromHierarchy();
				particles.RemoveAt(i);
			}
		}

		protected static float Range(Vector2 range) => UnityEngine.Random.Range(range.x, range.y);
	}
}
