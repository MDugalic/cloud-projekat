using Common;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class DiscussionController : Controller
    {
        // GET: Discussion
        private CloudTable Discussions => Storage.GetTable("Discussions");

        // GET: /Discussion
        public ActionResult Index()
        {
            // Za početak samo listamo sve (kasnije dodamo pretragu/sort/paginaciju)
            var list = Discussions.CreateQuery<DiscussionEntity>()
                                 .Where(d => d.PartitionKey == "Disc")
                                 .ToList();
            return View(list);
        }

        // GET: /Discussion/Create (placeholder – dodajemo kasnije)
        [HttpGet]
        public ActionResult Create() => View();
    }
}