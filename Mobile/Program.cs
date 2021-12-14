using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using HtmlAgilityPack;

namespace Mobile
{
    public class Program
    {
        private static Timer Timer;

        private static HashSet<string> Urls = new HashSet<string>();

        private const string PushoverKey = "token=foo&user=bar&message="; // ToDo change

        private static void Main()
        {
            Console.WriteLine("Start");

            DoStart("https://www.ebay-kleinanzeigen.de/s-wohnwagen-mobile/ford-nugget/k0c220");
            DoStart("https://www.ebay-kleinanzeigen.de/s-ford-euroline/k0");

            Timer = new Timer((int)TimeSpan.FromHours(25).TotalMilliseconds, false);
            Timer.Elapsed += CheckCars;

            CheckCars(null, null);

            Console.ReadLine();
            Timer.Dispose();
        }

        private static void DoStart(string ebayUrl)
        {
            IEnumerable<HtmlNode> ebayNodes;
            do
            {
                var ebayData = DownloadString(ebayUrl);
                ebayNodes = GetEbayNodes(ebayData);
            }
            while (ebayNodes == null && ThreadSleep5Minutes());

            foreach (var ebayNode in ebayNodes)
            {
                var ebayTuple = GetEbayData(ebayNode);
                Urls.Add(ebayTuple.Item3);
            }
        }

        private static bool ThreadSleep5Minutes(bool throwException = false)
        {
            Console.WriteLine("delay");
            Task.Delay(TimeSpan.FromMinutes(5)).GetAwaiter().GetResult();

            if (throwException)
            {
                Urls = new HashSet<string>();
                throw new Exception("foo");
            }

            return true;
        }

        private static void DoEbay(string ebayUrl)
        {
            Tuple<string, string, string> toBeSend = null;
            var maxEbayKm = 1;
            IEnumerable<HtmlNode> ebayNodes;
            do
            {
                var ebayData = DownloadString(ebayUrl);
                ebayNodes = GetEbayNodes(ebayData);
            }
            while (ebayNodes == null && ThreadSleep5Minutes(true));

            foreach (var ebayNode in ebayNodes)
            {
                var ebayTuple = GetEbayData(ebayNode);
                if (!Urls.Add(ebayTuple.Item3))
                    continue;

                var ebayKm = GetEbayKm(ebayTuple.Item3);
                if (ebayKm > 160000 || ebayKm == 0)
                    continue;

                if (ebayKm > maxEbayKm)
                {
                    maxEbayKm = ebayKm;
                    toBeSend = ebayTuple;
                }

                Console.WriteLine(ebayTuple.Item1);
                Console.WriteLine(ebayTuple.Item2);
                Console.WriteLine(ebayTuple.Item3);
            }

            if (toBeSend != null)
            {
                SendToPushoverApi(toBeSend.Item1, toBeSend.Item2, toBeSend.Item3);
            }
        }

        private static void CheckCars(object sender, EventArgs e)
        {
            try
            {
                //DoMobile("http://suchen.mobile.de/auto/ford-transit-nugget.html");
                //DoMobile("http://suchen.mobile.de/auto/ford-euroline.html");

                DoEbay("https://www.ebay-kleinanzeigen.de/s-wohnwagen-mobile/ford-nugget/k0c220");
                DoEbay("https://www.ebay-kleinanzeigen.de/s-ford-euroline/k0");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Timer.Start();
            }
        }

        private static string DownloadString(string address)
        {
            Task.Delay(TimeSpan.FromSeconds(91)).GetAwaiter().GetResult();
            using (var webClient = new HttpClient())
            {
                var data = webClient.GetAsync(address).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return data;
            }
        }

        private static IEnumerable<HtmlNode> GetEbayNodes(string data)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(data);
            var nodes = htmlDoc.DocumentNode.SelectNodes("//article[@class='aditem']");

            return nodes;
        }

        private static Tuple<string, string, string> GetEbayData(HtmlNode node)
        {
            var mainNode = node.SelectSingleNode(".//div[@class='aditem-main']");
            var a = mainNode.SelectSingleNode(".//a");
            var url = string.Format("https://www.ebay-kleinanzeigen.de{0}", a.GetAttributeValue("href", "NotFound"));
            var title = a.InnerText;
            var detailNode = node.SelectSingleNode(".//p[@class='aditem-main--middle--price']");
            var price = detailNode.InnerText.Trim();

            return new Tuple<string, string, string>(title, price, url);
        }

        private static void SendToPushoverApi(string title, string price, string url)
        {
            var client = new HttpClient();
            var toSend = string.Concat(PushoverKey, price);
            toSend = string.Format("{0}&title={1}&url={2}", toSend, title, url);
            var now = DateTime.Now;
            if (now.Hour >= 22 || now.Hour < 7)
                toSend = string.Format("{0}&sound=none", toSend);

            client.PostAsync("https://api.pushover.net/1/messages.json", new StringContent(toSend, Encoding.UTF8, "application/x-www-form-urlencoded")).GetAwaiter().GetResult();
        }

        private static int GetEbayKm(string url)
        {
            try
            {
                IEnumerable<HtmlNode> nodes;
                do
                {
                    var ebayData = DownloadString(url);
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(ebayData);
                    nodes = htmlDoc.DocumentNode.SelectNodes("//li[@class='addetailslist--detail']");
                }
                while (nodes == null && ThreadSleep5Minutes(true));

                var kmNode = nodes.FirstOrDefault(n => n.InnerText.Contains("Kilometerstand"));
                if (kmNode == null)
                    return 0;

                var kmString = new string(kmNode.SelectSingleNode(".//span").InnerText.Where(char.IsDigit).ToArray());
                var km = Convert.ToInt32(kmString);
                return km;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }
    }
}