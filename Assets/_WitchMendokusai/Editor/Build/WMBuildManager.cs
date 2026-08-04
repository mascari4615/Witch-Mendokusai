using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
    /// <summary>
    /// TASK-WM-197 — 빌드 전 구성 검사관.
    ///
    /// ★ 원칙: **빌드는 프로젝트 상태를 바꾸지 않는다.**
    /// 「폰 빌드니까 스팀을 잠깐 꺼두고 끝나면 되돌린다」 식이면, 되돌리기가 실패하거나 빌드가
    /// 중간에 죽었을 때 작업 폴더가 오염된 채 남고 동시 빌드도 위험하다. 그래서 포함/제외 같은
    /// 구조적 설정은 프로젝트 정본에 눈에 보이게 박아두고(유니티 Inspector 에서 그대로 보인다),
    /// 빌드는 <see cref="WMPlatform"/> 표와 실제 설정이 어긋나는지 **검사만** 한다.
    ///
    /// 어긋나면 빌드 전에 멈춘다 — 30분짜리 빌드를 태우고 컴파일 에러로 죽는 것보다 낫고,
    /// 무엇을 어디서 고쳐야 하는지 메시지에 적는다.
    /// </summary>
    public static class WMBuildManager
    {
        public static void Validate(WMPlatform platform)
        {
            ValidateForbiddenPlugins(platform);
            ValidateAndroidSigning(platform);
            Debug.Log($"[WM-BUILD] 구성 검사 통과 — {platform.Key}");
        }

        // 플러그인 포함 여부는 「모든 플랫폼」 체크 하나로 조용히 새어 들어간다. 이번 사고의 원인.
        private static void ValidateForbiddenPlugins(WMPlatform platform)
        {
            if (platform.ForbiddenPluginKeywords.Length == 0)
            {
                return;
            }

            List<string> leaked = new List<string>();
            foreach (PluginImporter importer in PluginImporter.GetAllImporters())
            {
                string path = importer.assetPath;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                bool forbidden = platform.ForbiddenPluginKeywords.Any(keyword =>
                    path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                if (forbidden == false)
                {
                    continue;
                }

                bool included = importer.GetCompatibleWithAnyPlatform()
                    ? importer.GetExcludeFromAnyPlatform(platform.Target) == false
                    : importer.GetCompatibleWithPlatform(platform.Target);
                if (included)
                {
                    leaked.Add(path);
                }
            }

            if (leaked.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{platform.Key} 빌드에 들어가면 안 되는 플러그인이 포함으로 잡혀 있다 ({leaked.Count}개):\n"
                + string.Join("\n", leaked.Select(path => "  - " + path))
                + $"\n\n고치는 법: Project 창에서 해당 파일 선택 → Inspector 플랫폼 설정에서 {platform.Key} 제외 → Apply.");
        }

        // 커스텀 keystore 를 켜둔 채 키 파일이 없으면 빌드가 한참 뒤에 서명 단계에서 죽는다.
        // 그 조합을 미리 잡는다. (키 파일은 비밀이라 저장소에 없다 = 기본은 디버그 키 서명)
        private static void ValidateAndroidSigning(WMPlatform platform)
        {
            if (platform.Target != BuildTarget.Android)
            {
                return;
            }

            if (PlayerSettings.Android.useCustomKeystore == false)
            {
                return;
            }

            string keystore = PlayerSettings.Android.keystoreName;
            if (string.IsNullOrEmpty(keystore) == false && System.IO.File.Exists(keystore))
            {
                return;
            }

            throw new InvalidOperationException(
                "안드로이드 커스텀 keystore 가 켜져 있는데 키 파일을 찾을 수 없다: "
                + $"'{keystore}'\n키 파일을 준비하거나, 테스트 빌드라면 Player Settings 에서 "
                + "Custom Keystore 를 꺼라(유니티 디버그 키로 서명된다).");
        }

        /// <summary>그 빌드 한 번의 출력 형태만 정한다 (프로젝트 구조는 건드리지 않는다).</summary>
        public static void ApplyOutputSettings(WMPlatform platform, bool development)
        {
            EditorUserBuildSettings.development = development;

            if (platform.Target == BuildTarget.Android)
            {
                // 폰에 바로 설치해 확인하는 게 목적 — 스토어용 aab 아님.
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.Generic;
            }
        }
    }
}
