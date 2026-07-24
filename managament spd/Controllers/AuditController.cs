using ManagementSPD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json; 

namespace ManagementSPD.Controllers
{
    [Authorize(Roles = "MasterAdmin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search, string actionFilter, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            // Filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.User.Username.Contains(search) || a.Details.Contains(search));
            }
            if (!string.IsNullOrEmpty(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }
            if (startDate.HasValue) query = query.Where(a => a.Timestamp.Date >= startDate.Value.Date);
            if (endDate.HasValue) query = query.Where(a => a.Timestamp.Date <= endDate.Value.Date);

            // Ambil Data Tabel
            var logs = await query.OrderByDescending(a => a.Timestamp).Take(500).ToListAsync();

            // LOGIKA BARU UNTUK GRAFIK (CHART)

            // 1. Chart Aksi (Action Distribution)
            var actionData = logs
                .GroupBy(l => l.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            ViewBag.ActionLabels = JsonSerializer.Serialize(actionData.Select(x => x.Action));
            ViewBag.ActionCounts = JsonSerializer.Serialize(actionData.Select(x => x.Count));

            // 2. Chart User Teraktif (Top 5 Actors)
            var actorData = logs
                .GroupBy(l => l.User != null ? l.User.Username : "System")
                .Select(g => new { User = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            ViewBag.ActorLabels = JsonSerializer.Serialize(actorData.Select(x => x.User));
            ViewBag.ActorCounts = JsonSerializer.Serialize(actorData.Select(x => x.Count));

         

            ViewBag.Search = search;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}