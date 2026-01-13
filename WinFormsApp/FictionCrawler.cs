using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;
using HtmlAgilityPack;

namespace WinFormsApp
{
    public class FictionCrawler
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly Channel<ChapterResult> _channel;
        private string _sourceUrl = "https://www.8tsw.com";
        private string _url = "https://www.8tsw.com/115_115843/";
        private List<Chapter> _chapters;
        private DateTime startTime;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxConcurrency">并发数</param>
        public FictionCrawler(int maxConcurrency=5)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpClientHandler handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla /5.0 (Windows NT 10.0; Win64; x64)");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri(_sourceUrl);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
            _semaphoreSlim = new SemaphoreSlim(maxConcurrency);
            _channel = Channel.CreateUnbounded<ChapterResult>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }
        /// <summary>
        /// 主要方法：并发下载+Channel+EPUB写入
        /// </summary>
        /// <param name="epub"></param>
        /// <param name="progress"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task RunAsync(EpubHelper epub,IProgress<ProgressInfo>? progress=null, IProgress<ChapterResult>? chapterCompleted = null,CancellationToken ct=default)
        {
            if (_chapters.Count == 0)
            {
                return;
            }
            var total = _chapters.Count;
            var completed = 0;
            var writerTask = Task.Run(async () =>
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(ct))
                {
                    await epub.AddChapterAsync(item.Index, item.Title, item.Content);
                    completed++;
                    chapterCompleted?.Report(item);
                    progress?.Report(ProgressInfo.Creat(completed, total, startTime));
                }
            }, ct);
            var downloadTasks = _chapters.Select(chapter => DownloadChapterAsync(chapter, ct)).ToList();
            await Task.WhenAll(downloadTasks);
            _channel.Writer.Complete();
            await writerTask;
            await epub.BuildAsync();
        }

        public async Task<List<Chapter>> GetChaptersAync()
        {
            startTime = DateTime.Now;
            var byts = await _httpClient.GetByteArrayAsync(_url);
            var html = Encoding.GetEncoding("GBK").GetString(byts);
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var contentsNode = doc.DocumentNode.SelectSingleNode("//dl").ChildNodes;
            int startTag = 0;
            _chapters = new();
            int index = 1;
            foreach (var item in contentsNode)
            {
                if (item.Name == "dt")
                {
                    startTag++;
                    continue;
                }
                if (startTag == 2 && item.Name == "dd")
                {
                    _chapters.Add(
                        new Chapter
                        {
                            Index = index++,
                            Title = item.SelectSingleNode("a").InnerText.Trim(),
                            Url = _sourceUrl + item.SelectSingleNode("a").GetAttributeValue("href", string.Empty),
                        }
                        );
                }
            }
            return _chapters;
        }

        private async Task DownloadChapterAsync(Chapter chapter,CancellationToken ct) 
        {
            await _semaphoreSlim.WaitAsync(ct);
            try
            {
                var contentHtml = await GetHtmlString(chapter.Url, 3);
                if (string.IsNullOrWhiteSpace(contentHtml)) return;
                var contentDoc = new HtmlAgilityPack.HtmlDocument();
                contentDoc.LoadHtml(contentHtml);
                var title = contentDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText;
                var contentNode = contentDoc.DocumentNode.SelectSingleNode("//div[@id='content']");
                if (contentNode == null) return;
                string content = ParseContent(contentNode.InnerHtml);
                await _channel.Writer.WriteAsync(new ChapterResult { Index = chapter.Index, Title = title, Content = content }, ct);
            }
            catch (Exception)
            {
            }
            finally 
            {
                _semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// 简单清洗数据
        /// </summary>
        /// <param name="innerHtml">章节内容</param>
        /// <returns></returns>
        private string ParseContent(string innerHtml)
        {
            if (string.IsNullOrWhiteSpace(innerHtml))
            {
                return string.Empty;
            }
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(innerHtml);
            foreach (var item in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
            {
                item.Remove();
            }
            string text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
            return string.Join("\n",
                text
                    .Replace("\r", "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line
                        .Replace("　", "")   // 全角空格
                        .Replace("&nbsp;", "")
                        .Trim()              // 行首行尾
                    ));
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
