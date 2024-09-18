using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.Creation;

namespace PipeTreeV4
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<SystemNumber> _systemNumbersList;
        private SystemNumber _selectedSystemNumber;
        private Autodesk.Revit.DB.Document document;
        public Autodesk.Revit.DB.Document Document
        {
            get { return document; }
            set
            {
                document = value;
                OnPropertyChanged("Document");
            }
        }
        public ObservableCollection<SystemNumber> SystemNumbersList
        {
            get => _systemNumbersList;
            set
            {
                _systemNumbersList = value;
                OnPropertyChanged(nameof(SystemNumbersList));
            }
        }

        public SystemNumber SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                _selectedSystemNumber = value;
                OnPropertyChanged(nameof(SelectedSystemNumber));
            }
        }

        public MainViewModel()
        {
            // Заполнение примерными данными
            SystemNumbersList = new ObservableCollection<SystemNumber>();
       
        }
        private string _selectedSystems;
        public string SelectedSystems
        {
            get => _selectedSystems;
            set
            {
                _selectedSystems = value;
                OnPropertyChanged(nameof(SelectedSystems));
            }
        }

        public ICommand ShowSelectedSystemsCommand { get; }
        
        public void ShowSelectedSystems( object param)
        {
            var selectedItems = SystemNumbersList.Where(x => x.IsSelected).Select(x => x.SystemName).ToList();
            SelectedSystems = string.Join(", ", selectedItems);
        }

            /*public void DeleteSelectedItems()
            {
                // Снимаем выделение и удаляем выбранные элементы
                var selectedItems = SystemNumbersList.Where(x => x.IsSelected).ToList();
                foreach (var item in selectedItems)
                {
                    SystemNumbersList.Remove(item);
                }
            }*/

            public MainViewModel (Autodesk.Revit.DB.Document doc, ObservableCollection<SystemNumber> systemNumbers)
        {
            Document = doc;
            SystemNumbersList = systemNumbers;
            ShowSelectedSystemsCommand = new RelayCommand(ShowSelectedSystems);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
