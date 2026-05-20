using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-084 Phase B + C — UGC Seed first-use 회귀 게이트.
	/// Phase A (SeedSaveData POCO + UGCJsonLoader + UGCJsonValidator) 위에서:
	///   - Phase B: 첫 sample (ServerData/UGC/Samples/seed_001_forest.json) 가 *게임 안* (TerrainParameters DataSO) 로 흐른다는 것
	///   - Phase C: UGC JSON 이 SeedSaveData schema 밖 필드를 *건드릴 수 없다* 는 것 (sandbox)
	/// 을 결정적으로 잠근다.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class UGCSeedLoaderTest
	{
		private const string SAMPLE_FILE = "seed_001_forest.json";

		[Test]
		public void TryLoadSeedFromSample_ForestStarter_ReturnsExpectedFields()
		{
			bool ok = UGCJsonLoader.TryLoadSeedFromSample(SAMPLE_FILE, out SeedSaveData seedData, out string error);

			Assert.That(ok, Is.True, $"sample 로딩 실패: {error}");
			Assert.That(seedData, Is.Not.Null);
			Assert.That(seedData.name, Is.EqualTo("Forest Starter"));
			Assert.That(seedData.octaves, Is.EqualTo(4));
			Assert.That(seedData.frequency, Is.EqualTo(0.05f).Within(0.0001f));
			Assert.That(seedData.persistence, Is.EqualTo(0.5f).Within(0.0001f));
			Assert.That(seedData.lacunarity, Is.EqualTo(2.0f).Within(0.0001f));
			Assert.That(seedData.biomeFrequency, Is.EqualTo(0.02f).Within(0.0001f));
		}

		[Test]
		public void ApplyTo_TerrainParameters_TransfersFiveNoiseFields()
		{
			SeedSaveData seedData = new()
			{
				name = "Apply Test",
				octaves = 6,
				frequency = 0.07f,
				persistence = 0.42f,
				lacunarity = 2.5f,
				biomeFrequency = 0.015f,
			};

			TerrainParameters target = ScriptableObject.CreateInstance<TerrainParameters>();
			int originalSeed = target.Seed;
			float originalAmplitude = target.Amplitude;

			seedData.ApplyTo(target);

			Assert.That(target.Octaves, Is.EqualTo(6));
			Assert.That(target.Frequency, Is.EqualTo(0.07f).Within(0.0001f));
			Assert.That(target.Persistence, Is.EqualTo(0.42f).Within(0.0001f));
			Assert.That(target.Lacunarity, Is.EqualTo(2.5f).Within(0.0001f));
			Assert.That(target.BiomeFrequency, Is.EqualTo(0.015f).Within(0.0001f));

			// sandbox 보강 — schema 밖 필드는 변경되지 않음
			Assert.That(target.Seed, Is.EqualTo(originalSeed), "Seed 는 SeedSaveData schema 밖 — 보존되어야 함");
			Assert.That(target.Amplitude, Is.EqualTo(originalAmplitude).Within(0.0001f), "Amplitude 는 SeedSaveData schema 밖 — 보존되어야 함");
		}

		[Test]
		public void TryValidateSeed_RejectsWrongSchemaVersion()
		{
			UGCSeedManifestData manifest = MakeValidManifest();
			manifest.schemaVersion = 99;

			bool ok = UGCJsonValidator.TryValidateSeed(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("Schema version"));
		}

		[Test]
		public void TryValidateSeed_RejectsOctavesOutOfRange()
		{
			UGCSeedManifestData manifest = MakeValidManifest();
			manifest.seedData.octaves = 99;

			bool ok = UGCJsonValidator.TryValidateSeed(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("octaves"));
		}

		[Test]
		public void TryValidateSeed_RejectsEmptySeedName()
		{
			UGCSeedManifestData manifest = MakeValidManifest();
			manifest.seedData.name = "";

			bool ok = UGCJsonValidator.TryValidateSeed(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("name"));
		}

		// Phase C — sandbox 증명: SeedSaveData 에 *없는 필드* 를 JSON 에 박아도 deserialize 결과 객체엔 안 들어옴.
		// MissingMemberHandling.Ignore + POCO 필드 화이트리스트 = 컴파일러 강제 sandbox 의 *실증*.
		[Test]
		public void Deserialize_DropsOutOfSchemaFields()
		{
			string maliciousJson = @"{
				""name"": ""Sandbox Probe"",
				""octaves"": 4,
				""frequency"": 0.05,
				""persistence"": 0.5,
				""lacunarity"": 2.0,
				""biomeFrequency"": 0.02,
				""amplitude"": 9999.0,
				""arbitraryEvilField"": ""attempt-to-escape""
			}";

			JsonSerializerSettings settings = new()
			{
				MissingMemberHandling = MissingMemberHandling.Ignore,
				NullValueHandling = NullValueHandling.Include,
			};

			SeedSaveData parsed = JsonConvert.DeserializeObject<SeedSaveData>(maliciousJson, settings);

			Assert.That(parsed, Is.Not.Null);
			Assert.That(parsed.name, Is.EqualTo("Sandbox Probe"));
			Assert.That(parsed.octaves, Is.EqualTo(4));

			// SeedSaveData 에 amplitude/arbitraryEvilField 같은 필드가 없으므로 컴파일 자체로 도달 불가 —
			// 그 사실 자체가 sandbox. 본 테스트는 JSON 페이로드가 *조용히 삭제됨* 을 회귀 게이트로 잠근다.
			System.Reflection.FieldInfo amplitudeField = typeof(SeedSaveData).GetField("amplitude");
			Assert.That(amplitudeField, Is.Null, "SeedSaveData 에 amplitude 필드가 생기면 sandbox 표면 확장 — 의도적 결정 필요");
		}

		private static UGCSeedManifestData MakeValidManifest()
		{
			return new UGCSeedManifestData
			{
				schemaVersion = 1,
				seedId = 1,
				version = 1,
				author = "test",
				seedData = new SeedSaveData
				{
					name = "Valid",
					octaves = 4,
					frequency = 0.05f,
					persistence = 0.5f,
					lacunarity = 2.0f,
					biomeFrequency = 0.02f,
				},
			};
		}
	}
}
