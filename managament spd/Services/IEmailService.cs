using System.Threading.Tasks;

namespace ManagementSPD.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}