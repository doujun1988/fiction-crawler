using System;
using System.Runtime.InteropServices;

namespace WinFormsApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            ofd.Title = "选择封面图片";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = ofd.FileName;
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            var crawler = new FictionCrawler();
            var chapters = await crawler.GetChaptersAync();
            progressBar1.Maximum = chapters.Count;
            var progress = new Progress<ProgressInfo>(p =>
            {
                progressBar1.Value = p.Completed;
                label1.Text =
                    $"已完成 {p.Completed}/{p.Total},已用时间 {p.Elapsed:mm\\:ss},剩余时间 {p.Remaining:mm\\:ss}";

            });
            var completed = new Progress<ChapterResult>(p => 
            {
                string mes = $"已下载章节：{p.Index}:{p.Title}";
                listBox1.Items.Insert(0, mes);
            });
            using var ct = new CancellationTokenSource();
            await using var epub = new EpubHelper(
           "莽荒纪.epub",
           "莽荒纪",
           "我吃西红柿",
           "纪宁死后来到阴曹地府，经判官审前生判来世，投胎到了部族纪氏。这里，有夸父逐日……有后羿射金乌……更有为了逍遥长生，历三灾九劫，纵死无悔的无数修仙者…………纪宁也成为了一名修仙者，开始了他的修仙之路……");
            await epub.AddCoverAsync(textBox1.Text.Trim());
            await crawler.RunAsync(
            epub, progress,completed,
            ct.Token);
            button2.Enabled = true;
            MessageBox.Show("下载成功");
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
