using System;
using HtmlAgilityPack;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using iText.Kernel.Pdf;
using System.Text;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Net;
using System.Text.RegularExpressions;

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

            var pdfLink = htmlDoc.DocumentNode.SelectSingleNode("//a[.='ZIP Codes by District']").Attributes["href"].Value;

            using (MemoryStream pdfStream = new MemoryStream())
            {
                ConvertToStream(pdfLink, pdfStream);
                pdfStream.Seek(0, SeekOrigin.Begin);

                using var reader = new PdfReader(pdfStream);
                using var pdfDoc = new PdfDocument(reader);
                ExtractPDFText(pdfDoc);

            }

            Console.WriteLine(pdfLink);
        }

        private static void ConvertToStream(string fileUrl, Stream stream)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fileUrl);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            try
            {
                Stream response_stream = response.GetResponseStream();

                response_stream.CopyTo(stream, 4096);
            }
            finally
            {
                response.Close();
            }
        }

        private static List<string> ExtractPDFText(PdfDocument pdf) {
            var zipCodes = new List<string>();
            StringBuilder processed = new StringBuilder();
            var strategy = new LocationTextExtractionStrategy();
            for (int i = 1; i<=pdf.GetNumberOfPages(); ++i)
			{
                var page = pdf.GetPage(i);
                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                Console.Write(text);
                processed.Append(text);
			}
            string pattern = @"\*[0-9]{5}";
            Regex rgx = new Regex(pattern);
            foreach (Match match in rgx.Matches(processed.ToString()))
            {
                zipCodes.Add(match.Value.Replace("*", ""));
                Console.WriteLine(match.Value);
            }
            return zipCodes;
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
