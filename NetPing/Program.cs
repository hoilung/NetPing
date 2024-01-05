using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

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

                Console.WriteLine("\r\n同网段ping，当前并发检测数量为：" + Environment.ProcessorCount + "，默认检测超时时间：200ms\r\n");
                var ip = args.Any() ? args[0] : string.Empty;
                var port = (args.Length == 2 && Regex.IsMatch(args[1], @"^\d+$")) ? int.Parse(args[1]) : 0;

                IPAddress address = null;

                do
                {
                    if (string.IsNullOrEmpty(ip))
                    {
                        Console.Write("输入网段中任意一个IP：");
                        Console.ForegroundColor = ConsoleColor.Green;
                        ip = Console.ReadLine();
                        Console.Write("是否检测指定端口号? (回车忽略继续，否则请输入端口号)：");
                        var ischecktcp = Console.ReadLine();
                        if (Regex.IsMatch(ischecktcp, @"^\d+$") && int.Parse(ischecktcp) is int a && a > 0 && a < 65535)
                        {
                            port = a;
                        }
                    }
                    if (ip == "q")
                        break;
                } while (!IPAddress.TryParse(ip, out address));
                if (ip == "q" || address == null)
                    break;

                if (port > 0)
                    checkTcpIp(address, port);
                else
                    checkIp(address);
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;
                if (args.Length > 0)
                {
                    Console.WriteLine();
                    break;
                }
                Console.WriteLine("\r\n任意键继续，q键退出");
                var k = Console.ReadKey();
                q = k.Key;
            } while (q != ConsoleKey.Q);


        }
        private static void checkTcpIp(IPAddress address, int port)
        {
            var ipsub = address.ToString().Split('.');
            var ips = Enumerable.Range(1, 254).Select(m => new IPInfo { ip = $"{ipsub[0]}.{ipsub[1]}.{ipsub[2]}.{m}", num = m, state = "" }).ToList();
            ConcurrentQueue<IPInfo> queue = new ConcurrentQueue<IPInfo>();
            foreach (var p in ips)
            {
                queue.Enqueue(p);
            }
            var tcpClients = new Socket[Environment.ProcessorCount];

            for (int i = 0; i < tcpClients.Length; i++)
            {
                //tcpClients[i] = new TcpClient();
                tcpClients[i] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tcpClients[i].ReceiveTimeout = 200;
                tcpClients[i].SendTimeout = 200;
            }
            var st = Stopwatch.StartNew();
            st.Start();
            var d = Parallel.ForEach(tcpClients, /*new ParallelOptions { MaxDegreeOfParallelism = 1 },*/ tcpclient =>
            {
                CancellationTokenSource cancellationToken = new CancellationTokenSource(200);
                while (queue.TryDequeue(out IPInfo kv))
                {
                    try
                    {
                        tcpclient.ConnectAsync(kv.ip, port).Wait(200);
                        kv.state = tcpclient.Connected ? IPStatus.Success.ToString() : IPStatus.Unknown.ToString();
                        //tcpclient.Send(Encoding.UTF8.GetBytes("Ping"));
                        tcpclient.Shutdown(SocketShutdown.Both);
                        tcpclient.Disconnect(false);
                    }
                    catch (Exception ex)
                    {
                        kv.state = IPStatus.Unknown.ToString();
                    }
                    finally
                    {
                        Console.WriteLine($"TCP\t{kv.ip}:{port} {kv.state}");
                        tcpclient.Close();
                        tcpclient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    }
                }
            });
            st.Stop();
            Console.WriteLine("\r\n本次TCP检测IP范围：" + ips.FirstOrDefault() + " 到 " + ips.LastOrDefault() + ", TCP端口：" + port + ", 用时：" + st.ElapsedMilliseconds + "ms\r\n");

            for (int i = 0; i < ips.Count; i += 10)
            {
                var col = ips.Skip(i).Take(10).ToList();
                write(col);

            }
            write(ips.Where(m => m.state == IPStatus.Success.ToString()).ToList(), true);
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


            var pings = new Ping[Environment.ProcessorCount];
            for (int i = 0; i < pings.Length; i++)
            {
                pings[i] = new Ping();
            }

            var st = Stopwatch.StartNew();
            st.Start();
            var d = Parallel.ForEach(pings, ip =>
            {
                while (queue.TryDequeue(out IPInfo kv))
                {
                    kv.state = ip.Send(kv.ip, 200).Status.ToString();
                    Console.WriteLine($"IMCP\t{kv.ip} {kv.state}");
                }
            });
            st.Stop();
            Console.WriteLine("\r\n本次IMCP检测IP范围：" + ips.FirstOrDefault() + "-" + ips.LastOrDefault() + " 用时：" + st.ElapsedMilliseconds + "ms\r\n");

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
                if (isOk && i > 0 && (i + 1) % 10 == 0)
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
