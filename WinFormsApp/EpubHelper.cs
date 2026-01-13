using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsApp
{
    public class EpubHelper:IAsyncDisposable
    {
        private readonly string _workDir;

        private readonly string _oebpsDir;

        private readonly string _textDir;

        private readonly string _imageDir;

        private readonly string _outputFile;

        private readonly List<(int Index, string Title)> _chapters = new ();
        //小说名
        private readonly string _bookTitle;
        //作者
        private readonly string _author;

        private readonly string _description;

        private string? _coverFileName;

        public EpubHelper(string outputFile,string bookTitle,string author,string description) 
        {
            _outputFile = outputFile;
            _bookTitle = bookTitle;
            _author = author;
            _description = description;
            _workDir = Path.Combine(Path.GetTempPath(),"epub_"+Guid.NewGuid());
            _oebpsDir = Path.Combine(_workDir, "OEBPS");
            _textDir = Path.Combine(_oebpsDir, "Text");
            _imageDir = Path.Combine(_oebpsDir, "Images");
            Directory.CreateDirectory(_textDir);
            Directory.CreateDirectory(_imageDir);
            Directory.CreateDirectory(Path.Combine(_workDir, "META-INF"));
            InitFixedFiles();
        }
        /// <summary>
        /// 添加封面图片
        /// </summary>
        /// <param name="coverImagePath"></param>
        /// <returns></returns>
        public async Task AddCoverAsync(string coverImagePath) 
        {
            var ext = Path.GetExtension(coverImagePath);
            _coverFileName = "cover" + ext;
            var destPath = Path.Combine(_imageDir, _coverFileName);
            await using var src = File.OpenRead(coverImagePath);
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst);
        }
        /// <summary>
        /// 添加章节内容
        /// </summary>
        /// <param name="index"></param>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task AddChapterAsync(int index, string title, string content) 
        {
            var fileName = $"chapter_{index:D4}.xhtml";
            var filePath = Path.Combine(_textDir, fileName);
            var xhtml = BuildXhtml(title, content);
            await File.WriteAllTextAsync(filePath, xhtml, Encoding.UTF8);
            _chapters.Add((index,title));
        }
        /// <summary>
        /// 生成EPUB文件
        /// </summary>
        /// <returns></returns>
        public async Task BuildAsync() 
        {
            await WriteOpfAsync();
            await WriteNavAsync();
            await WriteCoverXhtmlAsync();
            await Task.Run(() => 
            {
                if (File.Exists(_outputFile))
                {
                    File.Delete(_outputFile);
                }
                using var zip = ZipFile.Open(_outputFile,ZipArchiveMode.Create);
                zip.CreateEntryFromFile(Path.Combine(_workDir, "mimetype"), "mimetype", CompressionLevel.NoCompression);
                AddDirectoryToZip(zip,Path.Combine(_workDir,"META-INF"), "META-INF");
                AddDirectoryToZip(zip,_oebpsDir,"OEBPS");
            });
        }

        public async ValueTask DisposeAsync() 
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(_workDir))
                {
                    Directory.Delete(_workDir, true);
                }
            });
        }



        private void InitFixedFiles()
        {
            File.WriteAllText(Path.Combine(_workDir, "mimetype"), "application/epub+zip", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(_workDir, "META-INF", "container.xml"),
            """
            <?xml version="1.0"?>
            <container version="1.0"
                xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf"
                          media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);
            File.WriteAllText(Path.Combine(_oebpsDir, "style.css"),
               """
            body { line-height: 1.8; font-family: serif; }
            h1 { text-align: center; margin: 1em 0; }
            p { text-indent: 2em; margin: 0.5em 0; }
            img.cover { width: 100%; height: auto; }
            """);
        }
        private async Task WriteOpfAsync()
        {
            var mainfest = new StringBuilder();
            var spine = new StringBuilder();
            if (_coverFileName!=null)
            {
                mainfest.AppendLine($"<item id=\"cover-img\" href=\"Images/{_coverFileName}\" media-type=\"image/jpeg\" properties=\"cover-image\"/>");
                mainfest.AppendLine("<item id=\"cover\" href=\"cover.xhtml\" media-type=\"application/xhtml+xml\"/>");

                spine.AppendLine("<itemref idref=\"cover\" linear=\"no\"/>");
            }
            foreach (var c in _chapters.OrderBy(c=>c.Index))
            {
                mainfest.AppendLine($"<item id=\"c{c.Index}\" href=\"Text/chapter_{c.Index:D4}.xhtml\" media-type=\"application/xhtml+xml\"/>");
                spine.AppendLine($"<itemref idref=\"c{c.Index}\"/>");
            }
            var opf = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf"
                     version="3.0"
                     unique-identifier="bookid">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>{_bookTitle}</dc:title>
                <dc:creator>{_author}</dc:creator>
                <dc:language>zh-CN</dc:language>
                <dc:description>{_description}</dc:description>
                <dc:identifier id="bookid">urn:uuid:{Guid.NewGuid()}</dc:identifier>
              </metadata>
              <manifest>
                <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                <item id="css" href="style.css" media-type="text/css"/>
                {mainfest}
              </manifest>
              <spine>
                {spine}
              </spine>
            </package>
            """;
            await File.WriteAllTextAsync(
           Path.Combine(_oebpsDir, "content.opf"),
           opf,
           Encoding.UTF8);

        }

        private async Task WriteNavAsync()
        {
            var sb = new StringBuilder();
            foreach (var c in _chapters.OrderBy(c => c.Index))
            {
                sb.AppendLine(
                    $"<li><a href=\"Text/chapter_{c.Index:D4}.xhtml\">{c.Title}</a></li>");
            }
            var nav = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head><title>目录</title></head>
            <body>
              <nav epub:type="toc">
                <ol>
                  {sb}
                </ol>
              </nav>
            </body>
            </html>
            """;
            await File.WriteAllTextAsync(
                Path.Combine(_oebpsDir, "nav.xhtml"),
                nav,
                Encoding.UTF8);
        }
        private async Task WriteCoverXhtmlAsync()
        {
            if (_coverFileName == null)
                return;
            var cover = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head>
                <title>封面</title>
                <link rel="stylesheet" href="style.css"/>
            </head>
            <body>
                <img src="Images/{_coverFileName}" class="cover" />
            </body>
            </html>
            """;
            await File.WriteAllTextAsync(
                Path.Combine(_oebpsDir, "cover.xhtml"),
                cover,
                Encoding.UTF8);
        }
        private static void AddDirectoryToZip(ZipArchive zip,string sourceDir,string entryRoot)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var entryName = Path.Combine(
                    entryRoot,
                    Path.GetRelativePath(sourceDir, file))
                    .Replace("\\", "/");
                zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        }

        /// <summary>
        /// 获取章节内容的XHTML
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        private static string BuildXhtml(string title, string content)
        {
            var paragraphs = content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => $"<p>{System.Net.WebUtility.HtmlEncode(p)}</p>");
            return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head>
                <title>{title}</title>
                <link rel="stylesheet" href="../style.css"/>
            </head>
            <body>
                <h1>{title}</h1>
                {string.Join("\n", paragraphs)}
            </body>
            </html>
            """;
        }
    }
}
