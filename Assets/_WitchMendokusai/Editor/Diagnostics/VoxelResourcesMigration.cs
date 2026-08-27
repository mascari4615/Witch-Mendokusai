using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 복셀을 <c>Resources/</c> 밖으로 옮긴다 — <b>한 번 쓰고 지울 이사 도구</b> (TASK-WM-409).
	///
	/// ★ 손으로 옮기면 <c>.meta</c> 가 어긋나 참조가 통째로 끊긴다(GUID 유실).
	///   그래서 <c>AssetDatabase.MoveAsset</c> 으로 옮긴다 — GUID 가 따라가므로 참조가 산다.
	///
	/// 하는 일:
	///   1. `Voxel/Scripts/Resources/**` → `Voxel/Assets/**` 로 이사
	///   2. `BlockCatalog` 을 만들어 블록 전부 + 청크 재질을 담는다
	///   3. Lab 스테이지 프리팹의 `ChunkManager` 에 그 카탈로그를 꽂는다
	///
	/// 이 셋이 끝나면 복셀은 <b>참조로만</b> 산다 — 복셀을 안 쓰는 제품(방치형) 빌드에서 빠진다.
	/// </summary>
	public static class VoxelResourcesMigration
	{
		private const string TAG = "[VoxelMigration]";
		private const string SRC = "Assets/_WitchMendokusai/Domain/Voxel/Scripts/Resources";
		private const string DST = "Assets/_WitchMendokusai/Domain/Voxel/Assets";
		private const string CATALOG = DST + "/BlockCatalog.asset";
		private const string LAB_PREFAB = "Assets/_WitchMendokusai/Domain/World/Stage/0004_Lab/[Stage] [4] Lab.prefab";

		[MenuItem("WM/Migrate/Voxels out of Resources (TASK-WM-409)")]
		public static void Run()
		{
			if (Directory.Exists(SRC))
			{
				// ★ 폴더는 <b>AssetDatabase 로</b> 만들고 옮긴다.
				//   `Directory.CreateDirectory` 로 만든 폴더는 유니티가 모른다 —
				//   그 안으로 옮기려 하면 "Could not find parent directory GUID:000…" 로 죽는다(실측 2026-08-16).
				if (AssetDatabase.IsValidFolder(DST) == false)
				{
					AssetDatabase.CreateFolder("Assets/_WitchMendokusai/Domain/Voxel", "Assets");
				}

				// 하위 폴더는 <b>폴더째</b> 옮긴다 — 파일 하나씩보다 안전하고 GUID 도 그대로다.
				foreach (string dir in Directory.GetDirectories(SRC))
				{
					string name = Path.GetFileName(dir);
					string error = AssetDatabase.MoveAsset(SRC + "/" + name, DST + "/" + name);
					if (string.IsNullOrEmpty(error) == false)
					{
						Debug.LogError(TAG + " 폴더 이사 실패 " + name + " : " + error);
						return;
					}
				}

				foreach (string path in Directory.GetFiles(SRC))
				{
					if (path.EndsWith(".meta")) { continue; }
					string file = Path.GetFileName(path);
					string error = AssetDatabase.MoveAsset(SRC + "/" + file, DST + "/" + file);
					if (string.IsNullOrEmpty(error) == false)
					{
						Debug.LogError(TAG + " 이사 실패 " + file + " : " + error);
						return;
					}
				}

				// 빈 껍데기가 남으면 그것만으로도 Resources 폴더로 취급된다 — 지운다.
				if (Directory.GetFileSystemEntries(SRC).Length == 0)
				{
					AssetDatabase.DeleteAsset(SRC);
				}
				AssetDatabase.Refresh();
				Debug.Log(TAG + " 이사 완료 — " + SRC + " → " + DST);
			}
			else
			{
				Debug.Log(TAG + " 이미 옮겨져 있다 — " + SRC + " 없음");
			}

			// 2. 카탈로그 — 프로젝트 안 블록 전부 + 청크 재질.
			BlockCatalog catalog = AssetDatabase.LoadAssetAtPath<BlockCatalog>(CATALOG);
			if (catalog == null)
			{
				catalog = ScriptableObject.CreateInstance<BlockCatalog>();
				AssetDatabase.CreateAsset(catalog, CATALOG);
			}

			List<BlockData> blocks = new List<BlockData>();
			foreach (string guid in AssetDatabase.FindAssets("t:BlockData"))
			{
				BlockData data = AssetDatabase.LoadAssetAtPath<BlockData>(AssetDatabase.GUIDToAssetPath(guid));
				if (data != null) { blocks.Add(data); }
			}

			Material material = null;
			foreach (string guid in AssetDatabase.FindAssets("VoxelMaterial t:Material"))
			{
				material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
				if (material != null) { break; }
			}

			catalog.EditorFill(blocks.ToArray(), material);
			AssetDatabase.SaveAssets();
			Debug.Log(TAG + " 카탈로그 — 블록 " + blocks.Count + " · 재질 " + (material == null ? "없음" : material.name));

			// 3. 배선 — 복셀을 실제로 쓰는 곳에 꽂는다.
			GameObject lab = AssetDatabase.LoadAssetAtPath<GameObject>(LAB_PREFAB);
			if (lab == null)
			{
				Debug.LogError(TAG + " Lab 스테이지 프리팹을 못 찾았다 — " + LAB_PREFAB);
				return;
			}

			int wired = 0;
			foreach (ChunkManager manager in lab.GetComponentsInChildren<ChunkManager>(true))
			{
				SerializedObject so = new SerializedObject(manager);
				SerializedProperty prop = so.FindProperty("catalog");
				if (prop == null) { continue; }
				prop.objectReferenceValue = catalog;
				so.ApplyModifiedPropertiesWithoutUndo();
				wired++;
			}
			if (wired == 0)
			{
				Debug.LogError(TAG + " Lab 프리팹 안에서 ChunkManager 를 못 찾았다 — 배선 실패");
				return;
			}
			EditorUtility.SetDirty(lab);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			Debug.Log(TAG + " 배선 완료 — ChunkManager " + wired + "개에 카탈로그를 꽂았다");
		}
	}
}
