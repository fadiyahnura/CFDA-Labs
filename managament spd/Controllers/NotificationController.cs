using ManagementSPD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ManagementSPD.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var notifications = await _context.Notifications
                .Where(n => n.UserID == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }
        [HttpGet]
        public async Task<IActionResult> GetNotificationList()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

       
            var notifications = await _context.Notifications
                .Where(n => n.UserID == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return PartialView("_NotificationList", notifications);
        }

        // Action saat notifikasi diklik
        public async Task<IActionResult> ReadAndRedirect(int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            if (notif == null) return NotFound();

            // Tandai sudah dibaca
            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }

            // Redirect ke Detail Transaksi jika ada ID-nya
            if (notif.TransactionID.HasValue)
            {
                return RedirectToAction("Index", "LoanTransaction", new { openDetails = notif.TransactionID });
            }

            return Redirect(Request.Headers["Referer"].ToString());
        }
        // Tambahkan method ini di dalam class NotificationController
        [HttpPost]
        public async Task<IActionResult> MarkAllRemindersAsRead()
        {
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            // Cari notifikasi reminder milik user ini yang belum dibaca
            var reminders = await _context.Notifications
                .Where(n => n.UserID == user.Id && !n.IsRead && n.Message.Contains("Reminder:"))
                .ToListAsync();

            foreach (var rem in reminders)
            {
                rem.IsRead = true; 
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}