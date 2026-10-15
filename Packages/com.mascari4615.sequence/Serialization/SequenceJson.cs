using System;
using System.Collections.Generic;
using Motion;
using Motion.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SceneContent.Serialization;

namespace SceneSequence.Serialization
{
    public static class SequenceJson
    {
        public const int MAX_JSON_CHARACTERS = 32000000;

        public static SequenceDocument Read(string json)
        {
            if (json == null || json.Length > MAX_JSON_CHARACTERS)
            {
                throw new ArgumentException("Invalid sequence JSON size");
            }
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            string[] fields = { "Format", "Version", "Id", "Scene", "Clips" };
            foreach (string field in fields)
            {
                if (root[field] == null)
                {
                    throw new ArgumentException("Missing sequence field: " + field);
                }
            }
            foreach (JProperty property in root.Properties())
            {
                if (Array.IndexOf(fields, property.Name) < 0)
                {
                    throw new ArgumentException("Unknown sequence field: " + property.Name);
                }
            }
            if (root["Format"].Type != JTokenType.String || root["Version"].Type != JTokenType.Integer ||
                root["Id"].Type != JTokenType.String || root["Clips"].Type != JTokenType.Array || ((JArray)root["Clips"]).Count > 256)
            {
                throw new ArgumentException("Invalid sequence field type or clip count");
            }
            List<PoseClip> clips = new List<PoseClip>();
            foreach (JToken token in (JArray)root["Clips"])
            {
                clips.Add(PoseClipJson.Read(token.ToString(Formatting.None)));
            }
            SequenceDocument document = new SequenceDocument
            {
                Format = (string)root["Format"], Version = (int)root["Version"], Id = (string)root["Id"],
                Scene = SceneJson.Read(root["Scene"].ToString(Formatting.None)), Clips = clips.ToArray()
            };
            SequenceValidation.Ensure(document);
            return document;
        }

        public static string Write(SequenceDocument document)
        {
            SequenceValidation.Ensure(document);
            JArray clips = new JArray();
            foreach (PoseClip clip in document.Clips)
            {
                clips.Add(JObject.Parse(PoseClipJson.Write(clip)));
            }
            JObject root = new JObject
            {
                ["Format"] = document.Format, ["Version"] = document.Version, ["Id"] = document.Id,
                ["Scene"] = JObject.Parse(SceneJson.Write(document.Scene)), ["Clips"] = clips
            };
            string json = root.ToString(Formatting.Indented);
            if (json.Length > MAX_JSON_CHARACTERS)
            {
                throw new ArgumentException("Sequence JSON exceeds size limit");
            }
            return json;
        }
    }
}
