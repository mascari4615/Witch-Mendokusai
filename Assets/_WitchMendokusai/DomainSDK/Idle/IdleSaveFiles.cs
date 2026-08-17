using System;
using System.IO;
using System.Text;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 저장 파일을 <b>안전하게</b> 적고 되읽는 손놀림 (TASK-WM-406).
    ///
    /// ★ 왜 코어에 있나 — 여기엔 Unity 가 한 줄도 없다(파일 다루기는 .NET 이다).
    ///   전에는 이 손놀림이 엔진 편에 있어서 <b>시험이 하나도 없었다</b>. 그래서
    ///   「바꿔치기 한다」고 적어 놓고 실제로는 원본을 먼저 지우는 코드가 오래 살아 있었다
    ///   (적는 도중에 죽으면 저장이 통째로 사라진다 — 방치형에서 몇 주치다).
    ///   판정이 아니라 <b>사람의 판</b>을 지키는 자리라서, 시험이 없으면 안 되는 자리다.
    ///
    /// ★ 무엇을 적을지(JSON 짓기)는 여기가 안 한다 — 그건 그릇을 쥔 쪽 일이다.
    ///   여기는 <b>글자 하나</b>를 받아 잃지 않게 넣고 꺼내 준다.
    ///
    /// ★ 세 가지를 지킨다:
    ///   ① <b>디스크까지</b> 밀어 넣고 나서 바꿔치기한다 (캐시에만 있는 빈 껍데기로 원본을 갈지 않게)
    ///   ② 바꿔치기 — 원본이 없는 순간을 안 만든다. 덤으로 <b>직전 판</b>이 남는다
    ///   ③ 못 읽으면 직전 판으로 되살리고, 깨진 것은 옆으로 <b>옮긴다</b>
    ///      (그냥 두면 다음 저장이 덮어써서 증거가 사라진다)
    /// </summary>
    public static class IdleSaveFiles
    {
        /// <summary>어떻게 읽혔나 — 화면·로그가 사람에게 뭐라 할지 정하는 재료.</summary>
        public enum ReadOutcome
        {
            /// <summary>아무것도 없다 — 처음 켠 사람.</summary>
            Nothing = 0,

            /// <summary>제대로 읽었다.</summary>
            Fine = 1,

            /// <summary>본 파일이 깨져서 <b>직전 판</b>으로 되살렸다.</summary>
            FellBackToBackup = 2,

            /// <summary>둘 다 못 쓴다 — 처음부터 시작해야 한다.</summary>
            Lost = 3,
        }

        public static string BackupPathFor(string path)
        {
            return path + ".bak";
        }

        public static string BrokenPathFor(string path)
        {
            return path + ".broken";
        }

        /// <summary>
        /// 글자를 적는다. 적는 도중에 죽어도 <b>옛 판이 남는다</b>.
        /// </summary>
        public static void Write(string path, string payload)
        {
            string temporary = path + ".tmp";

            using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                stream.Write(bytes, 0, bytes.Length);

                // 캐시가 아니라 <b>디스크</b>까지. 이게 없으면 전원이 나갔을 때
                // 새 파일이 빈 껍데기인 채로 원본 자리에 올라앉는다.
                stream.Flush(true);
            }

            SwapIntoPlace(temporary, path);
        }

        /// <summary>
        /// 글자를 읽는다. 본 파일이 못 쓸 것이면 <b>직전 판</b>을 준다.
        /// </summary>
        /// <param name="looksUsable">읽은 글자가 쓸 만한지 판별한다 — 그릇 쪽이 정한다.</param>
        public static ReadOutcome Read(string path, Func<string, bool> looksUsable, out string payload)
        {
            payload = null;

            if (File.Exists(path) == false)
            {
                return File.Exists(BackupPathFor(path)) && TryRead(BackupPathFor(path), looksUsable, out payload)
                    ? ReadOutcome.FellBackToBackup
                    : ReadOutcome.Nothing;
            }

            if (TryRead(path, looksUsable, out payload))
            {
                return ReadOutcome.Fine;
            }

            // 다음 저장이 덮어쓰기 <b>전에</b> 옮긴다. 「남긴다」는 말만으로는 안 남는다.
            MoveAside(path);

            return TryRead(BackupPathFor(path), looksUsable, out payload)
                ? ReadOutcome.FellBackToBackup
                : ReadOutcome.Lost;
        }

        private static bool TryRead(string path, Func<string, bool> looksUsable, out string payload)
        {
            payload = null;

            if (File.Exists(path) == false)
            {
                return false;
            }

            try
            {
                string text = File.ReadAllText(path);

                if (looksUsable != null && looksUsable(text) == false)
                {
                    return false;
                }

                payload = text;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 새 파일을 제자리로 <b>바꿔치기</b>한다.
        ///
        /// ⚠ 백업 이름을 <b>반드시 준다</b>. null 로 부르면 이 환경에서 곧바로 터진다
        ///   (실측 2026-08-17: ArgumentException). 게다가 백업은 공짜로 직전 판을 남겨 준다.
        /// </summary>
        private static void SwapIntoPlace(string temporary, string path)
        {
            if (File.Exists(path) == false)
            {
                File.Move(temporary, path);
                return;
            }

            File.Replace(temporary, path, BackupPathFor(path));
        }

        private static void MoveAside(string path)
        {
            try
            {
                string broken = BrokenPathFor(path);

                if (File.Exists(broken))
                {
                    File.Delete(broken);
                }

                File.Move(path, broken);
            }
            catch (Exception)
            {
                // 못 옮겨도 판을 멈출 이유는 없다 — 되살리기는 계속 시도한다.
            }
        }
    }
}
