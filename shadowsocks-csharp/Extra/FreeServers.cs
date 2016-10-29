using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Shadowsocks.Extra
{
    public class FreeServers
    {
        public interface HttpGetCallback { void onGet(HttpWebResponse response); }
        private ShadowsocksController controller;

        public FreeServers(ShadowsocksController controller)
        {
            this.controller = controller;
            Model.Server server = new Model.Server();
        }
        /// <summary>
        /// GET请求与获取结果
        /// </summary>
        public static void HttpGet(string Url, string postDataStr, Action<Stream> callback)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url + (string.IsNullOrEmpty(postDataStr) ? "" : "?") + postDataStr);
            request.Method = "GET";
            //request.ContentType = "text/html;charset=UTF-8";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = response.GetResponseStream()) {
                    callback.Invoke(stream);
                    stream.Close();
                }
                response.Close();
            }
        }
        private void updateServerInfo(Server oldServer,Server newServer)
        {
            oldServer.server_port = newServer.server_port;
            oldServer.password = newServer.password;
            oldServer.method = newServer.method;
        }
        public void updateFreeServers()
        {
            try
            {
                this.updateFreeServers0();
            }
            catch (Exception e)
            {
                Logging.Error("An exception while getting free servers -> " + e);
            }
        }
        public void updateFreeServers0()
        {
            Server[][] newServers = new Server[2][];
            //Task[] tasks = new Task[1];
            Task task;
            //for (int i = 0; i < tasks.Length; i++)
            task = Task.Factory.StartNew(() =>
            {
                try
                {
                    newServers[0] = this.getFreeServer1();
                    newServers[1] = this.getFreeServer2();
                }
                catch (Exception e)
                {
                    Logging.Error("Free Server Err \n" + e);
                }
            });

            Logging.Info("Get Free Server Info Finished.");
            //while (!Task.WaitAll(tasks, 500)) // 一直等待线程完成任务
            while (task.Status != TaskStatus.RanToCompletion)
                System.Windows.Forms.Application.DoEvents();

            List<Server> currentServers = this.controller.GetConfigurationCopy().configs;
            Debug.WriteLine("Before Add Free Server Count -> " + currentServers.Count);
            //foreach (Server currentServer in currentServers) // 修改已存在的服务器信息
            for (int i = currentServers.Count - 1; i >= 0; i--)
            {
                Server currentServer = currentServers[i];
                foreach (Server[] eachNewServers in newServers)
                    foreach (Server newServer in eachNewServers)
                        if (string.IsNullOrEmpty(newServer.server)) // 标记已经添加的跳过
                            continue;
                        else if (newServer.server.Equals(currentServer.server, StringComparison.OrdinalIgnoreCase)) // 不区分大小写
                        {
                            Logging.Debug("Update Server Info -> " + currentServer.server);
                            this.updateServerInfo(currentServer, newServer);
                            newServer.server = null; // 标记不需要添加
                            continue;
                        }
                        else if (string.IsNullOrEmpty(currentServer.server)) // 移除默认空服务器
                        {
                            Debug.WriteLine("Remove Empty Server Index-> " + i);
                            currentServers.Remove(currentServer);
                        }else
                        {
                            Debug.WriteLine("Unknow Current Host -> '" + currentServer.server + "'");
                        }
            }

            foreach (Server[] eachNewServers in newServers)
                foreach (Server newServer in eachNewServers)
                    if (!string.IsNullOrEmpty(newServer.server)) // 添加新的服务器                    
                        currentServers.Add(newServer);

            Debug.WriteLine("After Add Free Server Count -> " + currentServers.Count);
            this.controller.SaveServers(currentServers, this.controller.GetConfigurationCopy().localPort); // 保存信息
        }
        protected Server[] getFreeServer1()
        {
            string[] urls = new string[] { @"http://www.shadowsocks8.net/images/server01.png"
                                        ,@"http://www.shadowsocks8.net/images/server02.png"
                                        ,@"http://www.shadowsocks8.net/images/server03.png"};
            Server[] u = new Server[urls.Length];
            for (int i = 0; i < urls.Length; i++)
            {
                string str = urls[i];
                HttpGet(str, Convert.ToString(Environment.TickCount), (responseStream) =>
                {
                    using (Bitmap target = new Bitmap(responseStream)) // 随机数
                    {
                        var source = new BitmapLuminanceSource(target);
                        var bitmap = new BinaryBitmap(new HybridBinarizer(source));
                        QRCodeReader reader = new QRCodeReader();
                        var result = reader.decode(bitmap);
                        if (result != null)
                            u[i] = new Server(result.Text);
                        else
                            Debug.WriteLine("Decode QR Code Err");
                    }
                });
            }
            return u;
        }

        protected Server[] getFreeServer2()
        {
            const string pattern = @"<h4>.服务器地址:([a-zA-Z0-9.]*?)</h4>[\d\D]*?<h4>端口:(\d*?)</h4>[\d\D]*?<h4>.密码:(.*?)</h4>[\d\D]*?<h4>加密方式:(.*?)</h4>[\d\D]*?</div>";
            Server[] servers = null;
            HttpGet("http://www.ishadowsocks.org/", Convert.ToString(Environment.TickCount),(responseStream)=>
            {
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string html = reader.ReadToEnd();
                    MatchCollection matcher = Regex.Matches(html, pattern);
                    servers = new Server[matcher.Count];
                    //foreach(Match match in matcher)
                    for (int i = 0; i < matcher.Count; i++)
                    {
                        Match match = matcher[i];
                        string server = match.Groups[1].Value;
                        int port = Convert.ToInt32(match.Groups[2].Value);
                        string password = match.Groups[3].Value;
                        string method = match.Groups[4].Value;

                        servers[i] = new Server();
                        servers[i].method = method;
                        servers[i].server = server;
                        servers[i].server_port = port;
                        servers[i].password = password;
                    }
                }
            });
            return servers;
        }
    }
}
