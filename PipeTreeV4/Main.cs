using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace PipeTreeV4
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class Main : IExternalCommand
    {
        static AddInId AddInId = new AddInId(new Guid("7DAFFD0C-8A70-4D30-A0C4-AD878D4BF2DC"));
        public ElementId GetStartPipe(Autodesk.Revit.DB.Document document, string selectedSystemNumber )
        {
            ElementId startPipe = null;
            List<Element> pipes = new List<Element>();
            List<Element> syspipes = new List<Element>();
            pipes = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements().ToList();

            foreach (var pipe in pipes)
            {
                var newpipe = pipe as Pipe;
                var fI = newpipe as MEPCurve;
                if (fI.LookupParameter("Сокращение для системы").AsString().Equals(selectedSystemNumber))
                {
                    syspipes.Add(pipe);
                }
            }

            double maxflow = -100000000;
            Element startpipe = null;
            foreach (var pipe in syspipes)
            {
                var flow = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble();
                if (flow > maxflow)
                {
                    startpipe = pipe;
                    maxflow = flow;
                }
            }
            startPipe = startpipe.Id;

            return startPipe;

        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uIDocument = uiapp.ActiveUIDocument;
            Autodesk.Revit.DB.Document doc = uIDocument.Document;

            List<string> systemnumbers = new List<string>();
            IList<Element> pipes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeCurves).WhereElementIsNotElementType().ToElements();
            foreach (Element pipe in pipes)
            {
                var newpipe = pipe as Pipe;
                try
                {
                    if (newpipe != null)
                    {
                        if (!systemnumbers.Contains(newpipe.LookupParameter("Сокращение для системы").AsString()))
                        {
                            systemnumbers.Add(newpipe.LookupParameter("Сокращение для системы").AsString());
                        }

                    }

                }
               
                catch (Exception ex)
                {
                    TaskDialog.Show("Revit", ex.ToString());
                }


            }
            ObservableCollection<SystemNumber> sysNums = new ObservableCollection<SystemNumber>();
            foreach (var systemnumber in systemnumbers)
            {
                SystemNumber system = new SystemNumber(systemnumber);
                sysNums.Add(system);
            }

            MainViewModel mainViewModel = new MainViewModel(doc, sysNums);
            UserControl1 window = new UserControl1();
            window.DataContext = mainViewModel;
            window.ShowDialog();

            List<ElementId> elIds = new List<ElementId>();
            

            var systemnames = mainViewModel.SystemNumbersList.Select(x=>x).Where(x=>x.IsSelected==true);
            //var systemelements = mainViewModel.SystemElements;

            List<ElementId> startelements = new List<ElementId>();
            foreach (var systemname in systemnames)
            {
                string systemName = systemname.SystemName;
               
                    var maxpipe = GetStartPipe(doc, systemName);
                    startelements.Add(maxpipe);
                
            }
            List<Node> mainnodes = new List<Node>(); // тут стояк 
            List<List<Node>> branches = new List<List<Node>>();
            foreach (var startelement in startelements)
            {
                // Get the system type from the starting element
                var systemtype = ((doc.GetElement(startelement) as Pipe).MEPSystem as PipingSystem).SystemType;
                var shortsystemname = (doc.GetElement(startelement) as Pipe).LookupParameter("Сокращение для системы").AsString();
                // Create a Node from the starting element
                Node node = new Node(doc, doc.GetElement(startelement), systemtype,shortsystemname);
                mainnodes.Add(node);
                Node lastnode = null;
                // Start a do-while loop to traverse the next elements
                do
                {
                    lastnode = mainnodes.Last(); // Get the last added node
                    
                    if (lastnode.ConnectorList.Count>0)
                    {
                        int branchnumber = lastnode.ConnectorList.Count;
                        List <Node> branch = new List<Node>();
                        do
                        {
                            try
                            {
                                var nextElement = doc.GetElement(lastnode.NextOwnerId);
                                Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                                branch.Add(newnode); // Add the new node to the nodes list
                                nextElement = doc.GetElement(branch.Last().ElementId);
                            }
                            catch
                            { break; }
                            branches.Add(branch);
                            branchnumber--;
                        }
                        while (branchnumber != 0);
                       
                    }
                    try
                    {
                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                        mainnodes.Add(newnode); // Add the new node to the nodes list
                    }
                    catch
                    {
                        break;
                    }
                     // Get the next element using the NextOwnerId

                    // Exit if there isn't a next element

                    // Create a new Node from the next element while preserving the pipe system type
                   

                } 
                while (lastnode.NextOwnerId != null ); // Continue while NextOwnerId is not null
            }

            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                totalids.Add(mainnode.ElementId);
            }
            foreach (var branch in branches)
            {
                foreach (var node in branch)
                {
                    totalids.Add(node.ElementId);
                }
            }
            uIDocument.Selection.SetElementIds(totalids);

            // Я докопался до сбора данных с трубы. Завтра продолжу копать по поиску остальных элементов
            // Что-то вроде рекурсии по обращению к последнему элементу в списке
            // Тут надо брать элемент и проверять два попавших коннектора у кого расход больше 


            return Result.Succeeded;
        }
    }
}
