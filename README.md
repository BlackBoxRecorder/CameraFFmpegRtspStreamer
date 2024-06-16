介绍一种使用FFmpeg将摄像头视频推流到RTSP服务器的方法，使用 OpenCVSharp 从摄像头读取视频帧数据并写入到控制台标准输出，使用 Process 启动 ffmpeg，ffmpeg 从控制台标准输入读取图像数据，推送到 RTSP 服务器。

**在开始之前需要：**

- 下载 FFmpeg 并添加到 Path 环境变量；不想添加环境变量需要自己在程序中指定FFmpeg的路径
- 下载 [MediaMTX](https://github.com/bluenviron/mediamtx) ，一个 RTSP 服务器，运行起来
- ffplay 播放 RTSP 视频流

运行 `CameraFFmpegRtspStreamer` 这个程序，会自动打开笔记本的摄像头，并推流到RTSP服务器。

**代码：**

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using OpenCvSharp;

namespace CameraFFmpegRtspStreamer
{
    static class Program
    {
        static bool isRunning = true;

        static readonly string rtspServer = "rtsp://localhost:8554/camera";
        static readonly VideoCapture cap = new();

        static async Task Main(string[] args)
        {
            // ffmpeg 命令行，使用 -i - 表示从标准输入读取
            string ffmpegCommand =
                $"-s 640x480 -r 30 -i - -c:v libx264 -preset veryfast -tune zerolatency -f rtsp {rtspServer}";

            Console.WriteLine("正在打开相机，请等待...");

            if (!cap.Open(0))
            {
                Console.WriteLine("相机未打开");
                return;
            }

            Console.WriteLine($"相机已打开，FPS:{cap.Fps}");

            // 启动 ffmpeg 进程
            using Process process = new();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = ffmpegCommand;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            var task = Task.Run(async () =>
            {
                // 获取标准输入流，用于写入图片数据
                using StreamWriter stdin = process.StandardInput;
                while (isRunning)
                {
                    await Task.Delay(1);

                    var mat = new Mat();
                    bool suc = cap.Read(mat);

                    if (!suc)
                    {
                        Console.WriteLine("视频结束");
                        break;
                    }

                    var point = new Point(20, 20);
                    Cv2.PutText(
                        mat,
                        DateTime.Now.ToString(),
                        point,
                        HersheyFonts.HersheyPlain,
                        1,
                        Scalar.BlueViolet
                    );

                    var imgBytes = mat.ToBytes();

                    try
                    { // 将图片数据写入标准输入
                        await stdin.BaseStream.WriteAsync(imgBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            });

            Console.WriteLine($"使用 ffplay.exe 播放RTSP流：ffplay.exe {rtspServer}");
            Console.WriteLine("按 Q 退出");

            var key = Console.ReadKey().Key;
            while (key != ConsoleKey.Q)
            {
                key = Console.ReadKey().Key;
            }

            isRunning = false;
            await task;
            Console.WriteLine("Task 退出，程序结束");

            cap.Release();
            cap.Dispose();

            process.Kill();
        }
    }
}

```

