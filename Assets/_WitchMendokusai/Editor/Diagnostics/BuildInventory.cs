using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// <b>빌드 인벤토리</b> — 「무엇이 <b>실제로</b> 실렸고, <b>왜</b> 실렸나」 (TASK-WM-408).
	///
	/// ★ 이 도구가 `ResourcesBudget` 을 대체하는 이유 — <b>내 첫 계측이 틀렸다</b>(2026-08-16):
	///   디스크 바이트로 쟀더니 `UAL1_Standard.fbx` 가 24.3MB 로 1위였는데,
	///   실제 빌드에서는 <b>946KB</b> 였다. FBX·PNG 는 임포트되며 통째로 다시 인코딩된다.
	///   즉 <b>디스크 크기와 빌드 크기는 다른 숫자</b>고, 그걸로 우선순위를 매기면 엉뚱한 걸 뜯는다.
	///
	/// ★ 그래서 유니티 자신이 만든 <c>BuildReport</c> 의 <c>PackedAssets</c> 를 읽는다 —
	///   거기에 <b>구워진 바이트</b>가 자산별로 들어 있다.
	///   ⚠ 유니티 6 에서는 <c>Library/LastBuild.buildreport</c> 가 <b>남지 않는다</b>(실측 2026-08-16).
	///   그래서 <b>굽는 그 순간</b> 빌드 스크립트가 손에 쥔 `BuildReport` 를 여기로 넘겨
	///   <c>Builds/last-build-inventory.json</c> 에 적는다 — 시험은 그 파일을 읽는다.
	///
	/// ★ 「왜 실렸나」 = <c>Resources/</c> 그래프와 교차한다.
	///   Resources 안의 것 + 그 참조 그래프는 <b>씬과 무관하게</b> 실린다. 그 집합에 속하면
	///   「Resources 때문」으로 표시한다 — 이게 제품 경계가 새는 지점이다.
	///
	/// 쓰는 법:
	///   Unity -batchmode -executeMethod WitchMendokusai.EditorTools.BuildInventory.PrintCli -quit
	///   (빌드를 한 번이라도 구운 뒤에 쓴다. 인벤토리가 없으면 그렇다고 말한다.)
	/// </summary>
	public static class BuildInventory
	{
		private const string TAG = "[BuildInventory]";
		/// <summary>굽는 순간 적어 두는 인벤토리. 시험·CLI 가 이걸 읽는다.</summary>
		public const string INVENTORY_PATH = "Builds/last-build-inventory.json";

		public sealed class Item
		{
			public string Path;
			public ulong Bytes;
			public bool FromResources;
		}

		/// <summary>굽는 순간 <b>실제로 담긴 것</b>을 적는다. `IdlePlayerBuild` 가 부른다.</summary>
		public static void Write(BuildReport report)
		{
			if (report == null) { return; }
			HashSet<string> resourcesGraph = ResourcesGraph();
			List<Item> items = new List<Item>();

			foreach (PackedAssets packed in report.packedAssets)
			{
				foreach (PackedAssetInfo info in packed.contents)
				{
					string path = info.sourceAssetPath;
					if (string.IsNullOrEmpty(path)) { continue; }   // 내장 자산은 우리가 어쩔 수 없다
					items.Add(new Item
					{
						Path = path,
						Bytes = info.packedSize,
						FromResources = resourcesGraph.Contains(path),
					});
				}
			}

			List<Item> merged = items
				.GroupBy(i => i.Path)
				.Select(g => new Item
				{
					Path = g.Key,
					Bytes = (ulong)g.Sum(x => (decimal)x.Bytes),
					FromResources = g.Any(x => x.FromResources),
				})
				.OrderByDescending(i => i.Bytes)
				.ToList();

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"at\": \"" + DateTime.Now.ToString("s") + "\",");
			sb.AppendLine("  \"items\": [");
			for (int i = 0; i < merged.Count; i++)
			{
				Item it = merged[i];
				sb.AppendLine("    {\"path\": \"" + it.Path.Replace("\\", "/").Replace("\"", "'") + "\", \"bytes\": "
					+ it.Bytes + ", \"res\": " + (it.FromResources ? "true" : "false") + "}"
					+ (i + 1 < merged.Count ? "," : string.Empty));
			}
			sb.AppendLine("  ]");
			sb.AppendLine("}");

			Directory.CreateDirectory(Path.GetDirectoryName(INVENTORY_PATH));
			File.WriteAllText(INVENTORY_PATH, sb.ToString());
			Debug.Log(TAG + " 적었다 — " + merged.Count + "개 · " + INVENTORY_PATH);
		}

		/// <summary>마지막으로 구운 빌드가 담은 것. 파일이 없으면 빈 목록(= 아직 안 구웠다).</summary>
		public static List<Item> Read()
		{
			List<Item> items = new List<Item>();
			if (File.Exists(INVENTORY_PATH) == false) { return items; }

			foreach (string line in File.ReadAllLines(INVENTORY_PATH))
			{
				int p = line.IndexOf("\"path\": \"", StringComparison.Ordinal);
				if (p < 0) { continue; }
				p += 9;
				int q = line.IndexOf('"', p);
				if (q < 0) { continue; }
				string path = line.Substring(p, q - p);

				int b = line.IndexOf("\"bytes\": ", StringComparison.Ordinal);
				int c = line.IndexOf(',', b + 9);
				ulong bytes = 0UL;
				if (b >= 0 && c > b) { ulong.TryParse(line.Substring(b + 9, c - b - 9), out bytes); }

				items.Add(new Item
				{
					Path = path,
					Bytes = bytes,
					FromResources = line.Contains("\"res\": true"),
				});
			}
			return items.OrderByDescending(i => i.Bytes).ToList();
		}

		/// <summary>`Resources/` 안의 것 + 그것이 끌고 오는 모든 것 = 씬과 무관하게 실리는 집합.</summary>
		public static HashSet<string> ResourcesGraph()
		{
			HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
			foreach (string path in AssetDatabase.GetAllAssetPaths())
			{
				if (path.Contains("/Resources/") == false) { continue; }
				if (path.Contains("/Editor/")) { continue; }
				if (Directory.Exists(path)) { continue; }
				foreach (string dep in AssetDatabase.GetDependencies(path, true)) { set.Add(dep); }
			}
			return set;
		}

		/// <summary>Resources 때문에 실린 바이트 합. 예산 시험이 보는 숫자다.</summary>
		public static ulong ResourcesBytes()
		{
			ulong sum = 0UL;
			foreach (Item i in Read()) { if (i.FromResources) { sum += i.Bytes; } }
			return sum;
		}

		public static string Report()
		{
			List<Item> items = Read();
			if (items.Count == 0)
			{
				return TAG + " 인벤토리가 없다 — 한 번 굽고 다시 부를 것 (" + INVENTORY_PATH + ")";
			}

			ulong total = 0UL, fromRes = 0UL;
			foreach (Item i in items) { total += i.Bytes; if (i.FromResources) { fromRes += i.Bytes; } }

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(string.Format("{0} 마지막 빌드가 담은 것 — 합계 {1:N1} MB · 그 중 Resources 때문 {2:N1} MB ({3:P0})",
				TAG, total / 1024f / 1024f, fromRes / 1024f / 1024f, total == 0UL ? 0f : (float)fromRes / total));
			sb.AppendLine();
			sb.AppendLine("무거운 것 상위 25 (R = Resources 때문에 실림):");
			foreach (Item i in items.Take(25))
			{
				sb.AppendLine(string.Format("  {0} {1,8:N0} KB  {2}", i.FromResources ? "R" : " ", i.Bytes / 1024f, i.Path));
			}
			return sb.ToString();
		}

		/// <summary>
		/// <b>어느 Resources 뿌리가 몇 MB를 끌고 오나</b> — 이관 순서를 정하는 숫자 (TASK-WM-409).
		///
		/// ★ 「Resources 때문 27MB」까지는 알아도, <b>어느 파일을 끊어야 하는지</b>는 그걸로 안 나온다.
		///   그래서 `Resources/` 안의 자산을 하나씩 뿌리로 잡고, 그 의존 그래프가
		///   <b>실제 빌드에서 차지한 바이트</b>를 더한다. 공유된 것은 여러 뿌리에 겹쳐 세어진다 —
		///   「이 하나를 끊으면 최대 이만큼」의 상한으로 읽으면 된다.
		/// </summary>
		public static string RootsReport()
		{
			List<Item> items = Read();
			if (items.Count == 0) { return TAG + " 인벤토리가 없다 — 한 번 굽고 부를 것"; }

			Dictionary<string, ulong> byPath = new Dictionary<string, ulong>(StringComparer.Ordinal);
			foreach (Item i in items) { byPath[i.Path] = i.Bytes; }

			List<KeyValuePair<string, ulong>> roots = new List<KeyValuePair<string, ulong>>();
			foreach (string path in AssetDatabase.GetAllAssetPaths())
			{
				if (path.Contains("/Resources/") == false) { continue; }
				if (path.Contains("/Editor/")) { continue; }
				if (Directory.Exists(path)) { continue; }

				ulong sum = 0UL;
				foreach (string dep in AssetDatabase.GetDependencies(path, true))
				{
					if (byPath.TryGetValue(dep, out ulong b)) { sum += b; }
				}
				if (sum > 0UL) { roots.Add(new KeyValuePair<string, ulong>(path, sum)); }
			}

			roots.Sort((a, b) => b.Value.CompareTo(a.Value));
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(TAG + " Resources 뿌리별로 끌고 오는 무게 (겹침 포함 = 상한):");
			foreach (KeyValuePair<string, ulong> r in roots.Take(15))
			{
				sb.AppendLine(string.Format("  {0,8:N1} MB  {1}", r.Value / 1024f / 1024f, r.Key));
			}
			return sb.ToString();
		}

		public static void PrintRootsCli()
		{
			string r = RootsReport();
			Console.WriteLine(r);
			Debug.Log(r);
		}

		[MenuItem("WM/진단/빌드 인벤토리 보기")]
		public static void Print() { Debug.Log(Report()); }

		public static void PrintCli()
		{
			string r = Report();
			Console.WriteLine(r);
			Debug.Log(r);
		}
	}
}
