using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 적중 효과 하나. 상태이상 이름은 UnitStatusType 이름만 (숫자 금지)
	internal sealed class SkillMaterialEffect
	{
		public UnitStatusType Status;
		public float Seconds;
		public float SlowFactor;
	}

	// 레시피 하나를 공통 기본값, 모양 기본값과 합친 결과. 수치는 이름으로 꺼내고 없으면 바로 실패
	internal sealed class SkillMaterialSpec
	{
		public string Id;
		public string Form;
		public string Element;
		public Color Color;
		public float BodyWhiten;
		public List<SkillMaterialEffect> Effects = new();
		public JObject Values;

		public float Float(string key) => Value(key).Value<float>();
		public int Int(string key) => Value(key).Value<int>();
		public string Text(string key) => Value(key).Value<string>();

		private JToken Value(string key) => Values.TryGetValue(key, out JToken token) ? token : throw new InvalidOperationException($"{Id}: 값 없음 {key}");
	}

	// 재료 목록 읽기와 검증. 정본 skill-materials.json (이 폴더)
	// - 덮어쓰는 순서: Defaults -> Forms[모양] -> 레시피. 레시피는 위 둘에 있는 키만 쓸 수 있음 (오타는 바로 실패)
	// - 전부 검증한 뒤에야 굽기 시작 (반쯤 구운 상태로 멈추지 않게)
	internal static class SkillMaterialCatalog
	{
		private const string DATA_PATH = "Assets/_WitchMendokusai/Editor/SkillMaterials/skill-materials.json";
		private static readonly Regex ID_PATTERN = new("^[A-Za-z][A-Za-z0-9_]*$");
		private static readonly HashSet<string> RECIPE_ONLY_KEYS = new() { "Id", "Form", "Element", "Effects" };
		private static readonly HashSet<string> EFFECT_KEYS = new() { "Status", "Seconds", "SlowFactor" };

		public static List<SkillMaterialSpec> Load() => Parse(LoadText());

		public static string LoadText()
		{
			TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(DATA_PATH);
			if (text == null)
				throw new InvalidOperationException($"재료 목록 없음: {DATA_PATH}");
			return text.text;
		}

		public static List<SkillMaterialSpec> Parse(string json)
		{
			JObject root = JObject.Parse(json);
			JObject defaults = Required<JObject>(root, "Defaults", "파일");
			JObject forms = Required<JObject>(root, "Forms", "파일");
			JObject elements = Required<JObject>(root, "Elements", "파일");
			float bodyWhiten = Required<JObject>(root, "Look", "파일").Value<float>("BodyWhiten");

			List<SkillMaterialSpec> specs = new();
			HashSet<string> ids = new();
			foreach (JObject recipe in Required<JArray>(root, "Recipes", "파일").Cast<JObject>())
			{
				string id = recipe.Value<string>("Id");
				if (id == null || ID_PATTERN.IsMatch(id) == false)
					throw new InvalidOperationException($"재료 이름이 비었거나 파일 이름에 못 씀: '{id}'");
				if (ids.Add(id) == false)
					throw new InvalidOperationException($"재료 이름 중복: {id}");

				string form = Required<JValue>(recipe, "Form", id).Value<string>();
				if (SkillMaterialForms.Has(form) == false)
					throw new InvalidOperationException($"{id}: 굽는 법 없는 모양 {form}");
				JObject formDefaults = Required<JObject>(forms, form, $"Forms ({id})");

				JObject values = (JObject)defaults.DeepClone();
				values.Merge(formDefaults);
				foreach (JProperty property in recipe.Properties())
				{
					if (RECIPE_ONLY_KEYS.Contains(property.Name))
						continue;
					if (values.ContainsKey(property.Name) == false)
						throw new InvalidOperationException($"{id}: 모르는 키 {property.Name} (Defaults 나 Forms.{form} 에 없음)");
					values[property.Name] = property.Value;
				}

				string element = Required<JValue>(recipe, "Element", id).Value<string>();
				JObject color = Required<JObject>(elements, element, $"Elements ({id})");
				specs.Add(new SkillMaterialSpec
				{
					Id = id,
					Form = form,
					Element = element,
					Color = new Color(color.Value<float>("r"), color.Value<float>("g"), color.Value<float>("b"), 1f),
					BodyWhiten = bodyWhiten,
					Effects = ParseEffects(recipe, id),
					Values = values,
				});
			}
			return specs;
		}

		private static List<SkillMaterialEffect> ParseEffects(JObject recipe, string id)
		{
			List<SkillMaterialEffect> effects = new();
			if (recipe.TryGetValue("Effects", out JToken token) == false)
				return effects;
			foreach (JObject effect in token.Cast<JObject>())
			{
				foreach (JProperty property in effect.Properties())
				{
					if (EFFECT_KEYS.Contains(property.Name) == false)
						throw new InvalidOperationException($"{id}: 효과의 모르는 키 {property.Name}");
				}
				string name = Required<JValue>(effect, "Status", id).Value<string>();
				if (int.TryParse(name, out _) || Enum.IsDefined(typeof(UnitStatusType), name) == false)
					throw new InvalidOperationException($"{id}: 없는 상태이상 {name}");
				UnitStatusType status = (UnitStatusType)Enum.Parse(typeof(UnitStatusType), name);
				effects.Add(new SkillMaterialEffect
				{
					Status = status,
					Seconds = Required<JValue>(effect, "Seconds", id).Value<float>(),
					SlowFactor = status == UnitStatusType.Slow ? Required<JValue>(effect, "SlowFactor", id).Value<float>() : 0f,
				});
			}
			return effects;
		}

		private static T Required<T>(JObject parent, string key, string where) where T : JToken
		{
			if (parent.TryGetValue(key, out JToken token) && token is T typed)
				return typed;
			throw new InvalidOperationException($"{where}: {key} 없음 또는 형식 다름");
		}
	}
}
