namespace WitchMendokusai
{
	/// <summary>
	/// 명령 실행에 주입되는 컨텍스트. 출력 채널 + 명령 재진입 + 원본 입력 보존.
	/// UI 슬롯 클릭 = ctx.Invoke("give", itemRef, "1") — 명령은 single source of truth.
	/// </summary>
	public class DevCommandContext
	{
		public const int MAX_INVOKE_DEPTH = 1;

		public ConsoleView Console { get; }
		public string RawInput { get; }
		public int Depth { get; }

		public DevCommandContext(ConsoleView console, string rawInput, int depth = 0)
		{
			Console = console;
			RawInput = rawInput;
			Depth = depth;
		}

		public void LogInfo(string message) => Console.AppendLog(message, ConsoleView.LogLevel.Info);
		public void LogSuccess(string message) => Console.AppendLog(message, ConsoleView.LogLevel.Success);
		public void LogWarn(string message) => Console.AppendLog(message, ConsoleView.LogLevel.Warn);
		public void LogError(string message) => Console.AppendLog(message, ConsoleView.LogLevel.Error);

		/// <summary>다른 명령을 호출. 재귀 깊이 MAX_INVOKE_DEPTH 초과면 LogError + false 반환.</summary>
		public bool Invoke(string commandName, params string[] args)
		{
			if (Depth >= MAX_INVOKE_DEPTH)
			{
				LogError($"명령 재진입 깊이 초과 ({Depth} >= {MAX_INVOKE_DEPTH}): {commandName}");
				return false;
			}

			if (DevCommandRegistry.Instance.TryGet(commandName, out IDevCommand command) == false)
			{
				LogError($"알 수 없는 명령: {commandName}");
				return false;
			}

			DevCommandContext nested = new(Console, $"{commandName} {string.Join(' ', args)}", Depth + 1);
			try
			{
				command.Execute(nested, args);
				return true;
			}
			catch (System.Exception exception)
			{
				LogError($"명령 실행 중 예외: {exception.Message}");
				UnityEngine.Debug.LogException(exception);
				return false;
			}
		}
	}
}
