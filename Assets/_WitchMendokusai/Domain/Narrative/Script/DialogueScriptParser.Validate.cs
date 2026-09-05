using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 대본 읽기의 검사 부분. 같은 클래스의 partial 조각이다.
	public static partial class DialogueScriptParser
	{
		/// <summary>
		/// 같은 이름의 장면이 둘 이상인가.
		///
		/// ★ 왜 오류인가: 이름으로 찾는 쪽(<see cref="ParsedDialogueScript.FindSection"/>)은 **첫 번째**를 집고,
		///   그래프를 세우는 쪽은 나중 것으로 덮어써 **마지막**으로 간다. 즉 검사는 A 를 보고 실제 재생은 B 로 간다 —
		///   눈으로는 「분명 저 장면을 가리켰는데 다른 대사가 나온다」로만 보이는 종류다.
		///   원고에서 「### 만남」 같은 흔한 제목을 두 번 쓰기 쉬우므로 반드시 잡아야 한다.
		/// </summary>
		private static void ValidateSectionNames(ParsedDialogueScript parsed)
		{
			HashSet<string> seen = new(StringComparer.Ordinal);
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				DialogueScriptSection section = parsed.Sections[i];
				if (seen.Add(section.Name))
				{
					continue;
				}
				parsed.Issues.Add(new DialogueScriptIssue(section.LineNumber,
					$"장면 이름이 겹친다: \"{section.Name}\" — 가리키는 쪽과 실제로 가는 곳이 달라진다"));
			}
		}

		/// <summary>
		/// 한 묶음 안에 **같은 라벨의 선택지**가 둘 이상인가.
		/// 플레이어 눈엔 똑같은 두 칸이 뜬다 — 무엇이 다른지 알 길이 없고, 고르고 나서야 다른 데로 간다.
		/// 대개 복사해 붙이고 라벨 고치는 걸 잊은 것이다.
		/// </summary>
		private static void ValidateChoiceLabels(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					if (entries[e].Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}

					HashSet<string> labels = new(StringComparer.Ordinal);
					IReadOnlyList<DialogueScriptChoice> choices = entries[e].Choices;
					for (int c = 0; c < choices.Count; c++)
					{
						if (labels.Add(choices[c].Label))
						{
							continue;
						}
						parsed.Issues.Add(new DialogueScriptIssue(entries[e].LineNumber,
							$"선택지 라벨이 겹친다: \"{choices[c].Label}\" — 플레이어 눈엔 같은 칸이 둘이다"));
					}
				}
			}
		}

		/// <summary>
		/// 화자 이름 오타로 **보이는** 것을 짚는다 — 「욘」을 「온」이라 쓴 경우.
		///
		/// ★ 왜 필요한가: 이름이 틀려도 **아무 일도 안 일어난다.** 그냥 다른 사람이 말한 게 되고,
		///   그 이름으로 등록된 캐릭터가 없으니 말풍선이 엉뚱한 자리(카메라 앞)에 뜬다.
		///   원고에도 게임에도 오류가 안 남아서 눈으로만 잡아야 하는 종류다.
		///
		/// 판정은 **보수적으로**: 그 원고에서 **딱 한 번** 나온 이름이면서, 여러 번 나온 이름과
		/// **글자 하나 차이**일 때만 묻는다. 새 등장인물을 오타라고 우기면 그 검사는 곧 무시당한다.
		///
		/// ★ 실측(2026-08-08)으로 배운 것: **두 글자 미만 이름은 아예 안 본다.**
		///   「욘」과 「링」은 서로 글자 하나 차이라, 짧은 이름끼리는 이 규칙이 **아무 뜻이 없다**
		///   (실제로 멀쩡한 원고 둘을 오타라고 잡았다). 그래서 「온」 같은 진짜 오타도 놓치지만,
		///   정상을 잡는 검사보다 낫다.
		/// </summary>
		private static void ValidateSpeakerNames(ParsedDialogueScript parsed)
		{
			Dictionary<string, int> counts = new(StringComparer.Ordinal);
			Dictionary<string, int> firstLine = new(StringComparer.Ordinal);

			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind != DialogueScriptEntryKind.Speak || string.IsNullOrEmpty(entry.Speaker))
					{
						continue;
					}
					counts.TryGetValue(entry.Speaker, out int count);
					counts[entry.Speaker] = count + 1;
					if (firstLine.ContainsKey(entry.Speaker) == false)
					{
						firstLine[entry.Speaker] = entry.LineNumber;
					}
				}
			}

			foreach (KeyValuePair<string, int> rare in counts)
			{
				if (rare.Value != 1 || rare.Key.Length < 2)
				{
					continue;
				}
				foreach (KeyValuePair<string, int> common in counts)
				{
					if (common.Value < 2 || common.Key.Length < 2 || IsOneEditApart(rare.Key, common.Key) == false)
					{
						continue;
					}
					parsed.Issues.Add(new DialogueScriptIssue(firstLine[rare.Key],
						$"\"{rare.Key}\" 는 이 원고에 한 번뿐인데 \"{common.Key}\" 와 글자 하나 차이다 — 오타인가?"));
					break;
				}
			}
		}

		/// <summary>글자 하나 차이인가(바꿈·넣음·뺌 한 번). 같은 글자면 false — 오타 후보가 아니다.</summary>
		private static bool IsOneEditApart(string left, string right)
		{
			if (left == right)
			{
				return false;
			}
			if (Math.Abs(left.Length - right.Length) > 1)
			{
				return false;
			}

			string shorter = left.Length <= right.Length ? left : right;
			string longer = left.Length <= right.Length ? right : left;

			int shortIndex = 0;
			int longIndex = 0;
			bool usedEdit = false;
			while (shortIndex < shorter.Length && longIndex < longer.Length)
			{
				if (shorter[shortIndex] == longer[longIndex])
				{
					shortIndex++;
					longIndex++;
					continue;
				}
				if (usedEdit)
				{
					return false;
				}
				usedEdit = true;
				if (shorter.Length == longer.Length)
				{
					shortIndex++;
				}
				longIndex++;
			}
			return true;
		}

		/// <summary>
		/// 한 장면 안에서 **이미 다른 데로 보낸 뒤에 더 쓴 줄**을 짚는다 — 그 줄부터는 절대 안 나온다.
		///
		/// ★ 왜 생기나: 갈 곳(<c>-&gt; 어디</c>)이나 선택지는 그 자리에서 흐름을 **가로챈다.**
		///   그 아래 대사는 다음 장면 것이 아니라 **아무 데도 아닌 글**이 된다.
		///   대개 선택지를 위로 옮기거나, 장면을 나눠야 할 걸 안 나눈 경우다.
		///
		/// ★ 왜 아무도 못 잡나: 도달 검사(<see cref="ValidateReachableSections"/>)는 **장면 단위**라
		///   여길 못 본다 — 장면 자체는 멀쩡히 도달하고, 그 안의 뒷줄만 굶는다.
		///   화면엔 아무 흔적이 없다. 「분명 썼는데 안 나온다」로만 보인다.
		///
		/// 조건부 갈 곳은 세지 않는다 — 조건이 거짓이면 그대로 아래로 읽어 내려가니까.
		/// </summary>
		private static void ValidateNoDeadEntries(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count - 1; e++)
				{
					DialogueScriptEntryKind kind = entries[e].Kind;
					if (kind != DialogueScriptEntryKind.Goto && kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}

					string reason = kind == DialogueScriptEntryKind.Goto ? "갈 곳" : "선택지";
					parsed.Issues.Add(new DialogueScriptIssue(entries[e + 1].LineNumber,
						$"이 줄부터는 절대 안 나온다 — 위 {reason}에서 이미 다른 데로 보냈다. 장면을 나눠라"));
					break;
				}
			}
		}

		/// <summary>
		/// **전부 조건부인 선택지 묶음**을 짚는다 — 조건이 다 거짓이면 아무 칸도 안 뜬다.
		///
		/// ★ 그때 무슨 일이 일어나나: 재생 쪽은 **대화를 그냥 끝낸다.** 그게 옳은 처리다 —
		///   고를 게 없는 화면에서 플레이어를 붙잡아 두는 것보다 낫다. 하지만 원고를 쓴 사람은
		///   그 규칙을 모른다. 눈에는 「말하다 말고 대화가 툭 끊긴다」로만 보이고,
		///   **재현도 안 된다**(조건이 하나라도 참인 저장에서는 멀쩡히 돌아가니까).
		///
		/// 그래서 쓰는 자리에서 알린다. 고치는 법은 하나 — **조건 없는 칸을 하나 두는 것**
		/// (「그냥 간다」 같은 퇴로). 잡아 두는 게 아니라 나갈 문을 만들어 두라는 뜻이다.
		/// </summary>
		private static void ValidateChoicesHaveAWayOut(ParsedDialogueScript parsed)
		{
			for (int s = 0; s < parsed.Sections.Count; s++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[s].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind != DialogueScriptEntryKind.Choice || entry.Choices.Count == 0)
					{
						continue;
					}

					bool hasUnconditionalChoice = false;
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						if (entry.Choices[c].Condition.HasCondition)
						{
							continue;
						}
						hasUnconditionalChoice = true;
						break;
					}

					if (hasUnconditionalChoice)
					{
						continue;
					}

					parsed.Issues.Add(new DialogueScriptIssue(entry.LineNumber,
						"선택지가 전부 조건부다 — 조건이 다 거짓이면 아무 칸도 안 뜨고 대화가 그냥 끝난다. 조건 없는 칸을 하나 둬라"));
				}
			}
		}

		/// <summary>
		/// **아무도 안 부르는 장면**을 짚는다 — 써 두었지만 게임에서 절대 안 나오는 글.
		///
		/// ★ 왜 필요한가: 이건 **아무 증상이 없다.** 원고도 멀쩡하고, 그래프도 멀쩡하고, 게임도 안 터진다.
		///   그냥 그 장면이 조용히 빠질 뿐이다. 대개 「-> 재회」 라고 쓸 걸 「-> 재회2」 로 쓰거나
		///   가지를 옮기다 보내는 쪽을 지운 경우다 — 쓴 사람은 다 이어 놨다고 믿는다.
		///   갈 곳 오타(<see cref="ValidateTargets"/>)는 **보내는 쪽**을 보고, 이건 **받는 쪽**을 본다.
		///
		/// 길은 둘이다: ① 누군가 이름으로 가리킨다(갈 곳·조건부 갈 곳·선택지)
		/// ② **앞 장면에서 그냥 흘러 들어온다** — 종이에 쓴 순서대로 읽히니까.
		/// 흘러가지 *못하는* 경우는 그 장면 안에 조건 없는 「갈 곳」이나 선택지가 있을 때다(거기서 새 버린다).
		/// 조건부 갈 곳은 안 세는데, 조건이 거짓이면 그대로 흘러가기 때문이다.
		/// </summary>
		private static void ValidateReachableSections(ParsedDialogueScript parsed)
		{
			if (parsed.Sections.Count == 0)
			{
				return;
			}

			HashSet<string> reachable = new(StringComparer.Ordinal);
			Queue<int> pending = new();
			// 첫 장면은 대화가 시작되는 자리다 — 아무도 안 가리켜도 도달한 것으로 친다.
			MarkReachable(parsed, 0, reachable, pending);

			while (pending.Count > 0)
			{
				int index = pending.Dequeue();
				DialogueScriptSection section = parsed.Sections[index];
				bool flowsToNext = true;

				for (int e = 0; e < section.Entries.Count; e++)
				{
					DialogueScriptEntry entry = section.Entries[e];
					if (entry.Kind == DialogueScriptEntryKind.Goto || entry.Kind == DialogueScriptEntryKind.ConditionalGoto)
					{
						MarkReachable(parsed, IndexOfSection(parsed, entry.TargetSection), reachable, pending);
						if (entry.Kind == DialogueScriptEntryKind.Goto)
						{
							// 조건 없는 갈 곳에서 흐름이 샌다 — 이 뒤로는 다음 장면까지 못 간다.
							flowsToNext = false;
							break;
						}
						continue;
					}
					if (entry.Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						MarkReachable(parsed, IndexOfSection(parsed, entry.Choices[c].TargetSection), reachable, pending);
					}
					// 선택지에서도 샌다 — 고르면 그리로 가고, 고를 게 없으면 대화가 끝난다.
					flowsToNext = false;
					break;
				}

				if (flowsToNext)
				{
					MarkReachable(parsed, index + 1, reachable, pending);
				}
			}

			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				DialogueScriptSection section = parsed.Sections[i];
				if (reachable.Contains(section.Name))
				{
					continue;
				}
				parsed.Issues.Add(new DialogueScriptIssue(section.LineNumber,
					$"\"{section.Name}\" 장면으로 가는 길이 없다 — 써 두었지만 게임에선 한 번도 안 나온다"));
			}
		}

		private static void MarkReachable(ParsedDialogueScript parsed, int index, HashSet<string> reachable, Queue<int> pending)
		{
			if (index < 0 || index >= parsed.Sections.Count)
			{
				return;
			}
			if (reachable.Add(parsed.Sections[index].Name) == false)
			{
				return;
			}
			pending.Enqueue(index);
		}

		/// <summary>이름이 겹치면 앞의 것을 집는다 — 찾는 쪽(FindSection)과 같은 판단이라야 한다.</summary>
		private static int IndexOfSection(ParsedDialogueScript parsed, string name)
		{
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				if (string.Equals(parsed.Sections[i].Name, name, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>갈 곳 이름이 실제 장면인지 — 오타는 여기서 잡아야 한다(런타임엔 그냥 대화가 끝나 버린다).</summary>
		private static void ValidateTargets(ParsedDialogueScript parsed)
		{
			for (int i = 0; i < parsed.Sections.Count; i++)
			{
				List<DialogueScriptEntry> entries = parsed.Sections[i].Entries;
				for (int e = 0; e < entries.Count; e++)
				{
					DialogueScriptEntry entry = entries[e];
					if (entry.Kind == DialogueScriptEntryKind.Goto || entry.Kind == DialogueScriptEntryKind.ConditionalGoto)
					{
						RequireSection(parsed, entry.TargetSection, entry.LineNumber);
						continue;
					}
					if (entry.Kind != DialogueScriptEntryKind.Choice)
					{
						continue;
					}
					for (int c = 0; c < entry.Choices.Count; c++)
					{
						RequireSection(parsed, entry.Choices[c].TargetSection, entry.LineNumber);
					}
				}
			}
		}

		private static void RequireSection(ParsedDialogueScript parsed, string name, int lineNumber)
		{
			DialogueScriptSection target = parsed.FindSection(name);
			if (target == null)
			{
				parsed.Issues.Add(new DialogueScriptIssue(lineNumber, $"그런 장면이 없다: \"{name}\""));
				return;
			}

			// 빈 장면 자체는 흠이 아니다(원고엔 산문만 있는 장면이 흔하다). 하지만 **거기로 보내면** 다르다 —
			// 아무 말도 없이 다음 장면으로 흘러가서, 쓴 사람은 「저기로 갔다」고 믿는데 화면은 딴 데를 보여준다.
			if (target.Entries.Count == 0)
			{
				parsed.Issues.Add(new DialogueScriptIssue(lineNumber,
					$"\"{name}\" 장면엔 대사가 없다 — 거기로 가면 그냥 다음 장면으로 흘러간다"));
			}
		}
	}
}
