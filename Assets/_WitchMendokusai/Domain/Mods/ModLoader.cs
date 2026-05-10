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
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void LoadMods()
		{
			Type modInterface = typeof(IMod);

			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Type[] modTypes = assemblies
				.Where(a => a.GetName().Name.StartsWith("WitchMendokusai.Mods."))
				.SelectMany(a => SafeGetTypes(a))
				.Where(t => t != null && modInterface.IsAssignableFrom(t) && t.IsClass && t.IsAbstract == false)
				.ToArray();

			Debug.Log($"[ModLoader] {modTypes.Length} mod 발견 (IMod 구현 in WitchMendokusai.Mods.* assembly)");

			foreach (Type modType in modTypes)
			{
				try
				{
					IMod mod = (IMod)Activator.CreateInstance(modType);
					mod.Initialize();
				}
				catch (Exception ex)
				{
					Debug.LogError($"[ModLoader] {modType.FullName} Initialize 실패 — {ex.Message}\n{ex.StackTrace}");
				}
			}
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
