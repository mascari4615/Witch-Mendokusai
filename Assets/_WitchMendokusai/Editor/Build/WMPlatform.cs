using System;
using System.Linq;
using UnityEditor;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-197 — 지원 플랫폼 명세. **새 플랫폼 추가 = 아래 표에 한 줄.**
    ///
    /// 플랫폼이 늘 때 흩어진 switch 문을 따라다니며 고치는 구조는 반드시 한 군데를 빠뜨린다
    /// (첫 APK 시도가 그랬다 — 스팀 플러그인이 폰 빌드에 새어 들어가 컴파일 102건 실패).
    /// 그래서 「CLI 인자 이름 / 빌드 타깃 / 산출 파일명 / 그 플랫폼에서 금지되는 플러그인」을
    /// 한 줄에 모아두고, 빌드 코드는 전부 이 표에서 파생시킨다.
    ///
    /// 금지 플러그인이 왜 여기 있나: 스팀(Facepunch.Steamworks)은 데스크톱 전용이다. 모바일
    /// 빌드에 들어가면 Win32/Win64 어셈블리가 동시에 포함돼 같은 타입이 중복된다. 이 표는
    /// 「무엇이 어디에 들어가면 안 되는가」의 정본이고, 실제 포함 여부는 프로젝트의 플러그인
    /// 설정이 정한다 — <see cref="WMBuildManager"/> 는 둘이 어긋나는지 *검사만* 한다.
    /// </summary>
    public sealed class WMPlatform
    {
        // 스팀은 데스크톱(Windows/macOS/Linux) 전용. 모바일·웹에서는 금지.
        private static readonly string[] SteamPlugins = { "Facepunch.Steamworks", "steam_api" };
        private static readonly string[] NoForbiddenPlugins = new string[0];

        /// <summary>
        /// 지원 플랫폼 표. 여기 없는 플랫폼은 빌드 진입점이 받아주지 않는다(FastFail).
        /// 실제로 빌드하려면 노트북 빌드머신에 해당 유니티 모듈이 설치돼 있어야 한다.
        /// </summary>
        public static readonly WMPlatform[] All =
        {
            new WMPlatform("windows", BuildTarget.StandaloneWindows64, "WitchMendokusai.exe", NoForbiddenPlugins),
            new WMPlatform("android", BuildTarget.Android, "WitchMendokusai.apk", SteamPlugins),
            new WMPlatform("macos", BuildTarget.StandaloneOSX, "WitchMendokusai.app", NoForbiddenPlugins),
            new WMPlatform("linux", BuildTarget.StandaloneLinux64, "WitchMendokusai.x86_64", NoForbiddenPlugins),
            new WMPlatform("ios", BuildTarget.iOS, "WitchMendokusai", SteamPlugins),
        };

        private WMPlatform(string key, BuildTarget target, string outputFileName, string[] forbiddenPluginKeywords)
        {
            Key = key;
            Target = target;
            OutputFileName = outputFileName;
            ForbiddenPluginKeywords = forbiddenPluginKeywords;
        }

        /// <summary>CLI `-wmTarget` 값이자 워크플로 입력값.</summary>
        public string Key { get; }

        public BuildTarget Target { get; }

        /// <summary>산출물 파일명. iOS 는 Xcode 프로젝트 폴더라 확장자가 없다.</summary>
        public string OutputFileName { get; }

        /// <summary>이 플랫폼 빌드에 들어가면 안 되는 플러그인 (경로에 이 문구가 있으면 해당).</summary>
        public string[] ForbiddenPluginKeywords { get; }

        public static WMPlatform Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return All[0];
            }

            string normalized = key.Trim().ToLowerInvariant();
            WMPlatform found = All.FirstOrDefault(platform => platform.Key == normalized);
            if (found == null)
            {
                throw new ArgumentException(
                    $"지원하지 않는 플랫폼: '{key}' (가능: {string.Join(" | ", All.Select(p => p.Key))})");
            }
            return found;
        }

        public static WMPlatform Of(BuildTarget target)
        {
            WMPlatform found = All.FirstOrDefault(platform => platform.Target == target);
            if (found == null)
            {
                throw new ArgumentException($"표에 없는 빌드 타깃: {target}");
            }
            return found;
        }
    }
}
