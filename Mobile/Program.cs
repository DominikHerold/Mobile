using System;
using System.Collections.Generic;
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

            Timer = new Timer(1800000, false);
            Timer.Elapsed += CheckCars;

            CheckCars(null, null);

            Console.ReadLine();
            Timer.Dispose();
        }

        private static void CheckCars(object sender, EventArgs e)
        {
            try
            {
                var data = DownloadString("http://suchen.mobile.de/auto/ford-transit-nugget.html");
                var nodes = GetNodes(data);
                foreach (var node in nodes)
                {
                    var parent = node.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode;
                    var href = parent.GetAttributeValue("href", "NotFound");
                    var ampIndex = href.IndexOf("&", StringComparison.Ordinal);
                    var url = href.Substring(0, ampIndex);
                    if (!Urls.Add(url))
                        continue;

                    var title = parent.SelectSingleNode(".//span[@class='h3 u-text-break-word']").InnerText;
                    var price = parent.SelectSingleNode(".//span[@class='h3 u-block']").InnerText;
                    Console.WriteLine(title);
                    Console.WriteLine(price);
                    Console.WriteLine(url);

                    SendToPushoverApi(title, price, url);
                }
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

        private static void SendToPushoverApi(string title, string price, string url)
        {
            var client = new HttpClient();
            var toSend = string.Concat(PushoverKey, price);
            toSend = string.Format("{0}&title={1}&url={2}", toSend, title, url);
            client.PostAsync("https://api.pushover.net/1/messages.json", new StringContent(toSend, Encoding.UTF8, "application/x-www-form-urlencoded")).GetAwaiter().GetResult();
        }
    }
}