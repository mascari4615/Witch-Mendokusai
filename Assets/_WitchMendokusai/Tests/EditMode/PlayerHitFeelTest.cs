using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai.Tests
{
	// 플레이어 피격 화면 연출의 공용 부품. 트라우마 흔들림, 카메라 손 여럿 겹침, 몸 번쩍임 되돌림 (2026-11-21 피격 연출 강화)
	public class PlayerHitFeelTest
	{
		private const string CONFIG_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Survival/SurvivalFeelConfig.asset";
		private static readonly MethodInfo ON_BEGIN = typeof(UltimateFeelCamera).GetMethod("OnBegin", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly MethodInfo ON_END = typeof(UltimateFeelCamera).GetMethod("OnEnd", BindingFlags.Instance | BindingFlags.NonPublic);

		private static UltimateScreenFeel.TraumaSettings Settings() => new PlayerHitFeelSettings().Trauma;

		[Test]
		public void Trauma_stays_under_cap_on_repeated_hits()
		{
			TraumaShake shake = new(1f);
			for (int i = 0; i < 10; i++)
				shake.Add(0.85f, 0.9f);
			Assert.That(shake.Trauma, Is.EqualTo(0.9f).Within(1e-5f));
		}

		[Test]
		public void Trauma_decays_to_zero_and_offset_vanishes()
		{
			UltimateScreenFeel.TraumaSettings settings = Settings();
			TraumaShake shake = new(1f);
			shake.Add(0.85f);
			float now = 0f;
			for (int i = 0; i < 60; i++)
			{
				now += 1f / 60f;
				shake.Step(settings, now, 1f / 60f, 0f, 1f, out _, out _);
			}
			shake.Step(settings, now, 1f / 60f, 0f, 1f, out Vector2 offset, out float roll);
			Assert.That(shake.Trauma, Is.EqualTo(0f));
			Assert.That(offset, Is.EqualTo(Vector2.zero));
			Assert.That(roll, Is.EqualTo(0f));
		}

		[Test]
		public void Shake_strength_zero_means_no_motion()
		{
			TraumaShake shake = new(1f);
			shake.Add(0.9f);
			shake.Step(Settings(), 0.37f, 1f / 60f, 0f, 0f, out Vector2 offset, out float roll);
			Assert.That(offset, Is.EqualTo(Vector2.zero));
			Assert.That(roll, Is.EqualTo(0f));
		}

		[Test]
		public void Hit_shake_peak_is_visible_size()
		{
			// 실측 쿼터뷰 흔들림 0.8~1.5% 화면 폭 (ult-feel-20261120). 가장 큰 피격의 첫 몇 프레임 최대 이동이 0.3% 이상
			UltimateScreenFeel.TraumaSettings settings = Settings();
			PlayerHitFeelSettings hit = new();
			TraumaShake shake = new(73.1f);
			shake.Add(hit.TraumaAtMax, hit.TraumaCap);
			float peak = 0f;
			for (int i = 1; i <= 6; i++)
			{
				shake.Step(settings, i / 60f, 1f / 60f, 0f, 1f, out Vector2 offset, out _);
				peak = Mathf.Max(peak, offset.magnitude);
			}
			Assert.That(peak, Is.GreaterThan(0.003f));
			Assert.That(peak, Is.LessThan(settings.MaxOffset * 1.5f));
		}

		[Test]
		public void Strength_follows_damage_ratio()
		{
			PlayerHitFeelSettings settings = new() { FullStrengthDamageRatio = 0.25f };
			Assert.That(settings.StrengthOf(0, 100), Is.EqualTo(0f));
			Assert.That(settings.StrengthOf(10, 100), Is.EqualTo(0.4f).Within(1e-5f));
			Assert.That(settings.StrengthOf(50, 100), Is.EqualTo(1f));
		}

		[Test]
		public void Two_camera_hands_restore_original_once()
		{
			GameObject cameraObject = new("PlayerHitFeelTestCamera");
			try
			{
				Camera camera = cameraObject.AddComponent<Camera>();
				Vector3 origin = new(1f, 5f, -7f);
				Quaternion rotation = Quaternion.Euler(40f, 10f, 0f);
				camera.transform.SetPositionAndRotation(origin, rotation);
				float fieldOfView = camera.fieldOfView;
				UltimateFeelCamera first = new(camera, false);
				UltimateFeelCamera second = new(camera, false);
				try
				{
					first.Set(Vector3.zero, new Vector2(0.01f, 0f), 1f, 0.05f, 0f);
					second.Set(Vector3.zero, new Vector2(0f, 0.01f), -0.5f, 0f, 0f);
					object[] args = { default(ScriptableRenderContext), camera };
					ON_BEGIN.Invoke(first, args);
					ON_BEGIN.Invoke(second, args);
					Assert.That(Vector3.Distance(camera.transform.position, origin), Is.GreaterThan(0.001f));
					// 구독 순서와 상관없이 먼저 불린 쪽이 원래 값으로
					ON_END.Invoke(first, args);
					Assert.That(Vector3.Distance(camera.transform.position, origin), Is.LessThan(1e-5f));
					Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(1e-3f));
					Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView).Within(1e-4f));
					ON_END.Invoke(second, args);
					Assert.That(Vector3.Distance(camera.transform.position, origin), Is.LessThan(1e-5f));

					// 값이 전부 0 이면 얹지 않음
					first.Set(Vector3.zero, Vector2.zero, 0f, 0f, 0f);
					second.Set(Vector3.zero, Vector2.zero, 0f, 0f, 0f);
					ON_BEGIN.Invoke(first, args);
					Assert.That(Vector3.Distance(camera.transform.position, origin), Is.LessThan(1e-6f));
					ON_END.Invoke(first, args);
				}
				finally
				{
					first.Dispose();
					second.Dispose();
				}
			}
			finally
			{
				Object.DestroyImmediate(cameraObject);
			}
		}

		[Test]
		public void Body_flash_restores_rest_colors()
		{
			GameObject root = new("PlayerHitFeelTestBody");
			Material material = new(Shader.Find("Universal Render Pipeline/Lit"));
			try
			{
				GameObject meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				meshObject.transform.SetParent(root.transform, false);
				MeshRenderer meshRenderer = meshObject.GetComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = material;
				Color tint = new(0.5f, 0.6f, 0.7f, 1f);
				MaterialPropertyBlock block = new();
				block.SetColor("_BaseColor", tint);
				meshRenderer.SetPropertyBlock(block, 0);

				GameObject spriteObject = new("Sprite", typeof(SpriteRenderer));
				spriteObject.transform.SetParent(root.transform, false);
				SpriteRenderer sprite = spriteObject.GetComponent<SpriteRenderer>();
				Color spriteRest = new(1f, 1f, 1f, 0.8f);
				sprite.color = spriteRest;

				PlayerBodyFlash flash = new();
				Color flashColor = new PlayerHitFeelSettings().BodyFlashColor;
				flash.Set(root.transform, new[] { sprite }, flashColor, 1f);
				meshRenderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetColor("_BaseColor").r, Is.GreaterThan(tint.r * 2f));
				Assert.That(sprite.color.g, Is.LessThan(0.5f));
				Assert.That(sprite.color.a, Is.EqualTo(spriteRest.a).Within(1e-5f));

				flash.Set(root.transform, new[] { sprite }, flashColor, 0f);
				meshRenderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(tint));
				Assert.That(sprite.color, Is.EqualTo(spriteRest));
				Assert.That(flash.IsFlashing, Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(material);
			}
		}

		[Test]
		public void Config_links_comfort_and_keeps_low_health_warning()
		{
			SurvivalFeelConfig config = AssetDatabase.LoadAssetAtPath<SurvivalFeelConfig>(CONFIG_PATH);
			Assert.That(config, Is.Not.Null);
			Assert.That(config.Comfort, Is.Not.Null, "세기 줄이기 에셋 (UltFeel_Comfort) 연결");
			Assert.That(config.LowHealthWarning, Is.True);
			Assert.That(config.PlayerHit.HitStopAtMax, Is.LessThanOrEqualTo(0.13f), "정지는 24fps 3장 이하");
		}
	}
}
