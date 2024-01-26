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
        private static ushort maxTimeout = 500;//ms
        static void Main(string[] args)
        {

            ConsoleKey q = ConsoleKey.None;
            do
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;
                Console.Title = "NetPing v1.0.3.24026 Author:hoilung@foxmail.com";
                Console.WriteLine("\r\n用于同网段批量ICMP或TCP端口检测，当前并发检测数量为：" + Environment.ProcessorCount + "，默认检测超时时间：" + maxTimeout + "ms\r\n");
                var ip = args.Any() ? args[0] : string.Empty;
                var port = (args.Length > 1 && Regex.IsMatch(args[1], @"^\d+$")) ? int.Parse(args[1]) : 0;

                var t_ms = (args.Length > 2 && Regex.IsMatch(args[2], @"^\d+$")) ? ushort.Parse(args[2]) : maxTimeout;
                if (t_ms > 200 && t_ms < 5000)
                {
                    maxTimeout = t_ms;
                }

                IPAddress address = null;
                do
                {
                    if (string.IsNullOrEmpty(ip))
                    {
                        var list = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(m => m.AddressFamily == AddressFamily.InterNetwork).ToList();
                        if (list.Count > 0)
                        {
                            Console.Write("本机IP：");
                            foreach (var item in list)
                            {
                                Console.Write(item.ToString() + "\t");
                            }
                            Console.WriteLine(Environment.NewLine);
                        }

                        Console.Write("请输入IPv4网段中任意一个IP：");
                        Console.ForegroundColor = ConsoleColor.Green;
                        ip = Console.ReadLine();
                        Console.Write("是否以检测指定端口号? (回车忽略继续，否则请输入端口号)：");
                        var ischecktcp = Console.ReadLine();
                        if (Regex.IsMatch(ischecktcp, @"^\d+$") && int.Parse(ischecktcp) is int a && a > 0 && a < 65535)
                        {
                            port = a;
                        }

                        Console.Write($"默认连接超时为：{maxTimeout}ms 是否修改? (回车忽略继续，否则请输入超时毫秒时间)：");
                        var time_ms = Console.ReadLine();
                        if (Regex.IsMatch(time_ms, @"^\d+$") && ushort.Parse(time_ms) is ushort t && t > 200 && t < 5000)
                        {
                            maxTimeout = t;
                        }
                    }
                    if (ip == "q" || !IPAddress.TryParse(ip, out address))
                        break;
                } while (!IPAddress.TryParse(ip, out address));
                if (ip == "q")
                    break;
                if (address == null)
                {
                    Console.WriteLine("无效的IP地址");
                    break;
                }

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
            var ips = Enumerable.Range(0, 255).Select(m => new IPInfo { ip = $"{ipsub[0]}.{ipsub[1]}.{ipsub[2]}.{m}", num = m, state = "" }).ToList();
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
                //tcpClients[i].ReceiveTimeout = 200;
                // tcpClients[i].SendTimeout = 200;
            }
            var st = Stopwatch.StartNew();
            st.Start();
            var d = Parallel.ForEach(tcpClients,/* new ParallelOptions { MaxDegreeOfParallelism = 1 },*/ tcpclient =>
            {
                //CancellationTokenSource cancellationToken = new CancellationTokenSource(200);
                var st2 = Stopwatch.StartNew();
                while (queue.TryDequeue(out IPInfo kv))
                {
                    try
                    {
                        st2.Restart();
                        //tcpclient.ConnectAsync(kv.ip, port).Wait(200);
                        var result = tcpclient.BeginConnect(kv.ip, port, null, null);
                        if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(maxTimeout)))
                        {
                            kv.state = tcpclient.Connected ? IPStatus.Success.ToString() : IPStatus.TimedOut.ToString();
                        }
                        else
                        {
                            kv.state = tcpclient.Connected ? IPStatus.Success.ToString() : IPStatus.TimedOut.ToString();
                            tcpclient.EndConnect(result);
                            tcpclient.Shutdown(SocketShutdown.Both);
                            tcpclient.Disconnect(false);
                        }
                        st2.Stop();
                    }
                    catch (Exception ex)
                    {
                        kv.state = IPStatus.Unknown.ToString();
                    }
                    finally
                    {
                        if (kv.state == "Success")
                        {                            
                            Console.WriteLine($"TCP\t{kv.ip}:{port} {kv.state} {st2.ElapsedMilliseconds}ms");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"TCP\t{kv.ip}:{port} {kv.state} ");
                        }
                        Console.ForegroundColor = ConsoleColor.Green;
                        tcpclient.Close();
                        tcpclient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    }
                }
            });
            st.Stop();
            Console.WriteLine("\r\n本次TCP检测IP范围：" + ips.FirstOrDefault() + "-" + ips.LastOrDefault() + ", TCP端口：" + port + ",最大超时时间：" + maxTimeout + "ms,本次检测用时：" + st.ElapsedMilliseconds + "ms\r\n");

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
                var stn = Stopwatch.StartNew();
                while (queue.TryDequeue(out IPInfo kv))
                {
                    stn.Restart();
                    kv.state = ip.Send(kv.ip, maxTimeout).Status.ToString();
                    stn.Stop();
                    if (kv.state == "Success")
                    {                        
                        Console.WriteLine($"IMCP\t{kv.ip} {kv.state} {stn.ElapsedMilliseconds}ms");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"IMCP\t{kv.ip} {kv.state} ");
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                }
            });
            st.Stop();
            Console.WriteLine("\r\n本次IMCP检测IP范围：" + ips.FirstOrDefault() + "-" + ips.LastOrDefault() + ",最大超时时间：" + maxTimeout + "ms,本次检测用时：" + st.ElapsedMilliseconds + "ms\r\n");

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
