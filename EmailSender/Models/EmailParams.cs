using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace EmailSender.Models.Domains
{
    public class EmailParams
    {
        public EmailParams() => SenderSettings = new();
        
        [Required(ErrorMessage = "Pole \"Adresy e-mail odbiorców\" jest wymagane.")]
        [Display(Name = "Adresy e-mail odbiorców")]
        public string Receivers { get; set; }

        [Display(Name = "Temat")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Pole \"Treść\" jest wymagane.")]
        [Display(Name = "Treść")]
        public string Body { get; set; }

        [Display(Name = "Czas nadania")]
        public DateTime SendingTime { get; set; }

        [Display(Name = "Status wysłania")]
        public bool IsDelivered { get; set; }

        public Attachment Attachment { get; set; }

        public SenderSettings SenderSettings { get; set; }
    }
}