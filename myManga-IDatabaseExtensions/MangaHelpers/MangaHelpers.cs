﻿using HtmlAgilityPack;
using myMangaSiteExtension.Attributes;
using myMangaSiteExtension.Enums;
using myMangaSiteExtension.Interfaces;
using myMangaSiteExtension.Objects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace MangaHelpers
{
    [IExtensionDescription(
        Name = "MangaHelpers",
        URLFormat = "mangahelpers.com",
        RefererHeader = "http://mangahelpers.com/",
        RootUrl = "http://mangahelpers.com",
        Author = "James Parks",
        Version = "0.0.1",
        SupportedObjects = SupportedObjects.All,
        Language = "English")]
    public sealed class MangaHelpers : IDatabaseExtension
    {
        #region IExtesion
        private IExtensionDescriptionAttribute EDA;
        public IExtensionDescriptionAttribute ExtensionDescriptionAttribute
        { get { return EDA ?? (EDA = GetType().GetCustomAttribute<IExtensionDescriptionAttribute>(false)); } }

        private Icon extensionIcon;
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

        public bool Authenticate(NetworkCredential credentials, CancellationToken ct, IProgress<Int32> ProgressReporter)
        {
            if (IsAuthenticated) return true;
            throw new NotImplementedException();
        }

        public void Deauthenticate()
        {
            if (!IsAuthenticated) return;
            Cookies = null;
            IsAuthenticated = false;
        }

        public List<MangaObject> GetUserFavorites()
        {
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

        public SearchRequestObject GetSearchRequestObject(String searchTerm)
        {
            return new SearchRequestObject()
            {
                Url = String.Format("{0}/manga/browse/?q={1}", ExtensionDescriptionAttribute.RootUrl, Uri.EscapeUriString(searchTerm)),
                Method = SearchMethod.GET,
                Referer = ExtensionDescriptionAttribute.RefererHeader,
            };
        }

        public DatabaseObject ParseDatabaseObject(String content)
        {
            HtmlDocument DatabaseObjectDocument = new HtmlDocument();
            DatabaseObjectDocument.LoadHtml(content);

            HtmlNode ContentNode = DatabaseObjectDocument.GetElementbyId("content"),
                InformationNode = ContentNode.SelectSingleNode(".//div[contains(@class,'subtabbox')]"),
                CoverNode = InformationNode.SelectSingleNode(".//div[2]/img"),
                DetailsNode = InformationNode.SelectSingleNode(".//table[contains(@class,'details')]"),
                SummaryNode = DatabaseObjectDocument.GetElementbyId("summary").SelectSingleNode(".//p"),
                TagsNode = DatabaseObjectDocument.GetElementbyId("tags").SelectSingleNode(".//p"),
                LocationNode = ContentNode.SelectSingleNode(".//div[contains(@class,'tab selected')]/a");

            String Name = InformationNode.SelectSingleNode(".//h1").InnerText;
            List<String> Genres = (from String Tag in TagsNode.InnerText.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries) select Tag.Trim()).ToList(),
                AlternateNames = new List<String>(),
                Staff = new List<String>();
            Int32 ReleaseYear = 0;

            LocationObject Location = new LocationObject()
            {
                ExtensionName = ExtensionDescriptionAttribute.Name,
                ExtensionLanguage = ExtensionDescriptionAttribute.Language,
                Url = String.Format("{0}{1}", ExtensionDescriptionAttribute.RootUrl, LocationNode.Attributes["href"].Value),
            };

            foreach (HtmlNode DetailNode in DetailsNode.SelectNodes(".//tr"))
            {
                String DetailType = DetailNode.SelectSingleNode(".//td[1]").InnerText.Trim().TrimEnd(':'),
                    DetailValue = DetailNode.SelectSingleNode(".//td[2]").InnerText.Trim();
                switch (DetailType)
                {
                    default:
                    case "":
                        break;

                    case "Original title":
                    case "Alternative Titles":
                        foreach (String value in DetailValue.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            if (!AlternateNames.Contains(value)) AlternateNames.Add(HtmlEntity.DeEntitize(value));
                        break;

                    case "Writer(s)":
                    case "Artist(s)":
                        foreach (String value in DetailValue.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            if (!Staff.Contains(value)) Staff.Add(HtmlEntity.DeEntitize(value));
                        break;

                    case "Year":
                        Int32.TryParse(DetailValue, out ReleaseYear);
                        break;
                }
            }

            List<LocationObject> Covers = new List<LocationObject>();
            if (CoverNode != null) Covers.Add(new LocationObject() {
                Url = String.Format("{0}{1}", ExtensionDescriptionAttribute.RootUrl, CoverNode.Attributes["src"].Value),
                ExtensionName = ExtensionDescriptionAttribute.Name,
                ExtensionLanguage = ExtensionDescriptionAttribute.Language
            });

            return new DatabaseObject()
            {
                Name = HtmlEntity.DeEntitize(Name),
                AlternateNames = AlternateNames,
                Covers = Covers,
                Description = HtmlEntity.DeEntitize(SummaryNode.InnerText),
                Genres = Genres,
                Locations = { Location },
                Staff = Staff,
                ReleaseYear = ReleaseYear,
            };
        }

        public List<DatabaseObject> ParseSearch(String content)
        {
            HtmlDocument DatabaseObjectDocument = new HtmlDocument();
            DatabaseObjectDocument.LoadHtml(content);
            HtmlWeb HtmlWeb = new HtmlWeb();
            HtmlNode TableResultsNode = DatabaseObjectDocument.GetElementbyId("results");
            if (TableResultsNode.InnerText.Contains("No results")) return new List<DatabaseObject>();
            return (from HtmlNode MangaNode 
                    in TableResultsNode.SelectNodes(".//tr")
                    where MangaNode.SelectSingleNode(".//td[1]/a") != null
                    select ParseDatabaseObject(HtmlWeb.Load(String.Format("{0}{1}",
                        ExtensionDescriptionAttribute.RootUrl,
                        MangaNode.SelectSingleNode(".//td[1]/a").Attributes["href"].Value)).DocumentNode.OuterHtml)
                    ).ToList();
        }

        List<SearchResultObject> IExtension.ParseSearch(string Content)
        { throw new NotImplementedException("Database extensions return DatabaseObjects"); }
    }
}