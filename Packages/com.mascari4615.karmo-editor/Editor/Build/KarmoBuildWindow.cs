using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using KarmoLab.KarmoEditor;

namespace KarmoLab.KarmoEditor.Builder
{
	public class KarmoBuildWindow : EditorWindow
	{
		[MenuItem(Define.RootMenu + "Build Helper %&b")]
		public static void ShowWindow() => GetWindow<KarmoBuildWindow>("Build Helper");

		// Config Keys
		private const string KEY_OUTPUT_PATH = "KarmoLab_BuildPath";
		private const string KEY_LIVE_PATH = "KarmoLab_LivePath";
		private const string KEY_PREFIX = "KarmoLab_Prefix";
		private const string KEY_BACKUP_PATTERNS = "KarmoLab_BackupPatterns";
		private const string KEY_RUN_AFTER_BUILD = "KarmoLab_RunAfterBuild";
		private const string KEY_DEPLOY_AFTER_BUILD = "KarmoLab_DeployAfterBuild";

		// Fields
		private string _outputPath;
		private string _livePath;
		private string _filePrefix = "KarmoLab";
		private string _backupPatterns = "*.json;Data/";
		private string _buildMemo = "";
		private bool _openFolderAfterBuild = true;
		private bool _deleteDoNotShip = true;
		private bool _runAfterBuild = false;
		private bool _deployAfterBuild = false;

		private void OnEnable()
		{
			_outputPath = EditorPrefs.GetString(KEY_OUTPUT_PATH, "");
			_livePath = EditorPrefs.GetString(KEY_LIVE_PATH, "");
			_filePrefix = EditorPrefs.GetString(KEY_PREFIX, "KarmoLab");
			_backupPatterns = EditorPrefs.GetString(KEY_BACKUP_PATTERNS, "*.json;Data/");
			_runAfterBuild = EditorPrefs.GetBool(KEY_RUN_AFTER_BUILD, false);
			_deployAfterBuild = EditorPrefs.GetBool(KEY_DEPLOY_AFTER_BUILD, false);
		}

		private void OnDisable()
		{
			EditorPrefs.SetString(KEY_OUTPUT_PATH, _outputPath);
			EditorPrefs.SetString(KEY_LIVE_PATH, _livePath);
			EditorPrefs.SetString(KEY_PREFIX, _filePrefix);
			EditorPrefs.SetString(KEY_BACKUP_PATTERNS, _backupPatterns);
			EditorPrefs.SetBool(KEY_RUN_AFTER_BUILD, _runAfterBuild);
			EditorPrefs.SetBool(KEY_DEPLOY_AFTER_BUILD, _deployAfterBuild);
		}

		private void OnGUI()
		{
			GUILayout.Label("Build Configuration", EditorStyles.boldLabel);

			// 1. Output Path
			EditorGUILayout.BeginHorizontal();
			_outputPath = EditorGUILayout.TextField("Build Output Path", _outputPath);
			if (GUILayout.Button("...", GUILayout.Width(30)))
			{
				string path = EditorUtility.OpenFolderPanel("Select Build Output Folder", _outputPath, "");
				if (!string.IsNullOrEmpty(path)) _outputPath = path;
			}
			EditorGUILayout.EndHorizontal();

			// 2. Live (Deploy) Path
			EditorGUILayout.BeginHorizontal();
			_livePath = EditorGUILayout.TextField("Live Deploy Path", _livePath);
			if (GUILayout.Button("...", GUILayout.Width(30)))
			{
				string path = EditorUtility.OpenFolderPanel("Select Live Deploy Folder", _livePath, "");
				if (!string.IsNullOrEmpty(path)) _livePath = path;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			GUILayout.Label("Build Settings", EditorStyles.boldLabel);

			_filePrefix = EditorGUILayout.TextField("File Prefix", _filePrefix);
			_buildMemo = EditorGUILayout.TextField("Memo (Optional)", _buildMemo);
			_backupPatterns = EditorGUILayout.TextField("Protect Patterns", _backupPatterns);
			EditorGUILayout.HelpBox("Semicolon-separated patterns to protect (e.g. *.json;Data/)", MessageType.None);
			_openFolderAfterBuild = EditorGUILayout.Toggle("Open Folder After Build", _openFolderAfterBuild);
			_deleteDoNotShip = EditorGUILayout.Toggle("Delete DoNotShip Folders", _deleteDoNotShip);
			
			EditorGUILayout.Space();
			GUILayout.Label("Post-Build Actions", EditorStyles.boldLabel);
			_runAfterBuild = EditorGUILayout.Toggle("Run after Build", _runAfterBuild);
			_deployAfterBuild = EditorGUILayout.Toggle("Deploy after Build", _deployAfterBuild);

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox($"Preview: {_outputPath}/{GetFolderName()}/{_filePrefix}.exe", MessageType.Info);

			EditorGUILayout.Space();

			string buttonText = "Build App";
			if (_deployAfterBuild) buttonText = "Build & Deploy (Patch)";
			else if (_runAfterBuild) buttonText = "Build & Run";

			if (_deployAfterBuild) GUI.backgroundColor = Color.green;

			if (GUILayout.Button(buttonText, GUILayout.Height(40)))
			{
				if (_deployAfterBuild)
				{
					if (!EditorUtility.DisplayDialog("Deploy Warning",
						"This will overwrite files in the Live Deploy Path.\nEnsure the application is closed.\nProceed?", "Yes, Patch it!", "Cancel"))
					{
						return;
					}
				}

				string builtExe = BuildApp(_deployAfterBuild);
				if (!string.IsNullOrEmpty(builtExe) && _runAfterBuild)
				{
					RunApp(builtExe);
				}
			}
			GUI.backgroundColor = Color.white;
		}

		private string GetFolderName()
		{
			string dateStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
			string memoPart = string.IsNullOrWhiteSpace(_buildMemo) ? "" : $"_{_buildMemo}";
			return $"{_filePrefix}_{dateStr}{memoPart}";
		}

		private string BuildApp(bool deploy)
		{
			if (string.IsNullOrEmpty(_outputPath))
			{
				EditorUtility.DisplayDialog("Error", "Please select a Build Output Path.", "OK");
				return null;
			}

			string folderName = GetFolderName();
			string fullPath = Path.Combine(_outputPath, folderName);
			string exePath = Path.Combine(fullPath, _filePrefix + ".exe");

			// Ensure Directory
			if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

			// Build Player Options
			BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
			buildPlayerOptions.scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
			buildPlayerOptions.locationPathName = exePath;
			buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
			buildPlayerOptions.options = BuildOptions.None;

			// Perform Build
			UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
			UnityEditor.Build.Reporting.BuildSummary summary = report.summary;

			if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
			{
				Debug.Log($"{Define.LogPrefix} Build Succeeded: {summary.totalSize / 1024 / 1024} MB");

				if (_deleteDoNotShip)
				{
					// Delete DoNotShip Folder
					string doNotShipPath = Path.Combine(fullPath, $"{PlayerSettings.productName}_BurstDebugInformation_DoNotShip");
					if (Directory.Exists(doNotShipPath))
					{
						Directory.Delete(doNotShipPath, true);
						Debug.Log($"{Define.LogPrefix} Deleted: {doNotShipPath}");
					}

					// Delete BackUpThisFolder_ButDontShipItWithYourGame
					string backupPath = Path.Combine(fullPath, $"{PlayerSettings.productName}_BackUpThisFolder_ButDontShipItWithYourGame");
					if (Directory.Exists(backupPath))
					{
						Directory.Delete(backupPath, true);
						Debug.Log($"{Define.LogPrefix} Deleted: {backupPath}");
					}
				}

				if (deploy)
				{
					DeployToLive(fullPath);
				}
				else if (_openFolderAfterBuild && !deploy) // Don't open if we are going to run it manually via Build & Run (handled by caller if needed, but here caller usually handles run)
				{
					// For simple Build Only, we open. For Build & Run, we might not want to open folder. 
					// Let's keep logic simple: return exe path.
					if (!deploy) EditorUtility.RevealInFinder(exePath);
				}

				return exePath;
			}
			else
			{
				Debug.LogError($"{Define.LogPrefix} Build Failed: {summary.result}");
				return null;
			}
		}

		private void RunApp(string exePath)
		{
			if (File.Exists(exePath))
			{
				Debug.Log($"{Define.LogPrefix} Launching: {exePath}");
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
				{
					FileName = exePath,
					WorkingDirectory = Path.GetDirectoryName(exePath)
				});
			}
			else
			{
				Debug.LogError($"{Define.LogPrefix} Executable not found at: {exePath}");
			}
		}

		private void DeployToLive(string sourceDir)
		{
			if (string.IsNullOrEmpty(_livePath))
			{
				EditorUtility.DisplayDialog("Error", "Please select a Live Deploy Path.", "OK");
				return;
			}

			if (!Directory.Exists(_livePath))
			{
				Directory.CreateDirectory(_livePath);
			}

			string backupDirPath = _livePath + "_Backup";

			try
			{
				// 1. Backup protected files/folders
				int backupCount = BackupFiles_Internal(_livePath, backupDirPath, _backupPatterns);
				if (backupCount > 0) Debug.Log($"{Define.LogPrefix} Backed up {backupCount} items for protection.");

				// 2. Deploy (Overwrite)
				CopyDirectory(sourceDir, _livePath);
				Debug.Log($"{Define.LogPrefix} Deployed successfully to: {_livePath}");

				// 3. Restore protected files
				if (backupCount > 0)
				{
					CopyDirectory(backupDirPath, _livePath);
					Debug.Log($"{Define.LogPrefix} Restored protected files from backup.");
				}

				EditorUtility.DisplayDialog("Success", "Build & Deploy Complete!\nFiles updated in Live Path (Protected files restored).", "Awesome!");

				if (_openFolderAfterBuild)
				{
					EditorUtility.RevealInFinder(Path.Combine(_livePath, _filePrefix + ".exe"));
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"{Define.LogPrefix} Deploy Failed: {ex.Message}");
				EditorUtility.DisplayDialog("Error", $"Deploy Failed.\n{ex.Message}", "OK");
			}
			finally
			{
				// 4. Cleanup backup folder
				if (Directory.Exists(backupDirPath))
				{
					Directory.Delete(backupDirPath, true);
				}
			}
		}

		private int BackupFiles_Internal(string liveDir, string backupDir, string patternsStr)
		{
			if (!Directory.Exists(liveDir)) return 0;
			if (string.IsNullOrWhiteSpace(patternsStr)) return 0;

			int count = 0;
			var patterns = patternsStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

			if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

			foreach (var pattern in patterns)
			{
				var trimPattern = pattern.Trim();
				if (string.IsNullOrEmpty(trimPattern)) continue;

				// Directory Pattern (ends with / or \)
				if (trimPattern.EndsWith("/") || trimPattern.EndsWith("\\"))
				{
					string subDirName = trimPattern.TrimEnd('/', '\\');
					string sourceSubDir = Path.Combine(liveDir, subDirName);
					if (Directory.Exists(sourceSubDir))
					{
						string destSubDir = Path.Combine(backupDir, subDirName);
						if (!Directory.Exists(destSubDir)) Directory.CreateDirectory(destSubDir);
						CopyDirectory(sourceSubDir, destSubDir);
						count++;
					}
				}
				else // File Pattern
				{
					var files = Directory.GetFiles(liveDir, trimPattern, SearchOption.TopDirectoryOnly);
					foreach (var f in files)
					{
						string fileName = Path.GetFileName(f);
						File.Copy(f, Path.Combine(backupDir, fileName), true);
						count++;
					}
				}
			}

			return count;
		}

		private void CopyDirectory(string sourceDir, string destDir)
		{
			DirectoryInfo dir = new DirectoryInfo(sourceDir);
			DirectoryInfo[] dirs = dir.GetDirectories();

			foreach (FileInfo file in dir.GetFiles())
			{
				string tempPath = Path.Combine(destDir, file.Name);
				file.CopyTo(tempPath, true);
			}

			foreach (DirectoryInfo subdir in dirs)
			{
				string tempPath = Path.Combine(destDir, subdir.Name);
				if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);
				CopyDirectory(subdir.FullName, tempPath);
			}
		}
	}
}
