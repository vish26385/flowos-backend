using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace FlowOS.Api.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpSection = _config.GetSection("SmtpSettings");

            using var client = new SmtpClient(smtpSection["Host"], int.Parse(smtpSection["Port"]))
            {
                Credentials = new NetworkCredential(smtpSection["UserName"], smtpSection["Password"]),
                EnableSsl = bool.Parse(smtpSection["EnableSsl"])
            };

            var mail = new MailMessage
            {
                From = new MailAddress(smtpSection["SenderEmail"], smtpSection["SenderName"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(to);

            await client.SendMailAsync(mail);
        }
    }
}
