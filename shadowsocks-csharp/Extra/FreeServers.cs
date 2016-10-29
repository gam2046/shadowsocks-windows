using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Shadowsocks.Extra
{
    public class FreeServers
    {
        private ShadowsocksController controller;

        public FreeServers(ShadowsocksController controller)
        {
            this.controller = controller;
            Model.Server server = new Model.Server();
        }
        /// <summary>
        /// GET请求与获取结果
        /// </summary>
        public void HttpGet(string Url, string postDataStr, Action<Stream> callback)
        {
            HttpWebRequest request;
            if (string.IsNullOrWhiteSpace(postDataStr))
                request = (HttpWebRequest)WebRequest.Create(Url);
            else
                request = (HttpWebRequest)WebRequest.Create(Url + "?" + postDataStr);

            request.Method = "GET";

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
        protected void addServerToConfig(Server[] newServers)
        {
            lock (this.controller) // 异步读写防止冲突
            {
                List<Server> currentServers = this.controller.GetConfigurationCopy().configs;
                Debug.WriteLine("Before Add Free Server Count -> " + currentServers.Count);
                //foreach (Server currentServer in currentServers) // 修改已存在的服务器信息
                for (int i = currentServers.Count - 1; i >= 0; i--)
                {
                    Server currentServer = currentServers[i];
                    foreach (Server newServer in newServers)
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
                        }
                        else
                        {
                            Debug.WriteLine("Unknow Current Host -> '" + currentServer.server + "'");
                        }
                }

                foreach (Server newServer in newServers)
                        if (!string.IsNullOrEmpty(newServer.server)) // 添加新的服务器                    
                            currentServers.Add(newServer);

                Debug.WriteLine("After Add Free Server Count -> " + currentServers.Count);
                this.controller.SaveServers(currentServers, this.controller.GetConfigurationCopy().localPort); // 保存信息
            }
        }
        public void updateFreeServers()
        {
            const string pattern = @"<h4>.?服务器地址:(?<server>[a-zA-Z0-9.]*?)</h4>[\d\D]*?<h4>端口:(?<port>\d*?)</h4>[\d\D]*?<h4>.?密码:(?<password>.*?)</h4>[\d\D]*?<h4>加密方式:(?<method>.*?)</h4>[\d\D]*?</div>";
            string[] regexUrls = new string[] { "http://www.ishadowsocks.org/?" + Convert.ToString(Environment.TickCount) ,
                                                "http://shadowsocksr.xyz/?" + Convert.ToString(Environment.TickCount)};

            string[] qrUrls = new string[] { @"http://www.shadowsocks8.net/images/server01.png"
                                        ,@"http://www.shadowsocks8.net/images/server02.png"
                                        ,@"http://www.shadowsocks8.net/images/server03.png"};

            foreach (string url in regexUrls)
                this.getFreeServerByRegexAsync(url, pattern, this.addServerToConfig);

            foreach (string url in qrUrls)
                this.getFreeServerByQrCodeAsync(url, this.addServerToConfig);
        }      
        /// <summary>
        /// 使用二维码匹配SS服务器信息
        /// </summary>
        /// <param name="url">二维码地址</param>
        /// <returns>结果的Server对象</returns>
        protected Server getFreeServerByQrCode(string url)
        {
            Server serv = null;
                HttpGet(url, Convert.ToString(Environment.TickCount), (responseStream) =>
                {
                    using (Bitmap target = new Bitmap(responseStream))
                    {
                        var source = new BitmapLuminanceSource(target);
                        var bitmap = new BinaryBitmap(new HybridBinarizer(source));
                        QRCodeReader reader = new QRCodeReader();
                        var result = reader.decode(bitmap);

                        if (result != null)
                            serv= new Server(result.Text);
                        else
                            Logging.Error("Decode QR Code Err");
                    }
                });
            return serv;
        }
        /// <summary>
        /// 异步使用二维码匹配SS服务器信息
        /// </summary>
        /// <param name="url">二维码URL</param>
        /// <param name="callback">回调函数处理获取的Server对象</param>
        protected void getFreeServerByQrCodeAsync(string url,Action<Server[]> callback)
        {
            Task.Factory.StartNew(() => {
                try
                {
                    Server server = this.getFreeServerByQrCode(url);
                    callback.Invoke(new Server[] { server });
                    Logging.Info("Async QrCode Task Finished -> " + url);
                }catch(Exception e)
                {
                    Logging.Error("An exception while getting qr-code servers -> " + e);
                }
            });
        }
        /// <summary>
        /// 异步使用正则表达式匹配SS服务器信息
        /// 参数含义同getFreeServerByRegex
        /// </summary>
        /// <param name="url"></param>
        /// <param name="pattern"></param>
        /// <param name="callback">异步调用成功后的回调函数</param>
        protected void getFreeServerByRegexAsync(string url, string pattern,Action<Server[]> callback)
        {
            Task.Factory.StartNew(() => {
                try
                {
                    Server[] servers = this.getFreeServerByRegex(url, pattern);
                    callback.Invoke(servers);
                    Logging.Info("Async Regex Task Finished -> " + url);
                }
                catch(Exception e)
                {
                    Logging.Error("An exception while getting regex servers -> " + e);
                }
            });
        }
        /// <summary>
        /// 使用正则表达式匹配SS服务器信息
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="pattern">正则表达式，必须使用命名捕获分组指定SS相关信息
        /// server -> 服务器IP或域名
        /// port -> 服务器端口
        /// password -> 服务器密码
        /// method -> 加密方式
        /// </param>
        /// <returns>捕获到的服务器列表</returns>
        protected Server[] getFreeServerByRegex(string url,string pattern)
        {
            // TODO 修改成配置文件方式读取参数
            Server[] servers = null;
            HttpGet(url, Convert.ToString(Environment.TickCount), (responseStream) =>
            {
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string html = reader.ReadToEnd();
                    MatchCollection matcher = Regex.Matches(html, pattern);
                    servers = new Server[matcher.Count];
                    for (int i = 0; i < matcher.Count; i++)
                    {
                        Match match = matcher[i];
                        string server = match.Groups["server"].Value;
                        int port = Convert.ToInt32(match.Groups["port"].Value);
                        string password = match.Groups["password"].Value;
                        string method = match.Groups["method"].Value;

                        servers[i] = new ServerEx();
                        servers[i].method = method;
                        servers[i].server = server;
                        servers[i].server_port = port;
                        servers[i].password = password;

                        Debug.WriteLine("Found Server ->\n" + servers[i]);
                    }
                }
            });
            return servers;
        }
    }
}
