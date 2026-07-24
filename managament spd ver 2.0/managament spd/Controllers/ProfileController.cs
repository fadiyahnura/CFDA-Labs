using ManagementSPD.Data;
using ManagementSPD.Models;
using ManagementSPD.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System; 
using System.Security.Cryptography; 
using System.Text; 
using System.Threading.Tasks;

namespace ManagementSPD.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Profile
        public async Task<IActionResult> Index()
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (currentUser == null) return NotFound();

            var model = new ProfileViewModel
            {
                Username = currentUser.Username,
                Role = currentUser.Role,
                ContractID = currentUser.ContractID,
                Email = currentUser.Email,
                EmployeeNo = currentUser.EmployeeNo,
            };

            return View(model);
        }

        // POST: Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var username = User.Identity.Name;
            var userToUpdate = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (userToUpdate == null) return NotFound();

            // Validasi 
            if (!ModelState.IsValid)
            {
                model.Username = userToUpdate.Username;
                model.Role = userToUpdate.Role;
                model.ContractID = userToUpdate.ContractID;
                model.EmployeeNo = userToUpdate.EmployeeNo;
                return View(model);
            }

            // Simpan data lama untuk Audit Log
            string oldEmail = userToUpdate.Email;
            bool passwordChanged = false;

            // 1. Update Email
            userToUpdate.Email = model.Email;

            // 2. Update Password (Jika diisi)
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                // Verifikasi Password Lama 
                var hashedCurrent = ComputeSha256Hash(model.CurrentPassword);

                if (userToUpdate.Password != model.CurrentPassword && userToUpdate.Password != hashedCurrent)
                {
                    ModelState.AddModelError("CurrentPassword", "Incorrect current password.");

                    // Isi ulang data readonly
                    model.Username = userToUpdate.Username;
                    model.Role = userToUpdate.Role;
                    model.ContractID = userToUpdate.ContractID;
                    model.EmployeeNo = userToUpdate.EmployeeNo;
                    return View(model);
                }
                userToUpdate.Password = ComputeSha256Hash(model.NewPassword);
                passwordChanged = true;
            }

            // Update User di Context
            _context.Update(userToUpdate);

            // AUDIT LOG: RECORD PROFILE UPDATE
            string changeDetails = "Updated profile. ";
            if (oldEmail != model.Email) changeDetails += $"Email changed. ";
            if (passwordChanged) changeDetails += "Password changed.";

            var audit = new AuditLog
            {
                UserID = userToUpdate.Id, 
                Action = "Update Profile",
                TableName = "Users",
                RecordID = userToUpdate.Id.ToString(),
                Details = changeDetails,
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction("Index");
        }

        // Helper Hashing (Wajib ada biar passwordnya sinkron sama Login)
        private string ComputeSha256Hash(string rawData)
        {
            if (string.IsNullOrEmpty(rawData)) return string.Empty;
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(rawData);
                var hash = sha256.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}