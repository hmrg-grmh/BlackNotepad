﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;
using Savaged.BlackNotepad.Lookups;
using Savaged.BlackNotepad.Models;
using Savaged.BlackNotepad.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Savaged.BlackNotepad.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly IList<string> _busyRegister;
        private readonly OpenFileDialog _openFileDialog;
        private readonly SaveFileDialog _saveFileDialog;
        private readonly IViewStateService _viewStateService;
        private readonly IList<FontZoomModel> _fontZoomIndex;
        private readonly int _defaultZoom;
        private readonly FindDialogViewModel _findDialog;
        private readonly ReplaceDialogViewModel _replaceDialog;
        private FileModel _selectedItem;
        private string _selectedText;
        private string _textSought;
        private bool _isFontZoomMin;
        private bool _isFontZoomMax;
        private int _caretLine;
        private int _caretColumn;

        public MainViewModel(
            IDialogService dialogService,
            IViewStateService viewStateService,
            IFontColourLookupService fontColourLookupService,
            IFontFamilyLookupService fontFamilyLookupService,
            IFontZoomLookupService fontZoomLookupService)
        {
            if (dialogService is null)
            {
                throw new ArgumentNullException(nameof(dialogService));
            }
            if (viewStateService is null)
            {
                throw new ArgumentNullException(nameof(viewStateService));
            }
            if (fontColourLookupService is null)
            {
                throw new ArgumentNullException(nameof(fontColourLookupService));
            }
            if (fontFamilyLookupService is null)
            {
                throw new ArgumentNullException(nameof(fontFamilyLookupService));
            }
            if (fontZoomLookupService is null)
            {
                throw new ArgumentNullException(nameof(fontZoomLookupService));
            }

            _dialogService = dialogService;

            const string filter = "Text Documents|*.txt";
            _openFileDialog = _dialogService.GetDialog<OpenFileDialog>();
            _openFileDialog.Filter = filter;

            _saveFileDialog = _dialogService.GetDialog<SaveFileDialog>();
            _saveFileDialog.Filter = filter;

            _viewStateService = viewStateService;
            ViewState = _viewStateService.Open();

            FontColours = fontColourLookupService.GetIndex();

            FontFamilyNames = fontFamilyLookupService.GetIndex();

            _fontZoomIndex = fontZoomLookupService.GetIndex();
            _defaultZoom = fontZoomLookupService.GetDefault().Zoom;
            _isFontZoomMin =
                ViewState.SelectedFontZoom.Key == _fontZoomIndex.First().Key;
            _isFontZoomMax =
                ViewState.SelectedFontZoom.Key == _fontZoomIndex.Last().Key;

            _busyRegister = new List<string>();
            _selectedItem = new FileModel();

            NewCmd = new RelayCommand(OnNew, () => CanExecute);
            OpenCmd = new RelayCommand(OnOpen, () => CanExecuteOpen);
            SaveCmd = new RelayCommand(OnSave, () => CanExecute);
            SaveAsCmd = new RelayCommand(OnSaveAs, () => CanExecute);
            ExitCmd = new RelayCommand(OnExit, () => CanExecute);
            FindCmd = new RelayCommand(OnFind, () => CanExecute);
            FindNextCmd = new RelayCommand(OnFindNext, () => CanExecuteFindNext);
            ReplaceCmd = new RelayCommand(OnReplace, () => CanExecuteReplace);
            GoToCmd = new RelayCommand(OnGoTo, () => CanExecute);
            TimeDateCmd = new RelayCommand(OnTimeDate, () => CanExecute);
            WordWrapCmd = new RelayCommand(OnWordWrap, () => CanExecute);
            ZoomInCmd = new RelayCommand(OnZoomIn, () => CanExecuteZoomIn);
            ZoomOutCmd = new RelayCommand(OnZoomOut, () => CanExecuteZoomOut);
            RestoreDefaultZoomCmd = new RelayCommand(
                OnRestoreDefaultZoom, () => CanExecute);
            StatusBarCmd = new RelayCommand(OnStatusBar, () => CanExecute);
            HelpCmd = new RelayCommand(OnHelp, () => CanExecute);
            AboutCmd = new RelayCommand(OnAbout, () => CanExecute);
            FontColourCmd = new RelayCommand<FontColourModel>(
                OnFontColour, (b) => CanExecute);
            FontFamilyCmd = new RelayCommand<FontFamilyModel>(
                OnFontFamily, (b) => CanExecute);

            _findDialog = _dialogService
                .GetDialogViewModel<FindDialogViewModel>();
            _findDialog.FindNextRaisedByDialog += OnFindNextRaisedByDialog;

            _replaceDialog = _dialogService
                .GetDialogViewModel<ReplaceDialogViewModel>();
            _replaceDialog.FindNextRaisedByDialog += OnFindNextRaisedByDialog;
            _replaceDialog.ReplaceRaisedByDialog += OnReplaceRaisedByDialog;
            _replaceDialog.ReplaceAllRaisedByDialog += OnReplaceAllRaisedByDialog;

            _findDialog.PropertyChanged += OnFindDialogPropertyChanged;
            _replaceDialog.PropertyChanged += OnFindDialogPropertyChanged;

            SelectedItem.PropertyChanged += OnSelectedItemPropertyChanged;
        }

        public override void Cleanup()
        {
            _replaceDialog.ReplaceAllRaisedByDialog -= OnReplaceAllRaisedByDialog;
            _replaceDialog.ReplaceRaisedByDialog -= OnReplaceRaisedByDialog;
            _replaceDialog.FindNextRaisedByDialog -= OnFindNextRaisedByDialog;
            _findDialog.FindNextRaisedByDialog -= OnFindNextRaisedByDialog;
            _replaceDialog.PropertyChanged -= OnFindDialogPropertyChanged;
            _findDialog.PropertyChanged -= OnFindDialogPropertyChanged;
            SelectedItem.PropertyChanged -= OnSelectedItemPropertyChanged;
            base.Cleanup();
        }

        public bool OnClosing()
        {
            var hasChangesToSave = SaveChangesConfirmation();
            if (hasChangesToSave)
            {
                Save();
            }
            _viewStateService.Save(ViewState);
            return true;
        }

        public void New(string location = null)
        {
            var hasChangesToSave = SaveChangesConfirmation();
            if (hasChangesToSave)
            {
                Save();
            }
            SelectedItem = new FileModel(location);
        }

        public string Title => $"{SelectedItem?.Name} - Black Notepad";

        public bool IsBusy => _busyRegister.Count > 0;

        public ViewStateModel ViewState { get; }

        public IList<FontColourModel> FontColours { get; }

        public IList<FontFamilyModel> FontFamilyNames { get; }

        public FileModel SelectedItem
        {
            get => _selectedItem;
            set
            {
                Set(ref _selectedItem, value);
                RaisePropertyChanged(nameof(Title));
            }
        }

        public string TextSought
        {
            get => _textSought;
            set => Set(ref _textSought, value);
        }

        public string SelectedText
        {
            get => _selectedText;
            set
            {
                Set(ref _selectedText, value);
                RaisePropertyChanged(nameof(IsCutOrCopyEnabled));
                RaisePropertyChanged(nameof(IsPasteEnabled));
            }
        }

        public int CaretLine
        {
            get => _caretLine;
            set
            {
                Set(ref _caretLine, value + 1);
                RaisePropertyChanged(nameof(CaretPosition));
            }
        }

        public int CaretColumn
        {
            get => _caretColumn;
            set
            {
                Set(ref _caretColumn, value + 1);
                RaisePropertyChanged(nameof(CaretPosition));
            }
        }

        public string CaretPosition => $"Ln {CaretLine}, Col {CaretColumn}";

        public RelayCommand NewCmd { get; }
        public RelayCommand OpenCmd { get; }
        public RelayCommand SaveCmd { get; }
        public RelayCommand SaveAsCmd { get; }
        public RelayCommand ExitCmd { get; }
        public RelayCommand FindCmd { get; }
        public RelayCommand FindNextCmd { get; }
        public RelayCommand ReplaceCmd { get; }
        public RelayCommand GoToCmd { get; }
        public RelayCommand<int> GoToLineCmd { get; }
        public RelayCommand TimeDateCmd { get; }
        public RelayCommand WordWrapCmd { get; }
        public RelayCommand ZoomInCmd { get; }
        public RelayCommand ZoomOutCmd { get; }
        public RelayCommand RestoreDefaultZoomCmd { get; }
        public RelayCommand StatusBarCmd { get; }
        public RelayCommand HelpCmd { get; }
        public RelayCommand AboutCmd { get; }
        public RelayCommand<FontColourModel> FontColourCmd { get; }
        public RelayCommand<FontFamilyModel> FontFamilyCmd { get; }

        public bool CanExecute => !IsBusy;

        public bool CanExecuteOpen => CanExecute &&
            !SelectedItem.IsDirty;

        public bool CanExecuteSave => CanExecute &&
            SelectedItem.IsDirty;

        public bool CanExecuteZoomIn => CanExecute && !_isFontZoomMax;

        public bool CanExecuteZoomOut => CanExecute && !_isFontZoomMin;

        public bool CanExecuteFindNext => CanExecute &&
            !string.IsNullOrEmpty(TextSought);

        public bool CanExecuteReplace => CanExecute;

        public bool CanExecuteGoTo => CanExecute &&
            SelectedItem.HasContent && !ViewState.IsWrapped;

        public bool CanExecuteDragDrop => CanExecute &&
            !SelectedItem.IsDirty;


        public bool IsUndoEnabled => !IsBusy &&
            SelectedItem.IsDirty;

        public bool IsCutOrCopyEnabled => !IsBusy &&
            !string.IsNullOrEmpty(SelectedText);

        public bool IsPasteEnabled => !IsBusy &&
            Clipboard.ContainsText();

        public bool IsSelectAllEnabled => !IsBusy &&
            SelectedItem.HasContent;

        public Action<int> GoToRequested = delegate { };

        private void RaiseGoToRequested(int position)
        {
            var handler = GoToRequested;
            handler?.Invoke(position);
        }

        private void StartLongOperation([CallerMemberName]string caller = "")
        {
            _busyRegister.Add(caller);
            RaisePropertyChanged(nameof(IsBusy));
        }

        private void EndLongOpertation([CallerMemberName]string caller = "")
        {
            _busyRegister.Remove(caller);
            RaisePropertyChanged(nameof(IsBusy));
        }

        private void OnNew()
        {
            New();
        }

        private void OnOpen()
        {
            var result = _openFileDialog.ShowDialog();
            if (result == true)
            {
                StartLongOperation();

                SelectedItem = new FileModel(_openFileDialog.FileName)
                {
                    IsDirty = false
                };

                EndLongOpertation();
            }
        }

        private void OnSave()
        {
            Save();
        }
        private void Save()
        {
            if (!SelectedItem.IsNew)
            {
                StartLongOperation();

                File.WriteAllText(
                    SelectedItem.Location, SelectedItem.Content);

                SelectedItem.IsDirty = false;

                EndLongOpertation();
            }
            else
            {
                SaveAs();
            }
        }

        private void OnSaveAs()
        {
            SaveAs();
        }
        private void SaveAs()
        {
            var result = _saveFileDialog.ShowDialog();
            if (result == true)
            {
                SelectedItem.Location = _saveFileDialog.FileName;
                OnSave();
            }
        }

        private void OnExit()
        {
            OnClosing();
            Application.Current.Shutdown();
        }

        private void OnFind()
        {
            _dialogService.ShowDialog(_findDialog);
        }

        private void OnFindNext()
        {
            FindNext();
        }

        private void OnFindNextRaisedByDialog()
        {
            FindNext();
        }

        private void FindNext()
        {            
            // TODO Need to write this
        }

        private void OnReplace()
        {
            _dialogService.ShowDialog(_replaceDialog);
        }

        private void OnReplaceRaisedByDialog()
        {
            Replace();
        }

        private void OnReplaceAllRaisedByDialog()
        {
            ReplaceAll();
        }

        private void Replace()
        {
            // TODO Need to write this
        }

        private void ReplaceAll()
        {
            // TODO Need to write this
        }

        private void OnGoTo()
        {
            var vm = _dialogService
                .GetDialogViewModel<GoToDialogViewModel>();
            vm.LineNumber = CaretLine;
            var result = _dialogService.ShowDialog(vm);
            if (result == true)
            {
                var lineEndingChar = '\r';
                var skipsToNewLine = 1;
                switch (SelectedItem.LineEnding)
                {
                    case LineEndings.CRLF:
                        skipsToNewLine++;
                        break;
                    case LineEndings.LF:
                        lineEndingChar = '\n';
                        break;
                }
                var text = SelectedItem.Content;
                var linesCount = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == lineEndingChar)
                    {
                        linesCount++;
                        if (vm.LineNumber == linesCount)
                        {
                            var lineStartPosition = i + skipsToNewLine;
                            RaiseGoToRequested(lineStartPosition);
                            break;
                        }
                    }
                }
            }
        }

        private void OnTimeDate()
        {
            SelectedItem.Content += DateTime.Now.ToString("HH:mm yyyy-MM-dd");
        }

        private void OnWordWrap()
        {
            ViewState.IsWrapped = !ViewState.IsWrapped;
        }

        /// <summary>
        /// Ctrl+Plus
        /// </summary>
        private void OnZoomIn()
        {
            var current = ViewState.SelectedFontZoom;
            if (current is null)
            {
                RestoreDefaultZoom();
            }
            FontZoomModel value = null;
            var isCurrentFound = false;
            foreach (var fontZoom in _fontZoomIndex)
            {
                if (isCurrentFound)
                {
                    value = fontZoom;
                    current.IsSelected = false;
                    value.IsSelected = true;
                    break;
                }
                isCurrentFound = fontZoom.Key == current.Key;
            }
            if (value is null)
            {
                value = current;
                _isFontZoomMax = true;
            }
            _isFontZoomMin = false;
            ViewState.SelectedFontZoom = value;
        }

        /// <summary>
        /// Ctrl+Minus
        /// </summary>
        private void OnZoomOut()
        {
            var current = ViewState.SelectedFontZoom;
            if (current is null)
            {
                RestoreDefaultZoom();
            }
            FontZoomModel value = null;
            foreach (var fontZoom in _fontZoomIndex)
            {
                if (fontZoom.Key == current.Key)
                {
                    current.IsSelected = false;
                    break;
                }
                value = fontZoom;
            }
            if (value is null)
            {
                value = current;
                _isFontZoomMin = true;
            }
            _isFontZoomMax = false;
            value.IsSelected = true;
            ViewState.SelectedFontZoom = value;
        }

        private void OnRestoreDefaultZoom()
        {
            RestoreDefaultZoom();
        }
        private void RestoreDefaultZoom()
        {
            FontZoomModel @default = null;
            foreach (var fontZoom in _fontZoomIndex)
            {
                if (fontZoom.Key == _defaultZoom)
                {
                    @default = fontZoom;
                    @default.IsSelected = true;
                }
                else
                {
                    fontZoom.IsSelected = false;
                }
            }
            ViewState.SelectedFontZoom = @default;
            _isFontZoomMax = _isFontZoomMin = false;
        }

        private void OnStatusBar()
        {
            ViewState.IsStatusBarVisible = !ViewState.IsStatusBarVisible;
        }

        private void OnHelp()
        {
            StartLongOperation();

            Process.Start(
                "https://www.microsoft.com/en-gb/search?q=BlackNotepad");

            EndLongOpertation();
        }

        private void OnAbout()
        {
            _dialogService.ShowDialog(
                "A black 'version' of the classic Microsoft Windows Notepad application", 
                "About");
        }

        private void OnFontColour(FontColourModel selected)
        {
            ViewState.SelectedFontColour = selected;
            foreach (var fontColour in FontColours)
            {
                if (fontColour.Key != ViewState.SelectedFontColour.Key)
                {
                    fontColour.IsSelected = false;
                }
            }
            RaisePropertyChanged(nameof(FontColours));
        }

        private void OnFontFamily(FontFamilyModel selected)
        {
            ViewState.SelectedFontFamily = selected;
            foreach (var fontFamily in FontFamilyNames)
            {
                if (fontFamily.Key != ViewState.SelectedFontFamily.Key)
                {
                    fontFamily.IsSelected = false;
                }
            }
            RaisePropertyChanged(nameof(FontFamilyNames));
        }

        private bool SaveChangesConfirmation()
        {
            var value = false;
            if (SelectedItem.IsDirty)
            {
                var result = _dialogService.ShowDialog(
                    $"Do you want to save changes to {SelectedItem.Name}?",
                    "Black Notepad",
                    yesNoCancelButtons: true);
                value = result == true;
            }
            return value;
        }

        private void OnSelectedItemPropertyChanged(
            object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SelectedItem.Content):
                    RaisePropertyChanged(nameof(IsUndoEnabled));
                    RaisePropertyChanged(nameof(IsSelectAllEnabled));
                    break;
            }
        }

        private void OnFindDialogPropertyChanged(
            object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TextSought):
                    if (sender is FindDialogViewModel vm)
                    {
                        TextSought = vm?.TextSought;
                    }
                    break;
            }
        }
    }
}
