using UnityEngine;

namespace WitchMendokusai
{
	// 등불과 바깥 가림의 색. 건물 등, 갱도가 같이 씀
	public sealed partial class VillageStage
	{
		[Header("Glow")]
		[SerializeField] private Color glowColor = new(1f, 0.82f, 0.6f);
		[SerializeField] private Color shadeColor = new(0.35f, 0.3f, 0.42f);

		private Light AddGlow(Transform body, Vector3 localPosition, float range, float intensity) =>
			VillageLights.AddGlow(body, localPosition, range, intensity, glowColor);
	}
}
