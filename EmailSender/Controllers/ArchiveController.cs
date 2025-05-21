using EmailSender.Models.Domains;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace EmailSender.Controllers
{
    public class ArchiveController : Controller
    {

        public ActionResult Index()
        {
            if (!System.IO.File.Exists(HomeController.MessagesPath))
            {
                TempData["Message"] = "Archiwum jest puste.";
                TempData["AlertType"] = "error";
                return RedirectToAction("Index", "Home");
            }
            var messages = GetMessages()
                .OrderByDescending(m => m.SendingTime)
                .ToList();

            return View("Archive", messages);
        }

        public ActionResult Details(int id)
        {
            var messages = GetMessages();
            if (id >= 0 && id < messages.Count)
            {
                ViewBag.MessageIndex = id;
                return View(messages[id]);
            }
            return RedirectToAction("Index");
        }
        public ActionResult Download(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            string filePath = Path.Combine(HomeController.AttachmentsPath, fileName);
            if (!System.IO.File.Exists(filePath))
                return HttpNotFound();

            return File(filePath, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
        }

        private List<EmailParams> GetMessages()
        {
            if (!System.IO.File.Exists(HomeController.MessagesPath))
                return [];

            try
            {
                string json = System.IO.File.ReadAllText(HomeController.MessagesPath);
                return JsonConvert.DeserializeObject<List<EmailParams>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
        public ActionResult ResendEmail(int id)
        {
            var messages = GetMessages();
            if (id < 0 || id >= messages.Count)
                return RedirectToAction("Index");

            EmailParams failedEmail = messages[id];

            return RedirectToAction("ResendFromArchive", "Home", new
            {
                receivers = failedEmail.Receivers,
                subject = failedEmail.Subject,
                body = failedEmail.Body,
                attachmentFileName = failedEmail.Attachment?.FileName,
                messageIndex = id 
            });
        }
    }
}