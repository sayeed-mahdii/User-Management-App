using System.Net;
using System.Net.Mail;

namespace UserManagement.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string toEmail, string confirmationLink);
    string GetUniqIdValue();
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates a unique identifier value for tokens or tracking.
    /// Note: requested as getUniqIdValue helper.
    /// </summary>
    public string GetUniqIdValue()
    {
        // Nota bene: Guid provides a cryptographically strong unique string
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Sends confirmation email asynchronously in background.
    /// Important: Should never block or fail the registration process if SMTP is unavailable.
    /// </summary>
    public async Task SendConfirmationEmailAsync(string toEmail, string confirmationLink)
    {
        // Run asynchronously on a background thread so registration responds instantly
        await Task.Run(() =>
        {
            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? _configuration["Smtp:Host"];
                var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? _configuration["Smtp:Port"];
                var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? _configuration["Smtp:User"];
                var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? _configuration["Smtp:Pass"];

                if (!string.IsNullOrEmpty(smtpHost) && !string.IsNullOrEmpty(smtpUser))
                {
                    int.TryParse(smtpPortStr, out int smtpPort);
                    if (smtpPort == 0) smtpPort = 587;

                    using var client = new SmtpClient(smtpHost, smtpPort)
                    {
                        Credentials = new NetworkCredential(smtpUser, smtpPass),
                        EnableSsl = true
                    };

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(smtpUser, "User Management App"),
                        Subject = "Confirm your email address",
                        Body = $"<h3>Welcome to the App!</h3><p>Please confirm your email by clicking the link below:</p><p><a href='{confirmationLink}'>Confirm Email</a></p><p>Or copy this link: {confirmationLink}</p>",
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(toEmail);

                    client.Send(mailMessage);
                    _logger.LogInformation("Confirmation email sent successfully to {Email}", toEmail);
                }
                else
                {
                    // Nota bene: When SMTP credentials are not yet configured in .env, we log the link to console for local testing
                    _logger.LogInformation("==================================================");
                    _logger.LogInformation("SIMULATED ASYNC EMAIL TO: {Email}", toEmail);
                    _logger.LogInformation("CONFIRMATION LINK: {Link}", confirmationLink);
                    _logger.LogInformation("==================================================");
                }
            }
            catch (Exception ex)
            {
                // Important: Log exception without crashing the user flow
                _logger.LogError(ex, "Failed to send confirmation email to {Email}", toEmail);
            }
        });
    }
}
