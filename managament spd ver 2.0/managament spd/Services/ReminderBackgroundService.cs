using ManagementSPD.Data;
using ManagementSPD.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ManagementSPD.Services
{
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReminderBackgroundService> _logger;

        public ReminderBackgroundService(IServiceProvider serviceProvider, ILogger<ReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reminder Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Checking for due date reminders...");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var today = DateTime.Today;
                    var warningDate = today.AddDays(3); // Warning 3 days before due date

                    var expiringSoon = await context.LoanTransactions
                        .Include(t => t.License)
                        .Include(t => t.Employee)
                        .Where(t => t.Status == "Approved" 
                                 && t.DueDate.Date >= today 
                                 && t.DueDate.Date <= warningDate)
                        .ToListAsync();

                    foreach (var loan in expiringSoon)
                    {
                        if (loan.Employee != null && !string.IsNullOrEmpty(loan.Employee.Email))
                        {
                            int daysLeft = (loan.DueDate.Date - today).Days;
                            string message = $@"
                                <h3>Loan Due Date Reminder</h3>
                                <p>Dear {loan.Employee.Username},</p>
                                <p>This is a reminder that your loan for <b>{loan.License?.LicenseName}</b> will expire in <b>{daysLeft} days</b> ({loan.DueDate:dd MMM yyyy}).</p>
                                <p>Please return the item or contact administrator for extension.</p>
                                <br/>
                                <p>Best Regards,<br/>Management SPD System</p>";

                            await emailService.SendEmailAsync(loan.Employee.Email, $"[REMINDER] Loan Due Date: {loan.License?.LicenseName}", message);
                        }
                    }
                }

                // Wait for 24 hours before the next check
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
