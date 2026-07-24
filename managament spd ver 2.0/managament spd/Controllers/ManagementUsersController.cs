using ManagementSPD.Data;
using ManagementSPD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System;

namespace ManagementSPD.Controllers
{
    [Authorize(Roles = "Staff,MasterAdmin")]
    public class ManagementUsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManagementUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (currentUser == null) return RedirectToAction("Login", "Account");

            List<User> usersToShow;
            if (currentUser.Role == "MasterAdmin")
            {
                usersToShow = await _context.Users.ToListAsync();
            }
            else
            {
                usersToShow = await _context.Users
                    .Where(u => u.Role == "Employee" || u.Role == "AGV")
                    .ToListAsync();
            }

            return View(usersToShow);
        }

        //  CREATE USER 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Username,Email,Password,Role,ContractID,EmployeeNo")] User user)
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            if (!User.IsInRole("MasterAdmin") && (user.Role == "Staff" || user.Role == "MasterAdmin"))
            {
                user.Role = "Employee";
            }

            // =========================================================================
            // EMPLOYEE NO FORMAT VALIDATION (Regex: 2 Digits, 2 Letters, 2 Digits)
            // =========================================================================
            var empNoRegex = new System.Text.RegularExpressions.Regex(@"^\d{2}[a-zA-Z]{2}\d{2}$");
            if (!string.IsNullOrEmpty(user.EmployeeNo) && !empNoRegex.IsMatch(user.EmployeeNo))
            {
                TempData["ErrorMessage"] = "Invalid Employee No format! It must be: 2 Digits + 2 Letters + 2 Digits (Example: 11AA26).";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrEmpty(user.EmployeeNo)) user.EmployeeNo = user.EmployeeNo.ToUpper();
            // =========================================================================

            if (_context.Users.Any(u => u.EmployeeNo == user.EmployeeNo)) ModelState.AddModelError("EmployeeNo", "Employee Number already exists.");
            if (_context.Users.Any(u => u.Username == user.Username)) ModelState.AddModelError("Username", "Username already exists.");

            ModelState.Remove("Notifications"); ModelState.Remove("LoansRequested"); ModelState.Remove("LoansManaged");
            ModelState.Remove("ApprovalsHistory"); ModelState.Remove("Id");

            if (ModelState.IsValid)
            {
                user.Password = ComputeSha256Hash(user.Password);
                _context.Add(user);
                await _context.SaveChangesAsync();

                // AUDIT LOG
                var audit = new AuditLog
                {
                    UserID = currentUser.Id,
                    Action = "Create User",
                    TableName = "Users",
                    RecordID = user.Id.ToString(),
                    Details = $"Created new user: {user.Username} ({user.Role})",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "User successfully created!";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to create a new user. Please verify your inputs.";
            return RedirectToAction(nameof(Index));
        }

        //  EDIT (GET) 
        [HttpGet]
        [Route("ManagementUsers/Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest("User ID is missing.");

            var userTarget = await _context.Users.FindAsync(id);
            if (userTarget == null) return NotFound("User not found.");

            // Security Check
            if (!User.IsInRole("MasterAdmin") && (userTarget.Role == "Staff" || userTarget.Role == "MasterAdmin"))
            {
                return Content("<div class='alert alert-danger'>Access Denied: You cannot edit this user.</div>");
            }

            ViewBag.IsMasterAdmin = User.IsInRole("MasterAdmin");
            return PartialView("_EditForm", userTarget);
        }

        //  EDIT (POST) 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user, string? Password)
        {
            if (id != user.Id) return NotFound();

            // =========================================================================
            // EMPLOYEE NO FORMAT VALIDATION FOR EDIT
            // =========================================================================
            var empNoRegex = new System.Text.RegularExpressions.Regex(@"^\d{2}[a-zA-Z]{2}\d{2}$");
            if (!string.IsNullOrEmpty(user.EmployeeNo) && !empNoRegex.IsMatch(user.EmployeeNo))
            {
                TempData["ErrorMessage"] = "Update failed! Employee No format must be: 2 Digits + 2 Letters + 2 Digits (Example: 11AA26).";
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrEmpty(user.EmployeeNo)) user.EmployeeNo = user.EmployeeNo.ToUpper();
            // =========================================================================

            ModelState.Remove("Password"); ModelState.Remove("Username"); ModelState.Remove("Notifications");
            ModelState.Remove("LoansRequested"); ModelState.Remove("LoansManaged"); ModelState.Remove("ApprovalsHistory");

            if (ModelState.IsValid)
            {
                try
                {
                    var userToUpdate = await _context.Users.FindAsync(id);
                    if (userToUpdate == null) return NotFound();

                    string oldRole = userToUpdate.Role;
                    bool passwordChanged = false;

                    if (User.IsInRole("MasterAdmin"))
                    {
                        userToUpdate.Role = user.Role;
                    }

                    userToUpdate.Email = user.Email;
                    userToUpdate.ContractID = user.ContractID;
                    userToUpdate.EmployeeNo = user.EmployeeNo;

                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        userToUpdate.Password = ComputeSha256Hash(Password);
                        passwordChanged = true;
                    }

                    _context.Update(userToUpdate);

                    // AUDIT LOG
                    var currentUserName = User.Identity.Name;
                    var executor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == currentUserName);

                    if (executor != null)
                    {
                        string changeDetails = $"Updated user '{userToUpdate.Username}'. ";
                        if (oldRole != userToUpdate.Role) changeDetails += $"Role: {oldRole}->{userToUpdate.Role}. ";
                        if (passwordChanged) changeDetails += "Password changed. ";

                        var audit = new AuditLog
                        {
                            UserID = executor.Id,
                            Action = "Update User",
                            TableName = "Users",
                            RecordID = id.ToString(),
                            Details = changeDetails,
                            Timestamp = DateTime.Now
                        };
                        _context.AuditLogs.Add(audit);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "User successfully updated!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Users.Any(e => e.Id == user.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }

        //  DELETE USER 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            if (user == null) return NotFound();

            if (!User.IsInRole("MasterAdmin") && (user.Role == "Staff" || user.Role == "MasterAdmin"))
            {
                return Forbid();
            }

            try
            {
                var audit = new AuditLog
                {
                    UserID = currentUser.Id,
                    Action = "Delete User",
                    TableName = "Users",
                    RecordID = id.ToString(),
                    Details = $"Deleted user account: {user.Username} ({user.Role})",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(audit);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "User successfully deleted!";
            }
            catch (Exception ex)
            {
                // FOREIGN KEY CONSTRAINT ERROR MESSAGE
                TempData["ErrorMessage"] = "Failed to delete user! This user cannot be deleted because they have active loan records, approvals, or notifications tied to the system.";
            }

            return RedirectToAction(nameof(Index));
        }

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