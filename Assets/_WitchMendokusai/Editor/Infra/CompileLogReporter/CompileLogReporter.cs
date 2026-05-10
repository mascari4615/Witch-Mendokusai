#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Networking;

namespace WitchMendokusai.Editor.Infra.CompileLogReporter
{
	/// <summary>
	/// TASK-WM-087 — WM 컴파일 결과 (error CS\d+ / warning CS\d+) 를 yawnbot 디스코봇
	/// webhook 으로 자동 알림. Mono runtime 직접 hook (CompilationPipeline) 이라 폴링 0 +
	/// parse 0 (CompilerMessage 직접) + 실시간.
	///
	/// 흐름:
	///   assemblyCompilationFinished × N (assembly 별 messages 누적)
	///     ↓
	///   compilationFinished (전체 끝)
	///     ↓
	///   fingerprint dedup → POST yawnbot.mascari4615.com/webhook/local
	///
	/// 노브 (EditorPrefs key):
	///   WitchMendokusai.CompileLogReporter.Url      — endpoint override (default = https://yawnbot.mascari4615.com/webhook/local)
	///   WitchMendokusai.CompileLogReporter.Secret   — X-Yawnbot-Secret header (yawnbot LOCAL_WEBHOOK_SECRET 와 정합)
	///   WitchMendokusai.CompileLogReporter.Disabled — true 면 비활성
	///
	/// 옛 KarmoLab Tauri 안 wm_log_watcher.rs (폴링 + parse + POST) 폐기됨 — 도메인 mismatch
	/// (KarmoLab desktop 영역에 WM 컴파일 watcher 박은 보수안). 본 클래스가 정본.
	/// </summary>
	[InitializeOnLoad]
	public static class CompileLogReporter
	{
		private const string DefaultUrl = "https://yawnbot.mascari4615.com/webhook/local";
		private const string EditorPrefsUrlKey = "WitchMendokusai.CompileLogReporter.Url";
		private const string EditorPrefsSecretKey = "WitchMendokusai.CompileLogReporter.Secret";
		private const string EditorPrefsDisabledKey = "WitchMendokusai.CompileLogReporter.Disabled";
		private const string SessionStateFingerprintKey = "WitchMendokusai.CompileLogReporter.LastFingerprint";
		private const string SourceLabel = "wm-editor/CompileLogReporter";
		private const int MaxLinesInSummary = 8;
		private const int MaxLocationLength = 120;
		private const int MaxMessageLength = 240;
		private const int RequestTimeoutSeconds = 5;

		private static readonly List<CompileEntry> CurrentErrors = new List<CompileEntry>();
		private static readonly List<CompileEntry> CurrentWarnings = new List<CompileEntry>();

		private static readonly Regex CsCodeRegex = new Regex(
			@"^(?:error|warning)\s+(CS\d+):\s*(.*)$",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		static CompileLogReporter()
		{
			CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
			CompilationPipeline.compilationFinished += OnCompilationFinished;
		}

		private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
		{
			if (EditorPrefs.GetBool(EditorPrefsDisabledKey, false) == true)
			{
				return;
			}
			if (messages == null || messages.Length == 0)
			{
				return;
			}

			for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++)
			{
				CompilerMessage compilerMessage = messages[messageIndex];
				if (TryParse(compilerMessage, out CompileEntry compileEntry) == false)
				{
					continue;
				}
				if (compileEntry.Severity == CompileSeverity.Error)
				{
					CurrentErrors.Add(compileEntry);
				}
				else
				{
					CurrentWarnings.Add(compileEntry);
				}
			}
		}

		private static void OnCompilationFinished(object compilationContext)
		{
			try
			{
				if (CurrentErrors.Count == 0 && CurrentWarnings.Count == 0)
				{
					return;
				}
				if (EditorPrefs.GetBool(EditorPrefsDisabledKey, false) == true)
				{
					return;
				}

				ulong fingerprint = ComputeFingerprint(CurrentErrors, CurrentWarnings);
				string previousFingerprintRaw = SessionState.GetString(SessionStateFingerprintKey, string.Empty);
				if (previousFingerprintRaw == fingerprint.ToString())
				{
					Debug.Log($"[CompileLogReporter] 같은 fingerprint — 알림 skip ({CurrentErrors.Count} error / {CurrentWarnings.Count} warning)");
					return;
				}

				List<CompileEntry> errorsCopy = CurrentErrors.ToList();
				List<CompileEntry> warningsCopy = CurrentWarnings.ToList();
				SendBatch(errorsCopy, warningsCopy, fingerprint);
			}
			finally
			{
				CurrentErrors.Clear();
				CurrentWarnings.Clear();
			}
		}

		private static bool TryParse(CompilerMessage compilerMessage, out CompileEntry compileEntry)
		{
			compileEntry = default;
			if (compilerMessage.type != CompilerMessageType.Error && compilerMessage.type != CompilerMessageType.Warning)
			{
				return false;
			}

			string rawMessage = compilerMessage.message ?? string.Empty;
			Match match = CsCodeRegex.Match(rawMessage);
			string csCode;
			string body;
			if (match.Success == true)
			{
				csCode = match.Groups[1].Value;
				body = match.Groups[2].Value;
			}
			else
			{
				csCode = "?";
				body = rawMessage;
			}

			string locationFile = string.IsNullOrEmpty(compilerMessage.file) == true
				? "?"
				: compilerMessage.file.Replace('\\', '/');
			string location = $"{locationFile}({compilerMessage.line},{compilerMessage.column})";

			compileEntry = new CompileEntry
			{
				Severity = compilerMessage.type == CompilerMessageType.Error
					? CompileSeverity.Error
					: CompileSeverity.Warning,
				CsCode = csCode,
				Location = location,
				Body = body,
			};
			return true;
		}

		private static ulong ComputeFingerprint(List<CompileEntry> errors, List<CompileEntry> warnings)
		{
			// FNV-1a 64-bit. severity+CS코드+location 만 (body 무관 — 같은 위치/코드 메시지
			// 다양 변형 흡수해서 spam dedup).
			ulong hash = 14695981039346656037UL;
			for (int errorIndex = 0; errorIndex < errors.Count; errorIndex++)
			{
				hash = MixFingerprint(hash, errors[errorIndex]);
			}
			for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
			{
				hash = MixFingerprint(hash, warnings[warningIndex]);
			}
			return hash;
		}

		private static ulong MixFingerprint(ulong hash, CompileEntry entry)
		{
			string fingerprintKey = $"{(int)entry.Severity}|{entry.CsCode}|{entry.Location}";
			for (int charIndex = 0; charIndex < fingerprintKey.Length; charIndex++)
			{
				hash ^= fingerprintKey[charIndex];
				hash *= 1099511628211UL;
			}
			return hash;
		}

		private static void SendBatch(List<CompileEntry> errors, List<CompileEntry> warnings, ulong fingerprint)
		{
			string endpointUrl = EditorPrefs.GetString(EditorPrefsUrlKey, DefaultUrl);
			string sharedSecret = EditorPrefs.GetString(EditorPrefsSecretKey, string.Empty);

			string title = BuildTitle(errors.Count, warnings.Count);
			string summary = BuildSummary(errors, warnings);
			string level = errors.Count > 0 ? "error" : "warning";
			string kind = errors.Count > 0 ? "wm-compile-error" : "wm-compile-warning";

			string payloadJson = BuildPayloadJson(kind, title, summary, level);
			byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

			UnityWebRequest webRequest = new UnityWebRequest(endpointUrl, UnityWebRequest.kHttpVerbPOST);
			webRequest.uploadHandler = new UploadHandlerRaw(payloadBytes);
			webRequest.downloadHandler = new DownloadHandlerBuffer();
			webRequest.SetRequestHeader("Content-Type", "application/json");
			if (string.IsNullOrEmpty(sharedSecret) == false)
			{
				webRequest.SetRequestHeader("X-Yawnbot-Secret", sharedSecret);
			}
			webRequest.timeout = RequestTimeoutSeconds;

			UnityWebRequestAsyncOperation asyncOperation = webRequest.SendWebRequest();
			asyncOperation.completed += operationContext =>
			{
				try
				{
					if (webRequest.result == UnityWebRequest.Result.Success)
					{
						Debug.Log($"[CompileLogReporter] yawnbot POST OK ({errors.Count} error / {warnings.Count} warning)");
						// POST 성공 시만 fingerprint 갱신 — fail 시 다음 batch 다시 시도.
						SessionState.SetString(SessionStateFingerprintKey, fingerprint.ToString());
					}
					else
					{
						string responseText = webRequest.downloadHandler != null
							? webRequest.downloadHandler.text
							: string.Empty;
						Debug.LogWarning($"[CompileLogReporter] yawnbot POST 실패 (result={webRequest.result}, status={webRequest.responseCode}, body={Truncate(responseText, 200)}, error={webRequest.error})");
					}
				}
				finally
				{
					webRequest.Dispose();
				}
			};
		}

		private static string BuildTitle(int errorCount, int warningCount)
		{
			if (errorCount > 0 && warningCount > 0)
			{
				return $"WM Editor.log: {errorCount} error · {warningCount} warning";
			}
			if (errorCount > 0)
			{
				return $"WM Editor.log: {errorCount} error";
			}
			return $"WM Editor.log: {warningCount} warning";
		}

		private static string BuildSummary(List<CompileEntry> errors, List<CompileEntry> warnings)
		{
			StringBuilder summaryBuilder = new StringBuilder();
			int linesShown = 0;
			for (int errorIndex = 0; errorIndex < errors.Count; errorIndex++)
			{
				if (linesShown >= MaxLinesInSummary)
				{
					break;
				}
				AppendEntry(summaryBuilder, errors[errorIndex], false);
				linesShown++;
			}
			if (errors.Count > linesShown)
			{
				summaryBuilder.AppendLine($"…+{errors.Count - linesShown} more errors");
				summaryBuilder.AppendLine();
			}

			int warningRoom = MaxLinesInSummary - linesShown;
			if (warnings.Count > 0 && warningRoom > 0)
			{
				summaryBuilder.AppendLine("---");
				int warningsShown = 0;
				for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
				{
					if (warningsShown >= warningRoom)
					{
						break;
					}
					AppendEntry(summaryBuilder, warnings[warningIndex], true);
					warningsShown++;
				}
				if (warnings.Count > warningsShown)
				{
					summaryBuilder.Append($"…+{warnings.Count - warningsShown} more warnings");
				}
			}
			return summaryBuilder.ToString();
		}

		private static void AppendEntry(StringBuilder builder, CompileEntry entry, bool labelWarning)
		{
			string label = labelWarning == true ? " (warning)" : string.Empty;
			builder.AppendLine($"`{entry.CsCode}`{label} {Truncate(entry.Location, MaxLocationLength)}");
			builder.AppendLine(Truncate(entry.Body, MaxMessageLength));
			builder.AppendLine();
		}

		private static string Truncate(string raw, int maxLength)
		{
			if (string.IsNullOrEmpty(raw) == true)
			{
				return string.Empty;
			}
			if (raw.Length <= maxLength)
			{
				return raw;
			}
			return raw.Substring(0, maxLength) + "…";
		}

		private static string BuildPayloadJson(string kind, string title, string summary, string level)
		{
			// JsonUtility 가 generic Dictionary unsupported → 명시 escaping. 본 5 field 만이라 충분.
			StringBuilder builder = new StringBuilder();
			builder.Append('{');
			builder.Append("\"kind\":\"").Append(EscapeJson(kind)).Append("\",");
			builder.Append("\"source\":\"").Append(EscapeJson(SourceLabel)).Append("\",");
			builder.Append("\"title\":\"").Append(EscapeJson(title)).Append("\",");
			builder.Append("\"summary\":\"").Append(EscapeJson(summary)).Append("\",");
			builder.Append("\"level\":\"").Append(EscapeJson(level)).Append('"');
			builder.Append('}');
			return builder.ToString();
		}

		private static string EscapeJson(string raw)
		{
			if (string.IsNullOrEmpty(raw) == true)
			{
				return string.Empty;
			}
			StringBuilder builder = new StringBuilder(raw.Length + 8);
			for (int charIndex = 0; charIndex < raw.Length; charIndex++)
			{
				char currentChar = raw[charIndex];
				switch (currentChar)
				{
					case '"': builder.Append("\\\""); break;
					case '\\': builder.Append("\\\\"); break;
					case '\n': builder.Append("\\n"); break;
					case '\r': builder.Append("\\r"); break;
					case '\t': builder.Append("\\t"); break;
					case '\b': builder.Append("\\b"); break;
					case '\f': builder.Append("\\f"); break;
					default:
						if (currentChar < 0x20)
						{
							builder.Append($"\\u{(int)currentChar:X4}");
						}
						else
						{
							builder.Append(currentChar);
						}
						break;
				}
			}
			return builder.ToString();
		}

		private enum CompileSeverity
		{
			Error = 0,
			Warning = 1,
		}

		private struct CompileEntry
		{
			public CompileSeverity Severity;
			public string CsCode;
			public string Location;
			public string Body;
		}
	}
}
#endif
