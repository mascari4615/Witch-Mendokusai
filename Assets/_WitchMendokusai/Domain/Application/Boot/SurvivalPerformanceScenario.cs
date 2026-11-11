using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// 명시 격리 프로필에서만 실행하는 Editor/Player 공통 성능 fixture
	public static class SurvivalPerformanceScenario
	{
		private const string SCENARIO = "survival-perf";
		private const string PROFILE = "WM Perf Compare20261111";
		private const int SEED = 20261111;
		private const int POPULATION = 180;
		private const float WARMUP_SECONDS = 5f;
		private const float MEASURE_SECONDS = 5f;
		private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

		private sealed class Counter : IDisposable
		{
			public string Name;
			public string Unit;
			public bool Available;
			public string Error;
			public ProfilerRecorder Recorder;
			public double Sum;
			public int Samples;
			public void Dispose() { if (Recorder.Valid) Recorder.Dispose(); }
		}

		private sealed class ScaleSamples
		{
			public int Frames;
			public int SlowedFrames;
			public float Minimum = float.MaxValue;
			public float Maximum;
			public double Sum;
			public double UnscaledSeconds;
			public double ScaledSeconds;
			public void Add()
			{
				float scale = Time.timeScale;
				Frames++;
				if (scale < 1f) SlowedFrames++;
				Minimum = Mathf.Min(Minimum, scale);
				Maximum = Mathf.Max(Maximum, scale);
				Sum += scale;
				UnscaledSeconds += Time.unscaledDeltaTime;
				ScaledSeconds += Time.unscaledDeltaTime * scale;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void Install()
		{
			if (BootMode.IsDeterministic == false || Environment.GetEnvironmentVariable("WM_BOOT_SCENARIO") != SCENARIO)
				return;
			BootSmokeSentinel.ScenarioName = SCENARIO;
			BootSmokeSentinel.ScenarioRoutine = Run;
		}

		// 내부 iterator 예외도 포착, 종료 권한은 기존 센티넬에 유지
		private static IEnumerator Run()
		{
			List<Counter> counters = new();
			Dictionary<string, object> report = new()
			{
				["scenario"] = SCENARIO, ["revision"] = Environment.GetEnvironmentVariable("WM_PERF_REVISION"),
				["seed"] = SEED, ["controlledFixture"] = true, ["healthInjected"] = true,
				["schemaVersion"] = 2,
				["frameSelection"] = "Yield one frame, reject menu/loading pause or population change, retain unpaused hitstop/slow-motion frames and record timeScale; sample unscaledDeltaTime with identical guards in Editor and Player"
			};
			string resultPath = Environment.GetEnvironmentVariable("WM_PERF_RESULT");
			bool success = false;
			Exception failure = null;
			double deadline = Time.realtimeSinceStartupAsDouble + 300d;
			IEnumerator routine = Execute(report, counters, resultPath, deadline);
			while (true)
			{
				bool moved = false;
				object current = null;
				try
				{
					if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Scenario 300s deadline");
					moved = routine.MoveNext();
					if (moved) current = routine.Current;
					else success = true;
				}
				catch (Exception exception) { failure = exception; }
				if (failure != null || moved == false) break;
				yield return current;
			}
			foreach (Counter counter in counters) counter.Dispose();
			(routine as IDisposable)?.Dispose();
			report["result"] = success ? "PASS" : "FAIL";
			report["failure"] = failure?.ToString();
			try
			{
				if (string.IsNullOrWhiteSpace(resultPath) || Path.IsPathRooted(resultPath) == false)
					throw new InvalidOperationException("WM_PERF_RESULT must be an explicit absolute path");
				if (File.Exists(resultPath)) throw new IOException("Result already exists; refusing overwrite");
				Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
				File.WriteAllText(resultPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
			}
			catch (Exception exception)
			{
				success = false;
				failure = exception;
				UnityEngine.Debug.LogError("[SURVIVAL-PERF] Result write failed: " + exception);
			}
			BootSmokeSentinel.ReportScenario(success, success ? resultPath : failure?.ToString());
		}

		private static IEnumerator Execute(Dictionary<string, object> report, List<Counter> counters, string resultPath, double deadline)
		{
			float warmupSeconds = ReadDuration("WM_PERF_WARMUP_SECONDS", WARMUP_SECONDS, 0f, 60f);
			float measureSeconds = ReadDuration("WM_PERF_MEASURE_SECONDS", MEASURE_SECONDS, 1f, 120f);
			report["warmupSeconds"] = warmupSeconds;
			report["measureSeconds"] = measureSeconds;
			Require(Application.productName == PROFILE, "Isolated product profile required: " + Application.productName);
			Require(Application.persistentDataPath.Replace('\\', '/').TrimEnd('/').EndsWith("/" + PROFILE, StringComparison.Ordinal), "Isolated persistentDataPath required");
			Require(string.IsNullOrWhiteSpace(resultPath) == false && Path.IsPathRooted(resultPath), "Absolute WM_PERF_RESULT required");
			Require(File.Exists(resultPath) == false, "Result already exists");
			Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Rendered graphics device required");
			UnityEngine.Random.InitState(SEED);
			int quality = Array.FindIndex(QualitySettings.names, name => string.Equals(name, "Medium", StringComparison.OrdinalIgnoreCase));
			Require(quality >= 0, "Medium quality level missing");
			QualitySettings.SetQualityLevel(quality, true);
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = -1;
			Application.runInBackground = true;
			Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
			report["audioBeforeInitialization"] = Map("volume", AudioListener.volume, "pause", AudioListener.pause);
			// FMOD 초기화 중 다른 오디오 부팅 코드 실행 가능, 엔진 listener 설정은 마지막에 적용
			FMODUnity.RuntimeManager.MuteAllEvents(true);
			FMOD.Studio.Bus masterBus = FMODUnity.RuntimeManager.GetBus("bus:/");
			FMOD.RESULT muteResult = masterBus.setMute(true);
			FMOD.RESULT readMuteResult = masterBus.getMute(out bool masterMuted);
			bool runtimeMuted = FMODUnity.RuntimeManager.IsMuted;
			report["audioAfterFmodInitialization"] = Map("volume", AudioListener.volume, "pause", AudioListener.pause, "runtimeMuted", runtimeMuted, "masterMuted", masterMuted, "setMuteResult", muteResult.ToString(), "getMuteResult", readMuteResult.ToString());
			AudioListener.volume = 0f;
			AudioListener.pause = true;
			report["audioVerified"] = Map("volume", AudioListener.volume, "pause", AudioListener.pause, "runtimeMuted", runtimeMuted, "masterMuted", masterMuted, "setMuteResult", muteResult.ToString(), "getMuteResult", readMuteResult.ToString());
			// 내장 오디오 비활성 프로젝트는 volume setter 무시 가능, 실제 FMOD 버스 mute와 listener pause 검증
			Require(muteResult == FMOD.RESULT.OK && readMuteResult == FMOD.RESULT.OK && masterMuted && runtimeMuted
				&& AudioListener.pause, "Audio mute verification failed; see audioVerified snapshot");
			DungeonManager dungeon = DungeonManager.Instance;
			Require(dungeon != null && dungeon.IsDungeon == false, "Fresh WorldReady outside dungeon required");
			dungeon.StartDungeon(SOHelper.GetDungeon(0), runMode: DungeonRunMode.Random, threatLevel: 0);
			double enterDeadline = Math.Min(deadline, Time.realtimeSinceStartupAsDouble + 30d);
			while (dungeon.IsDungeon == false && Time.realtimeSinceStartupAsDouble < enterDeadline) yield return null;
			Require(dungeon.IsDungeon, "Dungeon enter timeout");
			yield return null;
			yield return null;
			MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
			Require(spawner != null, "MonsterSpawner missing");
			spawner.StopWave();
			spawner.enabled = false;
			((IList)RequireField(typeof(MonsterSpawner), "waves").GetValue(spawner)).Clear();
			((IList)RequireField(typeof(MonsterSpawner), "runEvents").GetValue(spawner)).Clear();
			RequireField(typeof(MonsterSpawner), "eventTable").SetValue(spawner, null);
			foreach (GameObject monster in ObjectBufferManager.GetObjects(ObjectType.Monster).ToArray()) monster.SetActive(false);
			CardManager cards = UnityEngine.Object.FindAnyObjectByType<CardManager>();
			Require(cards != null, "CardManager missing");
			cards.CancelSelection();
			TimeManager timeManager = TimeManager.Instance;
			FieldInfo transitioning = RequireField(typeof(DungeonManager), "isTransitioning");
			double settleStart = Time.realtimeSinceStartupAsDouble;
			double settleDeadline = Math.Min(deadline, settleStart + 10d);
			int stableFrames = 0;
			int cancelledSelections = 0;
			while (stableFrames < 3 && Time.realtimeSinceStartupAsDouble < settleDeadline)
			{
				if (timeManager.IsPaused || cards.CurPanelType != CardPanelType.None || cards.IsChestOpen)
				{
					// 선택 소유 정지만 해제, 로딩 등 다른 정지 소유자는 그대로 대기
					cards.CancelSelection();
					cancelledSelections++;
				}
				bool ready = dungeon.IsDungeon && (bool)transitioning.GetValue(dungeon) == false
					&& timeManager.IsPaused == false && cards.CurPanelType == CardPanelType.None && cards.IsChestOpen == false;
				stableFrames = ready ? stableFrames + 1 : 0;
				yield return null;
			}
			report["initialSettle"] = Map("wallSeconds", Time.realtimeSinceStartupAsDouble - settleStart, "stableFrames", stableFrames, "cancelledSelections", cancelledSelections, "timeoutSeconds", 10);
			Require(stableFrames >= 3 && timeManager.IsPaused == false, "Dungeon transition/card settle timeout");
			Require(SOManager.Instance.SelectedCardBuffer.Data.Count == 0, "Fresh empty card buffer required");
			if (DataManager.Instance.RunLoadout.Equipments().Any(equipment => equipment != null && equipment.ID == 20000009) == false)
			{
				MethodInfo acquire = typeof(CardManager).GetMethod("AcquireEquipment", PRIVATE_INSTANCE);
				Require(acquire != null, "AcquireEquipment fixture seam missing");
				acquire.Invoke(cards, new object[] { RunLoadout.GetEquipment(20000009) });
			}
			for (int index = 0; index < 3; index++) SOManager.Instance.SelectedCardBuffer.Add(SOHelper.GetCardData(400));
			for (int index = 0; index < 2; index++) SOManager.Instance.SelectedCardBuffer.Add(SOHelper.GetCardData(401));
			cards.CancelSelection();
			PlayerObject player = PlayerProvider.Instance.CurrentObject;
			player.UnitStat[UnitStatType.HP_MAX] = 100000;
			player.UnitStat[UnitStatType.HP_CUR] = 100000;
			Monster source = SOManager.Instance.DataSOs[typeof(Monster)].Values.Cast<Monster>().Where(monster => monster.Type == MonsterType.Normal).OrderBy(monster => monster.ID).First();
			List<MonsterObject> enemies = new();
			for (int index = 0; index < POPULATION; index++)
			{
				GameObject instance = ObjectPoolManager.Instance.Spawn(source.Prefab);
				float angle = index * 2.399963f;
				float radius = 8 + index % 8;
				instance.transform.position = player.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
				MonsterObject monster = instance.GetComponent<MonsterObject>();
				monster.SetThreat(dungeon.CurThreat);
				monster.Init(source);
				instance.SetActive(true);
				enemies.Add(monster);
			}
			yield return null;
			yield return null;
			foreach (MonsterObject monster in enemies)
			{
				monster.UnitStat[UnitStatType.HP_MAX] = 1000000;
				monster.UnitStat[UnitStatType.HP_CUR] = 1000000;
			}
			List<GameObject> population = ObjectBufferManager.GetObjects(ObjectType.Monster);
			report["fixture"] = Map("monsterId", source.ID, "count", POPULATION, "radius", "8+index%8", "goldenAngle", 2.399963f, "enemyHp", 1000000, "playerHp", 100000, "mode", "Random", "threat", dungeon.CurThreat.Level, "dungeonId", 0);
			report["before"] = State();
			double warmupStart = Time.realtimeSinceStartupAsDouble;
			ScaleSamples warmupScales = new();
			while (Time.realtimeSinceStartupAsDouble - warmupStart < warmupSeconds)
			{
				Guard(timeManager, population);
				yield return null;
				Guard(timeManager, population);
				warmupScales.Add();
			}
			report["warmupTimeScale"] = ScaleInfo(warmupScales);
			Require(Screen.width == 1920 && Screen.height == 1080, "Actual render resolution is not FHD");
			OpenCounters(counters);
			List<double> frames = new(16384);
			ScaleSamples measureScales = new();
			int[] collectionsBefore = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
			long managedBytesBefore = GC.GetTotalMemory(false);
			double startElapsed = dungeon.Context.ElapsedTime.TotalSeconds;
			double measureStart = Time.realtimeSinceStartupAsDouble;
			while (Time.realtimeSinceStartupAsDouble - measureStart < measureSeconds)
			{
				Guard(timeManager, population);
				yield return null;
				Guard(timeManager, population);
				measureScales.Add();
				frames.Add(Time.unscaledDeltaTime * 1000d);
				foreach (Counter counter in counters)
				{
					if (counter.Recorder.Valid && counter.Recorder.Count > 0)
					{
						counter.Sum += counter.Recorder.LastValue;
						counter.Samples++;
					}
				}
			}
			double measuredWallSeconds = Time.realtimeSinceStartupAsDouble - measureStart;
			long managedBytesAfter = GC.GetTotalMemory(false);
			int[] collectionsAfter = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
			report["managedMemory"] = Map("beforeBytes", managedBytesBefore, "afterBytes", managedBytesAfter, "collectionsBefore", collectionsBefore, "collectionsAfter", collectionsAfter, "forcedCollection", false);
			double endElapsed = dungeon.Context.ElapsedTime.TotalSeconds;
			Require(frames.Count > 0, "No frame samples");
			frames.Sort();
			report["measurement"] = Map("frameCount", frames.Count, "frameSumMs", frames.Sum(), "fps", frames.Count * 1000d / frames.Sum(), "averageMs", frames.Average(), "p50Ms", frames[(int)((frames.Count - 1) * .5)], "p95Ms", frames[(int)((frames.Count - 1) * .95)], "maxMs", frames.Last(), "wallSeconds", measuredWallSeconds, "startElapsed", startElapsed, "endElapsed", endElapsed, "population", population.Count, "damageObjects", UnityEngine.Object.FindObjectsByType<DamagingObject>(FindObjectsInactive.Exclude).Length);
			report["measurementTimeScale"] = ScaleInfo(measureScales);
			report["counters"] = counters.Select(counter => Map("Name", counter.Name, "Available", counter.Available, "valid", counter.Recorder.Valid, "Unit", counter.Unit, "Samples", counter.Samples, "Error", counter.Error, "average", counter.Samples > 0 ? (double?)(counter.Sum / counter.Samples) : null)).ToArray();
			report["after"] = State();
			report["selected"] = SOManager.Instance.SelectedCardBuffer.Data.GroupBy(card => card.ID).Select(group => Map("id", group.Key, "count", group.Count())).ToArray();
			report["loadout"] = DataManager.Instance.RunLoadout.Equipments().Select(equipment => equipment == null ? (int?)null : equipment.ID).ToArray();
			report["stats"] = Enum.GetValues(typeof(UnitStatType)).Cast<UnitStatType>().ToDictionary(stat => stat.ToString(), stat => player.UnitStat[stat]);
			Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
			string screenshot = Path.Combine(Path.GetDirectoryName(resultPath), Path.GetFileNameWithoutExtension(resultPath) + "-" + Guid.NewGuid().ToString("N") + ".png");
			report["screenshot"] = screenshot;
			ScreenCapture.CaptureScreenshot(screenshot);
			while (File.Exists(screenshot) == false && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
			Require(File.Exists(screenshot), "Screenshot deadline");
		}

		private static void OpenCounters(List<Counter> counters)
		{
			List<ProfilerRecorderHandle> handles = new();
			ProfilerRecorderHandle.GetAvailable(handles);
			foreach (string name in new[] { "Main Thread", "Render Thread", "GPU Frame Time", "Physics.Simulate", "GC.Alloc" })
			{
				Counter counter = new() { Name = name };
				counters.Add(counter);
				foreach (ProfilerRecorderHandle handle in handles)
				{
					ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
					if (description.Name != name) continue;
					counter.Available = true;
					counter.Unit = description.UnitType.ToString();
					try { counter.Recorder = ProfilerRecorder.StartNew(description.Category, description.Name, 1); }
					catch (Exception exception) { counter.Error = exception.GetType().Name + ": " + exception.Message; }
					break;
				}
			}
		}

		private static float ReadDuration(string name, float fallback, float minimum, float maximum)
		{
			string text = Environment.GetEnvironmentVariable(name);
			if (string.IsNullOrWhiteSpace(text)) return fallback;
			Require(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)
				&& float.IsNaN(seconds) == false && float.IsInfinity(seconds) == false
				&& seconds >= minimum && seconds <= maximum, name + " is outside the diagnostic duration range");
			return seconds;
		}

		private static object State()
		{
			Camera camera = Camera.main;
			RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
			PropertyInfo renderScale = pipeline == null ? null : pipeline.GetType().GetProperty("renderScale");
			Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
			return Map(
				"pid", System.Diagnostics.Process.GetCurrentProcess().Id, "editor", Application.isEditor, "development", UnityEngine.Debug.isDebugBuild,
				"unity", Application.unityVersion, "product", Application.productName, "savePath", Application.persistentDataPath,
				"cpu", SystemInfo.processorType, "processors", SystemInfo.processorCount, "gpu", SystemInfo.graphicsDeviceName, "graphicsApi", SystemInfo.graphicsDeviceType.ToString(), "driver", SystemInfo.graphicsDeviceVersion, "os", SystemInfo.operatingSystem,
				"jobWorkerCount", Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount, "jobWorkerMaximumCount", Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount,
				"scene", SceneManager.GetActiveScene().path, "dollId", DataManager.Instance.CurDollID, "width", Screen.width, "height", Screen.height, "fullscreen", Screen.fullScreenMode.ToString(),
				"quality", QualitySettings.names[QualitySettings.GetQualityLevel()], "pipeline", pipeline == null ? null : pipeline.name, "pipelineType", pipeline == null ? null : pipeline.GetType().FullName, "renderScale", renderScale?.GetValue(pipeline),
				"antiAliasing", QualitySettings.antiAliasing, "shadowDistance", QualitySettings.shadowDistance, "shadows", QualitySettings.shadows.ToString(), "colorSpace", QualitySettings.activeColorSpace.ToString(),
				"vSyncCount", QualitySettings.vSyncCount, "targetFrameRate", Application.targetFrameRate, "runInBackground", Application.runInBackground, "timeScale", Time.timeScale, "fixedDeltaTime", Time.fixedDeltaTime,
				"defaultSolverIterations", Physics.defaultSolverIterations, "defaultSolverVelocityIterations", Physics.defaultSolverVelocityIterations, "volume", AudioListener.volume, "pause", AudioListener.pause, "fmodMute", FMODUnity.RuntimeManager.IsMuted,
				"camera", camera == null ? null : Map("name", camera.name, "background", camera.backgroundColor.ToString(), "clear", camera.clearFlags.ToString(), "cullingMask", camera.cullingMask, "allowHDR", camera.allowHDR, "allowMSAA", camera.allowMSAA, "fieldOfView", camera.fieldOfView, "position", camera.transform.position.ToString(), "rotation", camera.transform.eulerAngles.ToString()),
				"ambientMode", RenderSettings.ambientMode.ToString(), "ambient", RenderSettings.ambientLight.ToString(), "fog", RenderSettings.fog,
				"bake", Map(
					"mode", LightmapSettings.lightmapsMode.ToString(), "probeCount", LightmapSettings.lightProbes == null ? 0 : LightmapSettings.lightProbes.count,
					"maps", LightmapSettings.lightmaps.Select((map, index) => Map("index", index, "color", TextureInfo(map.lightmapColor), "direction", TextureInfo(map.lightmapDir), "shadowMask", TextureInfo(map.shadowMask))).ToArray(),
					"renderers", renderers.Length, "lightmappedRenderers", renderers.Count(renderer => renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length),
					"rendererIndices", renderers.GroupBy(renderer => renderer.lightmapIndex).Select(group => Map("index", group.Key, "count", group.Count())).ToArray()),
				"lights", UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Select(light => Map("name", light.name, "enabled", light.enabled, "intensity", light.intensity, "range", light.range, "type", light.type.ToString(), "color", light.color.ToString(), "shadows", light.shadows.ToString(), "baked", light.bakingOutput.isBaked, "bakeType", light.bakingOutput.lightmapBakeType.ToString())).ToArray());
		}
		private static object TextureInfo(Texture texture)
		{
			return texture == null ? null : Map("name", texture.name, "width", texture.width, "height", texture.height);
		}

		// Explicit keys avoid IL2CPP-stripped constructor/member names in JSON contracts.
		private static Dictionary<string, object> Map(params object[] entries)
		{
			Dictionary<string, object> result = new();
			for (int index = 0; index < entries.Length; index += 2) result.Add((string)entries[index], entries[index + 1]);
			return result;
		}

		private static Dictionary<string, object> ScaleInfo(ScaleSamples samples) => Map(
			"Frames", samples.Frames, "SlowedFrames", samples.SlowedFrames, "Minimum", samples.Minimum,
			"Maximum", samples.Maximum, "Sum", samples.Sum, "UnscaledSeconds", samples.UnscaledSeconds, "ScaledSeconds", samples.ScaledSeconds);

		private static FieldInfo RequireField(Type type, string name)
		{
			FieldInfo field = type.GetField(name, PRIVATE_INSTANCE);
			Require(field != null, "Fixture field missing: " + name);
			return field;
		}
		private static void Guard(TimeManager timeManager, List<GameObject> population)
		{
			Require(timeManager.IsPaused == false, "Paused measurement/warmup frame");
			if (population.Count != POPULATION) throw new InvalidOperationException("Population changed: " + population.Count);
			Require(float.IsNaN(Time.timeScale) == false && float.IsInfinity(Time.timeScale) == false && Time.timeScale >= 0f, "Invalid time scale");
		}
		private static void Require(bool condition, string reason)
		{
			if (condition == false) throw new InvalidOperationException(reason);
		}
	}
}
