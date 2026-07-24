using ManagementSPD.Data;
using ManagementSPD.Models;
using ManagementSPD.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ManagementSPD.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // LOGIN (GET) 
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectBasedOnRole(User.FindFirst(ClaimTypes.Role)?.Value);
            }
            return View();
        }

        //  LOGIN (POST) 
        [HttpPost]
#if DEBUG
        //[IgnoreAntiforgeryToken]  // Hanya aktif saat DEBUG/development
#endif
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var hashed = ComputeSha256Hash(model.Password);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmployeeNo == model.EmployeeNo);

                if (user != null && (user.Password == model.Password || user.Password == hashed))
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.EmployeeNo),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role)
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    // AUDIT LOG: RECORD LOGIN
                    var audit = new AuditLog
                    {
                        UserID = user.Id,
                        Action = "Login",
                        TableName = "Users",
                        RecordID = user.Id.ToString(),
                        Details = $"User '{user.Username}' logged in successfully.",
                        Timestamp = DateTime.Now
                    };
                    _context.AuditLogs.Add(audit);
                    await _context.SaveChangesAsync();

                    return RedirectBasedOnRole(user.Role);
                }

                ModelState.AddModelError(string.Empty, "Employee No or Password is incorrect.");
            }

            return View(model);
        }

        //  REGISTER (GET) 
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectBasedOnRole(User.FindFirst(ClaimTypes.Role)?.Value);
            }
            return View();
        }

        //  REGISTER (POST) 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // =========================================================================
                // EMPLOYEE NO FORMAT VALIDATION (Regex: 2 Digits, 2 Letters, 2 Digits)
                // =========================================================================
                var empNoRegex = new System.Text.RegularExpressions.Regex(@"^\d{2}[a-zA-Z]{2}\d{2}$");

                if (!string.IsNullOrEmpty(model.EmployeeNo) && !empNoRegex.IsMatch(model.EmployeeNo))
                {
                    ModelState.AddModelError("EmployeeNo", "Invalid Employee No format! It must be: 2 Digits (Date) + 2 Letters (Initials) + 2 Digits (Year). Example: 11AA26.");
                    return View(model);
                }
                // =========================================================================

                if (await _context.Users.AnyAsync(u => u.EmployeeNo == model.EmployeeNo))
                {
                    ModelState.AddModelError("EmployeeNo", "Employee Number already exists.");
                    return View(model);
                }

                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Username already exists.");
                    return View(model);
                }

                var user = new Models.User
                {
                    Username = model.Username,
                    Email = model.Email,
                    Role = model.Role ?? "Employee",
                    ContractID = model.ContractID,

                    // Convert to Uppercase
                    EmployeeNo = model.EmployeeNo.ToUpper(),

                    Password = ComputeSha256Hash(model.Password),
                    Notifications = new List<Notification>(),
                    LoansRequested = new List<LoanTransaction>(),
                    LoansManaged = new List<LoanTransaction>(),
                    ApprovalsHistory = new List<LoanApproval>()
                };

                _context.Users.Add(user);

                try
                {
                    await _context.SaveChangesAsync();

                    // AUDIT LOG: RECORD REGISTER
                    var audit = new AuditLog
                    {
                        UserID = user.Id,
                        Action = "Register",
                        TableName = "Users",
                        RecordID = user.Id.ToString(),
                        Details = $"New user registered: {user.Username} ({user.Role})",
                        Timestamp = DateTime.Now
                    };
                    _context.AuditLogs.Add(audit);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Registration successful! Please log in.";
                    return RedirectToAction("Login", "Account");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Database Error: " + ex.Message);
                }
            }
            return View(model);
        }

        // LOGOUT 
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        //  REDIRECT BASED ON ROLE 
        private IActionResult RedirectBasedOnRole(string? role)
        {
            if (string.Equals(role, "MasterAdmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            else if (string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard");
            }
            //else if (string.Equals(role, "AGV", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect("https://172.16.104.34:90/");
            }
            return RedirectToAction("Logout");
        }

        //  HASHING FUNCTION 
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