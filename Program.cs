using System;
using HtmlAgilityPack;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace TX_Rep._ZipCode_Scraper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var houseMembersUrl = @"https://capitol.texas.gov/Members/Members.aspx?Chamber=H";

            HtmlWeb web = new HtmlWeb();

            var houseMembersInfoUrls = GetMembersInfoLinks(houseMembersUrl);
            foreach(var url in houseMembersInfoUrls)
                ScrapePDF(url);
            //Console.WriteLine("Node Name: " + node.Name + "\n" + node.OuterHtml);
        }

        private static void ScrapePDF(string url) {
            HtmlWeb web = new HtmlWeb();
            Console.WriteLine(url);
            var htmlDoc = web.Load(url);

            var homePageUrl = htmlDoc.GetElementbyId("lnkHomePage").Attributes["href"].Value;

            htmlDoc = web.Load(homePageUrl);

            var pdfLink = htmlDoc.DocumentNode.SelectSingleNode("//a['ZIP Codes by District']").Attributes["href"].Value;

            var pdfStream = new MemoryStream();
            var reader = PDFReader(pdfStream);
            ExtractPDFText(pdf);

            Console.WriteLine(pdfLink);
        }

        private static List<string> ExtractPDFText(PDFDocument pdf) {


            return new List<string>();
        }

        private static List<string> GetMembersInfoLinks(string url) {
            List<string> infoUrls = new List<string>();

            HtmlWeb web = new HtmlWeb();

            var htmlDoc = web.Load(url);

            var node = htmlDoc.GetElementbyId("dataListMembers");
            foreach(var row in node.SelectNodes("tr")) {
                foreach(var col in row.SelectNodes("td")) {
                    var anchorELement = col?.SelectSingleNode("li/a");
                    var link = anchorELement?.Attributes["href"]?.Value;
                    if(!String.IsNullOrEmpty(link))
                        infoUrls.Add(GetAbsoluteUrlString(url,link));
                }
            }
            return infoUrls;
        }

        private static string GetAbsoluteUrlString(string baseUrl, string url)
        {
            var uri = new Uri(url, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri(new Uri(baseUrl), uri);
            return uri.ToString();
        }
    }
}
