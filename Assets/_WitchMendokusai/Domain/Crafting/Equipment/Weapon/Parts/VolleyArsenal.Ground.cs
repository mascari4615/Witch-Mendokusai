using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 바닥. 빛이 뜨도록 주위를 어둡게 하고, 발밑 진과 빛기둥, 조준 부채, 맞은 자리의 고리와 자국
	// - 바닥 겹은 인물과 몬스터 그림보다 먼저 그려져 캐릭터는 어두워지지 않음
	public sealed partial class VolleyArsenal
	{
		[Header("바닥")]
		[SerializeField, Min(0f)] private float groundLift = 0.06f;
		[SerializeField, Min(0f)] private float sigilRadius = 2.7f;
		[SerializeField] private float sigilSpin = 0.7f;
		[SerializeField, Min(0f)] private float dimRadius = 12f;
		[SerializeField, Range(0f, 1f)] private float dimAmount = 0.6f;
		[SerializeField, Min(0f)] private float fanLength = 11f;
		[SerializeField, Min(0f)] private float fanWidth = 9f;
		[SerializeField, Range(0f, 1f)] private float fanShade = 0.5f;
		[SerializeField, Min(0f)] private float fanGlow = 0.16f;
		[SerializeField] private Color fanTint = new(0.95f, 0.88f, 1f, 1f);
		[SerializeField, Range(0f, 1f), Tooltip("배경 전체를 누르는 정도. 인물과 몬스터 그림은 안 눌림")] private float screenDim = 0.62f;
		[SerializeField, Range(0f, 1f), Tooltip("쏘는 동안 진의 밝기")] private float sigilWhileFiring = 0.35f;
		[SerializeField, Min(0.02f)] private float groundInSeconds = 0.22f;
		[SerializeField, Min(0.02f)] private float groundOutSeconds = 0.45f;
		[SerializeField, Min(0f), Tooltip("마지막 발 뒤 바닥이 머무는 길이")] private float groundLinger = 0.3f;

		[Header("빛기둥")]
		[SerializeField, Min(0)] private int shafts = 12;
		[SerializeField, Min(0f)] private float shaftHeight = 3.2f;
		[SerializeField, Min(0f)] private float shaftWidth = 0.4f;
		[SerializeField, Min(0f), Tooltip("꺼내는 동안 문으로 모이는 불티 (초당)")] private float gatherRate = 60f;

		private FxQuadBatch dimBatch;
		private FxQuadBatch fanBatch;
		private FxQuadBatch sigilBatch;
		private FxQuadBatch poolBatch;
		private FxQuadBatch scorchBatch;
		private float raiseTime = float.NegativeInfinity;
		private float lastShotTime = float.NegativeInfinity;
		private float groundLevel;
		private float gatherCarry;

		private void PrepareGround()
		{
			dimBatch = Layer("Dim", art.Soft, 2940);
			fanBatch = Layer("Fan", art.Fan, 2942);
			scorchBatch = Layer("Scorch", art.Scorch, 2944);
			poolBatch = Layer("Pool", art.Portal, 2946);
			sigilBatch = Layer("Sigil", art.Sigil, 2948);
		}

		private void RaiseGround() => raiseTime = Time.time;

		private void StopGround() => groundLevel = 0f;

		private void DrawGround()
		{
			bool active = Time.time < Mathf.Max(raiseTime + WindupSeconds, lastShotTime) + groundLinger;
			groundLevel = Mathf.MoveTowards(groundLevel, active ? 1f : 0f, Time.deltaTime / (active ? groundInSeconds : groundOutSeconds));
			if (groundLevel <= 0f)
				return;
			Vector3 center = owner.position + Vector3.up * groundLift;
			Vector3 aim = Aim();
			Vector3 side = Vector3.Cross(Vector3.up, aim);
			float smooth = Mathf.SmoothStep(0f, 1f, groundLevel);
			float firing = Mathf.Clamp01(1f - (Time.time - lastShotTime) / 0.35f);

			DimScreen(smooth);
			Flat(dimBatch, center, dimRadius, FxQuadLook.Of(WithAlpha(shadeTint, dimAmount * smooth), 1f));
			Vector3 fanCenter = center + aim * (fanLength * 0.5f) + Vector3.up * 0.01f;
			fanBatch.Add(fanCenter, aim * (fanLength * 0.5f), side * (fanWidth * 0.5f), FxQuadLook.Of(WithAlpha(shadeTint, fanShade * smooth), 1f));
			fanBatch.Add(fanCenter + Vector3.up * 0.01f, aim * (fanLength * 0.5f), side * (fanWidth * 0.5f), FxQuadLook.Of(Scale(fanTint, fanGlow * firing * smooth)));

			float grown = active ? Back(groundLevel) : 1f + 0.15f * (1f - groundLevel);
			FxQuadLook look = FxQuadLook.Of(Scale(lightTint, 0.35f), 0.1f, 1f - groundLevel, 0.4f);
			look.Turn = -Time.time * sigilSpin * 2.2f;
			look.Swirl = 2.5f;
			look.Distort = 0.04f;
			Flat(poolBatch, center + Vector3.up * 0.02f, sigilRadius * 0.55f * grown, look);
			// 꺼낼 때 밝고, 쏘는 동안 낮췄다가 발마다 맥동, 막타에 부서짐
			float calm = Mathf.Lerp(1f, sigilWhileFiring, Mathf.Clamp01((Time.time - raiseTime - WindupSeconds + 0.1f) / 0.15f));
			float shattered = Mathf.Max(1f - groundLevel, Mathf.Clamp01((Time.time - finaleTime) / finaleSeconds));
			look = FxQuadLook.Of(WithAlpha(Color.Lerp(lightTint, hotTint, lightLevel * 0.4f), Mathf.Min(1f, calm + lightLevel * 0.25f)), 0.3f, shattered, 0.1f);
			look.Turn = Time.time * sigilSpin;
			Flat(sigilBatch, center + Vector3.up * 0.04f, sigilRadius * grown, look);

			DrawShafts(center, smooth);
			Gather();
		}

		// 카메라 바로 앞에 어둠 한 장. 불투명한 지형 뒤, 인물 그림 앞에 그려짐
		private void DimScreen(float level)
		{
			Transform cameraTransform = viewCamera.transform;
			float distance = viewCamera.nearClipPlane + 0.2f;
			float halfHeight = distance * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
			Vector3 right = cameraTransform.right * (halfHeight * viewCamera.aspect);
			dimBatch.Add(cameraTransform.position + cameraTransform.forward * distance, right, cameraTransform.up * halfHeight, new Rect(0.5f, 0.5f, 0f, 0f), FxQuadLook.Of(WithAlpha(shadeTint, screenDim * level), 1f));
		}

		// 진 둘레에서 솟는 빛기둥. 저마다 다른 때에 솟고 사라짐
		private void DrawShafts(Vector3 center, float level)
		{
			Vector3 viewForward = viewCamera.transform.forward;
			for (int i = 0; i < shafts; i++)
			{
				float cycle = Mathf.Repeat(Time.time * 1.1f + i * 0.618f, 1f);
				float angle = TAU * i / shafts + Time.time * sigilSpin * 0.5f;
				Vector3 foot = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (sigilRadius * Mathf.Lerp(0.72f, 0.95f, Mathf.Repeat(i * 0.37f, 1f)));
				float strength = Mathf.Sin(cycle * Mathf.PI) * level;
				float height = shaftHeight * Mathf.Lerp(0.5f, 1f, Mathf.Repeat(i * 0.73f, 1f));
				Vector3 bottom = foot + Vector3.up * (height * 0.35f * cycle);
				Vector3 half = Vector3.up * (height * 0.5f);
				float width = shaftWidth * Mathf.Lerp(0.5f, 1f, Mathf.Repeat(i * 0.41f, 1f));
				glowBehind.Add(bottom + half, half, Vector3.Cross(viewForward, Vector3.up).normalized * (width * 0.5f), FxQuadLook.Of(Scale(lightTint, strength * 0.55f)));
			}
		}

		// 꺼내는 동안 문마다 불티가 빨려 듦
		private void Gather()
		{
			if (Time.time > raiseTime + WindupSeconds || raised.Count == 0)
				return;
			gatherCarry += gatherRate * Time.deltaTime;
			while (gatherCarry >= 1f)
			{
				gatherCarry -= 1f;
				Pose(raised[Random.Range(0, raised.Count)], out Vector3 target, out _);
				Vector3 from = target + Random.onUnitSphere * Random.Range(0.8f, 1.8f);
				float seconds = Random.Range(0.2f, 0.35f);
				UltimateFxStage.Emit(sparks, from, (target - from) / seconds, Random.Range(0.05f, 0.11f), seconds, Color.white);
			}
		}

		private void DrawImpactGround(in Burst burst, float age)
		{
			Vector3 ground = new(burst.Position.x, burst.Ground, burst.Position.z);
			float faded = Mathf.Clamp01(age / scorchSeconds);
			FxQuadLook look = FxQuadLook.Of(Scale(lightTint, 1.2f * (1f - 0.5f * faded)), 0.6f, Mathf.Clamp01((faded - 0.35f) / 0.65f), burst.Seed);
			look.Turn = burst.Seed * TAU;
			Flat(scorchBatch, ground + Vector3.up * (0.01f + 0.01f * burst.Seed), scorchSize * burst.Scale * Back(age / 0.14f), look);
			float ring = age / ringSeconds;
			if (ring >= 1f)
				return;
			look = FxQuadLook.Of(Color.Lerp(hotTint, lightTint, ring), 0.2f, ring, burst.Seed);
			look.Turn = burst.Seed * TAU;
			look.Swirl = 3f * (1f - ring);
			Flat(poolBatch, ground + Vector3.up * 0.03f, ringSize * burst.Scale * (0.25f + 0.75f * Mathf.Sqrt(ring)), look);
		}

		private static void Flat(FxQuadBatch batch, Vector3 center, float radius, in FxQuadLook look) => batch.Add(center, Vector3.right * radius, Vector3.forward * radius, look);
	}
}
