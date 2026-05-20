// One-click "bind this worktree's Claude session to this Editor" menu.
//
// 왜: 다중 worktree 환경에서 각 Unity Editor 가 자기 worktree 의 .mcp.json 을
// 자기 HTTP MCP 포트로 자동 갱신해야 Claude session 이 형제 worktree 의 Editor
// 로 잘못 라우팅되지 않는다 (TASK-WM-109-G).
//
// 외부 PowerShell 스크립트 (.claude/scripts/wm-mcp-route.ps1) 가 동일한 일을
// 하지만, Editor 가 이미 떠 있는 시점엔 메뉴 한 번 클릭이 가장 빠르고,
// PID 파일 검색을 우회해서 *현재 Editor 가 실제로 사용 중인 포트* 를 그대로
// 가져올 수 있다.
//
// 정합:
//   - § Editor 메뉴 (CLAUDE.md) — top-level root = "WM/"
//   - § Unity-MCP layer (CLAUDE.md) — CoplayDev `com.coplaydev.unity-mcp` 정본,
//     `127.0.0.1:8080/mcp` 디폴트 (per-Editor 변경 가능)
//   - § DomainSDK / Mods (CLAUDE.md) — 본 클래스는 Editor 어셈블리 한정,
//     런타임 코드 영향 0
//
// 만든 .mcp.json 은 .gitignore 되어야 한다 (worktree 별로 다른 포트를 가질 수
// 있으므로 정본화 X). 본 PR 의 .gitignore 변경과 같이 묶인다.

#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WitchMendokusai.Editor.Infra.MCPRouting
{
    public static class McpWorktreeBinder
    {
        private const string MenuRoot = "WM/MCP/";
        private const string DefaultLoopbackUrl = "http://127.0.0.1:8080";

        [MenuItem(MenuRoot + "Bind Claude session to this Editor")]
        public static void BindClaudeSessionToThisEditor()
        {
            string projectRoot = GetProjectRoot();
            int port = DiscoverHttpPort(projectRoot);

            if (port <= 0)
            {
                EditorUtility.DisplayDialog(
                    "MCP Worktree Binder",
                    "MCP HTTP server has not started for this Editor yet.\n\n" +
                    "Open Window > MCP for Unity, ensure HTTP transport is enabled and the server is running, then try again.",
                    "OK");
                return;
            }

            string mcpJsonPath = Path.Combine(projectRoot, ".mcp.json");
            string url = $"http://127.0.0.1:{port}/mcp";
            string payload =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"unityMCP\": {\n" +
                "      \"type\": \"http\",\n" +
                "      \"url\": \"" + url + "\"\n" +
                "    }\n" +
                "  }\n" +
                "}\n";

            try
            {
                File.WriteAllText(mcpJsonPath, payload, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "MCP Worktree Binder",
                    $"Failed to write {mcpJsonPath}\n\n{ex.Message}",
                    "OK");
                return;
            }

            Debug.Log($"[McpWorktreeBinder] Routed Claude session at {projectRoot} -> {url}");
            EditorUtility.DisplayDialog(
                "MCP Worktree Binder",
                $"Bound this worktree's Claude session to:\n  {url}\n\nWrote {mcpJsonPath}",
                "OK");
        }

        [MenuItem(MenuRoot + "Show Routing Status")]
        public static void ShowRoutingStatus()
        {
            string projectRoot = GetProjectRoot();
            int port = DiscoverHttpPort(projectRoot);
            string editorInstance = Path.Combine(projectRoot, "Library", "EditorInstance.json");
            string mcpJsonPath = Path.Combine(projectRoot, ".mcp.json");

            string editorLine = File.Exists(editorInstance)
                ? $"Library/EditorInstance.json: present"
                : "Library/EditorInstance.json: MISSING (Editor not running?)";

            string portLine = port > 0
                ? $"MCP HTTP port: {port}"
                : "MCP HTTP port: not detected (server not running)";

            string mcpJsonLine = File.Exists(mcpJsonPath)
                ? $".mcp.json: {mcpJsonPath}\n{File.ReadAllText(mcpJsonPath)}"
                : ".mcp.json: not present (Claude session would use Claude Code defaults)";

            string body = string.Join("\n", new[]
            {
                $"Worktree: {projectRoot}",
                editorLine,
                portLine,
                "",
                mcpJsonLine
            });

            Debug.Log("[McpWorktreeBinder] " + body);
            EditorUtility.DisplayDialog("MCP Routing Status", body, "OK");
        }

        [MenuItem(MenuRoot + "Run wm-mcp-route.ps1 (-Status)")]
        public static void RunRouterScriptStatus()
        {
            string projectRoot = GetProjectRoot();
            string scriptPath = Path.Combine(projectRoot, ".claude", "scripts", "wm-mcp-route.ps1");
            if (!File.Exists(scriptPath))
            {
                EditorUtility.DisplayDialog(
                    "MCP Worktree Binder",
                    $"Script not found:\n  {scriptPath}\n\nThis menu item assumes the worktree includes .claude/scripts/wm-mcp-route.ps1.",
                    "OK");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Status",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);
                    string combined = stdout + (string.IsNullOrEmpty(stderr) ? string.Empty : "\nSTDERR:\n" + stderr);
                    Debug.Log("[McpWorktreeBinder] router -Status:\n" + combined);
                    EditorUtility.DisplayDialog("MCP Routing Status (script)", combined, "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "MCP Worktree Binder",
                    $"Failed to run router script:\n  {scriptPath}\n\n{ex.Message}",
                    "OK");
            }
        }

        private static string GetProjectRoot()
        {
            // Application.dataPath = "<root>/Assets"
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        // Best-effort port discovery without taking a hard dependency on the
        // CoplayDev MCP-for-Unity assembly (which is a separate package and may
        // not be available at compile time in all worktrees).
        //
        // Order:
        //   1. Library/MCPForUnity/RunState/mcp_http_<port>.pid -- filename encodes port.
        //   2. EditorPrefs "MCPForUnity.HttpBaseUrl" -- user-set base URL.
        //   3. Default 8080.
        private static int DiscoverHttpPort(string projectRoot)
        {
            string runStateDir = Path.Combine(projectRoot, "Library", "MCPForUnity", "RunState");
            if (Directory.Exists(runStateDir))
            {
                string[] candidates = Directory.GetFiles(runStateDir, "mcp_http_*.pid");
                int latestPort = 0;
                DateTime latestMtime = DateTime.MinValue;
                foreach (string file in candidates)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    const string prefix = "mcp_http_";
                    if (!name.StartsWith(prefix, StringComparison.Ordinal)) { continue; }
                    if (int.TryParse(name.Substring(prefix.Length), out int port) == false) { continue; }
                    DateTime mtime = File.GetLastWriteTimeUtc(file);
                    if (mtime > latestMtime)
                    {
                        latestMtime = mtime;
                        latestPort = port;
                    }
                }
                if (latestPort > 0) { return latestPort; }
            }

            string baseUrl = EditorPrefs.GetString("MCPForUnity.HttpBaseUrl", DefaultLoopbackUrl);
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) && uri.Port > 0)
            {
                return uri.Port;
            }

            return 8080;
        }
    }
}
#endif
