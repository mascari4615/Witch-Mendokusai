using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 싱글톤 프리팹을 <c>Resources/</c> 밖으로 — 제품 경계의 <b>본체</b> (TASK-WM-409 단계 B).
	///
	/// ★ 여기까지 온 근거 (전부 실측):
	///   ① 단계 A 로 조립 목록이 <b>참조</b>가 됐다(`SingletonCatalog`) — 이름 조회가 사라졌다.
	///   ② 부팅 스모크에서 <c>[BootConfig]</c> 로그가 <b>한 줄도 안 찍혔다</b> —
	///      즉 `VContainerSettings.RootLifetimeScope` 참조가 <b>살아 있어</b> Resources 폴백을 안 탔다.
	///      `TASK-WM-121` 이 적어 둔 「preloaded SO→prefab 참조가 player 에서 null」 고질은
	///      이 프로젝트·이 유니티(6000.5.6f1)에서 <b>재현되지 않는다</b>.
	///   → 그러면 `Core/Resources/Singletons` 는 <b>있을 이유가 없다</b>.
	///
	/// 옮기고 나면 그 그래프(싱글톤 27 + 그들이 물고 있는 스테이지·NPC·아이콘)가
	/// <b>본편 씬을 굽는 빌드에만</b> 실린다. 방치형은 그 씬을 안 구우므로 통째로 빠진다.
	///
	/// ⚠ 되돌리기: 이 도구를 반대로 돌릴 필요 없이 `git revert` 로 충분하다(자산 이동은 커밋에 남는다).
	/// </summary>
	public static class SingletonsOutOfResources
	{
		private const string TAG = "[Singletons이사]";
		private const string SRC = "Assets/_WitchMendokusai/Core/Resources/Singletons";
		private const string DST_PARENT = "Assets/_WitchMendokusai/Core/Assets";
		private const string DST = DST_PARENT + "/Singletons";

		[MenuItem("WM/Migrate/Singletons out of Resources (TASK-WM-409 B)")]
		public static void Run()
		{
			if (Directory.Exists(SRC) == false)
			{
				Debug.Log(TAG + " 이미 옮겨져 있다 — " + SRC + " 없음");
				return;
			}

			if (AssetDatabase.IsValidFolder(DST_PARENT) == false)
			{
				AssetDatabase.CreateFolder("Assets/_WitchMendokusai/Core", "Assets");
			}

			// ★ 폴더째 옮기되, <b>남는 것을 확인</b>한다.
			//   실측 2026-08-17: 폴더 이동이 <b>부분 실패</b>해 6개(RootLifetimeScope 포함)가 옛 자리에 남았고,
			//   그 하나가 24.8MB 를 계속 끌고 왔다 — 「옮겼다」는 로그만 믿으면 못 본다.
			if (AssetDatabase.IsValidFolder(DST) == false)
			{
				string error = AssetDatabase.MoveAsset(SRC, DST);
				if (string.IsNullOrEmpty(error) == false)
				{
					Debug.LogError(TAG + " 폴더 이사 실패 : " + error);
					return;
				}
			}

			// 남은 것 하나씩 마저 옮긴다.
			if (Directory.Exists(SRC))
			{
				foreach (string path in Directory.GetFiles(SRC, "*", SearchOption.AllDirectories))
				{
					if (path.EndsWith(".meta")) { continue; }
					string name = Path.GetFileName(path);
					string moveError = AssetDatabase.MoveAsset(SRC + "/" + name, DST + "/" + name);
					if (string.IsNullOrEmpty(moveError) == false)
					{
						Debug.LogError(TAG + " 남은 것 이사 실패 " + name + " : " + moveError);
						return;
					}
					Debug.Log(TAG + " 마저 옮김 — " + name);
				}
				if (Directory.GetFileSystemEntries(SRC).Length == 0)
				{
					AssetDatabase.DeleteAsset(SRC);
					Debug.Log(TAG + " 빈 옛 폴더 제거");
				}
			}

			AssetDatabase.Refresh();
			Debug.Log(TAG + " 이사 완료 — " + SRC + " → " + DST);

			// 남은 Resources 자산이 뭔지 같이 알려 준다 (다음 표적).
			string coreResources = "Assets/_WitchMendokusai/Core/Resources";
			if (Directory.Exists(coreResources))
			{
				string[] left = Directory.GetFiles(coreResources, "*", SearchOption.AllDirectories);
				int count = 0;
				foreach (string f in left) { if (f.EndsWith(".meta") == false) { count++; } }
				Debug.Log(TAG + " Core/Resources 에 남은 자산 " + count + "개 (다음 표적)");
			}
		}
	}
}
