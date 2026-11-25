using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 마지막 발. 꽃잎처럼 벌어지는 빛, 붓결 고리, 화면 섬광과 흔들림, 내려앉는 별 가루
	public sealed partial class VolleyArsenal
	{
		// 칸 밖으로 넘친 두 장 (7, 8) 은 건너뛰고 크기로 벌어지게 함
		private static readonly int[] GRAND_FRAMES = { 2, 3, 4, 5, 6, 6, 9, 10, 11, 12, 13, 14 };
		private const int STAR_FRAME = 3;

		[Header("막타")]
		[SerializeField, Min(0f), Tooltip("문이 닫히는 때를 흩뜨리는 폭")] private float closeSpread = 0.12f;
		[SerializeField, Min(0), Tooltip("크게 나가는 마지막 발 수")] private int finaleShots = 6;
		[SerializeField, Min(1f)] private float finaleScale = 1.5f;
		[SerializeField, Min(0.05f)] private float finaleSeconds = 0.45f;
		[SerializeField, Min(0f)] private float finaleRing = 4.8f;
		[SerializeField, Min(0f)] private float finaleFlash = 7f;
		[SerializeField, Min(0f), Tooltip("막타가 터지는 앞쪽 거리")] private float grandDistance = 5.2f;
		[SerializeField] private float grandHeight = 1.2f;
		[SerializeField, Min(0f)] private float grandSize = 9f;
		[SerializeField, Min(0.05f)] private float grandSeconds = 1f;
		[SerializeField, Min(0f), Tooltip("마지막 발 뒤 터지기까지")] private float grandDelay = 0.3f;
		[SerializeField, Range(0f, 1f), Tooltip("겨누는 쪽으로 늘어나는 정도")] private float grandStretch = 0.3f;
		[SerializeField, Min(0f), Tooltip("막타 자리 바닥 자국의 배율")] private float grandMark = 2.4f;

		[Header("막타 완드")]
		[SerializeField, Min(0f)] private float heroLength = 5.4f;
		[SerializeField, Min(0f)] private float heroPortal = 2.1f;
		[SerializeField, Tooltip("인물 뒤, 위")] private Vector2 heroOffset = new(2f, 3f);
		[SerializeField, Min(0.02f)] private float heroEmergeSeconds = 0.3f;
		[SerializeField, Min(0.02f), Tooltip("빛이 막타 자리에 닿을 때까지")] private float heroFlySeconds = 0.13f;
		[SerializeField, Min(0f)] private float heroBeamWidth = 3f;
		[SerializeField, Min(0f), Tooltip("막타 빛 탄환의 배율")] private float heroBulletScale = 3f;
		[SerializeField, Min(0.05f)] private float heroFadeSeconds = 0.4f;
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
		private float heroTime = float.PositiveInfinity;
		private FxQuadBatch heroWand;
		private FxQuadBatch heroGate;
		private FxQuadBatch heroBeam;
		private bool grandMarked;

		private void PrepareFinale()
		{
			grandBatch = Layer("Grand", art.Grand, 3017, true);
			heroGate = Layer("HeroGate", art.Portals[^1], 2972);
			heroWand = Layer("HeroWand", art.Wands[^1], 2974);
			heroBeam = Layer("HeroBeam", art.Streaks, 3011, true);
			dustBatch = Layer("Dust", art.Impact, 3019, true);
		}

		private void StartFinale()
		{
			finaleTime = Time.time;
			finaleAim = Aim();
			finaleCenter = owner.position + Vector3.up * (groundLift + 0.06f);
			grandMarked = false;
			// 남은 문을 한꺼번에 닫음
			foreach (Weapon weapon in weapons)
				weapon.BreakTime = Mathf.Min(weapon.BreakTime, Time.time + closeSpread * Mathf.Repeat(weapon.Phase, 1f));
			CameraManager.Instance.GenerateCameraImpulse(finaleShake);
		}

		private void StopFinale()
		{
			finaleTime = float.PositiveInfinity;
			heroTime = float.PositiveInfinity;
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
					Billboard(flashSoft, finaleCenter + Vector3.up * 1.6f + finaleAim, finaleFlash, 0f, FxQuadLook.Of(Scale(paleTint, heat * 0.3f)));
			}
			DrawHero(age);
			DrawGrand(age - grandDelay);
			if (age >= 0f && age < dustSeconds)
				SpawnDust();
			for (int i = dusts.Count - 1; i >= 0; i--)
			{
				if (DrawDust(dusts[i]) == false)
					dusts.RemoveAt(i);
			}
		}

		private void RaiseHero() => heroTime = Time.time;

		// 막타를 쏘는 큰 완드. 인물 뒤 큰 문에서 나와 빛을 모았다가 막타 자리로 굵은 빛줄기를 쏨. 완드는 문에 남았다가 같이 닫힘
		private void DrawHero(float sinceFinale)
		{
			float age = Time.time - heroTime;
			float launch = grandDelay - heroFlySeconds;
			float flying = sinceFinale - launch;
			float closing = Mathf.Clamp01((flying - heroFlySeconds) / heroFadeSeconds);
			if (age < 0f || closing >= 1f)
				return;
			Vector3 aim = Aim();
			Vector3 viewForward = viewCamera.transform.forward;
			Vector3 gate = InArms ? owner.position + aim * armHeroOffset.x + Vector3.up * armHeroOffset.y : owner.position - aim * heroOffset.x + Vector3.up * heroOffset.y;
			Vector3 target = owner.position + aim * grandDistance + Vector3.up * grandHeight;
			Vector3 direction = (target - gate).normalized;
			float open = Back(age / openSeconds) * (1f - closing);
			Vector3 facing = ((Vector3.Dot(direction, viewForward) > 0f ? -direction : direction) - viewForward * portalFacing).normalized;
			Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
			Vector3 up = Vector3.Cross(facing, right);
			FxQuadLook look = FxQuadLook.Of(Color.Lerp(portalTint, lightTint, 0.3f), 0.9f, Mathf.Max(1f - age / openSeconds, closing), 0.3f);
			look.Turn = Time.time * portalSpin * 0.5f;
			if (InArms == false)
				heroGate.Add(gate, right * (heroPortal * open), up * (heroPortal * open), look);
			float kick = flying >= 0f ? Mathf.Pow(0.5f, flying / flashHalfLife) : 0f;
			float shown = Mathf.Clamp01(Back(age / heroEmergeSeconds) * emergeFraction * (1f - recoil * kick));
			float gather = Mathf.Clamp01(sinceFinale / Mathf.Max(0.01f, launch));
			Vector3 muzzle = gate + direction * (gateWands ? heroLength * shown : gateLightOffset);
			if (gateWands)
				heroWand.AddStrip(gate, muzzle, heroLength / weaponAspects[^1], viewForward, new Rect(1f - shown, 0f, shown, 1f), FxQuadLook.Of(Color.Lerp(Color.white, hotTint, flying < 0f ? gather : kick), 1f - closing, closing, 0.7f));
			if (flying < 0f)
			{
				Billboard(flashSoft, muzzle, 0.5f + 0.9f * gather, 0f, FxQuadLook.Of(Scale(goldTint, 0.4f + 0.6f * gather)));
				return;
			}
			Vector3 head = Vector3.Lerp(muzzle, target, flying / heroFlySeconds);
			look = FxQuadLook.Of(paleTint, 0.7f, closing, 0.2f);
			look.Distort = 0.02f;
			heroBeam.AddStrip(muzzle, head, heroBeamWidth * (1f - 0.6f * closing), viewForward, PaintedFxSet.StreakRow(3), look);
			heroBeam.AddStrip(muzzle, head, heroBeamWidth * 0.8f * (1f - 0.6f * closing), viewForward, PaintedFxSet.StreakRow(1), FxQuadLook.Of(goldTint, 0.3f, closing, 0.6f));
			if (flying < heroFlySeconds)
				DrawBullet(head, direction, heroBulletScale, viewForward);
		}

		// 꽃잎처럼 벌어지는 막타. 부채 가운데 한 자리
		private void DrawGrand(float age)
		{
			if (age < 0f || age >= grandSeconds)
				return;
			if (grandMarked == false)
			{
				grandMarked = true;
				Vector3 mark = finaleCenter + finaleAim * grandDistance;
				bursts.Add(new Burst { Position = mark + Vector3.up * shotHeight, Forward = Vector3.up, Ground = mark.y, StartTime = Time.time, Seed = Random.value, Scale = grandMark });
				for (int i = 0; i < sparksPerImpact * 3; i++)
					UltimateFxStage.Emit(sparks, mark + Vector3.up, (Vector3.up + Random.onUnitSphere * 1.2f).normalized * Random.Range(3f, 11f), Random.Range(0.1f, 0.26f), Random.Range(0.4f, 0.9f), Color.white);
			}
			float progress = age / grandSeconds;
			int frame = GRAND_FRAMES[Mathf.Min(GRAND_FRAMES.Length - 1, Mathf.FloorToInt(progress * GRAND_FRAMES.Length))];
			Vector3 position = finaleCenter + finaleAim * grandDistance + Vector3.up * grandHeight;
			Billboard(flashSoft, position, grandSize * 0.7f, 0f, FxQuadLook.Of(WithAlpha(shadeTint, 0.7f * (1f - progress)), 1f));
			Transform cameraTransform = viewCamera.transform;
			float half = grandSize * 0.5f * (0.85f + 0.5f * Mathf.Sqrt(progress));
			float along = Mathf.Abs(Vector3.Dot(finaleAim, cameraTransform.right));
			Vector3 wide = cameraTransform.right * (half * (1f + grandStretch * along));
			Vector3 tall = cameraTransform.up * (half * (1f + grandStretch * (1f - along)));
			grandBatch.Add(position, wide, tall, FxQuadBatch.Cell(frame, 4, 4), FxQuadLook.Of(Color.white * 0.92f, 0.85f));
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
