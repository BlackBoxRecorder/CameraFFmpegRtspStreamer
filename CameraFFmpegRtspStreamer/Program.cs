using System.Collections.Concurrent;
using System.Diagnostics;
using OpenCvSharp;

namespace CameraFFmpegRtspStreamer
{
    static class Program
    {
        static ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();

        static CancellationTokenSource cts = new CancellationTokenSource();
        static CancellationToken token = cts.Token;

        static void Main(string[] args)
        {
            // ffmpeg 命令行，使用 -i - 表示从标准输入读取
            string ffmpegCommand =
                "-s 640x480 -r 30 -i - -c:v libx264 -preset veryfast -tune zerolatency -f rtsp rtsp://localhost:8554/camera";

            VideoCapture cap = new VideoCapture();
            if (!cap.Open(0))
            {
                Console.WriteLine("相机未打开");
                return;
            }
            Console.WriteLine($"FPS:{cap.Fps}");

            Task.Run(() =>
            {
                while (true)
                {
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
                    queue.Enqueue(imgBytes);
                    Thread.Sleep(5);
                }
            });

            // 启动 ffmpeg 进程
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = ffmpegCommand;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                var sw = new Stopwatch();
                // 获取标准输入流，用于写入图片数据
                using (StreamWriter stdin = process.StandardInput)
                {
                    while (!token.IsCancellationRequested)
                    {
                        Thread.Sleep(1);

                        if (queue.TryDequeue(out var imgBytes))
                        {
                            // 将图片数据写入标准输入
                            Task.Run(() =>
                            {
                                stdin.BaseStream.Write(imgBytes, 0, imgBytes.Length);
                            });
                            Thread.Sleep(1);
                        }
                    }
                }

                // 等待进程退出
                process.WaitForExit();

                // 读取标准输出和错误输出
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                // 输出信息
                Console.WriteLine(output);
                Console.WriteLine(error);
            }
        }
    }
}
