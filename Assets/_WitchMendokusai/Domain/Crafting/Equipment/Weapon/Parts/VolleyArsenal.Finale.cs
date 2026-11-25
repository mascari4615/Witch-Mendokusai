using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 마지막 발. 꽃잎처럼 벌어지는 빛, 붓결 고리, 화면 섬광과 흔들림, 내려앉는 별 가루
	public sealed partial class VolleyArsenal
	{
		private const int GRAND_FIRST_FRAME = 2;
		private const int STAR_FRAME = 3;

		[Header("막타")]
		[SerializeField, Min(0), Tooltip("크게 나가는 마지막 발 수")] private int finaleShots = 6;
		[SerializeField, Min(1f)] private float finaleScale = 1.5f;
		[SerializeField, Min(0.05f)] private float finaleSeconds = 0.45f;
		[SerializeField, Min(0f)] private float finaleRing = 4.8f;
		[SerializeField, Min(0f)] private float finaleFlash = 7f;
		[SerializeField, Min(0f), Tooltip("막타가 터지는 앞쪽 거리")] private float grandDistance = 5.2f;
		[SerializeField] private float grandHeight = 1.2f;
		[SerializeField, Min(0f)] private float grandSize = 9f;
		[SerializeField, Min(0.05f)] private float grandSeconds = 0.85f;
		[SerializeField, Min(0f), Tooltip("마지막 발 뒤 터지기까지")] private float grandDelay = 0.16f;
		[SerializeField, Min(0f), Tooltip("막타 뒤 어둠이 더 머무는 길이")] private float finaleLinger = 0.45f;
		[SerializeField, Min(0f), Tooltip("카메라 흔들림. 설정에서 끄면 안 흔들림")] private float finaleShake = 0.6f;
		[SerializeField, Min(0f), Tooltip("별 가루 (초당)")] private float dustRate = 110f;
		[SerializeField, Min(0f)] private float dustSeconds = 0.9f;
		[SerializeField] private Vector2 dustLife = new(0.7f, 1.3f);
		[SerializeField] private Vector2 dustSize = new(0.11f, 0.22f);

		private struct Dust
		{
			public Vector3 Position;
			public Vector3 Velocity;
			public float StartTime;
			public float Life;
			public float Size;
			public float Phase;
		}

		private readonly List<Dust> dusts = new();
		private FxQuadBatch grandBatch;
		private FxQuadBatch dustBatch;
		private float finaleTime = float.PositiveInfinity;
		private Vector3 finaleAim;
		private Vector3 finaleCenter;
		private float dustCarry;

		private void PrepareFinale()
		{
			grandBatch = Layer("Grand", art.Grand, 3017);
			dustBatch = Layer("Dust", art.Impact, 3019);
		}

		private void StartFinale()
		{
			finaleTime = Time.time;
			finaleAim = Aim();
			finaleCenter = owner.position + Vector3.up * (groundLift + 0.06f);
			foreach (Weapon weapon in decor)
				weapon.FireTime = Time.time + 0.1f * Mathf.Repeat(weapon.Phase, 1f);
			CameraManager.Instance.GenerateCameraImpulse(finaleShake);
		}

		private void StopFinale()
		{
			finaleTime = float.PositiveInfinity;
			dusts.Clear();
		}

		private void DrawFinale()
		{
			float age = Time.time - finaleTime;
			if (age >= 0f && age < finaleSeconds)
			{
				float progress = age / finaleSeconds;
				float spread = Mathf.Sqrt(progress);
				FxQuadLook look = FxQuadLook.Of(hotTint, 0.3f, progress, 0.2f);
				look.Turn = 0.6f;
				Flat(shockBatch, finaleCenter + finaleAim * grandDistance, Mathf.Lerp(0.25f, 1f, spread) * finaleRing, look);
				look.Color = WithAlpha(lightTint, 0.8f);
				look.Turn = 2.2f;
				Flat(shockBatch, finaleCenter, Mathf.Lerp(0.3f, 1f, spread) * sigilRadius * 1.2f, look);
				float heat = Mathf.Pow(0.5f, age / flashHalfLife);
				if (heat > 0.03f)
					Billboard(flashSoft, finaleCenter + Vector3.up * 1.6f + finaleAim, finaleFlash, 0f, FxQuadLook.Of(Scale(violetTint, heat * 0.3f)));
			}
			DrawGrand(age - grandDelay);
			if (age >= 0f && age < dustSeconds)
				SpawnDust();
			for (int i = dusts.Count - 1; i >= 0; i--)
			{
				if (DrawDust(dusts[i]) == false)
					dusts.RemoveAt(i);
			}
		}

		// 꽃잎처럼 벌어지는 막타. 부채 가운데 한 자리
		private void DrawGrand(float age)
		{
			if (age < 0f || age >= grandSeconds)
				return;
			float progress = age / grandSeconds;
			int frame = Mathf.Min(15, Mathf.FloorToInt(GRAND_FIRST_FRAME + (16 - GRAND_FIRST_FRAME) * Mathf.Pow(progress, 0.85f)));
			Vector3 position = finaleCenter + finaleAim * grandDistance + Vector3.up * grandHeight;
			Billboard(flashSoft, position, grandSize * 0.7f, 0f, FxQuadLook.Of(WithAlpha(shadeTint, 0.7f * (1f - progress)), 1f));
			Billboard(grandBatch, position, grandSize * 0.5f * (1f + 0.12f * progress), 0f, FxQuadBatch.Cell(frame, 4, 4), FxQuadLook.Of(Color.Lerp(Color.white, hotTint, Mathf.Max(0f, 1f - progress * 4f)), 0.75f));
		}

		private void SpawnDust()
		{
			dustCarry += dustRate * Time.deltaTime;
			Transform cameraTransform = viewCamera.transform;
			while (dustCarry >= 1f)
			{
				dustCarry -= 1f;
				dusts.Add(new Dust
				{
					Position = owner.position + cameraTransform.right * Random.Range(-3f, 3f) + Vector3.up * Random.Range(0.8f, 4f) + finaleAim * Random.Range(0f, 6f),
					Velocity = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.9f, -0.2f), Random.Range(-0.3f, 0.3f)),
					StartTime = Time.time,
					Life = Random.Range(dustLife.x, dustLife.y),
					Size = Random.Range(dustSize.x, dustSize.y),
					Phase = Random.value * TAU,
				});
			}
		}

		private bool DrawDust(in Dust dust)
		{
			float age = Time.time - dust.StartTime;
			float progress = age / dust.Life;
			if (progress >= 1f)
				return false;
			float twinkle = Mathf.Sin(progress * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(age * 30f + dust.Phase));
			Billboard(dustBatch, dust.Position + dust.Velocity * age, dust.Size, 0f, FxQuadBatch.Cell(STAR_FRAME, 4, 4), FxQuadLook.Of(Scale(hotTint, Mathf.Max(0f, twinkle))));
			return true;
		}
	}
}
