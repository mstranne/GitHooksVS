using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace GitHooksVS
{
    /// <summary>
    /// Interaction logic for HookManageFormControl.
    /// </summary>
    public partial class HookManageFormControl : UserControl, INotifyPropertyChanged
    {
        private string _githooksStatusText;
        private Brush _githooksStatusColor;
        private HookManageFormViewModel _viewModel;

        /// <summary>
        /// Event that is raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the status text for the .githooks folder.
        /// </summary>
        public string GithooksStatusText
        {
            get => _githooksStatusText;
            set
            {
                if (_githooksStatusText != value)
                {
                    _githooksStatusText = value;
                    OnPropertyChanged(nameof(GithooksStatusText));
                }
            }
        }

        /// <summary>
        /// Gets or sets the status color for the .githooks folder.
        /// </summary>
        public Brush GithooksStatusColor
        {
            get => _githooksStatusColor;
            set
            {
                if (_githooksStatusColor != value)
                {
                    _githooksStatusColor = value;
                    OnPropertyChanged(nameof(GithooksStatusColor));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HookManageFormControl"/> class.
        /// </summary>
        public HookManageFormControl()
        {
            InitializeComponent();

            DataContext = this;
            InitializeForm();
        }

        /// <summary>
        /// Initializes the form and updates the .githooks folder status.
        /// </summary>
        public void InitializeForm()
        {
            bool isAvailable = GitHookFolderManager.Instance.GitRootFolder != null;

            GithooksStatusText = isAvailable ? ".githooks folder available" : ".githooks folder not available";
            GithooksStatusColor = isAvailable ? Brushes.Green : Brushes.Red;

            _viewModel = new HookManageFormViewModel();
            DataContext = _viewModel;


            foreach(var hook in GitHookManager.HookScriptNameLookup)
            {
                var checkboxList = new CheckboxList(hook.Value); // Use the script name as the header
                List<ScriptEntry> scripts = ConfigManager.Instance.GetScriptEntries(hook.Key);
                foreach (var script in scripts)
                {
                    var checkboxItem = new CheckboxItem
                    {
                        Text = script.FilePath,
                        IsChecked = script.Enabled
                    };
                    checkboxList.Items.Add(checkboxItem);
                    checkboxItem.OnCheckedChanged += (item, isChecked) =>
                    {
                        script.Enabled = isChecked;
                        ConfigManager.Instance.UpdateScriptEntry(hook.Key, script.FilePath, isChecked);
                        List<string> updated_scripts = ConfigManager.Instance.GetEnabledScriptEntries(hook.Key).Select(s => s.FilePath).ToList();
                        GitHookManager.CreateGitHookScript(updated_scripts, GitHookFolderManager.Instance.GetCurrentHookFolder(), hook.Key);
                    };
                }

                if (scripts.Count > 0)
                {
                    _viewModel.CheckboxLists.Add(checkboxList);
                }
            }

            // Callback-Event für Änderungen
            foreach (var list in _viewModel.CheckboxLists)
            {
                foreach (var item in list.Items)
                {
                    item.OnCheckedChanged += OnCheckboxChanged;
                }
            }
        }

        private void OnCheckboxChanged(CheckboxItem item, bool isChecked)
        {
            MessageBox.Show($"Script '{item.Text}' is now {(isChecked ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Notifies the UI of property changes.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handles click on the button by closing the parent window.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            // Find the parent window and close it
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
    }


    public class CheckboxItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private string _text;

        /// <summary>
        /// Wird ausgelöst, wenn sich eine Eigenschaft ändert.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gibt an, ob die Checkbox aktiviert ist.
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                    OnCheckedChanged?.Invoke(this, _isChecked);
                }
            }
        }

        /// <summary>
        /// Der Text, der neben der Checkbox angezeigt wird.
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        /// <summary>
        /// Callback-Event, das ausgelöst wird, wenn die Checkbox aktiviert/deaktiviert wird.
        /// </summary>
        public event Action<CheckboxItem, bool> OnCheckedChanged;

        /// <summary>
        /// Benachrichtigt die UI über Änderungen an Eigenschaften.
        /// </summary>
        /// <param name="propertyName">Der Name der geänderten Eigenschaft.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CheckboxList
    {
        /// <summary>
        /// Die Überschrift der Liste.
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        /// Die Liste der Checkbox-Elemente.
        /// </summary>
        public ObservableCollection<CheckboxItem> Items { get; set; }

        public CheckboxList(string header)
        {
            Header = header;
            Items = new ObservableCollection<CheckboxItem>();
        }
    }




    public class HookManageFormViewModel
    {
        /// <summary>
        /// Eine Sammlung von Listen, die Checkboxen und Überschriften enthalten.
        /// </summary>
        public ObservableCollection<CheckboxList> CheckboxLists { get; set; }

        public HookManageFormViewModel()
        {
            CheckboxLists = new ObservableCollection<CheckboxList>();
        }

        /// <summary>
        /// Fügt eine neue Liste von Checkboxen hinzu.
        /// </summary>
        /// <param name="list">Die Checkbox-Liste, die hinzugefügt werden soll.</param>
        public void AddCheckboxList(CheckboxList list)
        {
            CheckboxLists.Add(list);
        }
    }
}

