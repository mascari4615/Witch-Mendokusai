using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>여러 공기를 한 층에 겹침. 예: 등불 + 김 = 연구소</summary>
	[CreateAssetMenu(fileName = "Ambience_0_Mix", menuName = "WM/UI/Ambience/Mix")]
	public sealed class MixAmbience : UIAmbience
	{
		public UIAmbience[] parts = new UIAmbience[0];

		public override void Begin(VisualElement layer)
		{
			foreach (UIAmbience part in parts)
			{
				if (part == null || part is MixAmbience)
					throw new InvalidOperationException($"{name}: 빈 칸이나 묶음이 있음");
				part.Begin(layer);
			}
		}
	}
}
