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
using IronXL;

namespace TX_Rep._ZipCode_Scraper
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting");

            File.Delete(@"..\..\..\house.xlsx");
            File.Delete(@"..\..\..\senate.xlsx");
            WorkBook xlsWorkbook = WorkBook.Create(ExcelFileFormat.XLS);
            xlsWorkbook.Metadata.Author = "TREAD";
            WorkSheet xlsSheet = xlsWorkbook.CreateWorkSheet("new_sheet");
            xlsWorkbook.SaveAs(@"..\..\..\senate.xlsx");
            xlsWorkbook.SaveAs(@"..\..\..\house.xlsx");

            //ScrapeHouseMemberZipCodes();
            ScrapeSenateMemberZipCodes();

            Console.WriteLine("Finished!!!");
        }

        private static void ScrapeHouseMemberZipCodes()
        {
            var houseMembersUrl = @"https://capitol.texas.gov/Members/Members.aspx?Chamber=H";

            var houseMembersInfoUrls = GetMembersInfoLinks(houseMembersUrl);
            WorkBook workbook = WorkBook.Load(@"..\..\..\house.xlsx");
            WorkSheet sheet = workbook.DefaultWorkSheet;

            int row = 1;
            foreach (var url in houseMembersInfoUrls)
            {
                var member = ScrapeMemberData(url);
                WriteMember(sheet, member, row++);
                Console.WriteLine($"Completed: {row - 1}/{houseMembersInfoUrls.Count}");
            }

            workbook.SaveAs(@"..\..\..\house.xlsx");
        }


        private static void ScrapeSenateMemberZipCodes()
		{
            WorkBook workbook = WorkBook.Load(@"..\..\..\senate.xlsx");
            WorkSheet sheet = workbook.DefaultWorkSheet;
            for (int district = 1; district < 32; district++)
            {
                var url = $"https://statisticalatlas.com/state-upper-legislative-district/Texas/State-Senate-District-{district}/Overview";
                HtmlWeb web = new HtmlWeb();
                Console.WriteLine(url);
                var htmlDoc = web.Load(url);
                var zipAnchors = htmlDoc.DocumentNode.SelectSingleNode("//div[.='ZIP Codes: ']").ParentNode.SelectNodes("div[2]/div");
                List<string> zipCodes = new List<string>();
                foreach(var zipAnchor in zipAnchors)
				{
                    zipCodes.Add(zipAnchor.InnerText);
				}
                Console.WriteLine(district + ": " + String.Join(" ", zipCodes));
                sheet[$"A{district}"].Value = $"District {district}";
                sheet[$"C{district}"].Value = String.Join(" ", zipCodes);
            }

            var url2 = "https://capitol.texas.gov/Members/Members.aspx?Chamber=S";
            HtmlWeb web2 = new HtmlWeb();
            var htmlDoc2 = web2.Load(url2);
            var node = htmlDoc2.GetElementbyId("dataListMembers");
            var infoUrls = new List<string>();
            foreach (var row in node.SelectNodes("tr"))
            {
                foreach (var col in row.SelectNodes("td"))
                {
                    var anchorELement = col?.SelectSingleNode("li/a");
                    var link = anchorELement?.Attributes["href"]?.Value;
                    if (!String.IsNullOrEmpty(link))
                    {
                        var senatorMemberUrl = GetAbsoluteUrlString(url2, link);
                        HtmlWeb web3 = new HtmlWeb();
                        var senatorMemberHtml = web3.Load(senatorMemberUrl);
                        var districtNumber = senatorMemberHtml.GetElementbyId("lblDistrict")?.InnerText;
                        if (districtNumber == null)
                            continue;
                        var name = senatorMemberHtml.GetElementbyId("usrHeader_lblPageTitle").InnerText;
                        Console.WriteLine(name);
                        sheet[$"B{districtNumber}"].Value = name.Substring(name.IndexOf("Sen."), name.Length- name.IndexOf("Sen."));
                        Console.WriteLine(name.Substring(name.IndexOf("Sen."), name.Length - name.IndexOf("Sen.")));
                    }
                }
            }

            workbook.SaveAs(@"..\..\..\senate.xlsx");
        }

        private static void WriteMember(WorkSheet sheet, Member member, int row)
		{
            //XLSX Format
            // A        B       C
            // Dist#     Name    ZipCodes(space serparated)

            sheet[$"A{row}"].Value = member.DistrictNumber;
            sheet[$"B{row}"].Value = member.Name;
            sheet[$"C{row}"].Value = String.Join(" ", member.ZipCodes);
        }

        private static Member ScrapeMemberData(string url) {
            HtmlWeb web = new HtmlWeb();
            Console.WriteLine(url);
            var htmlDoc = web.Load(url);

            var homePageUrl = htmlDoc.GetElementbyId("lnkHomePage").Attributes["href"].Value;

            htmlDoc = web.Load(homePageUrl);

            var name = htmlDoc.DocumentNode.SelectSingleNode("//*[@id=\"wrapper\"]/div[3]/h2[1]").InnerText;
            var districtNumber = htmlDoc.DocumentNode.SelectSingleNode("//*[@id=\"wrapper\"]/div[3]/h2[2]").InnerText;

            var pdfLink = htmlDoc.DocumentNode.SelectSingleNode("//a[.='ZIP Codes by District']").Attributes["href"].Value;


            var zipCodes = new List<string>();

            using (MemoryStream pdfStream = new MemoryStream())
            {
                ConvertToStream(pdfLink, pdfStream);
                pdfStream.Seek(0, SeekOrigin.Begin);

                using var reader = new PdfReader(pdfStream);
                using var pdfDoc = new PdfDocument(reader);
                zipCodes = ExtractPDFText(pdfDoc);

            }

            return new Member() { Name = name, DistrictNumber = districtNumber, ZipCodes = zipCodes };
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
                processed.Append(text);
			}
            string pattern = @"\*[0-9]{5}";
            Regex rgx = new Regex(pattern);
            foreach (Match match in rgx.Matches(processed.ToString()))
            {
                zipCodes.Add(match.Value.Replace("*", ""));
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

    internal class Member
	{
        public string DistrictNumber { get; set; }
        public string Name { get; set; }
        public List<string> ZipCodes { get; set; }

	}
}
