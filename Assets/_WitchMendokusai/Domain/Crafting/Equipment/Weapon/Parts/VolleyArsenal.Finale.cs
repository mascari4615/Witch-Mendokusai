using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 마지막 발. 부채 전체를 쓸고 가는 빛, 퍼지는 고리, 화면 섬광과 흔들림, 내려앉는 별 가루
	public sealed partial class VolleyArsenal
	{
		private const int RING_FRAME = 7;
		private const int INNER_RING_FRAME = 5;
		private const int STAR_FRAME = 3;

		[Header("막타")]
		[SerializeField, Min(0), Tooltip("크게 나가는 마지막 발 수")] private int finaleShots = 6;
		[SerializeField, Min(1f)] private float finaleScale = 1.5f;
		[SerializeField, Min(0.05f)] private float finaleSeconds = 0.45f;
		[SerializeField, Min(0f)] private float finaleRing = 8f;
		[SerializeField, Min(0f)] private float finaleFlash = 7f;
		[SerializeField, Min(0f), Tooltip("카메라 흔들림. 설정에서 끄면 안 흔들림")] private float finaleShake = 0.6f;
		[SerializeField, Min(0f), Tooltip("별 가루 (초당)")] private float dustRate = 70f;
		[SerializeField, Min(0f)] private float dustSeconds = 0.5f;
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
		private FxQuadBatch finaleFan;
		private FxQuadBatch finaleRings;
		private FxQuadBatch dustBatch;
		private float finaleTime = float.PositiveInfinity;
		private Vector3 finaleAim;
		private Vector3 finaleCenter;
		private float dustCarry;

		private void PrepareFinale()
		{
			finaleFan = Layer("FinaleFan", art.Fan, 2950);
			finaleRings = Layer("FinaleRing", art.Impact, 2951);
			dustBatch = Layer("Dust", art.Impact, 3019);
		}

		private void StartFinale()
		{
			finaleTime = Time.time;
			finaleAim = Aim();
			finaleCenter = owner.position + Vector3.up * (groundLift + 0.06f);
			foreach (Weapon weapon in decor)
				weapon.FireTime = Time.time + 0.3f * Mathf.Repeat(weapon.Phase, 1f);
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
				float reach = fanLength * (0.35f + 0.75f * spread);
				Vector3 side = Vector3.Cross(Vector3.up, finaleAim);
				finaleFan.Add(finaleCenter + finaleAim * (reach * 0.5f), finaleAim * (reach * 0.5f), side * (reach * 0.42f), FxQuadLook.Of(Scale(hotTint, 0.9f * (1f - progress) * (1f - progress)), 0f, progress * 0.8f, 0.3f));
				float radius = Mathf.Lerp(finaleRing * 0.2f, finaleRing, spread);
				FlatCell(finaleRings, RING_FRAME, radius * 1.1f, Scale(hotTint, 1f - progress));
				FlatCell(finaleRings, INNER_RING_FRAME, radius * 0.75f, Scale(lightTint, 1f - progress));
				float heat = Mathf.Pow(0.5f, age / flashHalfLife);
				if (heat > 0.03f)
					Billboard(flashSoft, finaleCenter + Vector3.up * 1.6f + finaleAim, finaleFlash, 0f, FxQuadLook.Of(Scale(fanTint, heat * 0.3f)));
			}
			if (age >= 0f && age < dustSeconds)
				SpawnDust();
			for (int i = dusts.Count - 1; i >= 0; i--)
			{
				if (DrawDust(dusts[i]) == false)
					dusts.RemoveAt(i);
			}
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
					Position = owner.position + cameraTransform.right * Random.Range(-5f, 5f) + Vector3.up * Random.Range(1.5f, 4.5f) + finaleAim * Random.Range(-1f, 3f),
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

		private void FlatCell(FxQuadBatch batch, int frame, float radius, Color color) => batch.Add(finaleCenter, Vector3.right * radius, Vector3.forward * radius, FxQuadBatch.Cell(frame, 4, 4), FxQuadLook.Of(color));
	}
}
