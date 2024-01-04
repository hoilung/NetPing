using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace NetPing
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ConsoleKey q = ConsoleKey.None;
            do
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;

                Console.WriteLine("\r\n同网段ping，当前并发检测数量为：" + Environment.ProcessorCount * 2 + "，默认检测超时时间：200ms\r\n");
                var ip = args.Any() ? args[0] : string.Empty;

                IPAddress address = null;

                do
                {
                    if (string.IsNullOrEmpty(ip))
                    {
                        Console.Write("输入网段中任意一个IP：");
                        Console.ForegroundColor = ConsoleColor.Green;
                        ip = Console.ReadLine();
                    }
                    if (ip == "q")
                        break;
                } while (!IPAddress.TryParse(ip, out address));
                if (ip == "q" || address == null)
                    break;

                checkIp(address);
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;
                if(args.Length>0)
                {
                    Console.WriteLine();
                    break;
                }
                Console.WriteLine("\r\n任意键继续，q键退出");
                var k = Console.ReadKey();
                q = k.Key;                
            } while (q != ConsoleKey.Q);


        }

        private static void checkIp(IPAddress address)
        {
            var ipsub = address.ToString().Split('.');
            var ips = Enumerable.Range(1, 254).Select(m => new IPInfo { ip = $"{ipsub[0]}.{ipsub[1]}.{ipsub[2]}.{m}", num = m, state = "" }).ToList();

            ConcurrentQueue<IPInfo> queue = new ConcurrentQueue<IPInfo>();

            foreach (var p in ips)
            {
                queue.Enqueue(p);
            }


            var pings = new Ping[20];
            for (int i = 0; i < pings.Length; i++)
            {
                pings[i] = new Ping();
            }

            var st = Stopwatch.StartNew();
            st.Start();
            var d = Parallel.ForEach(pings, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, ip =>
            {
                while (queue.TryDequeue(out IPInfo kv))
                {
                    kv.state = ip.Send(kv.ip, 200).Status.ToString();
                }
            });
            st.Stop();
            Console.WriteLine("\r\n本次检测范围：" + ips.FirstOrDefault() + "-" + ips.LastOrDefault() + " 用时：" + st.ElapsedMilliseconds + "ms\r\n");

            for (int i = 0; i < ips.Count; i += 10)
            {
                var col = ips.Skip(i).Take(10).ToList();
                write(col);

            }
            write(ips.Where(m => m.state == IPStatus.Success.ToString()).ToList(), true);

        }

        public static void write(List<IPInfo> ipinfos, bool isOk = false)
        {

            if (isOk)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\r\n有效IP数量：" + ipinfos.Count() + "\r\n");
            }
            for (var i = 0; i < ipinfos.Count(); i++)
            {
                var ipinfo = ipinfos[i];
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" | ");
                if (ipinfo.state == IPStatus.Success.ToString())
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                Console.Write(ipinfo.num.ToString().PadLeft(3, ' '));
                if (isOk && i > 0 && i % 10 == 0)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(" |");
                }
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" |");
        }
    }
    public class IPInfo
    {
        public string ip;
        public int num;
        public string state;

        public override string ToString()
        {
            return ip;
        }
    }
}
