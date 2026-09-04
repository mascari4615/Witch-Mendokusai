using System;

namespace WitchMendokusai.Presentation
{
	/// <summary>여러 UI 어셈블리가 공유하는 화면 블러 요청 수.</summary>
	public static class BlurDemand
	{
		private static int count;

		public static int Count => count;
		public static event Action<int> CountChanged = delegate { };

		public static void Add()
		{
			count++;
			CountChanged.Invoke(count);
		}

		public static void Remove()
		{
			count = Math.Max(0, count - 1);
			CountChanged.Invoke(count);
		}
	}
}
