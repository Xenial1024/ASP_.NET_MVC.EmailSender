using Cipher;
using EmailSender.Models.Domains;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Web.Mvc;


namespace EmailSender.Controllers
{
    public class SettingsController : Controller
    {
        static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "settings.json");
        static readonly StringCipher cipher = new("89CAEC33-45C8-4DBB-838A-AE5D891351B3");

        public ActionResult Index()
        {
            SenderSettings model = DeserializeSettings();
            return View("Settings", model);
        }

        [HttpPost]
        public ActionResult SerializeSettings(SenderSettings senderSettings)
        {
            if (!ModelState.IsValid)
            {
                foreach (var entry in ModelState)
                {
                    if (entry.Value.Errors.Count > 0)
                    {
                        foreach (var error in entry.Value.Errors)
                            _logger.Error($"Pole: {entry.Key}, Błąd: {error.ErrorMessage}");
                    }
                }
                return View("Settings", senderSettings);
            }

            if (!HomeController.EmailRegex.IsMatch(senderSettings.SenderEmail))
            {
                ModelState.AddModelError("SenderEmail", "Adres email musi być poprawny.");
                _logger.Error("Adres nie był poprawny.");
                return View(senderSettings);
            }

            var settings = new
            {
                senderSettings.SenderEmail,
                SenderEmailPassword = cipher.Encrypt(senderSettings.SenderEmailPassword),
                senderSettings.SenderName,
                senderSettings.HostSmtp,
                senderSettings.Port
            };

            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            string directory = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(SettingsPath, json);
            TempData["Message"] = "Zapisano ustawienia.";
            return RedirectToAction("Index", "Home");
        }

        public static SenderSettings DeserializeSettings()
        {
            try
            {
                SenderSettings settings = new();

                if(System.IO.File.Exists(SettingsPath))
                    settings = JsonConvert.DeserializeObject<SenderSettings>(System.IO.File.ReadAllText(SettingsPath));

                return settings ?? throw new InvalidOperationException("Deserializacja zwróciła nulla");

            }
            catch (Exception ex)
            {
                _logger.Error($"Błąd przy odczycie ustawień: {ex.Message}.");
                throw;
            }
        }
    }
}