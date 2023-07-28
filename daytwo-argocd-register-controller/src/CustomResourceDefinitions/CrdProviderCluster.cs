using k8s.Models;
using System.Text.Json.Serialization;

namespace daytwo.crd.tanzukubernetescluster
{
    public class CrdProviderCluster : CustomResourceDefinitions.CustomResource<CrdProviderSpec, CrdProviderStatus>
    {
        public override string ToString()
        {
            /*
            var labels = "{";
            foreach (var kvp in Metadata.Labels)
            {
                labels += kvp.Key + " : " + kvp.Value + ", ";
            }
            labels = labels.TrimEnd(',', ' ') + "}";

            return $"{Metadata.Name} (Labels: {labels}), Spec.Enabled: {Spec.Enabled}, Spec.Short: {Spec.Short}, Spec.Long: {Spec.Long}, Spec.Path: {Spec.Path}";
            */

            //return $"{Metadata.Name}, Spec.Enabled: {Spec.Enabled}, Spec.Short: {Spec.Short}, Spec.Long: {Spec.Long}, Spec.Path: {Spec.Path}";
            return "?";
        }
    }

    /*
    public class Token
    {
        [JsonPropertyName("email")]
        public string email { get; set; }
        [JsonPropertyName("claims")]
        public string claims { get; set; }
        [JsonPropertyName("api_key")]
        public string api_key { get; set; }
    }
    */
    public class CrdProviderSpec
    {
        [JsonPropertyName("asdf")]
        public string asdf { get; set; }
    }

    public class CrdProviderStatus : V1Status
    {
        [JsonPropertyName("state")]
        public string state { get; set; }
    }
}
