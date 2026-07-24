using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSPD.Data;
using ManagementSPD.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;

namespace ManagementSPD.Controllers
{
    [Authorize(Roles = "Staff, MasterAdmin, Employee")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            var today = DateTime.Today;

            if (currentUser != null)
            {
                var warningDate = today.AddDays(7);

                var expiringLoans = await _context.LoanTransactions
                    .Include(t => t.License)
                    .Where(t => t.EmployeeID == currentUser.Id
                             && t.Status == "Approved"
                             && t.DueDate.Date >= today
                             && t.DueDate.Date <= warningDate)
                    .ToListAsync();

                bool newNotifCreated = false;

                foreach (var loan in expiringLoans)
                {
                    int daysLeft = (loan.DueDate.Date - today).Days;
                    string notifMsg = $"Reminder: Item '{loan.License.LicenseName}' due in {daysLeft} days ({loan.DueDate:dd-MMM}).";

                    bool alreadyNotifiedToday = await _context.Notifications
                        .AnyAsync(n => n.TransactionID == loan.TransactionID
                                    && n.Message.Contains($"due in {daysLeft} days"));

                    if (!alreadyNotifiedToday)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserID = currentUser.Id,
                            Message = notifMsg,
                            CreatedAt = DateTime.Now,
                            TransactionID = loan.TransactionID,
                            IsRead = false
                        });
                        newNotifCreated = true;
                    }
                }

                if (newNotifCreated)
                {
                    await _context.SaveChangesAsync();
                }

                var activeReminders = await _context.Notifications
                    .Where(n => n.UserID == currentUser.Id
                             && !n.IsRead
                             && n.Message.Contains("Reminder:"))
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                ViewBag.ActiveReminders = activeReminders;
            }

            var totalTransactions = await _context.LoanTransactions.CountAsync();
            var thirtyDaysFromNow = today.AddDays(30);
            var sevenDaysFromNow = today.AddDays(7);

            var soonExpiring = await _context.LoanTransactions
                .Where(t => t.Status == "Approved" && t.DueDate <= thirtyDaysFromNow && t.DueDate >= today)
                .CountAsync();

            var urgentExpiring = await _context.LoanTransactions
                .Where(t => t.Status == "Approved" && t.DueDate <= sevenDaysFromNow && t.DueDate >= today)
                .CountAsync();

            var expired = await _context.LoanTransactions
                .Where(t => t.Status == "Approved" && t.DueDate < today)
                .CountAsync();

            var averageQuantity = totalTransactions > 0 ? await _context.LoanTransactions.AverageAsync(t => t.Qty) : 0;
            var totalUsers = await _context.Users.CountAsync();

            var latestTransactions = await _context.LoanTransactions
                .Include(t => t.License).Include(t => t.Employee)
                .OrderByDescending(t => t.RequestDate).Take(10).ToListAsync();

            var problematicTransactions = await _context.LoanTransactions
                .Include(t => t.License).Include(t => t.Employee)
                .Where(t => t.Status == "Approved" && (t.DueDate < today || (t.DueDate <= thirtyDaysFromNow && t.DueDate >= today)))
                .OrderBy(t => t.DueDate).Take(10).ToListAsync();

            var licenseUsage = await _context.LoanTransactions.Include(t => t.License).GroupBy(t => t.License.LicenseName).Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync();
            var picAnalysis = await _context.LoanTransactions.Include(t => t.Staff).Where(t => t.StaffID != null).GroupBy(t => t.Staff.Username).Select(g => new { PICName = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync();

            var trendLabels = new List<string>();
            var newLicensesData = new List<int>();
            var expiredLicensesData = new List<int>();
            for (int i = 11; i >= 0; i--) trendLabels.Add(today.AddMonths(-i).ToString("MMM yyyy", new CultureInfo("id-ID")));
            var startDateLimit = today.AddMonths(-11).AddDays(1 - today.Day);
            var newLoansTrend = await _context.LoanTransactions.Where(t => t.RequestDate >= startDateLimit).GroupBy(t => new { t.RequestDate.Year, t.RequestDate.Month }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();
            var expiredLoansTrend = await _context.LoanTransactions.Where(t => t.DueDate >= startDateLimit && t.DueDate < today && t.Status == "Approved").GroupBy(t => new { t.DueDate.Year, t.DueDate.Month }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();
            foreach (string label in trendLabels)
            {
                var monthDate = DateTime.ParseExact(label, "MMM yyyy", new CultureInfo("id-ID"));
                newLicensesData.Add(newLoansTrend.FirstOrDefault(t => t.Year == monthDate.Year && t.Month == monthDate.Month)?.Count ?? 0);
                expiredLicensesData.Add(expiredLoansTrend.FirstOrDefault(t => t.Year == monthDate.Year && t.Month == monthDate.Month)?.Count ?? 0);
            }

            var topLicenses = await _context.LoanTransactions.Include(t => t.License).GroupBy(t => t.License.LicenseName).Select(g => new { Name = g.Key, TotalQty = g.Sum(x => x.Qty) }).OrderByDescending(x => x.TotalQty).Take(5).ToListAsync();
            var yearlyTrend = await _context.LoanTransactions.GroupBy(t => t.RequestDate.Year).Select(g => new { Year = g.Key, Count = g.Count() }).OrderBy(x => x.Year).ToListAsync();
            var activeVsExpiredPerYear = await _context.LoanTransactions.GroupBy(t => t.RequestDate.Year).Select(g => new { Year = g.Key, Active = g.Count(x => x.DueDate >= today), Expired = g.Count(x => x.DueDate < today) }).OrderBy(x => x.Year).ToListAsync();
            var statusOverview = await _context.LoanTransactions.GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();

            ViewBag.TotalLicenses = totalTransactions;
            ViewBag.SoonExpiringLicenses = soonExpiring;
            ViewBag.UrgentExpiringLicenses = urgentExpiring;
            ViewBag.ExpiredLicenses = expired;
            ViewBag.AverageQuantity = averageQuantity.ToString("F2");
            ViewBag.TotalUsers = totalUsers;
            ViewBag.LatestLicenses = latestTransactions;
            ViewBag.ProblematicLicenses = problematicTransactions;

            ViewBag.LicenseLabels = JsonSerializer.Serialize(licenseUsage.Select(x => x.Name));
            ViewBag.LicenseCounts = JsonSerializer.Serialize(licenseUsage.Select(x => x.Count));
            ViewBag.PicLabels = JsonSerializer.Serialize(picAnalysis.Select(x => x.PICName));
            ViewBag.PicData = JsonSerializer.Serialize(picAnalysis.Select(x => x.Count));
            ViewBag.TrendLabels = JsonSerializer.Serialize(trendLabels);
            ViewBag.NewLicensesData = JsonSerializer.Serialize(newLicensesData);
            ViewBag.ExpiredLicensesData = JsonSerializer.Serialize(expiredLicensesData);
            ViewBag.TopLicensesLabels = JsonSerializer.Serialize(topLicenses.Select(x => x.Name));
            ViewBag.TopLicensesData = JsonSerializer.Serialize(topLicenses.Select(x => x.TotalQty));
            ViewBag.YearLabels = JsonSerializer.Serialize(yearlyTrend.Select(x => x.Year.ToString()));
            ViewBag.YearData = JsonSerializer.Serialize(yearlyTrend.Select(x => x.Count));
            ViewBag.ActiveYearLabels = JsonSerializer.Serialize(activeVsExpiredPerYear.Select(x => x.Year.ToString()));
            ViewBag.ActiveYearData = JsonSerializer.Serialize(activeVsExpiredPerYear.Select(x => x.Active));
            ViewBag.ExpiredYearData = JsonSerializer.Serialize(activeVsExpiredPerYear.Select(x => x.Expired));
            ViewBag.StatusApprovalLabels = JsonSerializer.Serialize(statusOverview.Select(x => x.Status));
            ViewBag.StatusApprovalData = JsonSerializer.Serialize(statusOverview.Select(x => x.Count));

            return View();
        }
    }
}