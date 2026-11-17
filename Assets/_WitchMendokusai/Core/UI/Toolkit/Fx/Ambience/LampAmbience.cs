using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>한 자리 등불 빛이 가끔 깜빡임</summary>
	[CreateAssetMenu(fileName = "Ambience_0_Lamp", menuName = "WM/UI/Ambience/Lamp Flicker")]
	public sealed class LampAmbience : UIAmbience
	{
		[Tooltip("등불 자리, 화면 비율")]
		public Vector2 position = new(0.72f, 0.25f);
		[Tooltip("빛 반경, 화면 가로 비율")]
		public float radius = 0.4f;
		public Color color = new(1f, 0.75f, 0.43f, 0.5f);
		[Tooltip("초당 깜빡일 확률과 깜빡일 때 밝기")]
		public float flickerPerSecond = 2.4f;
		[Range(0f, 1f)] public float flickerLow = 0.55f;
		[Tooltip("밝기 변화 속도")]
		public float response = 18f;

		public override void Begin(VisualElement layer)
		{
			VisualElement glow = new() { pickingMode = PickingMode.Ignore };
			glow.style.position = Position.Absolute;
			glow.style.backgroundImage = new StyleBackground(UIFxTextures.Glow);
			glow.style.unityBackgroundImageTintColor = color;
			layer.Add(glow);
			float brightness = 1f;
			Tick(layer, (deltaSeconds, area) =>
			{
				float size = radius * area.x * 2f;
				glow.style.width = size;
				glow.style.height = size;
				glow.style.translate = new Translate(position.x * area.x - size * 0.5f, position.y * area.y - size * 0.5f);
				float wanted = UnityEngine.Random.value < flickerPerSecond * deltaSeconds ? flickerLow : 1f;
				brightness = Mathf.Lerp(brightness, wanted, 1f - Mathf.Exp(-response * deltaSeconds));
				glow.style.opacity = brightness;
			});
		}
	}
}
