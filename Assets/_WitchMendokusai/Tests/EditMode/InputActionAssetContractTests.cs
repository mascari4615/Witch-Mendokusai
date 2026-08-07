using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 입력 이벤트 목록(코드)과 입력 자산(1700줄짜리 JSON)이 **어긋나지 않는지** 잠근다.
	///
	/// ★ 왜 필요한가: 부팅 때 매니저가 이벤트마다 자산에서 같은 이름의 action 을 꺼내 쓴다.
	///   없으면 그 자리에서 **예외로 죽는다 — 게임이 아예 안 켜진다.** 그런데 그 검증은 지금
	///   *부팅 때만* 돈다 = 이미 늦다. 게다가 에디터를 못 여는 세션은 이 자산을 손으로 고쳐야 해서
	///   (실제로 그래서 한 슬롯이 작업을 보류했다) 커밋 전에 잡아 줄 자리가 필요하다.
	///
	/// ★ 왜 JSON 을 직접 읽나: 시험 어셈블리는 입력 시스템 패키지를 참조하지 않는다(실측 — 참조를
	///   추가하면 공유 설정을 건드리게 된다). 어차피 사고는 **손으로 고친 JSON** 에서 나므로
	///   그 JSON 을 그대로 읽는 편이 실패 모양에 더 가깝다.
	///
	/// ★ 키가 0개인 것도 잡는다: 죽지는 않지만 그 입력이 **영영 안 먹는다** — 조용해서 더 오래 안 들킨다.
	/// </summary>
	public class InputActionAssetContractTests
	{
		private static JObject LoadAssetJson()
		{
			string found = null;
			foreach (string guid in AssetDatabase.FindAssets("t:Object WMInput"))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith(".inputactions", StringComparison.OrdinalIgnoreCase))
					found = path;
			}

			Assert.IsNotNull(found, "입력 자산 파일을 못 찾았다 — 못 찾으면 아래 검사가 「빈손 통과」가 된다");
			return JObject.Parse(File.ReadAllText(found));
		}

		/// <summary> action 이름 → 걸린 키 개수. 자산 전체(모든 map)를 한 번에 훑는다. </summary>
		private static Dictionary<string, int> BindingCountByAction(JObject asset)
		{
			Dictionary<string, int> counts = new();

			foreach (JToken map in asset["maps"] ?? new JArray())
			{
				foreach (JToken action in map["actions"] ?? new JArray())
				{
					string actionName = (string)action["name"];
					if (string.IsNullOrEmpty(actionName) == false)
						counts[actionName] = 0;
				}

				foreach (JToken binding in map["bindings"] ?? new JArray())
				{
					string actionName = (string)binding["action"];
					if (string.IsNullOrEmpty(actionName) == false && counts.ContainsKey(actionName))
						counts[actionName]++;
				}
			}

			Assert.IsNotEmpty(counts, "입력 자산에서 action 을 하나도 못 읽었다 — 형식이 바뀌었거나 파일이 깨졌다");
			return counts;
		}

		[Test]
		public void 모든_입력_이벤트가_자산에_실재한다()
		{
			Dictionary<string, int> counts = BindingCountByAction(LoadAssetJson());

			foreach (InputEventType eventType in Enum.GetValues(typeof(InputEventType)))
			{
				Assert.IsTrue(counts.ContainsKey(eventType.ToString()),
					$"입력 이벤트 「{eventType}」 에 해당하는 action 이 입력 자산에 없다 — "
					+ "부팅 때 그 자리에서 예외로 죽는다(게임이 아예 안 켜진다). 자산에 같은 이름으로 넣어라");
			}
		}

		[Test]
		public void 모든_입력_액션이_적어도_한_개의_키를_갖는다()
		{
			Dictionary<string, int> counts = BindingCountByAction(LoadAssetJson());

			foreach (InputEventType eventType in Enum.GetValues(typeof(InputEventType)))
			{
				if (counts.TryGetValue(eventType.ToString(), out int bindingCount) == false)
					continue; // 위 시험이 잡는다 — 여기서 또 죽으면 원인이 흐려진다.

				Assert.Greater(bindingCount, 0,
					$"입력 이벤트 「{eventType}」 에 키가 하나도 안 걸려 있다 — "
					+ "죽지는 않지만 그 입력이 **영영 안 먹는다**(조용해서 더 오래 안 들킨다)");
			}
		}
	}
}
