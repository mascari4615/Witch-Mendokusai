using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 지금 실내에 있는가. 실내 방 (욘의 집, 박물관) 과 갱도가 들어설 때 Enter, 나갈 때 Leave
	// 하늘 (SkyDirector, VillageLook) 은 실내면 빛을 시계에서 약하게만 따르고, 날씨 (WeatherDirector) 는 입자를 멈춤
	// 소유자 집합이라 둘이 겹쳐 켜도 하나가 나갈 때 꺼지지 않음
	public static class IndoorScope
	{
		private static readonly HashSet<object> owners = new();

		public static bool IsIndoor => owners.Count > 0;

		public static event Action Changed = delegate { };

		public static void Enter(object owner)
		{
			bool was = IsIndoor;
			owners.Add(owner);
			if (was != IsIndoor)
				Changed();
		}

		public static void Leave(object owner)
		{
			bool was = IsIndoor;
			owners.Remove(owner);
			if (was != IsIndoor)
				Changed();
		}

		// 도메인 리로드 없이 플레이를 다시 켜도 지난 판의 소유자가 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void Clear() => owners.Clear();
	}

	// 실내 빛. 한낮 값 쪽으로 섞어 시계를 약하게 따르고, 너무 어두워지지 않게 하한을 둠
	[Serializable]
	public struct IndoorLight
	{
		[Tooltip("섞을 기준 시각 (0~1, 0.5 한낮)"), Range(0f, 1f)] public float NoonTime;
		[Tooltip("시계를 따르는 정도. 0 이면 늘 한낮, 1 이면 바깥과 같음"), Range(0f, 1f)] public float ClockWeight;
		[Tooltip("실내 주변광 밝기 (휘도) 하한")] public float MinLuminance;

		public static IndoorLight Default => new() { NoonTime = 0.5f, ClockWeight = 0.35f, MinLuminance = 0.32f };

		public Color Blend(Gradient gradient, float time) => Color.Lerp(gradient.Evaluate(NoonTime), gradient.Evaluate(time), ClockWeight);

		public Color Ambient(Gradient ambient, float time)
		{
			Color color = Blend(ambient, time);
			float luminance = color.r * .2126f + color.g * .7152f + color.b * .0722f;
			if (luminance > .0001f && luminance < MinLuminance)
				color *= MinLuminance / luminance;
			return color;
		}
	}
}
