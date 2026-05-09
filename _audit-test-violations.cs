// TASK-WM-063 sub-K POC 검증 — 의도적 WM 룰 위반.
// 본 파일은 audit 가 위반을 catch 하는지 test. 검증 후 revert.
// Unity 가 안 보는 root 위치 (Assets/ 외).

namespace AuditTest
{
    public class ViolationsTest
    {
        // 위반 1: var 금지
        public void Method1()
        {
            var x = 5;  // ← var 금지 룰 위반
            var name = "test";  // ← 같음
        }

        // 위반 2: ! 연산자 (== false 사용해야)
        public void Method2(bool isValid)
        {
            if (!isValid)  // ← ! 금지 룰 위반
            {
                return;
            }
        }

        // 위반 3: 한 글자 변수명 (i, j 외)
        public void Method3()
        {
            int t = 100;  // ← 변수명 풀네임 룰 위반
            string s = "abc";  // ← 같음
        }

        // 위반 4: 상수 UPPER_SNAKE_CASE 안 지킴
        public const int maxValue = 99;  // ← 상수 UPPER_SNAKE_CASE 룰 위반
    }
}
