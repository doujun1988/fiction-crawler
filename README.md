# C#小说爬虫

简要描述你的项目用途和功能。例如：

> 这是一个用 C# 开发的小说爬虫程序，可以抓取《莽荒纪》的章节并生成 TXT 文件。

---

## 功能

- 自动抓取《莽荒纪》全部章节
- 处理 403 / 编码 / 乱码问题
- 抓取失败自动重试
- 日志输出
- 段落简单清洗
- async/await异步执行

---

## 安装

1. 克隆仓库：

```bash
git clone https://github.com/doujun1988/fiction-crawler.git
```

2. 用 Visual Studio 2022 打开解决方案文件 `.sln`
3. 编译并运行项目

---

## 使用方法

1. 打开解决方案，进入 `FictionCrawler` 类
2. 配置目标小说 URL 和输出文件名
3. 运行程序，程序会自动抓取章节并生成文件

---

## 文件说明

- `Program.cs` ：程序入口，调用 `FictionCrawler`
- `FictionCrawler.cs` ：爬取小说核心逻辑
- `*.txt` ：输出的小说文本文件

---

## 注意事项

- 确保网络通畅，否则抓取可能失败
- 使用时注意版权和合法性
- 第一次运行程序时可能需要等待较长时间，程序支持断点续爬

---

## 许可

说明项目的开源许可，例如 MIT 许可：

```
MIT License
```

---

## 联系方式

- 邮箱：doujun1988@gmail.com

- 手机：18652724369

- 微信：Enjoycodingandlove

