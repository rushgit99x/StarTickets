using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using StarTickets.Models;
using StarTickets.Models.Configuration;

namespace StarTickets.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task SendWelcomeEmailAsync(User user)
        {
            var subject = "Welcome to StarTickets!";
            var roleTitle = user.Role switch
            {
                2 => "Event Organizer",
                3 => "Customer",
                _ => "User"
            };

            var body = GenerateWelcomeEmailBody(user, roleTitle);
            await SendEmailAsync(user.Email, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(User user, string resetUrl)
        {
            var subject = "Reset Your StarTickets Password";
            var body = GeneratePasswordResetEmailBody(user, resetUrl);
            await SendEmailAsync(user.Email, subject, body);
        }

        public async Task SendPasswordResetConfirmationEmailAsync(User user)
        {
            var subject = "Password Reset Successful - StarTickets";
            var body = GeneratePasswordResetConfirmationEmailBody(user);
            await SendEmailAsync(user.Email, subject, body);
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
                client.EnableSsl = _emailSettings.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"Email sent successfully to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                // Don't throw exception to prevent registration failure
                // You might want to implement retry logic or queue failed emails
            }
        }

        private string GenerateWelcomeEmailBody(User user, string roleTitle)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .btn {{ display: inline-block; padding: 12px 25px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
                        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🎟️ Welcome to StarTickets!</h1>
                            <p>Your gateway to amazing events</p>
                        </div>
                        <div class='content'>
                            <h2>Hello {user.FirstName} {user.LastName}!</h2>
                            <p>Congratulations! Your StarTickets account has been successfully created.</p>
                            
                            <p><strong>Account Details:</strong></p>
                            <ul>
                                <li>Email: {user.Email}</li>
                                <li>Account Type: {roleTitle}</li>
                                <li>Registration Date: {DateTime.Now:MMMM dd, yyyy}</li>
                            </ul>

                            {(user.Role == 2 ?
                                @"<p>As an <strong>Event Organizer</strong>, you can now:</p>
                                <ul>
                                    <li>Create and manage your events</li>
                                    <li>Track ticket sales and revenue</li>
                                    <li>Communicate with attendees</li>
                                    <li>Access detailed analytics</li>
                                </ul>" :
                                @"<p>As a <strong>Customer</strong>, you can now:</p>
                                <ul>
                                    <li>Discover exciting events</li>
                                    <li>Book tickets instantly</li>
                                    <li>Manage your bookings</li>
                                    <li>Get event updates</li>
                                </ul>")}

                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='#' class='btn'>Get Started</a>
                            </div>

                            <p>If you have any questions or need assistance, please don't hesitate to contact our support team.</p>
                            
                            <p>Thank you for joining StarTickets!</p>
                            <p>The StarTickets Team</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2024 StarTickets. All rights reserved.</p>
                            <p>This email was sent to {user.Email}</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordResetEmailBody(User user, string resetUrl)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .btn {{ display: inline-block; padding: 15px 30px; background: #667eea; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; font-weight: bold; }}
                        .btn:hover {{ background: #5a67d8; }}
                        .warning {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                        .security-info {{ background: #e8f4f8; padding: 15px; border-left: 4px solid #667eea; margin: 20px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔒 Password Reset Request</h1>
                            <p>StarTickets Account Security</p>
                        </div>
                        <div class='content'>
                            <h2>Hello {user.FirstName},</h2>
                            <p>We received a request to reset the password for your StarTickets account associated with <strong>{user.Email}</strong>.</p>
                            
                            <div class='security-info'>
                                <h3>🔐 Security Information</h3>
                                <ul>
                                    <li>Request Time: {DateTime.UtcNow:MMMM dd, yyyy 'at' HH:mm} UTC</li>
                                    <li>This link will expire in <strong>10 Minutes</strong></li>
                                    <li>For security, this link can only be used once</li>
                                </ul>
                            </div>

                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{resetUrl}' class='btn'>Reset Your Password</a>
                            </div>

                            <div class='warning'>
                                <strong>⚠️ Important:</strong>
                                <ul>
                                    <li>If you didn't request this password reset, please ignore this email</li>
                                    <li>Never share this reset link with anyone</li>
                                    <li>Our support team will never ask for your password</li>
                                </ul>
                            </div>

                            <p>If the button above doesn't work, you can copy and paste this link into your browser:</p>
                            <p style='word-break: break-all; background: #f1f1f1; padding: 10px; border-radius: 5px; font-family: monospace;'>{resetUrl}</p>
                            
                            <p>If you have any questions or concerns, please contact our support team.</p>
                            
                            <p>Best regards,<br>The StarTickets Security Team</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2024 StarTickets. All rights reserved.</p>
                            <p>This email was sent to {user.Email}</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordResetConfirmationEmailBody(User user)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
                        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
                        .success {{ background: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 5px; margin: 20px 0; color: #155724; }}
                        .security-tips {{ background: #e8f4f8; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0; }}
                        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>✅ Password Reset Successful</h1>
                            <p>Your StarTickets account is secure</p>
                        </div>
                        <div class='content'>
                            <h2>Hello {user.FirstName},</h2>
                            
                            <div class='success'>
                                <strong>✅ Success!</strong> Your password has been successfully reset for your StarTickets account.
                            </div>
                            
                            <p><strong>Account Details:</strong></p>
                            <ul>
                                <li>Email: {user.Email}</li>
                                <li>Password Reset: {DateTime.UtcNow:MMMM dd, yyyy 'at' HH:mm} UTC</li>
                            </ul>

                            <div class='security-tips'>
                                <h3>🔐 Security Tips</h3>
                                <ul>
                                    <li>Use a strong, unique password for your account</li>
                                    <li>Never share your password with anyone</li>
                                    <li>Log out from shared or public computers</li>
                                    <li>Contact support if you notice any suspicious activity</li>
                                </ul>
                            </div>

                            <p><strong>What's Next?</strong></p>
                            <p>You can now log in to your StarTickets account using your new password. All previous reset tokens have been invalidated for security.</p>

                            <p><strong>Didn't reset your password?</strong></p>
                            <p>If you didn't initiate this password reset, please contact our support team immediately at support@startickets.com</p>
                            
                            <p>Thank you for keeping your account secure!</p>
                            <p>The StarTickets Team</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; 2024 StarTickets. All rights reserved.</p>
                            <p>This email was sent to {user.Email}</p>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
}