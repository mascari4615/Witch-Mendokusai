using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	/// <summary>
	/// 변신 연출의 무기. 가슴 빛에서 빛 막대를 뽑아 손 위로 세우고, 터진 뒤 끝에서부터 부서져 권총형 완드로 남음
	/// - 막대는 한 프레임에 끝까지 나타남 (자라는 과정 없음). 휘감는 바람 띠는 조금 늦게 최고
	/// </summary>
	public sealed partial class FormTransformFx
	{
		[Header("빛 막대")]
		[SerializeField, Min(0.5f)] private float barLength = 2.9f;
		[SerializeField, Min(0.01f)] private float barWidth = 0.24f;
		[SerializeField, Min(0.05f), Tooltip("둘레 빛 폭")] private float sheathWidth = 0.5f;
		[SerializeField, Tooltip("화면 오른쪽으로 기우는 정도")] private float barLean = 0.42f;
		[SerializeField, Min(0)] private int needleCount = 9;
		[SerializeField] private Vector2 needleLength = new(0.8f, 1.9f);
		[SerializeField, Min(0), Tooltip("막대가 부서질 때 초당 빛 조각")] private int breakPerSecond = 220;

		[Header("완드")]
		[SerializeField, Min(0.1f)] private float wandSize = 0.85f;

		private Vector3 BarDirection(in Pose pose) => (pose.Up + pose.Right * barLean).normalized;

		private Vector3 BarBase(in Pose pose, float now)
		{
			float raise = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, DrawTime, DrawTime + raiseSeconds));
			return Vector3.Lerp(Chest(pose) - pose.Away * 0.2f, Hand(pose), raise);
		}

		private void ComposeBar(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float breakStart = ImpactTime + barBreakDelay;
			float end = breakStart + barBreakSeconds + 0.1f;

			Material coreMaterial = Solid(run, new Vector3(5f, barLength * texelsPerUnit, 5f));
			coreMaterial.SetVector(DISSOLVE_RANGE_ID, new Vector4(0f, 1f, 0f, 0f));
			GameObject bar = UltimateFxStage.CreateMeshObject("FormBar", run.Root, Keep(run, FormMeshes.Bar(1f)), coreMaterial);
			Material sheathMaterial = Energy(run, FRONT_QUEUE);
			sheathMaterial.SetFloat(SHAPE_ID, GLOW);
			sheathMaterial.SetFloat("_NoiseAmount", 0.18f);
			sheathMaterial.SetFloat(COVER_ID, 0.5f);
			sheathMaterial.SetVector(TEXELS_ID, new Vector4(barLength * texelsPerUnit, sheathWidth * texelsPerUnit, 0f, 0f));
			GameObject sheath = UltimateFxStage.CreateMeshObject("FormSheath", run.Root, Keep(run, FormMeshes.Sheath(2, 12, 1f)), sheathMaterial);
			Material windMaterial = Energy(run, FRONT_QUEUE + 1);
			windMaterial.SetVector(TEXELS_ID, new Vector4(barLength * texelsPerUnit, 4f, 0f, 0f));
			List<GameObject> winds = new();
			for (int i = 0; i < 2; i++)
				winds.Add(UltimateFxStage.CreateMeshObject("FormBarWind", run.Root, Keep(run, FormMeshes.Helix(22, 1.1f + i * 0.25f, 0.14f + 0.03f * i, 0.3f + 0.06f * i, 0.12f, i / 3f)), windMaterial));
			bar.SetActive(false);
			sheath.SetActive(false);
			foreach (GameObject wind in winds)
				wind.SetActive(false);
			run.At(DrawTime, () =>
			{
				bar.SetActive(true);
				sheath.SetActive(true);
				foreach (GameObject wind in winds)
					wind.SetActive(true);
			});

			ParticleSystem pieces = PixelMotes(run, "FormBarPieces", breakPerSecond + 60, 0f, 0.35f);
			ParticleSystem sparks = Streaks(run, "FormBarSparks", 80, 0.05f);
			float carry = 0f;
			run.Track(DrawTime, end - DrawTime, _ =>
			{
				Pose pose = PoseNow(run);
				float now = Stepped(run.Now);
				Vector3 origin = BarBase(pose, now);
				Vector3 direction = BarDirection(pose);
				Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(pose.Away, direction), direction);
				float birth = Spike(now, DrawTime, 0.001f, 0.06f);
				float broken = UltimateFxRun.Span(now, breakStart, breakStart + barBreakSeconds);
				// 가슴에서 짧게 나와 손으로 올라가며 끝까지 뻗고, 부서질 때는 손 쪽으로 줄어듦
				float extend = Mathf.Lerp(0.22f, 1f, OverShoot(UltimateFxRun.Span(now, DrawTime, DrawTime + raiseSeconds), 0.08f));
				float length = barLength * extend * Mathf.Lerp(1f, 0.3f, UltimateFxRun.EaseIn(broken));
				// 정지 동안 식지 않고 터짐까지 점점 달아오름
				float charge = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, DrawTime + raiseSeconds, ImpactTime));
				float thick = 1f + charge * 0.15f + birth * 0.8f + 0.35f * Spike(now, ImpactTime, 0.001f, 0.1f);
				bar.transform.SetPositionAndRotation(origin, rotation * Quaternion.Euler(0f, 45f, 0f));
				bar.transform.localScale = new Vector3(barWidth * thick, length, barWidth * thick);
				coreMaterial.SetFloat(DISSOLVE_ID, broken);
				coreMaterial.SetColor(EMISSION_ID, Color.Lerp(coreColor, coreColor * 1.5f, charge));
				coreMaterial.SetFloat(HEAT_ID, birth);
				sheath.transform.SetPositionAndRotation(origin, rotation);
				float pulse = 1f + 0.06f * Mathf.Sin(now * 34f);
				sheath.transform.localScale = new Vector3(sheathWidth * thick * pulse, length * 1.04f, sheathWidth * thick * pulse);
				sheathMaterial.SetFloat(DISSOLVE_ID, broken);
				sheathMaterial.SetFloat(INTENSITY_ID, 1f + charge * 0.5f);
				sheathMaterial.SetFloat(FLASH_ID, birth * 0.3f);
				float windStrength = UltimateFxRun.Span(now, DrawTime, DrawTime + 0.1f) * (1f - UltimateFxRun.Span(now, ImpactTime + 0.25f, breakStart + 0.2f));
				windMaterial.SetFloat(ALPHA_ID, windStrength > 0.3f ? 1f : 0f);
				for (int i = 0; i < winds.Count; i++)
				{
					winds[i].transform.SetPositionAndRotation(origin, rotation * Quaternion.Euler(0f, now * (520f + 90f * i), 0f));
					winds[i].transform.localScale = new Vector3(1f, length * 0.92f, 1f);
				}
				if (broken <= 0f || broken >= 1f)
					return;
				carry += breakPerSecond * run.Delta;
				for (; carry >= 1f; carry -= 1f)
				{
					// 부서지는 경계에서 나와 손 쪽으로 모임
					Vector3 from = origin + direction * (length * Mathf.Clamp01(1f - broken + Random.Range(-0.12f, 0.05f))) + Random.insideUnitSphere * 0.14f;
					Vector3 toHand = Hand(pose) - from;
					float life = Random.Range(0.25f, 0.45f);
					UltimateFxStage.Emit(pieces, from, toHand / life * 0.75f + Random.insideUnitSphere * 0.9f, Random.Range(0.05f, 0.12f), life, new Color(1f, 0.85f, 0.45f, 1f));
				}
			}, () =>
			{
				run.Destroy(bar);
				run.Destroy(sheath);
				foreach (GameObject wind in winds)
					run.Destroy(wind);
			});
			run.At(DrawTime + 0.07f, () =>
			{
				// 손 자리 튐 불꽃
				Vector3 hand = Hand(PoseNow(run));
				for (int i = 0; i < 36; i++)
					UltimateFxStage.Emit(sparks, hand, Random.onUnitSphere * Random.Range(2.5f, 6f), Random.Range(0.04f, 0.09f), Random.Range(0.18f, 0.35f), new Color(1f, 0.86f, 0.5f, 1f));
			});
			ComposeNeedles(stage);
		}

		// 뽑는 순간 가슴에서 사방으로 곧게 뻗는 바늘 빛. 한 프레임에 다 뻗고 안쪽부터 사라짐
		private void ComposeNeedles(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Material material = Energy(run, FRONT_QUEUE + 2);
			material.SetFloat("_DissolveFromTip", 0f);
			material.SetVector(TEXELS_ID, new Vector4(36f, 2f, 0f, 0f));
			Mesh mesh = Keep(run, FormMeshes.Sheath(2, 8, 1f));
			List<(Transform needle, float angle, float length)> needles = new();
			for (int i = 0; i < needleCount; i++)
			{
				GameObject needle = UltimateFxStage.CreateMeshObject("FormNeedle", run.Root, mesh, material);
				needle.SetActive(false);
				float angle = (i + Random.Range(-0.3f, 0.3f)) / needleCount * 360f;
				needles.Add((needle.transform, angle, Random.Range(needleLength.x, needleLength.y)));
			}
			const float SECONDS = 0.3f;
			run.Track(DrawTime, SECONDS, progress =>
			{
				Pose pose = PoseNow(run);
				Vector3 origin = Chest(pose) - pose.Away * 0.25f;
				foreach ((Transform needle, float angle, float length) in needles)
				{
					needle.gameObject.SetActive(true);
					Vector3 direction = Quaternion.AngleAxis(angle, pose.Away) * pose.Up;
					needle.SetPositionAndRotation(origin, Quaternion.LookRotation(pose.Away, direction));
					needle.localScale = new Vector3(0.07f, length * (1f + 0.25f * progress), 0.07f);
				}
				material.SetFloat(DISSOLVE_ID, UltimateFxRun.Span(progress, 0.25f, 1f));
			}, () =>
			{
				foreach ((Transform needle, float _, float _) in needles)
					run.Destroy(needle.gameObject);
			});
		}

		private void ComposeWand(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			float appear = ImpactTime + barBreakDelay + barBreakSeconds * 0.5f;
			float show = ImpactTime + wingHold + wingBreakSeconds;
			float end = ImpactTime + wandHold;
			Material material = Solid(run, Vector3.one * (wandSize * texelsPerUnit));
			material.SetVector(DISSOLVE_RANGE_ID, new Vector4(-0.42f, 0.2f, 0f, 0f));
			material.SetFloat("_Outline", 0.045f);
			GameObject wand = UltimateFxStage.CreateMeshObject("FormWand", run.Root, Keep(run, FormMeshes.Wand()), material);
			wand.SetActive(false);
			ParticleSystem glint = Streaks(run, "FormWandGlint", 40, 0.05f);
			run.At(appear, () =>
			{
				Vector3 hand = Hand(PoseNow(run));
				for (int i = 0; i < 24; i++)
					UltimateFxStage.Emit(glint, hand, Random.onUnitSphere * Random.Range(2f, 5f), Random.Range(0.05f, 0.1f), Random.Range(0.2f, 0.4f), new Color(1f, 0.86f, 0.5f, 1f));
			});
			run.At(show, () =>
			{
				// 깃이 걷힌 뒤 총구가 한 번 빛남
				Pose pose = PoseNow(run);
				Vector3 muzzle = Hand(pose) + pose.Right * wandSize * 0.85f;
				for (int i = 0; i < 16; i++)
					UltimateFxStage.Emit(glint, muzzle, Random.onUnitSphere * Random.Range(1.5f, 4f), Random.Range(0.05f, 0.1f), Random.Range(0.2f, 0.35f), new Color(1f, 0.9f, 0.6f, 1f));
			});
			run.Track(appear, end - appear, _ =>
			{
				Pose pose = PoseNow(run);
				float now = run.Now;
				wand.SetActive(true);
				float pop = OverShoot(UltimateFxRun.Span(now, appear, appear + 0.18f), 0.35f);
				float birth = Mathf.Max(Spike(now, appear, 0.001f, 0.12f), 0.6f * Spike(now, show, 0.02f, 0.1f));
				Vector3 position = Hand(pose) + pose.Up * (0.05f * Mathf.Sin(now * 5f)) - pose.Up * 0.12f;
				wand.transform.SetPositionAndRotation(position, pose.Facing * Quaternion.Euler(0f, 0f, 14f));
				wand.transform.localScale = Vector3.one * wandSize * pop;
				material.SetColor(EMISSION_ID, coreColor);
				material.SetFloat(HEAT_ID, birth);
				material.SetFloat(DISSOLVE_ID, UltimateFxRun.Span(now, end - 0.3f, end));
			}, () => run.Destroy(wand));
		}
	}
}
