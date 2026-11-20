// Spike: find which USB receiver carries the Air 50 keyboard and log its input reports.
// Read-only: lists HID interfaces and records incoming reports for a fixed time while you type.
using System;
using System.Linq;
using HidSharp;

int[][] candidates = { new[] { 0x0C45, 0xFDFD }, new[] { 0x25A7, 0xFA70 } };
int seconds = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 15;

var devices = DeviceList.Local.GetHidDevices()
    .Where(d => candidates.Any(c => d.VendorID == c[0] && d.ProductID == c[1]))
    .ToList();

Console.WriteLine($"Interfaces found: {devices.Count}");
foreach (var d in devices)
{
    Console.WriteLine($"\n== VID {d.VendorID:X4} PID {d.ProductID:X4}  {d.GetProductName()}");
    Console.WriteLine($"   in={d.GetMaxInputReportLength()} out={d.GetMaxOutputReportLength()} feat={d.GetMaxFeatureReportLength()}");
    Console.WriteLine($"   path={d.DevicePath}");
}

// Listen to every interface at the same time so one window covers all of them.
var threads = new List<System.Threading.Thread>();
var output = new object();
var end = DateTime.UtcNow.AddSeconds(seconds);

foreach (var d in devices)
{
    if (!d.TryOpen(out var stream))
    {
        continue;
    }

    string tag = d.DevicePath.Length > 60 ? d.DevicePath.Substring(d.DevicePath.Length - 60) : d.DevicePath;
    var thread = new System.Threading.Thread(() =>
    {
        using (stream)
        {
            var buffer = new byte[Math.Max(d.GetMaxInputReportLength(), 8)];
            stream.ReadTimeout = 500;
            int count = 0;
            while (DateTime.UtcNow < end)
            {
                try
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) continue;
                    count++;
                    lock (output)
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {d.VendorID:X4}:{d.ProductID:X4} [{read}] {Convert.ToHexString(buffer, 0, read)}  <{tag}>");
                }
                catch (TimeoutException) { }
                catch (System.IO.IOException) { break; }
            }
            lock (output)
                Console.WriteLine($"-- interface done, reports={count}: {tag}");
        }
    });
    thread.Start();
    threads.Add(thread);
}

Console.WriteLine($"Listening {seconds}s on {threads.Count} open interfaces...");
foreach (var t in threads) t.Join();

Console.WriteLine("\nDone.");
