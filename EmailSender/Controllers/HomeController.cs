using Cipher;
using EmailSender.ContractResolvers;
using EmailSender.Extensions;
using EmailSender.Models.Domains;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace EmailSender.Controllers
{
    public class HomeController : Controller
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        static readonly StringCipher cipher = new("89CAEC33-45C8-4DBB-838A-AE5D891351B3");
        public static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");
        public static readonly string MessagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "messages.json");
        public static readonly string AttachmentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Attachments");

        MailMessage _mail;
        SmtpClient _smtp;

        public ActionResult Index() => View();

        public ActionResult Info() => View();

        public ActionResult Settings() => RedirectToAction("Index", "Settings");

        public ActionResult Archive() => RedirectToAction("Index", "Archive");

        [HttpGet]
        public ActionResult Send()
        {
            if (!System.IO.File.Exists(SettingsController.SettingsPath))
            {
                TempData["Message"] = "Brak pliku ustawień. Skonfiguruj ustawienia przed wysłaniem maila.";
                TempData["AlertType"] = "error";
                return RedirectToAction("Index");
            }
            return View(new EmailParams());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Send(EmailParams formModel, HttpPostedFileBase[] attachments, bool? removeAttachment)
        {
            if (removeAttachment == true)
            {
                formModel.Attachment = null;
                ModelState.Clear(); // Żeby nie trzymał starego błędu
                return View(formModel);
            }
            if (attachments != null && attachments.Length > 0)
            {
                HttpPostedFileBase attachment = attachments[0];
                if (attachment != null && attachment.ContentLength > 0)
                {
                    if (!Directory.Exists(AttachmentsPath))
                        Directory.CreateDirectory(AttachmentsPath);

                    string uniqueFileName = Path.GetFileName(attachment.FileName);
                    uniqueFileName = EnsureUniqueFileName(uniqueFileName);
                    string filePath = Path.Combine(AttachmentsPath, uniqueFileName);

                    attachment.SaveAs(filePath);

                    formModel.Attachment = new Models.Domains.Attachment
                    {
                        FileName = uniqueFileName,
                        FilePath = filePath
                    };
                }
            }
            
            if (!ModelState.IsValid)
                return View(formModel);

            if (!AreEmailsValid(formModel.Receivers, out string errorMessage))
            {
                ModelState.AddModelError("Receivers", errorMessage);
                return View(formModel);
            }

            try
            {
                SenderSettings senderSettings = SettingsController.DeserializeSettings();

                await SendEmailAsync(formModel, senderSettings);

                TempData["Message"] = "Wysłano e-maila.";

                return View("Index");
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Błąd podczas wysyłania e-maila.";
                TempData["AlertType"] = "error";
                _logger.Error("Błąd podczas wysyłania e-maila: " + ex);
                return View("Index");
            }
            finally
            {
                SenderSettings senderSettings = SettingsController.DeserializeSettings();
                formModel.SenderSettings.SenderEmail = senderSettings.SenderEmail;
                formModel.SendingTime = DateTime.Now;
                SerializeMessages(formModel);

                _smtp?.Dispose();
                _mail?.Dispose();
            }
        }

        [HttpGet]
        public ActionResult ResendFromArchive(string receivers, string subject, string body, string attachmentFileName, int messageIndex)
        {
            var model = new EmailParams
            {
                Receivers = receivers,
                Subject = subject,
                Body = body
            };

            if (!string.IsNullOrEmpty(attachmentFileName))
            {
                string filePath = Path.Combine(AttachmentsPath, attachmentFileName);
                if (System.IO.File.Exists(filePath))
                {
                    model.Attachment = new Models.Domains.Attachment
                    {
                        FileName = attachmentFileName,
                        FilePath = filePath
                    };
                }
            }

            ViewBag.MessageIndex = messageIndex;
            return View("Send", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResendFromArchive(EmailParams formModel, string attachmentFileName, int messageIndex)
        {
            try
            {
                if (!string.IsNullOrEmpty(attachmentFileName) && formModel.Attachment == null)
                {
                    string filePath = Path.Combine(AttachmentsPath, attachmentFileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        formModel.Attachment = new Models.Domains.Attachment
                        {
                            FileName = attachmentFileName,
                            FilePath = filePath
                        };
                    }
                }
                if (!AreEmailsValid(formModel.Receivers, out string errorMessage))
                    ModelState.AddModelError("Receivers", errorMessage);

                if (!ModelState.IsValid)
                {
                    ViewBag.MessageIndex = messageIndex;
                    return View("Send", formModel);
                }

                SenderSettings senderSettings = SettingsController.DeserializeSettings();

                // Sprawdzenie czy mamy załącznik z poprzedniej wiadomości
                if (!string.IsNullOrEmpty(attachmentFileName))
                {
                    string filePath = Path.Combine(AttachmentsPath, attachmentFileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        formModel.Attachment = new Models.Domains.Attachment
                        {
                            FileName = attachmentFileName,
                            FilePath = filePath
                        };
                    }
                }

                await SendEmailAsync(formModel, senderSettings);

                if (messageIndex >= 0)
                {
                    formModel.SenderSettings = senderSettings;
                    formModel.SendingTime = DateTime.Now;
                    formModel.IsDelivered = true;

                    SerializeMessages(formModel, messageIndex);
                }

                TempData["Message"] = "Wysłano e-maila.";
                return View("Index");
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Błąd podczas wysyłania maila.";
                TempData["AlertType"] = "error";
                _logger.Error("Błąd podczas ponownego wysyłania e-maila: " + ex);
                return View("Index");
            }
            finally
            {
                _smtp?.Dispose();
                _mail?.Dispose();
            }
        }

        [HttpPost]
        public void SerializeMessages(EmailParams emailParams, int? messageIndex = null)
        {
            if (emailParams.Attachment == null && _mail != null && _mail.Attachments.Count > 0)
            {
                var mailAttachment = _mail.Attachments[0];
                emailParams.Attachment = new Models.Domains.Attachment
                {
                    FileName = Path.GetFileName(mailAttachment.Name),
                    FilePath = mailAttachment.ContentStream is FileStream fileStream ?
                              fileStream.Name : Path.Combine(AttachmentsPath, Path.GetFileName(mailAttachment.Name))
                };
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new IgnoreSensitiveDataContractResolver()
            };

            List<EmailParams> messages;
            string appDataDir = Path.GetDirectoryName(MessagesPath);
            if (!Directory.Exists(appDataDir))
                Directory.CreateDirectory(appDataDir);
            if (!System.IO.File.Exists(MessagesPath))
                System.IO.File.WriteAllText(MessagesPath, "[]");
            string existingJson = System.IO.File.ReadAllText(MessagesPath);
            try
            {
                messages = JsonConvert.DeserializeObject<List<EmailParams>>(existingJson) ?? [];
            }
            catch
            {
                messages = [];
            }
            if (messageIndex.HasValue)
                messages[messageIndex.Value] = emailParams; //aktualizacja
            else
                messages.Add(emailParams); //dodanie nowej

            string json = JsonConvert.SerializeObject(messages, Formatting.Indented, settings);
            System.IO.File.WriteAllText(MessagesPath, json);
        }

        string EnsureUniqueFileName(string fileName)
        {
            string uniqueFileName = fileName;
            uint counter = 1;

            while (System.IO.File.Exists(Path.Combine(AttachmentsPath, uniqueFileName)))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(uniqueFileName);
                string extension = Path.GetExtension(uniqueFileName);
                uniqueFileName = $"{fileNameWithoutExtension}_{counter}{extension}";
                counter++;
            }
            return uniqueFileName;
        }

        async Task SendEmailAsync(EmailParams formModel, SenderSettings senderSettings)
        {
            if (string.IsNullOrWhiteSpace(formModel.Subject))
                formModel.Subject = "Nowa wiadomość";

            _mail = new MailMessage
            {
                From = new MailAddress(senderSettings.SenderEmail, senderSettings.SenderName),
                IsBodyHtml = true,
                Subject = formModel.Subject,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            foreach (string receiver in formModel.Receivers.Split(','))
                _mail.To.Add(receiver.Trim());

            _mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(formModel.Body.StripHTML(), null, MediaTypeNames.Text.Plain));

            _mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString($@"
            <html>
                 <head> 
                 </head>
                 <body>
                    <div style='font-size: 16px padding: 10px; font-family: Arial; line-height: 1.4;'>
                        {formModel.Body}
                    </div>
                 </body>
            </html>
            ", null, MediaTypeNames.Text.Html));

            if (formModel.Attachment != null && System.IO.File.Exists(formModel.Attachment.FilePath))
                _mail.Attachments.Add(new System.Net.Mail.Attachment(formModel.Attachment.FilePath));

            _smtp = new SmtpClient
            {
                Host = senderSettings .HostSmtp,
                EnableSsl = true,
                Port = senderSettings.Port,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential
                (
                    senderSettings.SenderEmail,
                    cipher.Decrypt(senderSettings.SenderEmailPassword)
                )
            };

            await _smtp.SendMailAsync(_mail);
            formModel.IsDelivered = true;
        }

        private bool AreEmailsValid(string receivers, out string errorMessage)
        {
            errorMessage = null;
            string[] emails = [.. receivers
        .Split([','], StringSplitOptions.RemoveEmptyEntries)
        .Select(email => email.Trim())];

            if (!emails.All(email => EmailRegex.IsMatch(email)))
            {
                errorMessage = "Wszystkie adresy email odbiorców muszą być poprawne.";
                return false;
            }

            if (emails.Any(email => email.Length > 254))
            {
                errorMessage = "Każdy adres e-mail może mieć maksymalnie 254 znaki.";
                return false;
            }

            return true;
        }
    }
}