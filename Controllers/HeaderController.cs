using Microsoft.AspNetCore.Mvc;

namespace ForwardHeaders.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HeaderController(ILogger<HeaderController> logger,
        IHttpContextAccessor httpContextAccessor) : ControllerBase
    {
        private readonly ILogger<HeaderController> _logger = logger;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        [HttpGet(Name = "GetIPAddress")]
        public ActionResult Get()
        {
            var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
            if (ipAddress == null) 
            {
                return Problem("Could not retrieve IP address");
            }
            return Ok(ipAddress.ToString());
        }
    }
}
