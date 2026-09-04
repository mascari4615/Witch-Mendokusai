using System;

namespace WitchMendokusai.Idle.UI
{
	public static class FixedGridPager
	{
		public static int PageCount(int itemCount, int pageSize)
		{
			RequirePageSize(pageSize);
			int safeCount = Math.Max(0, itemCount);
			return Math.Max(1, (safeCount + pageSize - 1) / pageSize);
		}

		public static int ClampPage(int page, int itemCount, int pageSize)
		{
			return Math.Max(0, Math.Min(page, PageCount(itemCount, pageSize) - 1));
		}

		public static int ItemIndex(int page, int cell, int pageSize)
		{
			RequirePageSize(pageSize);
			return page * pageSize + cell;
		}

		private static void RequirePageSize(int pageSize)
		{
			if (pageSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(pageSize));
			}
		}
	}
}
