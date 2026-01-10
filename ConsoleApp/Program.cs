using System.Net;
using System.Text;
using HtmlAgilityPack;
namespace ConsoleApp
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            await new FictionCrawler().RunAsync();
        }
    }
    
}
