using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-083 Phase B — 게임 시작 시 (AfterAssembliesLoaded) 모든 IMod 구현을 reflection 으로 발견 + Initialize 호출.
	/// 본 ModLoader 는 Domain (Assembly-CSharp.dll) 안 — Mods.Sample.dll 등은 별 dll 로 빌드되어 AppDomain 에 자동 로드됨 (autoReferenced=false 라도 Unity 가 dll 자체는 로드).
	/// </summary>
	public static class ModLoader
	{
		/// <summary>모드가 등록한 콘텐츠 (게임/테스트 조회점 = Bridge). LoadMods 후 채워짐.</summary>
		public static ModContentRegistry Content { get; private set; } = new ModContentRegistry();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void LoadMods()
		{
			Type modInterface = typeof(IMod);
			ModContentRegistry registry = new ModContentRegistry();
			Content = registry;

			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Type[] modTypes = assemblies
				.Where(a => a.GetName().Name.StartsWith("WitchMendokusai.Mods."))
				.SelectMany(a => SafeGetTypes(a))
				.Where(t => t != null && modInterface.IsAssignableFrom(t) && t.IsClass && t.IsAbstract == false)
				.ToArray();

			foreach (Type modType in modTypes)
			{
				try
				{
					IMod mod = (IMod)Activator.CreateInstance(modType);
					mod.Initialize(registry);
				}
				catch (Exception ex)
				{
					Debug.LogError($"[ModLoader] {modType.FullName} Initialize 실패 — {ex.Message}\n{ex.StackTrace}");
				}
			}

			Debug.Log($"[ModLoader] {modTypes.Length} mod 로드 — 등록 콘텐츠: quest {registry.RegisteredQuests.Count}");
		}

		private static Type[] SafeGetTypes(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				return ex.Types.Where(t => t != null).ToArray();
			}
		}
	}
}
