using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using k8s;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics; using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;

namespace daytwo.K8sHelpers
{
    /*
     * apiVersion: run.tanzu.vmware.com/v1alpha2
     * kind: TanzuKubernetesCluster
     */

    public class zK8sTanzuKubernetesCluster
    {
        /*
        static String api = "TanzuKubernetesCluster";
        static String group = "run.tanzu.vmware.com";
        static String version = "v1alpha2";
        static String plural = api + "s";

        public static GenericClient generic = new GenericClient(Globals.service.kubeclient, group, version, plural);
        */

        public static async Task<string> Post(
                bool isIndex = false
            )
        {
            /*
            // calculate timestamp
            string when = Timestamp.getUtcTimestampInMilliseconds().ToString();
            // default name is timestamp
            string name = block.daytwo +"-"+ when;
            // default state is ""
            string state = "";

            // an index block has the same name as the daytwo name
            if (isIndex)
            {
                name = block.daytwo;
                state = "index";
            }

            // new block instance
            var b = new daytwo.crd.block.CrdBlock()
            {
                Kind = "Block",
                ApiVersion = group + "/" + version,
                Metadata = new V1ObjectMeta
                {
                    Name = name,
                    NamespaceProperty = Globals.service.kubeconfig.Namespace,
                },
                // spec
                Spec = new daytwo.crd.block.CrdBlockSpec
                {
                    block = new daytwo.crd.block.Block()
                    {
                        daytwo = block.daytwo,
                        block = name,
                        owner = block.owner,
                        index = block.index,
                        size = block.size,
                        state = state,
                        when = when
                    }
                }
            };
            // debug
            Console.WriteLine(JsonSerializer.Serialize(b, new JsonSerializerOptions { WriteIndented = true }));

            try
            {
                Console.WriteLine("creating CR {0}", b.Metadata.Name);
                var response = await Globals.service.kubeclient.CustomObjects.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                    b,
                    group, version,
                    Globals.service.kubeconfig.Namespace,
                    plural).ConfigureAwait(false);
            }
            catch (HttpOperationException httpOperationException) when (httpOperationException.Message.Contains("422"))
            {
                var phase = httpOperationException.Response.ReasonPhrase;
                var content = httpOperationException.Response.Content;
                Console.WriteLine("response content: {0}", content);
                Console.WriteLine("response phase: {0}", phase);
            }
            catch (HttpOperationException ex)
            {
                Console.WriteLine("Exception: " + ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex);
            }


            return b.Metadata.Name.ToString();
            */
            return "";
        }
    }
}
