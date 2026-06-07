using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-083 Phase B + TASK-WM-188 — IMod 구현 reflection 발견 + IModContext 기반 Initialize 트리거.
	/// 본 ModLoader 는 Domain (Assembly-CSharp.dll) 안 — Mods.Sample.dll 등은 별 dll 로 빌드되어 AppDomain 에 자동 로드됨 (autoReferenced=false 라도 Unity 가 dll 자체는 로드).
	/// 2-단계: (1) DiscoverMods @ AfterAssembliesLoaded — IMod 인스턴스 수집만, Initialize 보류 / (2) InitializeDiscoveredMods — Core 어댑터 ctor 가 IModContext 들고 호출.
	/// </summary>
	public static class ModLoader
	{
		private static readonly List<IMod> discoveredMods = new();
		private static bool initialized = false;

		public static IReadOnlyList<IMod> DiscoveredMods => discoveredMods;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void DiscoverMods()
		{
			discoveredMods.Clear();
			initialized = false;

			Type modInterface = typeof(IMod);

			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Type[] modTypes = assemblies
				.Where(assembly => assembly.GetName().Name.StartsWith("WitchMendokusai.Mods."))
				.SelectMany(assembly => SafeGetTypes(assembly))
				.Where(modType => modType != null && modInterface.IsAssignableFrom(modType) && modType.IsClass && modType.IsAbstract == false)
				.ToArray();

			foreach (Type modType in modTypes)
			{
				try
				{
					IMod mod = (IMod)Activator.CreateInstance(modType);
					discoveredMods.Add(mod);
				}
				catch (Exception exception)
				{
					Debug.LogError($"[ModLoader] {modType.FullName} 인스턴스화 실패 — {exception.Message}\n{exception.StackTrace}");
				}
			}

			Debug.Log($"[ModLoader] discovered {discoveredMods.Count} mods (IMod 구현 in WitchMendokusai.Mods.* assembly) — Initialize 보류, Host ctx 대기.");
		}

		/// <summary>
		/// Core 어댑터(ModContentRegistryHost) ctor 가 호출. IModContext 주입 시점 = QuestManager 등 deps 준비 완료 후.
		/// 멱등 — 재호출 시 no-op (도메인 reload 시 DiscoverMods 가 initialized=false 리셋).
		/// </summary>
		public static void InitializeDiscoveredMods(IModContext context)
		{
			if (initialized)
			{
				return;
			}
			initialized = true;

			foreach (IMod mod in discoveredMods)
			{
				try
				{
					mod.Initialize(context);
				}
				catch (Exception exception)
				{
					Debug.LogError($"[ModLoader] {mod.Name} Initialize 실패 — {exception.Message}\n{exception.StackTrace}");
				}
			}

			Debug.Log($"[ModLoader] Initialized {discoveredMods.Count} mods via IModContext (TASK-WM-188 seam).");
		}

		private static Type[] SafeGetTypes(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException exception)
			{
				return exception.Types.Where(type => type != null).ToArray();
			}
		}
	}
}
