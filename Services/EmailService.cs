using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Data;

namespace MLM_Level.Services
{
    public class EmailService : IEmailService
    {
        private readonly ApplicationDbContext _context;

        public EmailService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var settings = await _context.MlmSettings.FirstOrDefaultAsync();
            if (settings == null || string.IsNullOrEmpty(settings.SmtpHost))
            {
                throw new InvalidOperationException("SMTP settings are not configured.");
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(settings.SmtpUsername, settings.SiteName),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            using var smtpClient = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword),
                EnableSsl = settings.SmtpEnableSsl
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
