# -*- coding: utf-8 -*-
"""
일부러 깨 보는 점검 (TASK-WM-052).

★ 왜 필요한가: 「시험으로 잠갔다」와 「그 시험이 실제로 잡는다」는 다른 말이다.
  실제로 왕복 시험 하나가 **뜻이 바뀌어도 통과**하고 있었고(2026-08-08), 아무도 몰랐다.
  초록 숫자는 「아무도 안 깨뜨렸다」는 뜻이지 「깨지면 잡는다」는 뜻이 아니다.

무엇을 하나: 핵심 동작을 하나씩 **일부러 망가뜨리고** 하네스를 돌린다.
  - CAUGHT = 시험이 잡았다(정상)
  - MISSED = 그 동작은 **아무도 안 보고 있다** → 시험을 하나 더 둬야 한다

원본은 매번 복구한다(실패하든 말든). 그래도 돌리기 전에 커밋해 두는 편이 안전하다.

실행: python Tools/dialogue-harness/mutation-check.py

새 규칙을 만들 때마다 여기 한 줄 추가하는 것을 권한다 — 그게 「깨 봤다」의 기록이 된다.
"""

import io
import os
import subprocess
import sys

# 콘솔 기본 인코딩이 한글을 못 내보내는 환경(윈도우 cp949)에서도 결과가 읽히게 한다.
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')).replace('\\', '/') + '/'

# (설명, 파일, 원래 코드, 망가뜨린 코드)
MUTATIONS = [
    ("건너뛰기가 선택지에서 안 멈춤",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/DialoguePlayback.cs',
     'if (step.Kind == DialogueStepKind.Speak)\n\t\t\t{\n\t\t\t\treturn true;\n\t\t\t}',
     'if (step.Kind == DialogueStepKind.Speak || step.Kind == DialogueStepKind.Choice)\n\t\t\t{\n\t\t\t\treturn true;\n\t\t\t}'),
    ("로그가 고른 답을 표시 안 함",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/DialogueTranscript.cs',
     'entries.Add(new Entry(null, label, true));',
     'entries.Add(new Entry(null, label, false));'),
    ("끝냈어도 시작 기록을 안 남김",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/DialogueHistory.cs',
     'public void MarkCompleted(int dialogueId)\n\t\t{\n\t\t\tstarted.Add(dialogueId);\n\t\t\tcompleted.Add(dialogueId);',
     'public void MarkCompleted(int dialogueId)\n\t\t{\n\t\t\tcompleted.Add(dialogueId);'),
    ("바쁠 때 줄을 안 세우고 버림",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/DialoguePlayCoordinator.cs',
     'if (IsBusy)\n\t\t\t{\n\t\t\t\treturn queue.Enqueue(request);\n\t\t\t}',
     'if (IsBusy)\n\t\t\t{\n\t\t\t\treturn false;\n\t\t\t}'),
    ("물건 조건이 늘 참",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/DialogueItemCriteria.cs',
     'public override bool Evaluate() => (GetCurValue() >= minimumCount) == expectedHave;',
     'public override bool Evaluate() => true;'),
    ("되돌려 쓰기가 조건 종류를 무시",
     'Assets/_WitchMendokusai/Domain/Narrative/Scripts/Script/DialogueScriptWriter.cs',
     'if (condition.Kind == DialogueScriptConditionKind.ItemCount)\n\t\t\t{',
     'if (false)\n\t\t\t{'),
]


def run_harness():
    result = subprocess.run(
        ['dotnet', 'run', '--project', '.'],
        cwd=ROOT + 'Tools/dialogue-harness',
        capture_output=True, text=True, encoding='utf-8', errors='replace')
    output = (result.stdout or '') + (result.stderr or '')
    for line in output.splitlines():
        if line.startswith('passed='):
            return line.strip()
    return 'BUILD-FAIL'


def main():
    missed = 0
    for name, relative_path, original, broken in MUTATIONS:
        path = ROOT + relative_path
        source = io.open(path, encoding='utf-8').read()
        if original not in source:
            print('SKIP(코드가 바뀌었다) ::', name)
            missed += 1
            continue

        io.open(path, 'w', encoding='utf-8').write(source.replace(original, broken, 1))
        try:
            result = run_harness()
        finally:
            io.open(path, 'w', encoding='utf-8').write(source)

        caught = result == 'BUILD-FAIL' or (result.startswith('passed=') and not result.endswith('failed=0'))
        if caught is False:
            missed += 1
        print(('CAUGHT ' if caught else 'MISSED '), result, '::', name)

    print()
    print('MISSED 가 하나라도 있으면 그 동작은 아무도 안 보고 있다. 시험을 하나 더 둬라.')
    return 1 if missed > 0 else 0


if __name__ == '__main__':
    raise SystemExit(main())
