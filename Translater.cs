using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace CodeTranslater
{
    public class Translater : IDisposable
    {
        static readonly string[] LanguageStrings = new string[]
        {
            "en",
            "ko",
            "ja"
        };

        public enum Language
        {
            English,
            Korean,
            Japaness
        };

        public string TempFile { get; set; }

        public Translater()
        {
            TempFile = Path.GetTempFileName();
            using (File.Create(TempFile))
            {
                Console.WriteLine("Tempfile: " + TempFile);
            }
        }

        public string TranslateGoogle(string sourceText, Language fromLang, Language toLang)
        {
            string translation = null;
            string url = 
                $"https://translate.googleapis.com/translate_a/" +
                $"single?client=gtx&sl={LanguageStrings[(int)fromLang]}&tl={LanguageStrings[(int)toLang]}&dt=t&q={HttpUtility.UrlEncode(sourceText)}";
            string outputFile = TempFile;

            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
                wc.DownloadFile(url, outputFile);
            }

            // Get translated text
            if (File.Exists(outputFile))
            {
                string text = File.ReadAllText(outputFile);

                // Get translated phrases
                text = text.Replace("\\\"", "\\*");
                string[] phrases = text.Split(new[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; (i < phrases.Count() - 2); i += 4)
                {
                    string translatedPhrase = phrases[i];
                    if (translatedPhrase.StartsWith(",,"))
                    {
                        i--;
                        continue;
                    }
                    translation += translatedPhrase + "  ";
                }

                // Fix up translation
                translation = translation.Trim();
                translation = translation.Replace(" ?", "?");
                translation = translation.Replace(" !", "!");
                translation = translation.Replace(" ,", ",");
                translation = translation.Replace(" .", ".");
                translation = translation.Replace(" ;", ";");
                translation = translation.Replace("\\r", "\r");
                translation = translation.Replace("\\n", "\n");
                translation = translation.Replace("\\*", "\"");
            }

            return translation;
        }

        public void Dispose()
        {
            if (TempFile != null && File.Exists(TempFile))
            {
                File.Delete(TempFile);
                TempFile = null;
            }
        }
    }
}