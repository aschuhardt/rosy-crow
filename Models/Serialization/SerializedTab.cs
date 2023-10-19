using Newtonsoft.Json;

namespace RosyCrow.Models.Serialization;

internal class SerializedTab
{
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("icon")]
    public string Icon { get; set; }
}