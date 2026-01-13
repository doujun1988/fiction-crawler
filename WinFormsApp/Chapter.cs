using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsApp
{
    /// <summary>
    /// 章节元数据
    /// </summary>
    public class Chapter
    {
        public int Index { get; init; }
        public string Title { get; init; } = string.Empty;

        public string Url { get; init; } = string.Empty;
    }
    /// <summary>
    /// 下载完成后的结果
    /// </summary>
    public class ChapterResult
    {
        public int Index { get; init; }
        public string Title { get; init; } = "";
        public string Content { get; init; } = "";
    }

    /// <summary>
    /// 进度&剩余时间
    /// </summary>
    /// <param name="Completed"></param>
    /// <param name="Total"></param>
    /// <param name="Elapsed"></param>
    /// <param name="Remaining"></param>
    public record ProgressInfo(int Completed,int Total,TimeSpan Elapsed,TimeSpan Remaining) 
    {
        public static ProgressInfo Creat(int completed, int total, DateTime start) 
        {
            var elapsed = DateTime.Now - start;
            var avg = elapsed.TotalSeconds / Math.Max(1, completed);
            var remaining = TimeSpan.FromSeconds(avg*(total-completed));
            
            return new ProgressInfo(completed, total, elapsed, remaining);
        }
    }
}
