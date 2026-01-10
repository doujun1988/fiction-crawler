using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace ConsoleApp
{
    public class FictionCrawler
    {
        private readonly HttpClient _httpClient;
        private string sourceUrl = "https://www.8tsw.com";
        private string url = "https://www.8tsw.com/115_115843/";
        private string fileName = "莽荒纪.txt";

        public FictionCrawler()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpClientHandler handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
        }
        public async Task RunAsync()
        {
            DateTime dtStart = DateTime.Now;

            #region 初始化_httpClient

            _httpClient.Timeout = TimeSpan.FromSeconds(120);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla /5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(sourceUrl);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
            #endregion

            #region 获取章节列表
            var byts = await _httpClient.GetByteArrayAsync(url);
            var html = Encoding.GetEncoding("GBK").GetString(byts);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var contentsNode = doc.DocumentNode.SelectSingleNode("//dl").ChildNodes;
            int startTag = 0;
            List<string> alist = new List<string>();
            foreach (var item in contentsNode)
            {
                if (item.Name == "dt")
                {
                    startTag++;
                    continue;
                }
                if (startTag == 2 && item.Name == "dd")
                {
                    alist.Add(sourceUrl + item.SelectSingleNode("a").GetAttributeValue("href", string.Empty));
                }
            }
            if (alist.Count == 0)
            {
                Console.WriteLine("尚未读取到目录");
                return;
            }
            #endregion

            #region 下载内容
            if (File.Exists(fileName))
            {
                Console.WriteLine($"删除旧数据:{fileName}");
                File.Delete(fileName);
                Thread.Sleep(500);
            }

            foreach (var item in alist)
            {
                var contentHtml = await GetHtmlString(item, 3);
                if (string.IsNullOrWhiteSpace(contentHtml)) continue;
                var contentDoc = new HtmlDocument();
                contentDoc.LoadHtml(contentHtml);
                var title = contentDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText;
                var contentNode = contentDoc.DocumentNode.SelectSingleNode("//div[@id='content']");
                if (contentNode == null) continue;
                string content = GetContent(contentNode.InnerHtml);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content)) continue;
                File.AppendAllText("莽荒纪.txt", title + Environment.NewLine + content + Environment.NewLine + Environment.NewLine);
                Console.WriteLine($"【{title}】已下载");
                await Task.Delay(300);
            }
            #endregion

            DateTime dtEnd = DateTime.Now;
            TimeSpan diff = dtEnd - dtStart;
            Console.WriteLine($"下载完成,总耗时{diff.Minutes}分{diff.Seconds}秒");
        }
        /// <summary>
        /// 简单清洗数据
        /// </summary>
        /// <param name="innerHtml">章节内容</param>
        /// <returns></returns>
        private string GetContent(string innerHtml)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(innerHtml);
            foreach (var item in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
            {
                item.Remove();
            }
            string text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
            return text.Replace("\r", "").Replace("\n\n", "\n").Trim();
        }
        /// <summary>
        /// 获取HTML字符串
        /// </summary>
        /// <param name="url">章节URL</param>
        /// <param name="times">重连尝试次数</param>
        /// <returns></returns>
        private async Task<string> GetHtmlString(string url, int times)
        {
            for (int i = 0; i < times; i++)
            {
                try
                {
                    var byts = await _httpClient.GetByteArrayAsync(url);
                    var html = Encoding.GetEncoding("GBK").GetString(byts);
                    return html;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine(ex.Message + $",正在重新读取{url}中的HTML数据");
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return string.Empty;
                }
            }
            return string.Empty;
        }


    }
}
