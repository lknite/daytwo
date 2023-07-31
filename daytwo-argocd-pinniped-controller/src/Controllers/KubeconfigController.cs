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
        /// <param name="managementCluster"></param>
        /// <param name="workloadCluster"></param>
        /// <returns></returns>
        [HttpGet("{managementCluster}/{workloadCluster}")]
        public async Task<IActionResult> Get(string managementCluster, string workloadCluster)
        {
            Console.WriteLine($"GET {managementCluster}/{workloadCluster}");

            String tmp = string.Empty;
            try
            {
                tmp = System.IO.File.ReadAllText($"/opt/www/{managementCluster}/{workloadCluster}/kubeconfig");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(StatusCodes.Status404NotFound);
            }

            return Content(tmp);
        }
    }
}