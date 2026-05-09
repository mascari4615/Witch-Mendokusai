using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 모드 list. 사이드바는 등록 순서로 그려짐.
	/// 순수 C# 싱글톤 (MonoBehaviour 아님).
	/// </summary>
	public class DevModeRegistry
	{
		private static DevModeRegistry instance;
		public static DevModeRegistry Instance => instance ??= new DevModeRegistry();

		private readonly List<IDevMode> modes = new();

		public IReadOnlyList<IDevMode> Modes => modes;

		public void Register(IDevMode mode)
		{
			modes.Add(mode);
		}

		public IDevMode FindById(string id)
		{
			for (int i = 0; i < modes.Count; i++)
			{
				if (modes[i].Id == id)
					return modes[i];
			}
			return null;
		}

		public void Clear() => modes.Clear();
	}
}
