using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

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
            var foundedelements = GetElements(Document, SystemNumbersList);
            SystemElements = GetSystemElements(foundedelements);
        }

        private List<SystemElement> systemElements; 
        public List<SystemElement> SystemElements
        {
            get { return systemElements; }
            set
            {
                systemElements = value;
                OnPropertyChanged("SystemElements");
            }
        }






        public List<Element> GetElements (Autodesk.Revit.DB.Document document, ObservableCollection<SystemNumber> SystemNumbersList)
        {
            List<Element> foundedelements= new List<Element>();
            foreach (var systemnumber in SystemNumbersList )
            {
                var mechEquip = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElements();
                var pipes = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements();
                var fittings = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType().ToElements();
                var armatura = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipeAccessory).WhereElementIsNotElementType().ToElements();

                foreach (var meq in mechEquip)
                {
                    var fI = meq as FamilyInstance;

                    if (fI.LookupParameter("Имя системы").AsString().Contains(systemnumber.SystemName))
                    {
                        foundedelements.Add(meq);
                    }
                }

                foreach (var pipe in pipes)
                {
                    var newpipe = pipe as Pipe;
                    var fI = newpipe as MEPCurve;
                    if (fI.LookupParameter("Имя системы").AsString().Contains(systemnumber.SystemName))
                    {
                        foundedelements.Add(pipe);
                    }
                   
                }
                foreach (var fit in fittings)
                {
                    var fI = fit as FamilyInstance;

                    if (fI.LookupParameter("Имя системы").AsString().Contains(systemnumber.SystemName))
                    {
                        foundedelements.Add(fit);
                    }
                }

                foreach(var arm in armatura)
                {
                    var fI =arm as FamilyInstance;

                    if (fI.LookupParameter("Имя системы").AsString().Contains(systemnumber.SystemName))
                    {
                        foundedelements.Add(arm);
                    }
                    
                }
            }

            return foundedelements;
        }

        public List<SystemElement> GetSystemElements (List<Element> elements)
        {
            List<SystemElement> systemElements = new List<SystemElement>();
            foreach (var element in elements)
            {
                SystemElement systemElement = new SystemElement(element);
                systemElements.Add(systemElement);
            }
            return systemElements;
        }







       
            public MainViewModel (Autodesk.Revit.DB.Document doc, ObservableCollection<SystemNumber> systemNumbers)
        {
            Document = doc;
            SystemNumbersList = systemNumbers;
            ShowSelectedSystemsCommand = new RelayCommand(ShowSelectedSystems);
            SystemElements = new List<SystemElement>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
