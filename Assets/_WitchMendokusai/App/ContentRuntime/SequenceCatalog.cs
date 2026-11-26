using System;
using System.Collections.Generic;
using System.IO;

namespace WitchMendokusai.ContentRuntime
{
    // 게임 보관함의 연출 이름과 파일. 이름은 장면 편집기의 '게임 보관함에 내보내기' 파일 이름에서 확장자를 뺀 것
    public static class SequenceCatalog
    {
        public const string SUFFIX = ".sequence.json";

        public static IReadOnlyList<string> Keys
        {
            get
            {
                string directory = SequenceLibraryLocation.Directory;
                List<string> keys = new List<string>();
                if (Directory.Exists(directory) == false) return keys;
                foreach (string file in Directory.GetFiles(directory, "*" + SUFFIX, SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    keys.Add(name.Substring(0, name.Length - SUFFIX.Length));
                }
                keys.Sort(StringComparer.Ordinal);
                return keys;
            }
        }

        public static bool Contains(string key)
        {
            return IsPlainName(key) && File.Exists(PathOf(key));
        }

        // 없는 이름은 있는 이름 목록과 함께 예외. 사람과 AI 가 메시지만 보고 고칠 수 있게
        public static string FileOf(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("연출 이름이 비었다", "key");
            if (IsPlainName(key) == false) throw new ArgumentException("연출 이름에 폴더 구분자나 .. 를 쓸 수 없다: " + key, "key");
            string file = PathOf(key);
            if (File.Exists(file) == false)
            {
                IReadOnlyList<string> keys = Keys;
                string known = keys.Count == 0 ? "보관함이 비었다" : "있는 이름: " + string.Join(", ", keys);
                throw new FileNotFoundException("보관함에 없는 연출: \"" + key + "\". " + known, file);
            }
            return file;
        }

        private static string PathOf(string key)
        {
            return Path.Combine(SequenceLibraryLocation.Directory, key + SUFFIX);
        }

        private static bool IsPlainName(string key)
        {
            return string.IsNullOrWhiteSpace(key) == false && key.IndexOfAny(new[] { '/', '\\' }) < 0 && key.Contains("..") == false;
        }
    }
}
