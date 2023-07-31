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
        /// asdf
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        [HttpGet("{managementCluster}/{workloadCluster}")]
        public async Task<IActionResult> Get(string managementCluster, string workloadCluster)
        {
            Console.WriteLine($"GET {managementCluster}/{workloadCluster}");

            String tmp = System.IO.File.ReadAllText($"/var/www/{managementCluster}/{workloadCluster}/kubeconfig");
            return Content(tmp);
        }
    }
}