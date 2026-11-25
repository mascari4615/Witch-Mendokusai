using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>
	/// trace [board | study &lt;itemRef&gt; | stop | kills]
	/// board: 조합판 글자로. study: 연구할 재료. kills: 이번 판 도구별 처치 수와 몫
	/// </summary>
	public class TraceCommand : IDevCommand
	{
		private static readonly string[] SUB_COMMANDS = { "board", "study", "stop", "kills" };

		public string Name => "trace";
		public string Usage => "trace [board | study <itemRef> | stop | kills]";

		public void Execute(DevCommandContext context, string[] args)
		{
			TraceSaveSlice traces = DataManager.Instance.FeatureSave<TraceSaveSlice>();
			string sub = args.Length > 0 ? args[0] : "board";
			if (DevCommandArgs.Is(sub, "board"))
				Board(context, traces.Notebook);
			else if (DevCommandArgs.Is(sub, "study") && args.Length > 1)
				Study(context, traces.Notebook, args[1]);
			else if (DevCommandArgs.Is(sub, "stop"))
			{
				traces.Notebook.StudyItemId = TraceNotebook.NO_STUDY;
				context.LogSuccess("연구 멈춤");
			}
			else if (DevCommandArgs.Is(sub, "kills"))
				Kills(context, traces);
			else
				context.LogError(Usage);
		}

		public IEnumerable<string> Suggest(string[] partial) =>
			partial.Length == 1 ? DevCommandArgs.StartsWith(SUB_COMMANDS, partial[0]) : Enumerable.Empty<string>();

		private static void Board(DevCommandContext context, TraceNotebook notebook)
		{
			TraceBookData book = TraceBookData.Current;
			List<TraceCombo> combos = book.ToCombos();
			foreach (TraceBookData.MaterialRow row in book.Materials)
			{
				TraceMaterial material = new(row.Item.ID, row.Need);
				string mark = notebook.StudyItemId == row.Item.ID ? "*" : " ";
				string cells = string.Join(", ", TraceRules.ALL.Select(trace => CellText(notebook, book, material, trace, combos)));
				context.LogSuccess($"{mark} {row.Item.Name}: {cells}");
			}
			TraceProgress progress = TraceRules.ProgressOf(notebook, book.ToMaterials(), combos, book.HasslesPerWarped);
			context.LogSuccess($"마법 {progress.Magics} / {book.Magics.Count}, 귀찮음 {progress.Hassles}, 삐뚤어진 마법 {progress.Warped}");
		}

		private static string CellText(TraceNotebook notebook, TraceBookData book, TraceMaterial material, TraceType trace, List<TraceCombo> combos)
		{
			switch (TraceRules.CellOf(notebook, material, trace, combos))
			{
				case TraceCellState.Magic:
					return $"{trace} {book.MagicName(material.ItemId, trace)}";
				case TraceCellState.Hassle:
					return $"{trace} 귀찮음";
				default:
					return $"{trace} {notebook.CountOf(material.ItemId, trace)}/{material.Need}";
			}
		}

		private static void Study(DevCommandContext context, TraceNotebook notebook, string reference)
		{
			ItemData target = DevItemResolver.Resolve(reference, out List<ItemData> matches);
			if (target == null)
			{
				context.LogError($"재료를 하나로 못 정함 (후보 {matches.Count}): '{reference}'");
				return;
			}
			if (TraceBookData.Current.Materials.Any(row => row.Item.ID == target.ID) == false)
			{
				context.LogError($"조합판에 없는 재료: {target.Name}");
				return;
			}
			notebook.StudyItemId = target.ID;
			context.LogSuccess($"{target.Name} 연구 시작");
		}

		private static void Kills(DevCommandContext context, TraceSaveSlice traces)
		{
			int total = traces.RunKills.Values.Sum();
			if (total == 0)
			{
				context.LogWarn("이번 판 처치 없음");
				return;
			}
			foreach (KeyValuePair<int, int> pair in traces.RunKills.OrderByDescending(pair => pair.Value))
			{
				string toolName = pair.Key == DamageInfo.NO_DATA_ID ? "도구 모름" : SOHelper.Get<ItemData>(pair.Key).Name;
				context.LogSuccess($"{toolName}: {pair.Value} ({100f * pair.Value / total:0.0}%), 자취 {TraceSource.OfTool(pair.Key)}");
			}
		}
	}
}
