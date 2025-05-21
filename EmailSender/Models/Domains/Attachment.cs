using Newtonsoft.Json;
using System.IO;

namespace EmailSender.Models.Domains
{
    public class Attachment
    {
        public string FileName { get; set; }

        [JsonIgnore]
        public byte[] Content { get; set; }

        public string FilePath { get; set; }

        public void LoadContent()
        {
            if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                Content = File.ReadAllBytes(FilePath);
        }
    }
}