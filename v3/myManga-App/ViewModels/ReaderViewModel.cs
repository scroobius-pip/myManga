﻿using Core.IO;
using Core.IO.Storage.Manager.BaseInterfaceClasses;
using Core.MVVM;
using Core.Other.Singleton;
using myManga_App.IO.Network;
using myManga_App.IO.ViewModel;
using myManga_App.Objects;
using myMangaSiteExtension.Objects;
using myMangaSiteExtension.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace myManga_App.ViewModels
{
    public sealed class ReaderViewModel : BaseViewModel
    {
        #region Variables
        private Boolean ContinueReading { get; set; }

        private Boolean PreloadingNext { get; set; }
        private Boolean PreloadingPrev { get; set; }

        private MangaObject _MangaObject;
        public MangaObject MangaObject
        {
            get { return _MangaObject; }
            set { SetProperty(ref this._MangaObject, value); }
        }

        private ChapterObject _ChapterObject;
        public ChapterObject ChapterObject
        {
            get { return _ChapterObject; }
            set { SetProperty(ref this._ChapterObject, value); }
        }
        private ChapterObject PrevChapterObject { get; set; }
        private ChapterObject NextChapterObject { get; set; }

        private PageObject _SelectedPageObject;
        public PageObject SelectedPageObject
        {
            get { return _SelectedPageObject; }
            set
            {
                SetProperty(ref this._SelectedPageObject, value);
                if (value != null) SaveBookmarkObject();
            }
        }

        private BookmarkObject _BookmarkObject;
        public BookmarkObject BookmarkObject
        {
            get { return _BookmarkObject; }
            set { SetProperty(ref this._BookmarkObject, value); }
        }

        private String _ArchiveFilePath;
        public String ArchiveFilePath
        {
            get { return _ArchiveFilePath; }
            set { SetProperty(ref this._ArchiveFilePath, value); }
        }
        private String PrevArchiveFilePath { get; set; }
        private String NextArchiveFilePath { get; set; }

        public String ArchiveImageURL
        { get { return this.SelectedPageObject.ImgUrl; } }

        private Double _PageZoom;
        public Double PageZoom
        {
            get { return _PageZoom; }
            set { SetProperty(ref this._PageZoom, value < 0.5 ? 0.5 : value); }
        }
        #endregion

        #region Commands
        #region NextPage
        private DelegateCommand _NextPageCommand;
        public ICommand NextPageCommand
        { get { return _NextPageCommand ?? (_NextPageCommand = new DelegateCommand(NextPage, CanNextPage)); } }

        private void NextPage()
        {
            if (this.BookmarkObject.Page < this.ChapterObject.Pages.Last().PageNumber) ++this.BookmarkObject.Page;
            else { this.ContinueReading = true; OpenChapter(this.MangaObject, this.NextChapterObject); }
            this.SelectedPageObject = this.ChapterObject.PageObjectOfBookmarkObject(this.BookmarkObject);
        }
        private Boolean CanNextPage()
        {
            Boolean n_page = this.BookmarkObject.Page < this.ChapterObject.Pages.Last().PageNumber;
            if (this.NextArchiveFilePath != null && File.Exists(this.NextArchiveFilePath)) n_page = true;
            return n_page;
        }
        #endregion

        #region PrevPage
        private DelegateCommand _PrevPageCommand;
        public ICommand PrevPageCommand
        { get { return _PrevPageCommand ?? (_PrevPageCommand = new DelegateCommand(PrevPage, CanPrevPage)); } }

        private void PrevPage()
        {
            if (this.BookmarkObject.Page > this.ChapterObject.Pages.First().PageNumber) --this.BookmarkObject.Page;
            else { this.ContinueReading = true; OpenChapter(this.MangaObject, this.PrevChapterObject); }
            this.SelectedPageObject = this.ChapterObject.PageObjectOfBookmarkObject(this.BookmarkObject);
        }
        private Boolean CanPrevPage()
        {
            Boolean p_page = this.BookmarkObject.Page > this.ChapterObject.Pages.First().PageNumber;
            if (this.PrevArchiveFilePath != null && File.Exists(this.PrevArchiveFilePath)) p_page = true;
            return p_page;
        }
        #endregion

        #region Reset PageZoom
        private DelegateCommand _ResetPageZoomCommand;
        public ICommand ResetPageZoomCommand
        { get { return _ResetPageZoomCommand ?? (_ResetPageZoomCommand = new DelegateCommand(ResetPageZoom)); } }

        private void ResetPageZoom()
        { this.PageZoom = App.UserConfig.DefaultPageZoom; }
        #endregion
        #endregion

        private void OpenChapter(ReadChapterRequestObject ReadChapterRequest)
        { this.PageZoom = App.UserConfig.DefaultPageZoom; OpenChapter(ReadChapterRequest.MangaObject, ReadChapterRequest.ChapterObject); }

        private void OpenChapter(MangaObject MangaObject, ChapterObject ChapterObject)
        {
            //Messenger.Default.Send(this, "FocusRequest");
            this.PullFocus();

            this.MangaObject = MangaObject;
            this.ChapterObject = ChapterObject;
            this.PrevChapterObject = this.MangaObject.PrevChapterObject(this.ChapterObject);
            this.NextChapterObject = this.MangaObject.NextChapterObject(this.ChapterObject);

            String base_path = Path.Combine(App.CHAPTER_ARCHIVE_DIRECTORY, MangaObject.MangaFileName());
            this.ArchiveFilePath = Path.Combine(base_path, ChapterObject.ChapterArchiveName(App.CHAPTER_ARCHIVE_EXTENSION));
            try { this.PrevArchiveFilePath = Path.Combine(base_path, PrevChapterObject.ChapterArchiveName(App.CHAPTER_ARCHIVE_EXTENSION)); }
            catch { this.PrevArchiveFilePath = null; }
            try { this.NextArchiveFilePath = Path.Combine(base_path, NextChapterObject.ChapterArchiveName(App.CHAPTER_ARCHIVE_EXTENSION)); }
            catch { this.NextArchiveFilePath = null; }

            PreloadingPrev = PreloadingNext = false;

            PreloadChapters();
            LoadChapterObject();
            LoadBookmarkObject();
            this.ContinueReading = false;
        }

        public ReaderViewModel()
            : base()
        {
            this.ContinueReading = false;
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                this.PageZoom = App.UserConfig.DefaultPageZoom;
                Messenger.Default.RegisterRecipient<FileSystemEventArgs>(this, ChapterObjectArchiveWatcher_Event, "ChapterObjectArchiveWatcher");
                Messenger.Default.RegisterRecipient<ReadChapterRequestObject>(this, OpenChapter, "ReadChapterRequest");
            }
            else
            {
                this.PageZoom = 1;
            }
        }

        #region Methods
        public void PreloadChapters()
        {
            if (!PreloadingPrev && !File.Exists(this.PrevArchiveFilePath))
            { DownloadManager.Default.Download(this.MangaObject, this.PrevChapterObject); this.PreloadingPrev = true; }
            if (!PreloadingNext && !File.Exists(this.NextArchiveFilePath))
            { DownloadManager.Default.Download(this.MangaObject, this.NextChapterObject); this.PreloadingNext = true; }
        }

        private void LoadChapterObject()
        {
            Stream archive_file;
            if (Singleton<ZipStorage>.Instance.TryRead(this.ArchiveFilePath, out archive_file, typeof(ChapterObject).Name))
            { using (archive_file) this.ChapterObject = archive_file.Deserialize<ChapterObject>(SaveType: App.UserConfig.SaveType); }
        }

        private void LoadBookmarkObject()
        {
            Stream bookmark_file;
            if (Singleton<ZipStorage>.Instance.TryRead(Path.Combine(App.MANGA_ARCHIVE_DIRECTORY, this.MangaObject.MangaArchiveName(App.MANGA_ARCHIVE_EXTENSION)), out bookmark_file, typeof(BookmarkObject).Name))
            { using (bookmark_file) this.BookmarkObject = bookmark_file.Deserialize<BookmarkObject>(SaveType: App.UserConfig.SaveType); }
            if (this.BookmarkObject == null) { this.BookmarkObject = new BookmarkObject(); }
            else if (this.BookmarkObject.Volume != this.ChapterObject.Volume || this.BookmarkObject.Chapter != this.ChapterObject.Chapter || this.BookmarkObject.SubChapter != this.ChapterObject.SubChapter)
            {
                this.BookmarkObject.Volume = this.ChapterObject.Volume;
                this.BookmarkObject.Chapter = this.ChapterObject.Chapter;
                this.BookmarkObject.SubChapter = this.ChapterObject.SubChapter;
                if (this.ContinueReading && this.BookmarkObject.Page <= 1) this.BookmarkObject.Page = this.ChapterObject.Pages.Last().PageNumber;
                else this.BookmarkObject.Page = this.ChapterObject.Pages.First().PageNumber;
            }
            this.SelectedPageObject = this.ChapterObject.PageObjectOfBookmarkObject(this.BookmarkObject);
        }

        private void SaveBookmarkObject()
        {
            if (this.ChapterObject != null)
            {
                this.BookmarkObject.Volume = this.ChapterObject.Volume;
                this.BookmarkObject.Chapter = this.ChapterObject.Chapter;
                this.BookmarkObject.SubChapter = this.ChapterObject.SubChapter;
                this.BookmarkObject.Page = this.ChapterObject.Pages.First().PageNumber;
            }
            if (this.SelectedPageObject != null) this.BookmarkObject.Page = this.SelectedPageObject.PageNumber;
            Singleton<ZipStorage>.Instance.Write(
                Path.Combine(App.MANGA_ARCHIVE_DIRECTORY, this.MangaObject.MangaArchiveName(App.MANGA_ARCHIVE_EXTENSION)),
                typeof(BookmarkObject).Name,
                this.BookmarkObject.Serialize(SaveType: App.UserConfig.SaveType));
        }
        #endregion

        #region Event Handlers
        private void ChapterObjectArchiveWatcher_Event(FileSystemEventArgs e)
        {
            // TODO: This needs to be fixed
            if (e.FullPath.Equals(ArchiveFilePath))
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Changed:
                        Stream archive_file;
                        if (Singleton<ZipStorage>.Instance.TryRead(e.FullPath, out archive_file, typeof(ChapterObject).Name))
                        { using (archive_file) { this.ChapterObject = archive_file.Deserialize<ChapterObject>(SaveType: App.UserConfig.SaveType); } }
                        break;

                    case WatcherChangeTypes.Deleted:
                        DownloadManager.Default.Download(this.MangaObject, this.ChapterObject);
                        break;
                }
        }
        #endregion
    }
}
