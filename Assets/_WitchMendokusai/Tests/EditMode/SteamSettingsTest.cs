using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// Steam 설정 정본 잠금. App ID 는 SteamSettings 한 곳, steam_appid.txt 는 빌드 때 그 값으로 생성.
	/// </summary>
	public sealed class SteamSettingsTest
	{
		private const uint SPACEWAR_TEST_APP_ID = 480;
		private const string STEAM_ASMDEF_PATH = "Assets/_WitchMendokusai/Platform/Steam/WM.Platform.Steam.asmdef";

		[Test]
		public void Settings_load_with_real_app_id()
		{
			SteamSettings settings = SteamSettings.Load();

			Assert.That(settings.AppId, Is.Not.EqualTo(0u), "App ID 비어 있음");
			Assert.That(settings.AppId, Is.Not.EqualTo(SPACEWAR_TEST_APP_ID), "시험용 Spacewar ID 그대로");
		}

		[Test]
		public void Steam_assembly_is_desktop_only()
		{
			string json = File.ReadAllText(STEAM_ASMDEF_PATH);
			AsmdefPlatforms asmdef = JsonUtility.FromJson<AsmdefPlatforms>(json);

			Assert.That(asmdef.includePlatforms, Is.EquivalentTo(new[] { "Editor", "WindowsStandalone64" }),
				"Facepunch.Steamworks 는 Win64 플러그인만 켜져 있음. 다른 플랫폼에 열면 dll 없이 빌드 실패");
		}

		[System.Serializable]
		private sealed class AsmdefPlatforms
		{
			public string[] includePlatforms = new string[0];
		}
	}
}
