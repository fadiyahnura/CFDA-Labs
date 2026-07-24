using ManagementSPD.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ManagementSPD.Controllers
{
    [Authorize]
    public class CatalogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CatalogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var licenses = await _context.Licenses.ToListAsync();

            var trendingIds = await _context.LoanTransactions
                .GroupBy(t => t.LicenseID)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToListAsync();

            ViewBag.TrendingIds = trendingIds;

            return View(licenses);
        }
    }
}