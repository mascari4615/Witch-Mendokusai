using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Appearance.Serialization
{
    public static class AppearanceJson
    {
        private static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 16
        };

        public static AppearanceDocument Read(string json)
        {
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            foreach (string field in new[] { "Format", "Version", "Id", "Colors" })
            {
                if (root[field] == null)
                {
                    throw new ArgumentException("Missing required field: " + field, nameof(json));
                }
            }
            AppearanceDocument document = JsonConvert.DeserializeObject<AppearanceDocument>(json, SETTINGS);
            IReadOnlyList<string> errors = AppearanceApplication.Validate(document);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("\n", errors), nameof(json));
            }
            return document;
        }

        public static string Write(AppearanceDocument document)
        {
            IReadOnlyList<string> errors = AppearanceApplication.Validate(document);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("\n", errors), nameof(document));
            }
            return JsonConvert.SerializeObject(document, Formatting.Indented, SETTINGS);
        }
    }
}
