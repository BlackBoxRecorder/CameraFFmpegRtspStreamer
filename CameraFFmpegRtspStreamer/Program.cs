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
