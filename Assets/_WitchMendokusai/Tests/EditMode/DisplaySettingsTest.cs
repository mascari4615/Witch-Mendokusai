using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 화면 설정의 값 풀이. PlayerPrefs 와 화면 적용은 건드리지 않는 순수 함수만
	public class DisplaySettingsTest
	{
		[Test]
		public void VSync_on_leaves_frame_rate_unlimited()
			=> Assert.AreEqual(DisplaySettings.FRAME_RATE_UNLIMITED, DisplaySettings.ResolveTargetFrameRate(true, 60, 144d));

		[Test]
		public void Monitor_choice_follows_refresh_rate()
			=> Assert.AreEqual(144, DisplaySettings.ResolveTargetFrameRate(false, DisplaySettings.FRAME_RATE_MONITOR, 143.9d));

		[Test]
		public void Monitor_choice_without_refresh_rate_is_unlimited()
			=> Assert.AreEqual(DisplaySettings.FRAME_RATE_UNLIMITED, DisplaySettings.ResolveTargetFrameRate(false, DisplaySettings.FRAME_RATE_MONITOR, 0d));

		[Test]
		public void Fixed_choice_is_used_as_is()
			=> Assert.AreEqual(60, DisplaySettings.ResolveTargetFrameRate(false, 60, 144d));

		private static readonly Resolution[] SUPPORTED =
		{
			new Resolution { width = 1280, height = 720 },
			new Resolution { width = 1600, height = 900 },
			new Resolution { width = 1920, height = 1080 },
		};

		[Test]
		public void Fullscreen_larger_than_monitor_falls_back_to_monitor()
			=> Assert.AreEqual(new Vector2Int(1920, 1080), DisplaySettings.FitToDisplay(new Vector2Int(2560, 1440), new Vector2Int(1920, 1080), true, SUPPORTED));

		[Test]
		public void Window_as_big_as_monitor_steps_down_one_size()
			=> Assert.AreEqual(new Vector2Int(1600, 900), DisplaySettings.FitToDisplay(new Vector2Int(1920, 1080), new Vector2Int(1920, 1080), false, SUPPORTED));

		[Test]
		public void Window_smaller_than_monitor_is_kept()
			=> Assert.AreEqual(new Vector2Int(1280, 720), DisplaySettings.FitToDisplay(new Vector2Int(1280, 720), new Vector2Int(1920, 1080), false, SUPPORTED));

		[Test]
		public void Resolutions_are_unique_sorted_and_keep_current()
		{
			Resolution[] supported =
			{
				new Resolution { width = 1920, height = 1080 },
				new Resolution { width = 800, height = 600 },
				new Resolution { width = 1280, height = 720 },
				new Resolution { width = 1920, height = 1080 },
			};

			List<Vector2Int> result = SettingView.CollectResolutions(supported, new Vector2Int(1600, 900), 720);

			CollectionAssert.AreEqual(new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) }, result);
		}
	}
}
