using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 「지금 켠 이게 어느 빌드인가」의 단일 정본.
    ///
    /// 빌드 직전에 <c>WMBuildInfoBuildStep</c> 이 커밋·브랜치·시각·CI 번호를 긁어
    /// <c>Resources/BuildInfo.json</c> 으로 굽는다. 런타임은 그 파일만 읽는다.
    ///
    /// **화면에 보이는 글자 = 로그에 남는 글자 = 디스코드에 뜨는 글자.** 세 곳이 각자
    /// 문자열을 조립하면 하나만 바뀌어도 추적이 끊긴다. 그래서 조립은 여기 한 곳에서만 한다.
    ///
    /// 파일이 없으면(에디터 플레이·구버전 빌드) 죽지 않고 「editor」로 답한다 —
    /// 진단 도구가 없다고 게임이 멈추는 건 본말전도다.
    /// </summary>
    [Serializable]
    public class BuildInfo
    {
        public const string RESOURCE_NAME = "BuildInfo";
        private const int SHORT_COMMIT_LENGTH = 7;

        /// <summary>전체 커밋 해시. 비면 미상.</summary>
        public string commit = string.Empty;
        public string branch = string.Empty;
        /// <summary>CI 실행 번호 = 안드로이드 versionCode. 0 = 손으로 구운 빌드.</summary>
        public int buildNumber;
        /// <summary>빌드 시각 (KST, "yyyy-MM-dd HH:mm").</summary>
        public string builtAtKst = string.Empty;
        /// <summary>"dev" 또는 "release".</summary>
        public string channel = string.Empty;
        public string platform = string.Empty;
        public string unityVersion = string.Empty;
        /// <summary>이 빌드를 구운 CI 실행 주소 (없으면 빈 문자열).</summary>
        public string runUrl = string.Empty;
        /// <summary>빌드 시점에 작업 폴더가 더러웠는가 — 커밋만으로는 재현이 안 되는 빌드라는 뜻.</summary>
        public bool dirty;

        private static BuildInfo _current;

        /// <summary>이 실행의 빌드 정보. 없으면 에디터/미상용 기본값.</summary>
        public static BuildInfo Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Load();
                }
                return _current;
            }
        }

        private static BuildInfo Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
            if (asset != null)
            {
                BuildInfo parsed = Parse(asset.text);
                if (parsed != null)
                {
                    return parsed;
                }
            }
            return Fallback();
        }

        /// <summary>JSON → BuildInfo. 깨져 있으면 null (호출부가 기본값으로 간다).</summary>
        public static BuildInfo Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                BuildInfo parsed = JsonUtility.FromJson<BuildInfo>(json);
                return parsed;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>구운 정보가 없을 때 — 에디터거나, 이 장치가 만들어지기 전 빌드다.</summary>
        public static BuildInfo Fallback()
        {
            return new BuildInfo
            {
                commit = string.Empty,
                branch = string.Empty,
                buildNumber = 0,
#if UNITY_EDITOR
                channel = "editor",
#else
                channel = Debug.isDebugBuild ? "dev" : "release",
#endif
                platform = UnityEngine.Application.platform.ToString(),
                unityVersion = UnityEngine.Application.unityVersion,
                builtAtKst = string.Empty,
                runUrl = string.Empty,
                dirty = false,
            };
        }

        public string ShortCommit =>
            string.IsNullOrEmpty(commit)
                ? "?"
                : commit.Substring(0, Math.Min(SHORT_COMMIT_LENGTH, commit.Length));

        /// <summary>접힌 상태 1줄 — 화면 구석에 늘 떠 있는 글자. 예: "dev #412 · a3f9c21*".</summary>
        public string CollapsedLine()
        {
            string number = buildNumber > 0 ? $" #{buildNumber.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
            string mark = dirty ? "*" : string.Empty;
            string channelText = string.IsNullOrEmpty(channel) ? "?" : channel;
            return $"{channelText}{number} · {ShortCommit}{mark}";
        }

        /// <summary>펼친 상태 줄들 — 라벨/값 쌍. 값이 없는 줄은 아예 안 넣는다(빈칸이 더 헷갈린다).</summary>
        public List<KeyValuePair<string, string>> DetailRows()
        {
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            rows.Add(new KeyValuePair<string, string>("빌드", CollapsedLine()));
            if (string.IsNullOrEmpty(branch) == false)
            {
                rows.Add(new KeyValuePair<string, string>("가지", branch));
            }
            if (string.IsNullOrEmpty(builtAtKst) == false)
            {
                rows.Add(new KeyValuePair<string, string>("구운 때", builtAtKst + " KST"));
            }
            if (dirty)
            {
                rows.Add(new KeyValuePair<string, string>("주의", "커밋 안 된 변경이 섞인 빌드"));
            }
            rows.Add(new KeyValuePair<string, string>("기기", $"{SystemInfo.deviceModel}"));
            rows.Add(new KeyValuePair<string, string>("체제", SystemInfo.operatingSystem));
            rows.Add(new KeyValuePair<string, string>("유니티", string.IsNullOrEmpty(unityVersion)
                ? UnityEngine.Application.unityVersion
                : unityVersion));
            rows.Add(new KeyValuePair<string, string>("앱", UnityEngine.Application.version));
            if (string.IsNullOrEmpty(runUrl) == false)
            {
                rows.Add(new KeyValuePair<string, string>("CI", runUrl));
            }
            return rows;
        }

        /// <summary>복사·로그·디스코드에 실리는 한 덩어리. 사람이 그대로 붙여넣어 신고할 수 있게.</summary>
        public string Describe()
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> row in DetailRows())
            {
                parts.Add($"{row.Key}: {row.Value}");
            }
            return string.Join("\n", parts);
        }

        /// <summary>로그 세션에 실리는 한 줄 라벨 (화면 접힌 줄과 같은 뼈대 + 앱 버전).</summary>
        public string ShortLabel()
        {
            return $"{CollapsedLine()} · {UnityEngine.Application.version}";
        }
    }
}
