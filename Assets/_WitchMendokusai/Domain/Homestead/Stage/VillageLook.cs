using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// 마을에 있는 동안의 화면 색. 스테이지의 자식이라 마을을 나가면 같이 꺼짐
	public sealed class VillageLook : MonoBehaviour
	{
		[SerializeField] private float priority = 10f;
		[SerializeField] private float exposure = -0.06f;
		[SerializeField] private float contrast = 20f;
		[SerializeField] private float saturation = 16f;
		[SerializeField] private float bloomThreshold = 0.95f;
		[SerializeField] private float bloomIntensity = 0.35f;
		[SerializeField] private float bloomScatter = 0.68f;
		[SerializeField] private Color bloomTint = new(1f, 0.95f, 0.85f);

		private VolumeProfile profile;

		public static void Ensure(Transform parent)
		{
			if (parent.GetComponentInChildren<VillageLook>(true) != null)
				return;
			GameObject look = new(nameof(VillageLook));
			look.transform.SetParent(parent, false);
			look.AddComponent<VillageLook>();
		}

		private void Awake()
		{
			profile = ScriptableObject.CreateInstance<VolumeProfile>();
			profile.hideFlags = HideFlags.DontSave;
			ColorAdjustments color = profile.Add<ColorAdjustments>();
			color.postExposure.Override(exposure);
			color.contrast.Override(contrast);
			color.saturation.Override(saturation);
			Bloom bloom = profile.Add<Bloom>();
			bloom.threshold.Override(bloomThreshold);
			bloom.intensity.Override(bloomIntensity);
			bloom.scatter.Override(bloomScatter);
			bloom.tint.Override(bloomTint);
			Volume volume = gameObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = priority;
			volume.sharedProfile = profile;
		}

		private void OnDestroy()
		{
			if (profile != null)
				Destroy(profile);
		}
	}
}
