using Azure.Storage.Blobs;
using Common;
using Common.DTOs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;

namespace MovieDiscussionService.Controllers
{
    public class DiscussionController : Controller
    {
        // Definišite tabelu sa diskusijama
        private CloudTable Discussions => Storage.GetTable("Discussions");
        private CloudTable Comments => Storage.GetTable("Comments");

        private CloudTable Follows => Storage.GetTable("FollowTable");
        private string CurrentUserEmail => Session["email"]?.ToString()?.ToLowerInvariant();

        private CloudTable Votes => Storage.GetTable("Votes");

        // GET: /Discussion
        public ActionResult Index()
        {
            var discussions = Discussions.CreateQuery<DiscussionEntity>()
                                 .Where(d => d.PartitionKey == "Disc")
                                 .ToList();

            // Kreiramo mapu za broj komentara po diskusiji
            var commentCount = new Dictionary<string, int>();

            // Kreiramo mapu za pratioce i status praćenja
            var followerCountMap = new Dictionary<string, int>();
            var isFollowingMap = new Dictionary<string, bool>();
            var isCreatorMap = new Dictionary<string, bool>();


            foreach (var d in discussions)
            {
                var comments = Comments.CreateQuery<CommentEntity>()
                                       .Where(c => c.PartitionKey == d.RowKey)
                                       .ToList();

                commentCount[d.RowKey] = comments.Count;

                // Broj pratilaca po diskusiji
                var followers = Follows.CreateQuery<FollowEntity>()
                                        .Where(f => f.PartitionKey == d.RowKey)
                                        .ToList();
                followerCountMap[d.RowKey] = followers.Count;

                // Da li trenutni korisnik prati diskusiju
                var currentUserEmail = Session["email"]?.ToString()?.ToLowerInvariant();
                var isFollowing = Follows.CreateQuery<FollowEntity>()
                         .Where(f => f.PartitionKey == d.RowKey && f.RowKey == currentUserEmail)
                         .ToList()
                         .Count > 0;

                isFollowingMap[d.RowKey] = isFollowing;

                // Provera da li je trenutni korisnik kreator diskusije
                isCreatorMap[d.RowKey] = d.CreatorEmail?.ToLowerInvariant() == currentUserEmail;
            }

            ViewBag.CommentCount = commentCount;
            ViewBag.FollowerCountMap = followerCountMap;
            ViewBag.IsFollowingMap = isFollowingMap;
            ViewBag.IsCreatorMap = isCreatorMap;

            return View(discussions);
        }

        

        // POST: /Discussion/Create
        [HttpPost]
        public ActionResult Create(CreateDiscussionDTO dto, HttpPostedFileBase PosterFile)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var email = Session["email"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "Account");

            // Proveri da li je korisnik ovlašćen za kreiranje diskusije
            var users = Storage.GetTable("Users");
            var user = users.Execute(TableOperation.Retrieve<UserEntity>("User", email)).Result as UserEntity;
            if (user == null || !user.IsAuthorVerified)
            {
                ModelState.AddModelError("", "Only verified authors can create discussions.");
                return View(dto);
            }

            string posterUrl = dto.PosterUrl;

            // Ako je korisnik poslao poster (sliku)
            if (PosterFile != null && PosterFile.ContentLength > 0)
            {
                // Proveri veličinu fajla
                if (PosterFile.ContentLength > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("", "The file size exceeds the allowed limit (5MB).");
                    return View(dto);
                }

                try
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(PosterFile.FileName);

                    // Koristi postojeću Storage klasu
                    var containerName = ConfigurationManager.AppSettings["BlobContainerName"];
                    var container = Storage.GetContainer(containerName);

                    // Podesi public access ako prvi put pravimo kontejner
                    var permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    container.SetPermissions(permissions); // <-- OVO postavlja public read za blobove

                    // Uploaduj sliku
                    var blob = container.GetBlockBlobReference(fileName);
                    blob.UploadFromStream(PosterFile.InputStream);

                    // Snimi URL slike
                    posterUrl = blob.Uri.AbsoluteUri;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "An error occurred while uploading the image: " + ex.Message);
                    return View(dto);
                }
            }

            // Kreiraj novu diskusiju
            var discussion = new DiscussionEntity(Guid.NewGuid().ToString("N"))
            {
                CreatorEmail = email,
                MovieTitle = dto.MovieTitle,
                Year = dto.Year,
                Genre = dto.Genre,
                ImdbRating = dto.ImdbRating,
                Synopsis = dto.Synopsis,
                DurationMin = dto.DurationMin,
                PosterUrl = posterUrl,
                Positive = 0,
                Negative = 0,
                CreatedUtc = DateTime.UtcNow
            };

            // Upisivanje u Azure tabelu
            Discussions.Execute(TableOperation.Insert(discussion));

            return RedirectToAction("Index");
        }


        // GET: /Discussion/Create
        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        // POST: /Discussion/Edit/{id}
        [HttpPost]
        public ActionResult Edit(string id, CreateDiscussionDTO dto, HttpPostedFileBase PosterFile)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var email = Session["email"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Login", "Account");

            // Preuzmi diskusiju iz baze
            var discussion = Discussions.Execute(TableOperation.Retrieve<DiscussionEntity>("Disc", id)).Result as DiscussionEntity;
            if (discussion == null || discussion.CreatorEmail != email)
            {
                return HttpNotFound();
            }

            string posterUrl = dto.PosterUrl;

            // Ako je korisnik poslao novi poster (sliku)
            if (PosterFile != null && PosterFile.ContentLength > 0)
            {
                // Proveri veličinu fajla
                if (PosterFile.ContentLength > 5 * 1024 * 1024) // 5MB limit
                {
                    ModelState.AddModelError("", "The file size exceeds the allowed limit (5MB).");
                    return View(dto);
                }

                try
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(PosterFile.FileName);

                    // Koristi postojeći Storage helper za dobavljanje kontejnera
                    var containerName = ConfigurationManager.AppSettings["BlobContainerName"];
                    var container = Storage.GetContainer(containerName);

                    // Postavi public access ako kontejner nije postojao
                    var permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    container.SetPermissions(permissions);

                    // Kreiraj blob reference
                    var blob = container.GetBlockBlobReference(fileName);

                    // Uploaduj sliku
                    blob.UploadFromStream(PosterFile.InputStream);

                    // Snimi URL slike
                    posterUrl = blob.Uri.AbsoluteUri;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "An error occurred while uploading the image: " + ex.Message);
                    return View(dto);
                }
            }

            // Ažuriraj diskusiju sa novim podacima
            discussion.MovieTitle = dto.MovieTitle;
            discussion.Year = dto.Year;
            discussion.Genre = dto.Genre;
            discussion.ImdbRating = dto.ImdbRating;
            discussion.Synopsis = dto.Synopsis;
            discussion.DurationMin = dto.DurationMin;
            discussion.PosterUrl = posterUrl;

            // Ažuriraj diskusiju u tabeli
            Discussions.Execute(TableOperation.Replace(discussion));

            return RedirectToAction("Index");
        }


        // GET: /Discussion/Edit/{id}
        [HttpGet]
        public ActionResult Edit(string id)
        {
            var discussion = Discussions.Execute(TableOperation.Retrieve<DiscussionEntity>("Disc", id)).Result as DiscussionEntity;
            if (discussion == null || discussion.CreatorEmail != Session["email"]?.ToString()?.ToLowerInvariant())
            {
                return HttpNotFound();
            }

            var dto = new CreateDiscussionDTO
            {
                MovieTitle = discussion.MovieTitle,
                Year = discussion.Year,
                Genre = discussion.Genre,
                ImdbRating = discussion.ImdbRating,
                Synopsis = discussion.Synopsis,
                DurationMin = discussion.DurationMin,
                PosterUrl = discussion.PosterUrl
            };

            return View(dto);
        }

        // GET: /Discussion/Delete/{id}
        [HttpGet]
        public ActionResult Delete(string id)
        {
            var discussion = Discussions.Execute(TableOperation.Retrieve<DiscussionEntity>("Disc", id)).Result as DiscussionEntity;
            if (discussion == null || discussion.CreatorEmail != Session["email"]?.ToString()?.ToLowerInvariant())
            {
                return HttpNotFound();
            }

            return View(discussion);
        }

        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(string id)
        {
            var email = Session["email"]?.ToString()?.ToLowerInvariant();
            var discussion = Discussions.Execute(TableOperation.Retrieve<DiscussionEntity>("Disc", id)).Result as DiscussionEntity;
            if (discussion == null || discussion.CreatorEmail != email)
            {
                return HttpNotFound();
            }

            // 1. Delete related comments
            var commentsTable = Storage.GetTable("Comments");
            var commentQuery = new TableQuery<CommentEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, id));
            var comments = commentsTable.ExecuteQuery(commentQuery).ToList();

            foreach (var comment in comments)
            {
                commentsTable.Execute(TableOperation.Delete(comment));
            }

            // 2. Delete poster image from blob storage (if exists)
            if (!string.IsNullOrEmpty(discussion.PosterUrl))
            {
                try
                {
                    var containerName = ConfigurationManager.AppSettings["BlobContainerName"];
                    var blobContainer = Storage.GetContainer(containerName);

                    var uri = new Uri(discussion.PosterUrl);
                    var blobName = Path.GetFileName(uri.LocalPath); // Extracts only file name
                    var blob = blobContainer.GetBlockBlobReference(blobName);
                    blob.DeleteIfExists();
                }
                catch (Exception ex)
                {
                    
                }
            }

            // 3. Delete discussion itself
            Discussions.Execute(TableOperation.Delete(discussion));

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Details(string id)
        {
            // Preuzmi diskusiju
            var discussion = Discussions.Execute(TableOperation.Retrieve<DiscussionEntity>("Disc", id)).Result as DiscussionEntity;
            if (discussion == null)
                return HttpNotFound();

            // Preuzmi komentare vezane za ovu diskusiju
            var query = new TableQuery<CommentEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, id));

            var comments = Comments.ExecuteQuery(query)
                .OrderByDescending(c => c.CreatedUtc)
                .ToList();

            ViewBag.Discussion = discussion;
            ViewBag.Comments = comments;

            return View(new AddCommentDTO { DiscussionId = id });  // Prosleđujemo DTO
        }

        [HttpPost]
        public ActionResult AddComment(AddCommentDTO dto)
        {
            if (!ModelState.IsValid)
            {
                var discussion = Discussions.Execute(
                    TableOperation.Retrieve<DiscussionEntity>("Disc", dto.DiscussionId)).Result as DiscussionEntity;

                var comments = Comments.CreateQuery<CommentEntity>()
                    .Where(c => c.PartitionKey == dto.DiscussionId)
                    .OrderByDescending(c => c.CreatedUtc)
                    .ToList();

                ViewBag.Discussion = discussion;
                ViewBag.Comments = comments;

                return View("Details", dto); // Vraćamo ga na Details sa postojećim komentarima
            }

            var comment = new CommentEntity(dto.DiscussionId, Guid.NewGuid().ToString("N"))
            {
                Text = dto.Text,
                AuthorEmail = (Session["email"] as string)?.ToLowerInvariant(),
                CreatedUtc = DateTime.UtcNow
            };

            Comments.Execute(TableOperation.Insert(comment));

            // Vraćanje na istu stranicu nakon postavljanja komentara
            return RedirectToAction("Details", new { id = dto.DiscussionId });
        }

        [HttpPost]
        public ActionResult Follow(string id)
        {
            if (string.IsNullOrEmpty(CurrentUserEmail))
                return RedirectToAction("Login", "Account");

            var entity = new FollowEntity(id, CurrentUserEmail);

            try
            {
                var insert = TableOperation.Insert(entity);
                Follows.Execute(insert);
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 409)
            {
                // Već postoji — ignoriši
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Unfollow(string id)
        {
            if (string.IsNullOrEmpty(CurrentUserEmail))
                return RedirectToAction("Login", "Account");

            var retrieve = TableOperation.Retrieve<FollowEntity>(id, CurrentUserEmail);
            var result = Follows.Execute(retrieve);

            if (result.Result is FollowEntity entity)
            {
                var delete = TableOperation.Delete(entity);
                Follows.Execute(delete);
            }

            return RedirectToAction("Index");
        }

        private int GetFollowerCount(string discussionId)
        {
            var filter = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, discussionId);
            var query = new TableQuery<FollowEntity>().Where(filter);

            int count = 0;
            TableContinuationToken token = null;

            do
            {
                var segment = Follows.ExecuteQuerySegmented(query, token);
                count += segment.Results.Count;
                token = segment.ContinuationToken;
            }
            while (token != null);

            return count;
        }

        private VoteEntity CheckIfUserVoted(string userEmail, string discussionId)
        {
            // Proveravamo da li postoji glas korisnika za datu diskusiju u tabeli Votes
            var vote = Votes.CreateQuery<VoteEntity>()
                            .Where(v => v.PartitionKey == discussionId && v.RowKey == userEmail)
                            .FirstOrDefault();

            return vote;
        }

        [HttpPost]
        public ActionResult RateFilm(string id, bool isLike)
        {
            var currentUserEmail = Session["email"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(currentUserEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // Dohvatanje diskusije na koju korisnik reaguje
            var discussion = Discussions.CreateQuery<DiscussionEntity>()
                                        .Where(d => d.PartitionKey == "Disc" && d.RowKey == id)
                                        .FirstOrDefault();

            if (discussion == null)
                return RedirectToAction("Index");

            // Proveravamo da li je korisnik već glasao za ovu diskusiju
            var existingVote = CheckIfUserVoted(currentUserEmail, id);

            if (existingVote == null)
            {
                // Prvi glas
                var newVote = new VoteEntity(id, currentUserEmail)
                {
                    IsLike = isLike
                };

                // Spremamo glas u tabelu Votes
                Votes.Execute(TableOperation.Insert(newVote));

                // Ažuriramo brojače lajkova/dislajkova u diskusiji
                if (isLike)
                    discussion.Positive++;
                else
                    discussion.Negative++;
            }
            else
            {
                // Ako je korisnik već glasao, promeniti glas ako je suprotan
                if (existingVote.IsLike != isLike)
                {
                    // Brisanje prethodnog glasa
                    Votes.Execute(TableOperation.Delete(existingVote));

                    // Dodavanje novog glasa
                    var newVote = new VoteEntity(id, currentUserEmail)
                    {
                        IsLike = isLike
                    };
                    Votes.Execute(TableOperation.Insert(newVote));

                    // Ažuriramo brojače u diskusiji
                    if (isLike)
                    {
                        discussion.Positive++;
                        discussion.Negative--;
                    }
                    else
                    {
                        discussion.Negative++;
                        discussion.Positive--;
                    }
                }
                else
                {
                    // Ako je korisnik kliknuo isti glas ponovo, poništavamo glas
                    Votes.Execute(TableOperation.Delete(existingVote));

                    // Ažuriramo brojače
                    if (isLike)
                        discussion.Positive--;
                    else
                        discussion.Negative--;
                }
            }

            // Ažuriraj diskusiju u bazi
            Discussions.Execute(TableOperation.Replace(discussion));

            return RedirectToAction("Index");
        }




    }
}
