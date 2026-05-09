using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 명령 dictionary. 순수 C# 싱글톤 (MonoBehaviour 아님).
	/// DevWindowController 가 시작 시 내장 명령들을 Register.
	/// </summary>
	public class DevCommandRegistry
	{
		private static DevCommandRegistry instance;
		public static DevCommandRegistry Instance => instance ??= new DevCommandRegistry();

		private readonly Dictionary<string, IDevCommand> commands = new();

		public IEnumerable<string> AllNames => commands.Keys;
		public IEnumerable<IDevCommand> AllCommands => commands.Values;

		public void Register(IDevCommand command)
		{
			commands[command.Name] = command;
		}

		public bool TryGet(string name, out IDevCommand command) => commands.TryGetValue(name, out command);

		public void Clear() => commands.Clear();
	}
}
