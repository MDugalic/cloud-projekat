using Common;
using Common.DTOs;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace MovieDiscussionService.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account
        private CloudTable Users => Storage.GetTable("Users");

        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Register(RegisterDTO dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var existing = Users.Execute(TableOperation.Retrieve<UserEntity>("User", dto.Email.ToLower())).Result as UserEntity;
            if (existing != null)
            {
                ModelState.AddModelError("", "Email already registered.");
                return View(dto);
            }

            var user = new UserEntity(dto.Email)
            {
                FullName = dto.FullName,
                Gender = dto.Gender,
                Country = dto.Country,
                City = dto.City,
                Address = dto.Address,
                PasswordHash = HashPassword(dto.Password),
                PhotoUrl = dto.PhotoUrl,
                IsAuthorVerified = false
            };

            Users.Execute(TableOperation.Insert(user));

            Session["email"] = dto.Email.ToLower();
            return RedirectToAction("Index", "Discussion");
        }

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(LoginDTO dto)
        {
            var user = Users.Execute(TableOperation.Retrieve<UserEntity>("User", dto.Email.ToLower())).Result as UserEntity;
            if (user == null || user.PasswordHash != HashPassword(dto.Password))
            {
                ModelState.AddModelError("", "Invalid credentials.");
                return View(dto);
            }

            Session["email"] = user.RowKey;
            return RedirectToAction("Index", "Discussion");
        }

        [HttpGet]
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }
        [HttpGet]
        public ActionResult EditProfile()
        {
            var email = Session["email"]?.ToString();
            if (email == null)
                return RedirectToAction("Login");

            var user = Users.Execute(TableOperation.Retrieve<UserEntity>("User", email)).Result as UserEntity;
            if (user == null)
                return RedirectToAction("Login");

            var dto = new RegisterDTO
            {
                FullName = user.FullName,
                Gender = user.Gender,
                Country = user.Country,
                City = user.City,
                Address = user.Address,
                Email = user.RowKey,
                PhotoUrl = user.PhotoUrl
            };

            return View(dto);
        }

        [HttpPost]
        public ActionResult EditProfile(RegisterDTO dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var email = Session["email"]?.ToString();
            if (email == null)
                return RedirectToAction("Login");

            var user = Users.Execute(TableOperation.Retrieve<UserEntity>("User", email)).Result as UserEntity;
            if (user == null)
                return RedirectToAction("Login");

            user.FullName = dto.FullName;
            user.Gender = dto.Gender;
            user.Country = dto.Country;
            user.City = dto.City;
            user.Address = dto.Address;
            user.PhotoUrl = dto.PhotoUrl;

            // Lozinku menjaj samo ako je uneta nova
            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = HashPassword(dto.Password);

            Users.Execute(TableOperation.Replace(user));

            return RedirectToAction("Index", "Discussion");
        }

    }
}