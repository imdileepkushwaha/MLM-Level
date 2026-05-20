using System.Threading.Tasks;

namespace MLM_Level.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
    }
}
