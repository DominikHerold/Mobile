using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

using HtmlAgilityPack;

namespace Mobile
{
    public class Program
    {
        private static Timer Timer;

        private static readonly HashSet<string> Urls = new HashSet<string>();

        private const string PushoverKey = "token=foo&user=bar&message="; // ToDo change

        private static void Main()
        {
            Console.WriteLine("Start");
            
            DoStart("http://www.ebay-kleinanzeigen.de/s-wohnwagen-mobile/ford-nugget/k0c220");
            DoStart("http://www.ebay-kleinanzeigen.de/s-ford-euroline/k0");

            Timer = new Timer(1800000, false);
            Timer.Elapsed += CheckCars;

            CheckCars(null, null);

            Console.ReadLine();
            Timer.Dispose();
        }

        private static void DoStart(string ebayUrl)
        {
            var ebayData = DownloadString(ebayUrl);
            var ebayNodes = GetEbayNodes(ebayData);
            foreach (var ebayNode in ebayNodes)
            {
                var ebayTuple = GetEbayData(ebayNode);
                Urls.Add(ebayTuple.Item3);
            }
        }

        private static void DoMobile(string mobileUrl)
        {
            var data = DownloadString(mobileUrl);
            var nodes = GetNodes(data);
            foreach (var node in nodes)
            {
                var parent = node.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode;
                var href = parent.GetAttributeValue("href", "NotFound");
                var ampIndex = href.IndexOf("&", StringComparison.Ordinal);
                var url = href.Substring(0, ampIndex);
                if (!Urls.Add(url))
                    continue;

                if (GetMobileKm(url) > 110000)
                    continue;

                var title = parent.SelectSingleNode(".//span[@class='h3 u-text-break-word']").InnerText;
                var price = parent.SelectSingleNode(".//span[@class='h3 u-block']").InnerText;

                if (!title.Contains("Ford") && !title.Contains("ford") && !title.Contains("FORD"))
                    continue;

                Console.WriteLine(title);
                Console.WriteLine(price);
                Console.WriteLine(url);

                SendToPushoverApi(title, price, url);
            }
        }

        private static void DoEbay(string ebayUrl)
        {
            var ebayData = DownloadString(ebayUrl);
            var ebayNodes = GetEbayNodes(ebayData);
            foreach (var ebayNode in ebayNodes)
            {
                var ebayTuple = GetEbayData(ebayNode);
                if (!Urls.Add(ebayTuple.Item3))
                    continue;

                if (GetEbayKm(ebayTuple.Item3) > 110000)
                    continue;

                Console.WriteLine(ebayTuple.Item1);
                Console.WriteLine(ebayTuple.Item2);
                Console.WriteLine(ebayTuple.Item3);

                SendToPushoverApi(ebayTuple.Item1, ebayTuple.Item2, ebayTuple.Item3);
            }
        }

        private static void CheckCars(object sender, EventArgs e)
        {
            try
            {
                DoMobile("http://suchen.mobile.de/auto/ford-transit-nugget.html");
                DoMobile("http://suchen.mobile.de/auto/ford-euroline.html");
                
                DoEbay("http://www.ebay-kleinanzeigen.de/s-wohnwagen-mobile/ford-nugget/k0c220");
                DoEbay("http://www.ebay-kleinanzeigen.de/s-ford-euroline/k0");
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
            using (var webClient = new HttpClient())
            {
                var data = webClient.GetAsync(address).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return data;
            }
        }

        private static IEnumerable<HtmlNode> GetNodes(string data)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(data);
            var nodes = htmlDoc.DocumentNode.SelectNodes("//span[@class='new-headline-label']");

            return nodes;
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
            var mainNode = node.SelectSingleNode(".//section[@class='aditem-main']");
            var a = mainNode.SelectSingleNode(".//a");
            var url = string.Format("https://www.ebay-kleinanzeigen.de{0}", a.GetAttributeValue("href", "NotFound"));
            var title = a.InnerText;
            var detailNode = node.SelectSingleNode(".//section[@class='aditem-details']");
            var price = detailNode.SelectSingleNode(".//strong").InnerText;

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
                var ebayData = DownloadString(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(ebayData);
                var nodes = htmlDoc.DocumentNode.SelectNodes("//dt[@class='attributelist--key']");
                var kmNode = nodes.FirstOrDefault(n => n.InnerText.Contains("Kilometerstand"));
                if (kmNode == null)
                    return 0;

                var kmString = kmNode.NextSibling.NextSibling.SelectSingleNode(".//span").InnerText.Trim().Replace(".", string.Empty);
                var km = Convert.ToInt32(kmString);
                return km;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
        }

        private static int GetMobileKm(string url)
        {
            try
            {
                var mobileData = DownloadString(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(mobileData);
                var kmNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='rbt-mileage-v']");
                if (kmNode == null)
                    return 0;

                var kmString = new string(kmNode.InnerText.Where(char.IsDigit).ToArray());
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