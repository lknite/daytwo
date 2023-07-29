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

    public class CrdProviderSpec
    {
        /*
        [JsonPropertyName("asdf")]
        public string asdf { get; set; }
        */
    }

    public class CrdProviderStatus : V1Status
    {
        /*
        [JsonPropertyName("state")]
        public string state { get; set; }
        */
    }
}
