using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// FMOD 프로젝트 메타데이터에서 이벤트 전체 경로 (event:/폴더/이름) 를 모음. 뱅크를 열지 않고 경로 오타와 지워진 이벤트를 잡음
	// 이름만 보면 다른 폴더의 같은 이름 (SFX 의 Victory 등) 이 통과하므로 폴더 사슬까지 풂
	public static class FmodProjectPaths
	{
		private static readonly Regex OBJECT_NAME = new("<object class=\"(Event|EventFolder)\" id=\"(\\{[^}]+\\})\">\\s*<property name=\"name\">\\s*<value>([^<]*)</value>");
		private static readonly Regex PARENT_FOLDER = new("<relationship name=\"folder\">\\s*<destination>(\\{[^}]+\\})</destination>");

		public static HashSet<string> Load()
		{
			string metadata = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FMODAssets", "Metadata"));
			Dictionary<string, (string name, string parent)> folders = new();
			List<(string name, string parent)> events = new();
			foreach (string kind in new[] { "EventFolder", "Event" })
			{
				string folder = Path.Combine(metadata, kind);
				Assert.IsTrue(Directory.Exists(folder), folder);
				foreach (string file in Directory.GetFiles(folder, "*.xml"))
				{
					string text = File.ReadAllText(file);
					Match name = OBJECT_NAME.Match(text);
					if (name.Success == false)
						continue;
					Match parent = PARENT_FOLDER.Match(text);
					(string, string) item = (name.Groups[3].Value, parent.Success ? parent.Groups[1].Value : string.Empty);
					if (name.Groups[1].Value == "EventFolder")
						folders[name.Groups[2].Value] = item;
					else
						events.Add(item);
				}
			}

			HashSet<string> paths = new();
			foreach ((string name, string parent) in events)
			{
				string path = name;
				string folderId = parent;
				// 최상위 폴더 (Master) 는 EventFolder 파일에 없어 사슬이 거기서 끝남
				while (folders.TryGetValue(folderId, out (string name, string parent) folder))
				{
					path = folder.Item1 + "/" + path;
					folderId = folder.Item2;
				}
				paths.Add("event:/" + path);
			}
			Assert.IsTrue(paths.Contains("event:/SFX/Combat/Victory"), "경로 풀기 자체 확인");
			return paths;
		}

		private static readonly Regex GROUP_NAME = new("<object class=\"MixerGroup\" id=\"(\\{[^}]+\\})\">\\s*<property name=\"name\">\\s*<value>([^<]*)</value>");
		private static readonly Regex GROUP_OUTPUT = new("<relationship name=\"output\">\\s*<destination>(\\{[^}]+\\})</destination>");

		// 믹서 그룹 버스 경로 (bus:/부모/이름). 최상위 마스터는 Group 파일에 없어 사슬이 거기서 끝남
		public static HashSet<string> LoadBuses()
		{
			string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FMODAssets", "Metadata", "Group"));
			Assert.IsTrue(Directory.Exists(folder), folder);
			Dictionary<string, (string name, string output)> groups = new();
			foreach (string file in Directory.GetFiles(folder, "*.xml"))
			{
				string text = File.ReadAllText(file);
				Match name = GROUP_NAME.Match(text);
				if (name.Success == false)
					continue;
				Match output = GROUP_OUTPUT.Match(text);
				groups[name.Groups[1].Value] = (name.Groups[2].Value, output.Success ? output.Groups[1].Value : string.Empty);
			}

			HashSet<string> buses = new();
			foreach ((string name, string output) in groups.Values)
			{
				string path = name;
				string parentId = output;
				while (groups.TryGetValue(parentId, out (string name, string output) parent))
				{
					path = parent.Item1 + "/" + path;
					parentId = parent.Item2;
				}
				buses.Add("bus:/" + path);
			}
			return buses;
		}
	}
}
