using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using PipeTreeV4;

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

        public List<List<Node>> GetManifoldBranches(Autodesk.Revit.DB.Document doc, Node node, PipeSystemType pipeSystemType)
        {

            List<List<Node>> branches = new List<List<Node>>();
            //var connectors = node.Connectors;
            ElementId elementId = node.ElementId;
            PipeSystemType systemtype;
            string shortsystemname;

            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                if (pipeSystemType == systemtype)
                {
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);


                }

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();

            }


            var nconnectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;

            foreach (Connector connect in nconnectors)
            {
                systemtype = connect.PipeSystemType;
                if (pipeSystemType == systemtype)
                {
                    ConnectorSet connectorSet = connect.AllRefs;
                    foreach (Connector nextconnector in connectorSet)
                    {
                        List<Node> branch = new List<Node>();
                        if (doc.GetElement(nextconnector.Owner.Id) is PipingSystem)
                        {
                            continue;
                        }
                        else if (connect.Owner.Id == nextconnector.Owner.Id)
                        {
                            continue; // Игнорируем те же элементы
                        }

                        else if (connect.Domain == Autodesk.Revit.DB.Domain.DomainHvac || connect.Domain == Autodesk.Revit.DB.Domain.DomainPiping)
                        {
                            if (pipeSystemType==PipeSystemType.SupplyHydronic && nextconnector.Direction == FlowDirectionType.In)
                            {
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname);
                                branch.Add(newnode);
                                branches.Add(branch);
                            }
                            else if (pipeSystemType == PipeSystemType.ReturnHydronic && nextconnector.Direction == FlowDirectionType.Out)
                            {
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname);
                                branch.Add(newnode);
                                branches.Add(branch);
                            }

                        }


                    }
                }

            }

            return branches;
        }
            
         public List<List<Node>>GetSecondaryBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, List<List<Node>> mainnodes)
        {
            List<Node> branch = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                    branch.Add(newnode);
                }
            }
            Node lastnode = null;
            do
            {
                lastnode = branch.Last(); // Get the last added node
                


                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                    branch.Add(newnode); // Add the new node to the nodes list
                    mainnodes.Add(branch);
                }
                catch
                {
                    break;
                }

            }
            while (lastnode.NextOwnerId != null);

            // Continue while NextOwnerId is not null
            return mainnodes;
        }
    


        public List<List<Node>> GetBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, List<List<Node>> mainnodes)
        {
            List<Node> branch = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                    branch.Add(newnode);
                }
            }
                Node lastnode = null;

            do
            {
                lastnode = branch.Last(); // Get the last added node
                if (lastnode.Connectors.Count>=2)
                {
                    var manifoldbranches = GetManifoldBranches(doc, lastnode, lastnode.PipeSystemType);
                    foreach(var manifoldbranch in manifoldbranches)
                    {
                        foreach (var node in manifoldbranch)
                        {
                            //branch.AddRange(GetSecondaryBranch(doc, node.ElementId, mainnodes));
                            GetSecondaryBranch(doc,node.ElementId,mainnodes);
                        }
                    }
                    break;
                }


                    try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                    branch.Add(newnode); // Add the new node to the nodes list
                    mainnodes.Add(branch);
                }
                catch
                {
                    break;
                }

            }
            while (lastnode.NextOwnerId != null);
           
            // Continue while NextOwnerId is not null
            return mainnodes;
        }

        public (List<List<Node>>, List<CustomConnector>) GetBranches (Autodesk.Revit.DB.Document doc,    ElementId elementId)
        {
            List<List<Node>> mainnodes = new List<List<Node>>();
            List<CustomConnector> additionalNodes = new List<CustomConnector>();
            List<Node> mainnode = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                mainnode.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                    mainnode.Add(newnode);

                }
            }

            
        
                Node lastnode = null;
               
                do
                {
                    lastnode = mainnode.Last(); // Get the last added node
                    PipeSystemType systemtype2;
                    string shortsystemname2;
                        if (doc.GetElement(elementId) is Pipe)
                        {
                            systemtype2 = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                            shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                            Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname);
                            mainnode.Add(newnode);

                        }
                        else
                        {
                            shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                            var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                            foreach (Connector connector in connectors)
                            {
                                systemtype2 = connector.PipeSystemType;
                                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname);
                                mainnode.Add(newnode);

                            }
                        }
                    if (lastnode.Connectors.Count>=4)
                        {

                        
                          var manifoldbranches = GetManifoldBranches(doc, lastnode, lastnode.PipeSystemType);
                          foreach(var manifoldbranch in manifoldbranches)
                          {
                            foreach (var node in manifoldbranch)
                            {
                                GetBranch(doc, node.ElementId,mainnodes);
                            }
                            
                          }
                          mainnodes.AddRange(manifoldbranches);
                          additionalNodes = mainnodes.SelectMany(innerList => innerList    .SelectMany(node => node.Connectors        .Where(connector => connector.IsSelected==false))).ToList(); 

                    break;  
                        
                    }
                    else
                    {
                        try
                        {
                            var nextElement = doc.GetElement(lastnode.NextOwnerId);
                            Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                            mainnode.Add(newnode); // Add the new node to the nodes list
                        }
                        catch
                        {
                            break;
                        }
                    }
                   
                   
                }
                while (lastnode.NextOwnerId != null) ;
                mainnodes.Add(mainnode);
                
               
            // Continue while NextOwnerId is not null
            return (mainnodes, additionalNodes);
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
            List<List<Node>> mainnodes = new List<List<Node>>(); // тут стояк 
            List<List<Node>> branches = new List<List<Node>>();
            List<CustomConnector> additionalNodes = new List<CustomConnector>();
            PipeSystemType systemtype;
            string shortsystemname;
           
            foreach (var startelement in startelements)
            {
                (mainnodes, additionalNodes) = GetBranches(doc, startelement);

               
            }

            foreach (var additionalNode in additionalNodes)
            {
                continue;
            }
            




                
            





            
           
            






            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                foreach (var node in mainnode)
                {
                    totalids.Add(node.ElementId);
                }
                
            }
           /* foreach (var branch in newbranches)
            {
                foreach (var node in branch)
                {
                    totalids.Add(node.ElementId);
                }
            }*/
            uIDocument.Selection.SetElementIds(totalids);

            // Я докопался до сбора данных с трубы. Завтра продолжу копать по поиску остальных элементов
            // Что-то вроде рекурсии по обращению к последнему элементу в списке
            // Тут надо брать элемент и проверять два попавших коннектора у кого расход больше 


            return Result.Succeeded;
        }
    }
}
