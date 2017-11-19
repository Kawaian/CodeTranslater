using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace CodeTranslater
{
    class Program : IDisposable
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (Program p = new Program())
            {
                p.Run();
            }
            Console.ReadLine();
        }

        public string[] SearchExt = new string[] { ".cs" };

        List<Translater> Translaters = new List<Translater>();
        Translater.Language From = Translater.Language.Japaness;
        Translater.Language To = Translater.Language.English;

        public Program()
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                Translaters.Add(new Translater());
            }
        }

        public void Run()
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                DirectoryInfo di = new DirectoryInfo(fbd.SelectedPath);
                Console.Title = $"Translate \"{di.FullName}\" : {From} to {To}";
                TranslateDir(di);
                Console.WriteLine("Translate Finished...");
            }
        }

        private bool CheckExt(string text)
        {
            foreach (var item in SearchExt)
            {
                if (text.ToLower().EndsWith(item))
                    return true;
            }
            return false;
        }

        private void TranslateDir(DirectoryInfo di, int maxDepth = 100)
        {
            Console.WriteLine($"Search: {di.FullName}");

            var fis = di.GetFiles();
            for (int i = 0; i < fis.Length; i++)
            {
                var fi = fis[i];

                if (CheckExt(fi.FullName))
                {
                    TranslateFile(fi);
                }
            }

            var dis = di.GetDirectories();
            var nextDepth = maxDepth--;
            foreach (var nextDi in dis)
            {
                TranslateDir(nextDi, nextDepth);
            }
        }

        private void TranslateFile(FileInfo fi)
        {
            Console.WriteLine($"Translating: {fi.FullName}");

            // Regex rgx = new Regex("(/\\*([^*]|[\\r\\n]|(\\*+([^*/]|[\\r\\n])))*\\*+/)|(//.*)|([\"'](?:(?<=\")[^\"\\\\]*(?s:\\\\.[^\"\\\\]*)*\"))", RegexOptions.None);
            Regex rgx = new Regex("((?<=\\/\\*)(.|[\\r\\n])*?(?=\\*\\/))|((?<=\\/\\/).*)|((?<=\\bException\\(\")(.|[\\r\\n])*?(?=\"\\)))");

            Regex engRgx = new Regex("^[ -~]*$");

            var content = File.ReadAllText(fi.FullName);
            var contentLocker = new object();

            int ind = 0;
            object indLocker = new object();

            var collection = rgx.Matches(content);
            var parallelCount = Environment.ProcessorCount;
            Parallel.For(0, collection.Count, new ParallelOptions() { MaxDegreeOfParallelism = parallelCount }, (i) =>
            {
                var item = collection[i];
                int getInd;
                lock (indLocker)
                {
                    ind++;
                    getInd = ind;
                    Console.CursorLeft = 0;
                    Console.Write(ProgressBar(fi.Name, (double)(ind) / collection.Count));
                }
                var trans = Translaters[ind % parallelCount];
                lock (trans)
                {
                    var target = item.Groups[0];
                    var originContent = target.Value;
                    var targetContent = target.Value;
                    if (targetContent.Length > 0 && !targetContent.ToLower().StartsWith("\"http") && !engRgx.IsMatch(targetContent))
                    {
                        // var isComment = false;
                        // var commentPrefix = "";
                        // if (targetContent.TrimStart().StartsWith("///"))
                        // {
                        //     isComment = true;
                        //     commentPrefix = "///";
                        // }
                        // else if (targetContent.TrimStart().StartsWith("//"))
                        // {
                        //     isComment = true;
                        //     commentPrefix = "//";
                        // }
                           
                        // if (isComment)
                        // {
                        //     originContent = originContent.TrimStart();
                        //     targetContent = originContent.Replace(commentPrefix, "");
                        // }

                        targetContent = TryTranslate(trans, targetContent, From, To);
                        if (targetContent == null)
                        {
                            targetContent = originContent;
                        }
                        else
                        {
                            targetContent = targetContent.Replace("/ / / /", "////");
                            targetContent = targetContent.Replace("/ / /", "///");
                            targetContent = targetContent.Replace("/ /", "//");
                            targetContent = targetContent.Replace("/ *", "/*");
                            targetContent = targetContent.Replace("* /", "*/");
                            targetContent = targetContent.Replace("\\u003c", "<");
                            targetContent = targetContent.Replace("\\U003C", "<");
                            targetContent = targetContent.Replace("\\u003e", ">");
                            targetContent = targetContent.Replace("\\U003E", ">");
                            targetContent = targetContent.Replace("\\\\ \"", "\\\\\"");
                            targetContent = targetContent.Replace("\\\\\"", "\\\"");
                            targetContent = targetContent.Replace("/ >", "/>");
                            targetContent = targetContent.Replace(" />", "/>");
                            targetContent = targetContent.Replace("< /", "</");
                            targetContent = targetContent.Replace("</ ", "</");

                            //if (isComment)
                            //{
                            //    targetContent = commentPrefix + targetContent;
                            //}
                        }

                        lock (contentLocker)
                        {
                            content = content.Replace(originContent, targetContent);
                        }
                    }
                }
            });
            Console.WriteLine();

            //Console.WriteLine(content);
            File.WriteAllText(fi.FullName, content);

            Console.WriteLine($"Translated: {fi.FullName}");
        }

        private string TryTranslate(Translater trans, string content, Translater.Language from, Translater.Language to, int maxRetry = 5)
        {
            if (maxRetry == 0)
                return null;

            try
            {
                //Console.WriteLine($"Requested: {content}");
                var translated = trans.TranslateGoogle(content, from, to);
                //Console.WriteLine($"Get: {translated}");
                return translated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error (Retry{5 - maxRetry}): {ex.ToString()}");
                Thread.Sleep(250);
                return TryTranslate(trans, content, from, to, maxRetry--);
            }
        }

        private string ProgressBar(string tag, double percent)
        {
            var ret = $"{tag} | [";
            var width = 50;
            for (int i = 0; i < Math.Round(percent * width); i++)
            {
                ret += "=";
            }
            for (int i = 0; i < Math.Round((1 - percent) * width); i++)
            {
                ret += "-";
            }
            ret += $"] ({(percent * 100).ToString("0.00")}%)";
            return ret;
        }

        public void Dispose()
        {
            if (Translaters != null)
            {
                foreach (var item in Translaters)
                {
                    item.Dispose();
                }
                Translaters.Clear();
                Translaters = null;
            }
        }
    }
}
