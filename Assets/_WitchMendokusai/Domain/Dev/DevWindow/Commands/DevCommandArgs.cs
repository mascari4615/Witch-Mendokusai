using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WitchMendokusai
{
	// 개발자 명령 인자 공용 해석. 소수는 문화권 무관
	public static class DevCommandArgs
	{
		public static bool TryFloat(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

		public static bool Is(string text, string expected) => string.Equals(text, expected, StringComparison.OrdinalIgnoreCase);

		public static IEnumerable<string> StartsWith(IEnumerable<string> candidates, string partial) =>
			candidates.Where(candidate => string.IsNullOrEmpty(partial) || candidate.StartsWith(partial, StringComparison.OrdinalIgnoreCase));

		public static IEnumerable<string> EnumNames<T>() where T : Enum => Enum.GetNames(typeof(T)).Select(name => name.ToLowerInvariant());

		public static bool TryEnum<T>(string text, out T value) where T : struct, Enum => Enum.TryParse(text, true, out value) && Enum.IsDefined(typeof(T), value);

		// 판 안에서만 되는 명령의 공통 검사
		public static bool RequireDungeon(DevCommandContext context)
		{
			if (DungeonManagerBridge.IsDungeon)
				return true;
			context.LogError("던전 판 안에서만 됨. showcase 로 진열장에 들어가기");
			return false;
		}
	}
}
