using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using System.Diagnostics;

namespace Shadowsocks.Extra.Tests
{
    [TestClass()]
    public class FreeServersTests
    {
        [TestMethod()]
        public void updateFreeServersTest()
        {            
            ShadowsocksController controller =new ShadowsocksController();
            FreeServers servers = new FreeServers(controller);

            Debug.WriteLine(System.Environment.CurrentDirectory);
            Debug.WriteLine(System.IO.Directory.GetCurrentDirectory());
            Debug.WriteLine(System.AppDomain.CurrentDomain.BaseDirectory);
            Debug.WriteLine(System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

            Debug.WriteLine("Before Total Count -> " + controller.GetConfigurationCopy().configs.Count);
            
            servers.updateFreeServers();
            System.Threading.Thread.Sleep(5000); // wait for thread worked.
            Debug.WriteLine("Len\tServer\tPort\tPassword\tMethod");
            foreach(Server server in controller.GetConfigurationCopy().configs)
            {
                Debug.WriteLine(string.Format("{4}\t{0}\t{1}\t{2}\t{3}"
                    , server.server
                    , server.server_port
                    , server.password
                    , server.method
                    , server.server.Length));
            }

            Debug.WriteLine("Total Count -> " + controller.GetConfigurationCopy().configs.Count);
            Assert.IsTrue(controller.GetConfigurationCopy().configs.Count > 2);
        }
    }
}