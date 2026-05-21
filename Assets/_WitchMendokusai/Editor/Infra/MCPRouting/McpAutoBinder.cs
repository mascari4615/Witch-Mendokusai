// Auto-bind <worktree>/.mcp.json to this Editor's MCP HTTP port on startup.
//
// Why (TASK-WM-109-D):
//   WM-109-G shipped the worktree-aware routing primitives (PowerShell script
//   + WM/MCP menu items + .gitignore for .mcp.json). Both paths still require
//   a *manual trigger* — the user has to either run the script or click the
//   menu before Claude's MCP client can talk to this Editor.
//
//   In day-to-day multi-worktree work, every new Editor session needs the
//   same one-shot binding. Doing it manually means the Claude session
//   silently misroutes (or fails with "Unity Editor for WitchMendokusai not
//   running") until the human remembers.
//
//   This script closes the loop: on Editor load, poll for the MCP HTTP PID
//   file (the same authoritative signal McpWorktreeBinder uses). When it
//   appears AND the .mcp.json's port doesn't match, write the file. Done.
//
// Safety / scope:
//   - No-op if port already matches what's in .mcp.json (idempotent).
//   - No-op if the user has disabled auto-bind via EditorPrefs
//       "WM.MCP.AutoBindEnabled" = false.
//   - No-op in batch/CI mode (-batchmode) so headless runs do not stomp on a
//     human's interactive .mcp.json.
//   - Polling is bounded: every ~3s for the first 2 minutes after assembly
//     load, then unsubscribed. Once a successful bind happens, subsequent
//     polls become cheap no-ops; once the timeout elapses we stop entirely.
//
// 정합:
//   - § Editor 메뉴 (CLAUDE.md) — top-level root = "WM/"
//   - § Unity-MCP layer (CLAUDE.md) — CoplayDev `com.coplaydev.unity-mcp`
//     정본, `127.0.0.1:<port>/mcp` URL.
//   - § 객체 참조 획득 — init-order: this runs from [InitializeOnLoadMethod]
//     which fires after all assemblies are loaded. We do NOT call any
//     runtime singletons; only EditorApplication APIs + file I/O.
//   - § 자동화 우선: replaces a manual menu click. Visual / domain
//     verification stays with the user.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WitchMendokusai.Editor.Infra.MCPRouting
{
    [InitializeOnLoad]
    public static class McpAutoBinder
    {
        private const string MENU_ROOT = "WM/MCP/";
        private const string AUTO_BIND_TOGGLE_MENU = MENU_ROOT + "Auto-Bind on Editor Startup";
        private const string RUN_NOW_MENU = MENU_ROOT + "Run Auto-Bind Now";
        private const string PREF_KEY_ENABLED = "WM.MCP.AutoBindEnabled";

        // Total polling window — the MCP HTTP server takes a few seconds after
        // domain reload to write its PID file. 2 minutes covers slow CI boxes
        // and cold first runs without burning cycles forever.
        private const double POLL_WINDOW_SECONDS = 120.0;
        private const double POLL_INTERVAL_SECONDS = 3.0;

        private static double pollStartTime;
        private static double nextPollTime;
        private static bool boundThisSession;
        private static bool pollingActive;

        static McpAutoBinder()
        {
            // Skip batch / headless — those sessions should never overwrite a
            // human's .mcp.json. Tests and CI invocations land here.
            if (Application.isBatchMode)
            {
                return;
            }

            if (IsEnabled() == false)
            {
                return;
            }

            // Defer registration to the next editor tick so domain reload
            // bookkeeping fully settles before we touch IO.
            EditorApplication.delayCall += BeginPolling;
        }

        [MenuItem(AUTO_BIND_TOGGLE_MENU)]
        private static void ToggleAutoBind()
        {
            bool newValue = !IsEnabled();
            EditorPrefs.SetBool(PREF_KEY_ENABLED, newValue);
            Debug.Log($"[McpAutoBinder] Auto-bind on Editor startup = {newValue}");
            if (newValue && pollingActive == false)
            {
                BeginPolling();
            }
        }

        [MenuItem(AUTO_BIND_TOGGLE_MENU, true)]
        private static bool ToggleAutoBindValidate()
        {
            Menu.SetChecked(AUTO_BIND_TOGGLE_MENU, IsEnabled());
            return true;
        }

        [MenuItem(RUN_NOW_MENU)]
        public static void RunAutoBindNow()
        {
            BindResult result = TryBindOnce(verbose: true);
            string body;
            switch (result.Status)
            {
                case BindStatus.Wrote:
                    body = $"Wrote .mcp.json -> port {result.Port}.";
                    break;
                case BindStatus.AlreadyCurrent:
                    body = $".mcp.json already routes to port {result.Port}. No change.";
                    break;
                case BindStatus.NoPort:
                    body =
                        "MCP HTTP port not detected. Open Window > MCP for Unity and " +
                        "ensure HTTP transport is enabled, then try again.";
                    break;
                default:
                    body = $"Auto-bind error: {result.Message}";
                    break;
            }
            EditorUtility.DisplayDialog("MCP Auto-Binder", body, "OK");
        }

        private static bool IsEnabled()
        {
            return EditorPrefs.GetBool(PREF_KEY_ENABLED, true);
        }

        private static void BeginPolling()
        {
            if (pollingActive)
            {
                return;
            }

            pollStartTime = EditorApplication.timeSinceStartup;
            nextPollTime = pollStartTime;
            boundThisSession = false;
            pollingActive = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void StopPolling()
        {
            if (pollingActive == false)
            {
                return;
            }
            pollingActive = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPollTime)
            {
                return;
            }
            nextPollTime = now + POLL_INTERVAL_SECONDS;

            BindResult result = TryBindOnce(verbose: false);

            // Successful write: keep polling at the same cadence so a server
            // restart on a different port still gets picked up automatically
            // within the polling window. Set the "bound" flag to silence the
            // log once we're known-good.
            if (result.Status == BindStatus.Wrote || result.Status == BindStatus.AlreadyCurrent)
            {
                boundThisSession = true;
            }

            if (now - pollStartTime >= POLL_WINDOW_SECONDS)
            {
                StopPolling();
                if (boundThisSession == false)
                {
                    Debug.Log(
                        "[McpAutoBinder] Polling window elapsed without detecting an MCP HTTP server. " +
                        "If you start the server later, use 'WM > MCP > Run Auto-Bind Now' or click " +
                        "'WM > MCP > Bind Claude session to this Editor'.");
                }
            }
        }

        private static BindResult TryBindOnce(bool verbose)
        {
            try
            {
                string projectRoot = McpWorktreeBinder.GetProjectRoot();
                int port = McpWorktreeBinder.DiscoverHttpPort(projectRoot);

                // DiscoverHttpPort falls back to 8080 by default; treat that
                // as "no real signal" when no PID file or EditorPrefs URL is
                // available. We check for an actual PID file first to avoid
                // writing speculative configs that point at a dead port.
                if (HasLivePidFile(projectRoot) == false && HasExplicitHttpBaseUrl() == false)
                {
                    return new BindResult(BindStatus.NoPort, port, "no live MCP HTTP signal");
                }

                string mcpJsonPath = Path.Combine(projectRoot, ".mcp.json");
                int currentPort = ReadExistingPort(mcpJsonPath);
                if (currentPort == port)
                {
                    return new BindResult(BindStatus.AlreadyCurrent, port, mcpJsonPath);
                }

                WriteMcpJson(mcpJsonPath, port);
                if (verbose || boundThisSession == false)
                {
                    Debug.Log(
                        $"[McpAutoBinder] Bound this worktree's Claude session to http://127.0.0.1:{port}/mcp " +
                        $"(previous port {(currentPort > 0 ? currentPort.ToString() : "none")}). " +
                        $"Wrote {mcpJsonPath}.");
                }
                return new BindResult(BindStatus.Wrote, port, mcpJsonPath);
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    Debug.LogWarning($"[McpAutoBinder] Auto-bind failed: {ex.Message}");
                }
                return new BindResult(BindStatus.Error, 0, ex.Message);
            }
        }

        private static bool HasLivePidFile(string projectRoot)
        {
            string runStateDir = Path.Combine(projectRoot, "Library", "MCPForUnity", "RunState");
            if (Directory.Exists(runStateDir) == false)
            {
                return false;
            }
            string[] files = Directory.GetFiles(runStateDir, "mcp_http_*.pid");
            return files.Length > 0;
        }

        private static bool HasExplicitHttpBaseUrl()
        {
            string value = EditorPrefs.GetString("MCPForUnity.HttpBaseUrl", string.Empty);
            return string.IsNullOrEmpty(value) == false;
        }

        private static int ReadExistingPort(string mcpJsonPath)
        {
            if (File.Exists(mcpJsonPath) == false)
            {
                return 0;
            }
            try
            {
                string text = File.ReadAllText(mcpJsonPath);
                int idx = text.IndexOf("127.0.0.1:", StringComparison.Ordinal);
                if (idx < 0)
                {
                    return 0;
                }
                int start = idx + "127.0.0.1:".Length;
                int end = start;
                while (end < text.Length && char.IsDigit(text[end]))
                {
                    end++;
                }
                if (end == start)
                {
                    return 0;
                }
                if (int.TryParse(text.Substring(start, end - start), out int port))
                {
                    return port;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void WriteMcpJson(string mcpJsonPath, int port)
        {
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
            File.WriteAllText(mcpJsonPath, payload, new UTF8Encoding(false));
        }

        private enum BindStatus
        {
            Wrote,
            AlreadyCurrent,
            NoPort,
            Error
        }

        private readonly struct BindResult
        {
            public readonly BindStatus Status;
            public readonly int Port;
            public readonly string Message;

            public BindResult(BindStatus status, int port, string message)
            {
                Status = status;
                Port = port;
                Message = message;
            }
        }
    }
}
#endif
