using k8s.Models;
using System.Text.Json.Serialization;

namespace daytwo.crd.provider
{
    public class CrdProviderCluster : CustomResourceDefinitions.CustomResource<CrdProviderSpec, CrdProviderStatus>
    {
        public override string ToString()
        {
            return "?";
        }
    }

    public class Version
    {
        [JsonPropertyName("name")]
        public string name { get; set; }
    }
    public class Names
    {
        [JsonPropertyName("kind")]
        public string kind { get; set; }
        [JsonPropertyName("plural")]
        public string plural { get; set; }
        [JsonPropertyName("singular")]
        public string singular { get; set; }
    }
    public class CrdProviderSpec
    {
        [JsonPropertyName("group")]
        public string group { get; set; }
        [JsonPropertyName("names")]
        public Names names { get; set; }
        [JsonPropertyName("versions")]
        public List<Version> versions { get; set; }
    }

    public class CrdProviderStatus : V1Status
    {
        /*
        [JsonPropertyName("state")]
        public string state { get; set; }
        */
    }
}
