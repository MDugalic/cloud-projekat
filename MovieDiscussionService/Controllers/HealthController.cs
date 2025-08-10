using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class HealthController : Controller
    {
     
            [HttpGet]
            [Route("health-monitoring")]
            public ActionResult Ping() => Content("OK");
    }
}