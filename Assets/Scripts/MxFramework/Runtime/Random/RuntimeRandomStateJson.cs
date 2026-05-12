using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Runtime
{
    public static class RuntimeRandomStateJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        public static string SaveToJson(RuntimeRandomState state)
        {
            return JsonConvert.SerializeObject(new SerializableState(state), Settings);
        }

        public static RuntimeRandomState LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Random state json cannot be null or empty.", nameof(json));
            }

            SerializableState state = JsonConvert.DeserializeObject<SerializableState>(json, Settings);
            if (state == null)
            {
                throw new ArgumentException("Random state json parsed to null.", nameof(json));
            }

            return new RuntimeRandomState(state.AlgorithmId, state.Seed, state.State, state.DrawCount);
        }

        private sealed class SerializableState
        {
            public SerializableState()
            {
            }

            public SerializableState(RuntimeRandomState state)
            {
                AlgorithmId = state.AlgorithmId;
                Seed = state.Seed;
                State = state.State;
                DrawCount = state.DrawCount;
            }

            public string AlgorithmId { get; set; }
            public uint Seed { get; set; }
            public uint State { get; set; }
            public long DrawCount { get; set; }
        }
    }
}
