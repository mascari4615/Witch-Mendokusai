using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	// 트리 파일 하나의 텍스트 형식
	public class SkillTreeFile
	{
		[JsonProperty("equipment")] public int? Equipment;
		[JsonProperty("nodes")] public List<SkillTreeNodeFile> Nodes = new();
	}

	// card 는 기존 카드 참조, cardId 는 파일 안에서 정의하는 새 카드
	public class SkillTreeNodeFile
	{
		[JsonProperty("id")] public string Id;
		[JsonProperty("card")] public int? Card;
		[JsonProperty("cardId")] public int? CardId;
		[JsonProperty("name")] public string Name;
		[JsonProperty("description")] public string Description;
		[JsonProperty("iconCard")] public int? IconCard;
		[JsonProperty("kind")] public string Kind;
		[JsonProperty("rarity")] public string Rarity;
		[JsonProperty("maxStack")] public int? MaxStack;
		[JsonProperty("effects")] public List<SkillTreeEffectFile> Effects;
		[JsonProperty("requires")] public List<SkillTreeRequireFile> Requires = new();
	}

	public class SkillTreeEffectFile
	{
		[JsonProperty("type")] public string Type;
		[JsonProperty("target")] public string Target;
		[JsonProperty("op")] public string Operator;
		[JsonProperty("value")] public int? Value;
	}

	// node 는 같은 트리의 노드, card 는 다른 장비의 카드
	public class SkillTreeRequireFile
	{
		[JsonProperty("node")] public string Node;
		[JsonProperty("card")] public int? Card;
		[JsonProperty("stack")] public int Stack = 1;
	}

	public readonly struct SkillTreeSource
	{
		public readonly string Name;
		public readonly string Text;

		public SkillTreeSource(string name, string text)
		{
			Name = name;
			Text = text;
		}
	}

	public static class SkillTreeParser
	{
		private static readonly JsonSerializerSettings SETTINGS = new() { MissingMemberHandling = MissingMemberHandling.Error };

		public static SkillTreeFile Parse(SkillTreeSource source, List<string> errors)
		{
			try
			{
				SkillTreeFile file = JsonConvert.DeserializeObject<SkillTreeFile>(source.Text, SETTINGS);
				if (file == null)
					errors.Add($"[{source.Name}] 빈 파일");
				return file;
			}
			catch (JsonException exception)
			{
				errors.Add($"[{source.Name}] JSON 오류: {exception.Message}");
				return null;
			}
		}
	}
}
