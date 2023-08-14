using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace daytwo.Controllers
{
    [ApiController]
    [Route("")]
    //[Produces("application/json")]
    public class KubeconfigController : ControllerBase
    {
        /// <summary>
        /// Get specified kubeconfig file
        /// </summary>
        /// <param name="managementCluster"></param>
        /// <param name="workloadCluster"></param>
        /// <returns></returns>
        [HttpGet("{managementCluster}/{workloadCluster}/kubeconfig")]
        public async Task<IActionResult> Get(string managementCluster, string workloadCluster)
        {
            Globals.log.LogInformation($"GET {managementCluster}/{workloadCluster}");

            String tmp = string.Empty;
            try
            {
                tmp = System.IO.File.ReadAllText($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig");
            }
            catch (Exception ex)
            {
                Globals.log.LogInformation(ex.Message);
                return StatusCode(StatusCodes.Status404NotFound);
            }

            return Content(tmp);
        }

        /// <summary>
        /// Get listing of available kubeconfig files
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        [HttpGet("/index.html")]
        [Produces("application/json")]
        public async Task<IActionResult> GetIndex()
        {
            Globals.log.LogInformation($"GET /");

            // if option is set to disable index, then return immediately
            if ((Environment.GetEnvironmentVariable("ENABLE_INDEX") != null)
                && (Environment.GetEnvironmentVariable("ENABLE_INDEX") == "false"))
            {
                Globals.log.LogInformation($"- index has been disabled via env var ENABLE_INDEX");
                return Ok();
            }

            // check if existing kasten secrets have a matching secret
            List<string> index = new List<string>();
            if (Directory.Exists("/opt/www"))
            {
                var files = from file in Directory.EnumerateFiles("/opt/www", "*", SearchOption.AllDirectories) select file;
                foreach (var file in files)
                {
                    if (file.IndexOf("kubeconfig") != -1)
                    {
                        index.Add(file.Substring(8));
                    }
                }
            }

            return Ok(index);
        }
    }
}