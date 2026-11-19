using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// fx [이름 일부]
	/// 전투 연출 부품 (CombatFx 에셋) 을 플레이어 자리에서 미리보기 값으로 재생. 이름 없이 부르면 목록
	/// 에디터에서는 프로젝트의 부품 전부, 빌드에서는 이미 불러온 부품만
	/// </summary>
	public class FxCommand : IDevCommand
	{
		public string Name => "fx";
		public string Usage => "fx [nameFragment] | fx ultimate [nameFragment]";

		public void Execute(DevCommandContext context, string[] args)
		{
			List<CombatFx> all = FindAll();
			if (args.Length < 1)
			{
				context.LogInfo($"전투 연출 부품 {all.Count}개:");
				foreach (CombatFx fx in all)
					context.LogInfo("  " + fx.name);
				return;
			}

			if (string.Equals(args[0], "ultimate", StringComparison.OrdinalIgnoreCase))
			{
				PlayUltimate(context, all, args.Length > 1 ? args[1] : null);
				return;
			}

			List<CombatFx> matches = all.Where(fx => fx.name.IndexOf(args[0], StringComparison.OrdinalIgnoreCase) >= 0).ToList();
			CombatFx exact = matches.FirstOrDefault(fx => string.Equals(fx.name, args[0], StringComparison.OrdinalIgnoreCase));
			if (exact != null)
				matches = new List<CombatFx> { exact };

			if (matches.Count == 0)
			{
				context.LogError($"매칭되는 부품 없음: '{args[0]}'");
				return;
			}
			if (matches.Count > 1)
			{
				context.LogWarn($"여러 후보 ({matches.Count}):");
				foreach (CombatFx fx in matches)
					context.LogWarn("  " + fx.name);
				return;
			}

			matches[0].PlayPreview();
			context.LogSuccess($"{matches[0].name} 재생");
		}

		// fx ultimate [이름 일부]: 궁극기 연출 (UltimateFx) 을 주인공 자리에서 처음부터 끝까지 재생. 이름이 없으면 첫 번째
		private static void PlayUltimate(DevCommandContext context, List<CombatFx> all, string fragment)
		{
			List<UltimateFx> ultimates = all.OfType<UltimateFx>()
				.Where(fx => string.IsNullOrEmpty(fragment) || fx.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
				.ToList();
			if (ultimates.Count == 0)
			{
				context.LogError($"궁극기 연출 부품 없음: '{fragment}'. 메뉴 WM/CombatFx/Build Ultimate Presets 로 만들기");
				return;
			}
			if (ultimates.Count > 1 && string.IsNullOrEmpty(fragment) == false)
			{
				context.LogWarn($"여러 후보 ({ultimates.Count}):");
				foreach (UltimateFx fx in ultimates)
					context.LogWarn("  " + fx.name);
				return;
			}
			ultimates[0].PlayPreview();
			context.LogSuccess($"{ultimates[0].name} 재생 (약 {ultimates[0].DurationSeconds:0.0}초)");
		}

		public IEnumerable<string> Suggest(string[] partial)
		{
			if (partial.Length != 1)
				return Enumerable.Empty<string>();
			return FindAll()
				.Select(fx => fx.name)
				.Where(name => name.IndexOf(partial[0], StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static List<CombatFx> FindAll()
		{
#if UNITY_EDITOR
			List<CombatFx> result = new();
			foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(CombatFx)))
			{
				CombatFx fx = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatFx>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
				if (fx != null)
					result.Add(fx);
			}
			return result.OrderBy(fx => fx.name).ToList();
#else
			return CombatFx.Loaded.OrderBy(fx => fx.name).ToList();
#endif
		}
	}
}
