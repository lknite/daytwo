using k8s.Models;
using System.Text.Json.Serialization;

namespace daytwo.crd.K10Cluster
{
    public class CrdK10Cluster : CustomResourceDefinitions.CustomResource<CrdK10ClusterSpec, CrdK10ClusterStatus>
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
    public class ControlPlaneRef
    {
        [JsonPropertyName("apiVersion")]
        public string apiVersion { get; set; }
        [JsonPropertyName("kind")]
        public string kind { get; set; }
        [JsonPropertyName("name")]
        public string name { get; set; }
    }
    public class InfrastructureRef
    {
        [JsonPropertyName("apiVersion")]
        public string apiVersion { get; set; }
        [JsonPropertyName("kind")]
        public string kind { get; set; }
        [JsonPropertyName("name")]
        public string name { get; set; }
    }
    */
    public class CrdK10ClusterSpec
    {
        /*
        [JsonPropertyName("controlPlaneRef")]
        public ControlPlaneRef controlPlaneRef { get; set; }
        [JsonPropertyName("infrastructureRef")]
        public InfrastructureRef infrastructureRef { get; set; }
        */
    }

    public class CrdK10ClusterStatus : V1Status
    {
        /*
        [JsonPropertyName("infrastructureReady")]
        public bool infrastructureReady { get; set; }
        [JsonPropertyName("controlPlaneReady")]
        public bool controlPlaneReady { get; set; }
        [JsonPropertyName("phase")]
        public string phase { get; set; }
        */
    }
}
