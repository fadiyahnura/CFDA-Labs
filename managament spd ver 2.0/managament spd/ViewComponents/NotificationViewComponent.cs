using ManagementSPD.Data;
using ManagementSPD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ManagementSPD.ViewComponents
{
    public class NotificationViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (currentUser == null)
            {
                return View(new List<Notification>());
            }

            // Ambil notifikasi dari tabel Notifications yang baru
            
            var notifications = await _context.Notifications
                .Where(n => n.UserID == currentUser.Id && !n.IsRead) 
                .OrderByDescending(n => n.CreatedAt)
                .Take(10) // Batasi 10 notifikasi terakhir
                .ToListAsync();

            return View(notifications);
        }
    }
}