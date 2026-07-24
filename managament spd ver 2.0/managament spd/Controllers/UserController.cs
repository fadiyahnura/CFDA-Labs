using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManagementSPD.Data;

namespace ManagementSPD.Controllers
{
    [Authorize(Roles = "Employee")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var komponen = await _context.LoanTransactions.ToListAsync();
            return View(komponen);
        }
    }
}