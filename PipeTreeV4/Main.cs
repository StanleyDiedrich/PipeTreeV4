using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using PipeTreeV4;

namespace PipeTreeV4
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class Main : IExternalCommand
    {
        static AddInId AddInId = new AddInId(new Guid("7DAFFD0C-8A70-4D30-A0C4-AD878D4BF2DC"));


        public List<List<Node>> RemoveDuplicatesByElementId(List<List<Node>> nodeLists)
        {
            return nodeLists
         .Select(nodeList => nodeList
             .GroupBy(n => n.ElementId) // Группируем по ElementId
             .Select(g => g.First()) // Берём первый элемент из каждой группы
             .ToList()) // Преобразуем в список
         .ToList(); // Преобразуем весь результат в список
        }
        public ElementId GetStartPipe(Autodesk.Revit.DB.Document document, string selectedSystemNumber)
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
        public List<Branch> GetNewManifoldBranches(Autodesk.Revit.DB.Document doc, Node node, PipeSystemType pipeSystemType)
        {
            List<Branch> branches = new List<Branch>();
            
            //List<List<Node>> branches = new List<List<Node>>();
            //var connectors = node.Connectors;
            ElementId elementId = node.ElementId;
            bool isock = node.IsOCK;
            PipeSystemType systemtype;
            string shortsystemname;

            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                if (pipeSystemType == systemtype)
                {
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname);
                    node.IsOCK = isock;
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
                        Branch branch = new Branch();
                        //List<Node> branch = new List<Node>();
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
                            if (pipeSystemType == PipeSystemType.SupplyHydronic && nextconnector.Direction == FlowDirectionType.In)
                            {
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname);
                                newnode.IsOCK = isock;
                                branch.Add(newnode);
                                branches.Add(branch);
                            }
                            else if (pipeSystemType == PipeSystemType.ReturnHydronic && nextconnector.Direction == FlowDirectionType.Out)
                            {
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname);
                                newnode.IsOCK = isock;
                                branch.Add(newnode);
                                branches.Add(branch);
                            }

                        }


                    }
                }

            }
            // тут нашли стартовые элементы ветвей после коннектора

            return branches;
        }

        
        

        public Branch GetNewSecondaryBranch(Autodesk.Revit.DB.Document doc, ElementId elementId) // пришли с 377 строки
        {
            Branch branch = new Branch();
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
                lastnode = branch.Nodes.Last();
                
                
                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                    
                    branch.Add(newnode); // Add the new node to the nodes list
                   
                }
                catch
                {
                    break;
                }

            }
            while (lastnode.NextOwnerId != null);

            branch.GetPressure();
            // тут мы просто собрали ветвь и вернули ее 
            
           
            return branch;
        }

        public Branch GetNewManifoldBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnodes )
        {
            Branch branch = new Branch();
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
                lastnode = branch.Nodes.Last(); // Get the last added node
               


                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                    mainnodes.Add(newnode); // Add the new node to the nodes list
                     //
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

        public Branch GetNewBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnode, List<Branch>mainnodes)
        {
           Branch branch = new Branch();
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
            Node secnode = null;
            do
            {
                lastnode = branch.Nodes.Last(); // Get the last added node

               
               // secnode = lastnode;
                if (lastnode.Connectors.Count >= 2)
                {

                    
                    

                    

                    break;
                    
                }


                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname);
                    branch.Add(newnode); // Add the new node to the nodes list
                   // mainnodes.Add(branch); //
                }
                catch
                {
                    break;
                }

            }
            while (lastnode.NextOwnerId != null);

            
            return branch;
        }

        public (List<Branch>, Branch) GetNewBranches(Autodesk.Revit.DB.Document doc, ElementId elementId)
        {
            List<Branch> mainnodes = new List<Branch>();
            Branch additionalNodes = new Branch();
            Branch mainnode = new Branch();
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
                lastnode = mainnode.Nodes.Last(); // Get the last added node
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
                if (lastnode.Connectors.Count >= 3)
                {
                    foreach (var node in mainnode.Nodes)
                    {
                        node.IsOCK = true;
                    }
                    List<Branch> manbranches = new List<Branch>();
                    //Отсюда ушел разбираться с коллекторами
                    var manifoldbranches = GetNewManifoldBranches(doc, lastnode, lastnode.PipeSystemType); 
                    
                    foreach (var manifoldbranch in manifoldbranches)
                    {
                        Branch manbranch = new Branch();

                        foreach (var node in manifoldbranch.Nodes)
                        {
                            
                            // GetNewBranch(doc, node.ElementId, mainnodes);
                            manbranch=GetNewBranch(doc, node.ElementId, manbranch, mainnodes);
                            manbranch.GetPressure();
                            manbranches.Add(manbranch);
                        }
                        
                    }

                    

                    Branch selectedbranch = new Branch();
                    double maxpressure = double.MinValue;
                    foreach (var manifoldbranch in manbranches)
                    {
                        double pressure = manifoldbranch.DPressure;
                        if (pressure>maxpressure)
                        {
                            selectedbranch = manifoldbranch;
                            maxpressure = pressure;
                            
                        }
                    }
                    selectedbranch.IsOCK = true;
                    selectedbranch.OCKCheck();

                    // Тут мы дошли только до первого уровня и тут покажутся коллекторы поквартирные.


                    var secondarymanifolds = manbranches.SelectMany(x => x.Nodes).Select(x => x).Where(x => x.IsManifold == true).ToList();
                    List<Branch> selectedsecondarybranches = new List<Branch>();
                    Branch selectedsecondbranch = new Branch();
                    foreach (var node in secondarymanifolds)
                    {
                        var secondarymanifoldbranches = GetNewManifoldBranches(doc, node, node.PipeSystemType);
                       
                        
                        foreach (var secondmanifoldbranch in secondarymanifoldbranches)
                        {
                            foreach (var secnode in secondmanifoldbranch.Nodes)
                            {
                                var secondbranch = GetNewSecondaryBranch(doc, secnode.ElementId);
                                selectedsecondarybranches.Add(secondbranch);
                            }
                            
                        }
                        
                        
                       
                        
                    }
                    selectedsecondbranch = selectedsecondarybranches.OrderByDescending(x => x.DPressure).FirstOrDefault(); 
                    if (selectedsecondbranch != null)
                    {
                        selectedsecondbranch.IsOCK = true;
                        selectedsecondbranch.OCKCheck();
                        mainnodes.AddRange(manbranches);
                        mainnodes.AddRange(selectedsecondarybranches);
                    }
                    else
                    {
                        break;
                    }
                    

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
            while (lastnode.NextOwnerId != null);
            mainnodes.Add(mainnode);

            var additionalConnectors = mainnodes.SelectMany(branch => branch.Nodes)
            .SelectMany(node => node.Connectors) 
            .Where(connector => connector.IsSelected == false) 
            .ToList();
            List<ElementId> additionalElements = additionalConnectors.Select(x => x.NextOwnerId).ToList();

            foreach (var addel in additionalElements)
            {
                if (doc.GetElement(addel) is Pipe)
                {
                    systemtype = ((doc.GetElement(addel) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(addel) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname);
                    additionalNodes.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(addel) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(addel) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname);
                        additionalNodes.Add(newnode);

                    }
                }
                
            }

            // Continue while NextOwnerId is not null
            return (mainnodes, additionalNodes);
        }







        public void SaveFile(string content) // спрятали функцию сохранения 
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.Title = "Save CSV File";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        writer.Write(content);
                    }

                    Console.WriteLine("CSV file saved successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving CSV file: " + ex.Message);
                }
            }
        }

        public string GetContent (Autodesk.Revit.DB.Document doc, List<Branch>mainnodes)
        {
            List<ElementId> checkednodes = new List<ElementId>();
            string csvcontent = "";
            int branchcounter = 0;
            foreach (var mainnode in mainnodes)
            {
                int counter = 0;
                foreach (var node in mainnode.Nodes)
                {
                    if (checkednodes.Contains(node.ElementId))
                    {
                        continue;
                    }
                    else
                    {
                        ModelElement modelelement = new ModelElement(doc, node, branchcounter, counter);
                        checkednodes.Add(node.ElementId);
                        string a = $"{modelelement.ModelElementId};{modelelement.ModelTrack};{modelelement.ModelLvl};{modelelement.ModelBranchNumber};{modelelement.ModelTrackNumber};{modelelement.ModelName};{modelelement.ModelDiameter};{modelelement.ModelLength};{modelelement.ModelVolume};{modelelement.Type.ToString()};{modelelement.ModelTrack}-{modelelement.ModelLvl}-{modelelement.ModelBranchNumber}-{modelelement.ModelTrackNumber}\n";
                        csvcontent += a;
                        counter++;
                    }



                }
                branchcounter++;
            }
            return csvcontent;
        }
        public void SelectBranches (UIDocument uidoc, List<Branch> mainnodes)
        {
            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                foreach (var node in mainnode.Nodes)
                {
                    totalids.Add(node.ElementId);
                }
            }

            uidoc.Selection.SetElementIds(totalids);
        }

        public void SelectNodes(UIDocument uidoc, List<Branch> mainnodes)
        {
            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                foreach (var node in mainnode.Nodes)
                {
                    if (node.IsOCK==true)
                    {
                        totalids.Add(node.ElementId);
                    }
                   
                }
            }

            uidoc.Selection.SetElementIds(totalids);
        }
        public void SelectAdditionalNodes(UIDocument uidoc, List<Branch> mainnodes)
        {
            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                foreach (var node in mainnode.Nodes)
                {
                    if (node.IsOCK == false)
                    {
                        totalids.Add(node.ElementId);
                    }

                }
            }

            uidoc.Selection.SetElementIds(totalids);
        }
        public void SelectAllNodes(UIDocument uidoc, List<Branch> mainnodes)
        {
            List<ElementId> totalids = new List<ElementId>();
            foreach (var mainnode in mainnodes)
            {
                foreach (var node in mainnode.Nodes)
                {
                    
                    
                        totalids.Add(node.ElementId);
                    

                }
            }

            uidoc.Selection.SetElementIds(totalids);
        }

        public List<Branch> AlgorithmQuartierCollectorsAndRayPipes (Autodesk.Revit.DB.Document doc, List<ElementId> startelements)
        {
            List<Branch> mainnodes = new List<Branch>(); // тут стояк 
            List<Branch> secondarynodes = new List<Branch>();
            List<Branch> secondarySupernodes = new List<Branch>();
            List<Branch> branches = new List<Branch>();
            Branch additionalNodes = new Branch();
            Branch secAdditionalNodes = new Branch();
            List<ModelElement> modelElements = new List<ModelElement>();
            PipeSystemType systemtype;
            string shortsystemname;

            foreach (var startelement in startelements)
            {
                (mainnodes, additionalNodes) = GetNewBranches(doc, startelement);


            }

            //mainnodes = RemoveDuplicatesByElementId(mainnodes);

           // Тут удалил проход по всем элементам mainnodes 
            foreach (var branch in mainnodes)
            {
                foreach (var node in branch.Nodes)
                {
                    if (node.Connectors.Count == 2 && node.Connectors.Any(x => x.IsSelected == false) && node.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                    {
                        additionalNodes.Add(node);
                    }
                }
            }


            // Это пока не трогаем, потому что не понятно что там с другими
            // Разбираемся с GetNewBranches
            var totalIds = new HashSet<int>();
            foreach (var startelement in additionalNodes.Nodes)
            {
                var nextStartelement = startelement.ElementId;
                (secondarynodes, secAdditionalNodes) = GetNewBranches(doc, nextStartelement);

                foreach (var secondarynode in secondarynodes)
                {
                    Branch branch = new Branch();
                    foreach (var node in secondarynode.Nodes)
                    {
                        if (totalIds.Add(node.ElementId.IntegerValue))
                        {
                            branch.Add(node);
                        }
                    }
                    // Добавляем только уникальные элементы
                    if (branch.Nodes.Count != 0)
                    {
                        secondarySupernodes.Add(branch);
                    }
                    else
                    { continue; }

                }
            }


                mainnodes.AddRange(secondarySupernodes);
            return mainnodes;
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

            UserControl1 window = new UserControl1();
            MainViewModel mainViewModel = new MainViewModel(doc,window, sysNums);
           
            window.DataContext = mainViewModel;
            window.ShowDialog();



            List<ElementId> elIds = new List<ElementId>();
            var systemnames = mainViewModel.SystemNumbersList.Select(x => x).Where(x => x.IsSelected == true);
            //var systemelements = mainViewModel.SystemElements;

            List<ElementId> startelements = new List<ElementId>();

            //Ну тут вроде норм
            foreach (var systemname in systemnames)
            {
                string systemName = systemname.SystemName;

                var maxpipe = GetStartPipe(doc, systemName);
                startelements.Add(maxpipe);

            }
            // Это можно не трогать

           



            // 
            List<Branch> mainnodes= AlgorithmQuartierCollectorsAndRayPipes(doc, startelements);
            
            
            string csvcontent = GetContent(doc, mainnodes);
            //SaveFile(csvcontent);
            //SelectBranches(uIDocument, mainnodes);
           SelectNodes(uIDocument, mainnodes);
            //SelectAllNodes(uIDocument, mainnodes);
            //SelectAdditionalNodes(uIDocument, mainnodes);





            return Result.Succeeded;
        }
    }
}