﻿using HtmlAgilityPack;
using myMangaSiteExtension.Attributes;
using myMangaSiteExtension.Enums;
using myMangaSiteExtension.Interfaces;
using myMangaSiteExtension.Objects;
using myMangaSiteExtension.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace Batoto
{
    [IExtensionDescription(
        Name = "Batoto",
        URLFormat = "bato.to",
        RefererHeader = "https://bato.to/reader",
        RootUrl = "https://bato.to",
        Author = "James Parks",
        Version = "0.0.1",
        SupportedObjects = SupportedObjects.All,
        Language = "English",
        RequiresAuthentication = true)]
    public class Batoto : ISiteExtension
    {
        protected const String AUTH_KEY = "880ea6a14ea49e853634fbdc5015a024",
            SECURE_KEY = "ff65abdb3406e0c4459ab7f1c873b621";

        #region IExtesion
        private IExtensionDescriptionAttribute EDA;
        public IExtensionDescriptionAttribute ExtensionDescriptionAttribute
        { get { return EDA ?? (EDA = GetType().GetCustomAttribute<IExtensionDescriptionAttribute>(false)); } }

        protected Icon extensionIcon;
        public Icon ExtensionIcon
        {
            get
            {
                if (Equals(extensionIcon, null)) extensionIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                return extensionIcon;
            }
        }

        public CookieCollection Cookies
        { get; private set; }

        public Boolean IsAuthenticated
        { get; private set; }

        public bool Authenticate(NetworkCredential Credentials, CancellationToken ct, IProgress<Int32> ProgressReporter)
        {
            // DO NOT RETURN TRUE IF `IsAuthenticated`
            // ALLOW USERS TO REAUTHENTICATE
            // if (IsAuthenticated) return true;

            CookieContainer cookieContainer = new CookieContainer();
            StringBuilder urlString = new StringBuilder();
            urlString.Append("https://bato.to/forums/index.php?");
            urlString.AppendUrlEncoded("app", "core", true);
            urlString.AppendUrlEncoded("module", "global");
            urlString.AppendUrlEncoded("section", "login");
            urlString.AppendUrlEncoded("do", "process");
            HttpWebRequest request = HttpWebRequest.CreateHttp(urlString.ToString());
            request.Method = WebRequestMethods.Http.Post;

            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(10);
            ct.ThrowIfCancellationRequested();

            // Generate login data
            StringBuilder loginData = new StringBuilder();
            loginData.AppendUrlEncoded("auth_key", AUTH_KEY, true);
            loginData.AppendUrlEncoded("anonymous", "1");
            loginData.AppendUrlEncoded("rememberMe", "1");
            loginData.AppendUrlEncoded("referer", this.ExtensionDescriptionAttribute.RefererHeader);
            loginData.AppendUrlEncoded("ips_username", Credentials.UserName);
            loginData.AppendUrlEncoded("ips_password", Credentials.Password);

            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(20);
            ct.ThrowIfCancellationRequested();

            // Apply loginData to request
            Byte[] loginDataBytes = Encoding.UTF8.GetBytes(loginData.ToString());
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = loginDataBytes.Length;
            request.CookieContainer = cookieContainer;

            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(30);
            ct.ThrowIfCancellationRequested();

            // Write loginData to request
            using (Stream requestStream = request.GetRequestStream())
            { requestStream.Write(loginDataBytes, 0, loginDataBytes.Length); }

            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(55);
            ct.ThrowIfCancellationRequested();

            // Get response and store cookies
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            String responseContent = null;
            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(75);
            using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
            { responseContent = streamReader.ReadToEnd(); }

            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(95);
            ct.ThrowIfCancellationRequested();
            Cookies = response.Cookies;

            IsAuthenticated = responseContent.IndexOf("username or password incorrect", StringComparison.OrdinalIgnoreCase) < 0;
            if (!Equals(ProgressReporter, null)) ProgressReporter.Report(100);
            return IsAuthenticated;
        }

        public void Deauthenticate()
        {
            if (!IsAuthenticated) return;
            CookieContainer cookieContainer = new CookieContainer();
            StringBuilder urlString = new StringBuilder();
            urlString.Append("https://bato.to/forums/index.php?");
            urlString.AppendUrlEncoded("app", "core", true);
            urlString.AppendUrlEncoded("module", "global");
            urlString.AppendUrlEncoded("section", "login");
            urlString.AppendUrlEncoded("do", "logout");
            urlString.AppendUrlEncoded("k", SECURE_KEY);
            HttpWebRequest request = HttpWebRequest.CreateHttp(urlString.ToString());
            request.Method = WebRequestMethods.Http.Get;
            request.CookieContainer = cookieContainer;
            request.CookieContainer.Add(Cookies);
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            Cookies = null;
            IsAuthenticated = false;
        }

        public List<MangaObject> GetUserFavorites()
        {
            // https://bato.to/myfollows
            throw new NotImplementedException();
        }

        public bool AddUserFavorites(MangaObject MangaObject)
        {
            throw new NotImplementedException();
        }

        public bool RemoveUserFavorites(MangaObject MangaObject)
        {
            throw new NotImplementedException();
        }
        #endregion

        public SearchRequestObject GetSearchRequestObject(string searchTerm)
        {
            return new SearchRequestObject()
            {
                Url = String.Format("{0}/search?name={1}", ExtensionDescriptionAttribute.RootUrl, Uri.EscapeUriString(searchTerm)),
                Method = SearchMethod.GET,
                Referer = ExtensionDescriptionAttribute.RefererHeader
            };
        }

        public MangaObject ParseMangaObject(string content)
        {
            HtmlDocument MangaObjectDocument = new HtmlDocument();
            MangaObjectDocument.LoadHtml(content);

            HtmlNode InformationNode = MangaObjectDocument.DocumentNode.SelectSingleNode("//div[contains(@class,'ipsBox')]/div");
            String Cover = InformationNode.SelectSingleNode(".//div[1]/img").Attributes["src"].Value;

            HtmlNode MangaProperties = InformationNode.SelectSingleNode(".//table[contains(@class,'ipb_table')]"),
                ChapterListing = MangaObjectDocument.DocumentNode.SelectSingleNode("//table[contains(@class,'chapters_list')]");

            String MangaName = HtmlEntity.DeEntitize(MangaObjectDocument.DocumentNode.SelectSingleNode("//h1[contains(@class,'ipsType_pagetitle')]").InnerText.Trim()),
                MangaTypeProp = HtmlEntity.DeEntitize(MangaProperties.SelectSingleNode(".//tr[5]/td[2]").InnerText),
                Desciption = HtmlEntity.DeEntitize(MangaProperties.SelectSingleNode(".//tr[7]/td[2]").InnerText.Replace("<br>", "\n"));
            MangaObjectType MangaType = MangaObjectType.Unknown;
            FlowDirection PageFlowDirection = FlowDirection.RightToLeft;
            switch (MangaTypeProp.ToLower())
            {
                default:
                    MangaType = MangaObjectType.Unknown;
                    PageFlowDirection = FlowDirection.RightToLeft;
                    break;

                case "manga (japanese)":
                    MangaType = MangaObjectType.Manga;
                    PageFlowDirection = FlowDirection.RightToLeft;
                    break;

                case "manhwa (korean)":
                    MangaType = MangaObjectType.Manhwa;
                    PageFlowDirection = FlowDirection.LeftToRight;
                    break;

                case "manhua (chinese)":
                    MangaType = MangaObjectType.Manhua;
                    PageFlowDirection = FlowDirection.LeftToRight;
                    break;
            }

            HtmlNodeCollection AlternateNameNodes = MangaProperties.SelectSingleNode(".//tr[1]/td[2]").SelectNodes(".//span"),
                GenreNodes = MangaProperties.SelectSingleNode(".//tr[4]/td[2]").SelectNodes(".//a/span");
            String[] AlternateNames = { },
                Authors = { HtmlEntity.DeEntitize(MangaProperties.SelectSingleNode(".//tr[2]/td[2]/a").InnerText) },
                Artists = { HtmlEntity.DeEntitize(MangaProperties.SelectSingleNode(".//tr[3]/td[2]/a").InnerText) },
                Genres = { };
            if (AlternateNameNodes != null && AlternateNameNodes.Count > 0)
                AlternateNames = (from HtmlNode AltNameNode in AlternateNameNodes select HtmlEntity.DeEntitize(AltNameNode.InnerText.Trim())).ToArray();
            if (GenreNodes != null && GenreNodes.Count > 0)
                Genres = (from HtmlNode GenreNode in GenreNodes select HtmlEntity.DeEntitize(GenreNode.InnerText.Trim())).ToArray();

            List<ChapterObject> Chapters = new List<ChapterObject>();
            HtmlNodeCollection ChapterNodes = ChapterListing.SelectNodes(String.Format(".//tr[contains(@class,'lang_{0} chapter_row')]", ExtensionDescriptionAttribute.Language));
            if (ChapterNodes != null && ChapterNodes.Count > 0)
            {
                foreach (HtmlNode ChapterNode in ChapterNodes)
                {
                    HtmlNode VolChapNameNode = ChapterNode.SelectSingleNode("td[1]/a");
                    Match VolChapMatch = Regex.Match(VolChapNameNode.InnerText, @"(Vol\.(?<Volume>\d+)\s)?(Ch\.(?<Chapter>\d+))(\.(?<SubChapter>\d+))?");
                    String ChapterName = VolChapNameNode.InnerText.Substring(VolChapMatch.Length + 2).Trim(),
                        ReleaseData = ReleaseData = ChapterNode.SelectSingleNode("td[5]").InnerText;
                    ChapterObject PrevChapter = Chapters.LastOrDefault();
                    UInt32 Volume = 0, Chapter = 0, SubChapter = 0;
                    if (VolChapMatch.Groups["Volume"].Success)
                        UInt32.TryParse(VolChapMatch.Groups["Volume"].Value, out Volume);
                    if (VolChapMatch.Groups["Chapter"].Success)
                        UInt32.TryParse(VolChapMatch.Groups["Chapter"].Value, out Chapter);
                    if (VolChapMatch.Groups["SubChapter"].Success)
                        UInt32.TryParse(VolChapMatch.Groups["SubChapter"].Value, out SubChapter);

                    DateTime Released = DateTime.Now;
                    if (ReleaseData.Contains("-"))
                    {
                        ReleaseData = ReleaseData.Split(new String[] { " - " }, StringSplitOptions.RemoveEmptyEntries)[0];
                        DateTime.TryParseExact(ReleaseData, "dd MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out Released);
                    }
                    else if (ReleaseData.EndsWith("ago"))
                    {
                        String[] ReleaseDataParts = ReleaseData.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        Double Offset = 1;
                        if (!Double.TryParse(ReleaseDataParts[0], out Offset)) Offset = 1;
                        Offset *= -1;
                        switch (ReleaseDataParts[1].ToLower())
                        {
                            default:
                            case "seconds":
                                Released = Released.AddSeconds(Offset);
                                break;

                            case "minutes":
                                Released = Released.AddMinutes(Offset);
                                break;

                            case "hours":
                                Released = Released.AddHours(Offset);
                                break;

                            case "days":
                                Released = Released.AddDays(Offset);
                                break;

                            case "weeks":
                                Released = Released.AddDays(7 * Offset);
                                break;
                        }
                    }

                    String ChapterUrl = VolChapNameNode.Attributes["href"].Value;
                    String ChapterHash = ChapterUrl.Split('#').Last().Split('_').First();
                    ChapterUrl = String.Format("https://bato.to/areader?id={0}&p=1&supress_webtoon=t", ChapterHash);
                    ChapterObject chapterObject = new ChapterObject()
                    {
                        Name = HtmlEntity.DeEntitize(ChapterName),
                        Volume = Volume,
                        Chapter = Chapter,
                        SubChapter = SubChapter,
                        Released = Released,
                        Locations = {
                            new LocationObject() {
                                ExtensionName = ExtensionDescriptionAttribute.Name,
                                ExtensionLanguage = ExtensionDescriptionAttribute.Language,
                                Url = ChapterUrl
                            }
                        }
                    };
                    if (!Chapters.Any(o => o.Chapter == chapterObject.Chapter && ((Int32)o.SubChapter - chapterObject.SubChapter).InRange(-4, 4)))
                        Chapters.Add(chapterObject);
                    else
                        Chapters.Find(o => o.Chapter == chapterObject.Chapter && ((Int32)o.SubChapter - chapterObject.SubChapter).InRange(-4, 4)).Merge(chapterObject);
                }
            }
            Chapters.Reverse();

            Double Rating = -1;
            try
            {
                HtmlNode RatingNode = MangaObjectDocument.DocumentNode.SelectSingleNode("//div[contains(@class,'rating')]");
                String RatingText = new String(RatingNode.InnerText.Trim().Substring(1, 4).Where(IsValidRatingChar).ToArray());
                Double.TryParse(RatingText, out Rating);
            }
            catch { }

            return new MangaObject()
            {
                Name = MangaName,
                MangaType = MangaType,
                PageFlowDirection = PageFlowDirection,
                Description = HtmlEntity.DeEntitize(Desciption),
                AlternateNames = AlternateNames.ToList(),
                CoverLocations = { new LocationObject() {
                    Url = Cover,
                    ExtensionName = ExtensionDescriptionAttribute.Name,
                    ExtensionLanguage = ExtensionDescriptionAttribute.Language } },
                Authors = Authors.ToList(),
                Artists = Artists.ToList(),
                Genres = Genres.ToList(),
                Released = (Chapters.FirstOrDefault() ?? new ChapterObject()).Released,
                Chapters = Chapters,
                Rating = Rating
            };
        }

        protected Boolean IsValidRatingChar(Char c)
        { return Char.IsDigit(c) || c.Equals('.'); }

        public ChapterObject ParseChapterObject(string content)
        {
            HtmlDocument ChapterObjectDocument = new HtmlDocument();
            ChapterObjectDocument.LoadHtml(content);

            ChapterObject ParsedChapterObject = new ChapterObject();
            HtmlNodeCollection PageNodes = ChapterObjectDocument.GetElementbyId("page_select").SelectNodes(".//option");
            if (PageNodes != null && PageNodes.Count > 0)
            {
                foreach (HtmlNode PageNode in PageNodes)
                {
                    HtmlNode PrevNode = PageNode.SelectSingleNode(".//preceding-sibling::option"),
                        NextNode = PageNode.SelectSingleNode(".//following-sibling::option");

                    UInt32 PageNumber = UInt32.Parse(PageNode.NextSibling.InnerText.Substring(5));
                    String PageUrl = PageNode.Attributes["value"].Value,
                        NextPageUrl = (NextNode != null) ? NextNode.Attributes["value"].Value : null,
                        PrevPageUrl = (PrevNode != null) ? PrevNode.Attributes["value"].Value : null;

                    if (!String.IsNullOrWhiteSpace(PageUrl))
                    {
                        String PageHash = PageUrl.Split('#').Last().Split('_').First();
                        PageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber);
                    }

                    if (!String.IsNullOrWhiteSpace(NextPageUrl))
                    {
                        String PageHash = NextPageUrl.Split('#').Last().Split('_').First();
                        NextPageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber + 1);
                    }

                    if (!String.IsNullOrWhiteSpace(PrevPageUrl))
                    {
                        String PageHash = PrevPageUrl.Split('#').Last().Split('_').First();
                        PrevPageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber - 1);
                    }

                    ParsedChapterObject.Pages.Add(new PageObject()
                    {
                        PageNumber = PageNumber,
                        Url = PageUrl,
                        NextUrl = NextPageUrl,
                        PrevUrl = PrevPageUrl
                    });
                }
            }

            return ParsedChapterObject;
        }

        public PageObject ParsePageObject(string content)
        {
            HtmlDocument PageObjectDocument = new HtmlDocument();
            PageObjectDocument.LoadHtml(content);

            HtmlNode PageSelect = PageObjectDocument.GetElementbyId("page_select"),
                PageNode = PageSelect.SelectSingleNode(".//option[@selected]"),
                PrevNode = PageNode.SelectSingleNode(".//preceding-sibling::option"),
                NextNode = PageNode.SelectSingleNode(".//following-sibling::option");

            Uri ImageLink = new Uri(PageObjectDocument.GetElementbyId("comic_page").Attributes["src"].Value);
            String Name = ImageLink.ToString().Split('/').Last();

            UInt32 PageNumber = UInt32.Parse(PageNode.NextSibling.InnerText.Substring(5));
            String PageUrl = PageNode.Attributes["value"].Value,
                NextPageUrl = (NextNode != null) ? NextNode.Attributes["value"].Value : null,
                PrevPageUrl = (PrevNode != null) ? PrevNode.Attributes["value"].Value : null;

            if (!String.IsNullOrWhiteSpace(PageUrl))
            {
                String PageHash = PageUrl.Split('#').Last().Split('_').First();
                PageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber);
            }

            if (!String.IsNullOrWhiteSpace(NextPageUrl))
            {
                String PageHash = NextPageUrl.Split('#').Last().Split('_').First();
                NextPageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber + 1);
            }

            if (!String.IsNullOrWhiteSpace(PrevPageUrl))
            {
                String PageHash = PrevPageUrl.Split('#').Last().Split('_').First();
                PrevPageUrl = String.Format("https://bato.to/areader?id={0}&p={1}&supress_webtoon=t", PageHash, PageNumber - 1);
            }

            return new PageObject()
            {
                Name = Name,
                PageNumber = PageNumber,
                Url = PageUrl,
                NextUrl = NextPageUrl,
                PrevUrl = PrevPageUrl,
                ImgUrl = ImageLink.ToString()
            };
        }

        public List<SearchResultObject> ParseSearch(string content)
        {
            List<SearchResultObject> SearchResults = new List<SearchResultObject>();
            Regex IdMatch = new Regex(@"r\d+");
            HtmlDocument SearchResultDocument = new HtmlDocument();
            SearchResultDocument.LoadHtml(content);
            HtmlWeb HtmlWeb = new HtmlWeb();
            HtmlNodeCollection HtmlSearchResults = SearchResultDocument.DocumentNode.SelectNodes("//table[contains(@class,'ipb_table chapters_list')]/tbody/tr[not(contains(@class,'header'))]");
            if (!Equals(HtmlSearchResults, null))
                foreach (HtmlNode SearchResultNode in HtmlSearchResults)
                {
                    HtmlNode NameLink = SearchResultNode.SelectSingleNode(".//td[1]/strong/a");
                    if (NameLink != null)
                    {
                        Int32 Id = -1;
                        String Name = HtmlEntity.DeEntitize(NameLink.InnerText).Trim(),
                            Link = NameLink.Attributes["href"].Value,
                            Description = null;
                        LocationObject Cover = null;
                        if (Int32.TryParse(IdMatch.Match(Link).Value.Substring(1), out Id))
                        {
                            HtmlDocument PopDocument = HtmlWeb.Load(String.Format("{0}/comic_pop?id={1}", ExtensionDescriptionAttribute.RootUrl, Id));
                            HtmlNode CoverNode = PopDocument.DocumentNode.SelectSingleNode("//img"),
                                DescriptionNode = PopDocument.DocumentNode.SelectSingleNode("//table/tbody/tr[6]/td[2]");
                            if (!HtmlNode.Equals(CoverNode, null)) Cover = new LocationObject()
                            {
                                Url = CoverNode.Attributes["src"].Value,
                                ExtensionName = ExtensionDescriptionAttribute.Name,
                                ExtensionLanguage = ExtensionDescriptionAttribute.Language
                            };
                            if (!HtmlNode.Equals(DescriptionNode, null)) Description = DescriptionNode.InnerText.Trim();
                        }
                        String[] Author_Artists = { SearchResultNode.SelectSingleNode(".//td[2]").InnerText.Trim() };
                        SearchResults.Add(new SearchResultObject()
                        {
                            Cover = Cover,
                            Description = Description,
                            ExtensionName = ExtensionDescriptionAttribute.Name,
                            ExtensionLanguage = ExtensionDescriptionAttribute.Language,
                            Name = Name,
                            Url = Link,
                            Id = Id.ToString(),
                            Rating = Double.Parse(SearchResultNode.SelectSingleNode(".//td[3]/div").Attributes["title"].Value.Substring(0, 4)),
                            Artists = Author_Artists.ToList(),
                            Authors = Author_Artists.ToList()
                        });
                    }
                }
            return SearchResults;
        }
    }
}
