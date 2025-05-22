using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace EmailSender.Models.Domains
{
    public class SenderSettings
    {
        public SenderSettings()
        {
            Port = 587;
            HostSmtp = "smtp.gmail.com";
        }

        [Required(ErrorMessage = "Pole \"E-mail nadawcy\" jest wymagane.")]
        [StringLength(254, ErrorMessage = "Adres email jest zbyt długi.")]
        [EmailAddress(ErrorMessage = "Adres e-mail nadawcy jest nieprawidłowy.")]
        [Display(Name = "E-mail nadawcy")]
        public string SenderEmail { get; set; }

        [Required(ErrorMessage = "Pole \"Hasło do e-maila nadawcy\" jest wymagane.")]
        [Display(Name = "Hasło do e-maila nadawcy")]
        public string SenderEmailPassword { get; set; }

        [Required(ErrorMessage = "Pole \"Nazwa nadawcy\" jest wymagane.")]
        [Display(Name = "Nazwa nadawcy")]
        public string SenderName { get; set; }

        [Required(ErrorMessage = "Pole \"HostSmtp\" jest wymagane.")]
        [RegularExpression(@"^([^\s@:/\\]+?\.)+[^\s@:/\\]{2,}$", ErrorMessage = "Host SMTP musi być nazwą domenową lub adresem IP.")]
        [Display(Name = "Host SMTP")]
        public string HostSmtp { get; set; }

        [Required(ErrorMessage = "Pole \"Port\" jest wymagane.")]
        [Range(1, 65535, ErrorMessage = "Port jest nieprawidłowy.")]
        [RegularExpression("^[0-9]+$", ErrorMessage = "Port musi być liczbą.")]
        public ushort Port { get; set; }
    }
}