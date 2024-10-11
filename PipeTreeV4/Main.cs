using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using PipeTreeV4;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

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
            bool mode = false;
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
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
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
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname, mode);
                                newnode.IsOCK = isock;
                                branch.Add(newnode);
                                branches.Add(branch);
                            }
                            else if (pipeSystemType == PipeSystemType.ReturnHydronic && nextconnector.Direction == FlowDirectionType.Out)
                            {
                                Node newnode = new Node(doc, doc.GetElement(nextconnector.Owner.Id), systemtype, shortsystemname, mode);
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
            bool mode = false;
            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;

                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);

            }
            else
            {

                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
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

                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);

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

        public Branch GetNewManifoldBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnodes)
        {
            bool mode = false;
            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
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
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
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
        public Node GetNextElemAfterEquipment(Document doc, Node lastnode)
        {
            bool mode = false;
            Node nxtnode = null;
            Element element = doc.GetElement(lastnode.ElementId);
            FamilyInstance familyInstance = element as FamilyInstance;
            MEPModel mEPModel = familyInstance.MEPModel;


            ConnectorSet connectorSet = mEPModel.ConnectorManager.Connectors;
            foreach (Connector connector in connectorSet)
            {
                ConnectorSet nextconnectors = connector.AllRefs;
                foreach (Connector nextconnector in nextconnectors)
                {
                    if (doc.GetElement(nextconnector.Owner.Id) is PipingSystem)
                    {
                        continue;
                    }
                    else if (nextconnector.Owner.Id == lastnode.ElementId)
                    {
                        continue; // Игнорируем те же элементы
                    }


                    /*else if (!doc.GetElement(nextconnector.Owner.Id).LookupParameter("Имя системы").AsString().Contains(SystemName))
                    {
                        continue;
                    }*/
                    else if (connectorSet.Size < 1)
                    {
                        continue;
                    }
                    else
                    {
                        if (nextconnector.Domain == Domain.DomainHvac || nextconnector.Domain == Domain.DomainPiping)
                        {
                            if (lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                            {
                                if (nextconnector.Direction == FlowDirectionType.In)
                                {
                                    PipeSystemType systemtype = PipeSystemType.ReturnHydronic;
                                    var nextelement = doc.GetElement(lastnode.ElementId);
                                    if (doc.GetElement(nextelement.Id) is Pipe)
                                    {
                                        //systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                                        string shortsystemname = (doc.GetElement(nextelement.Id) as Pipe).LookupParameter("Сокращение для системы").AsString();
                                        Node newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                        newnode1.NextOwnerId = nextconnector.Owner.Id;
                                        newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                        newnode1.Reverse = true;
                                        nxtnode = newnode1;
                                       

                                    }
                                    else
                                    {
                                        string shortsystemname = lastnode.ShortSystemName;
                                        Node newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                        newnode1.NextOwnerId = nextconnector.Owner.Id;
                                        newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                        newnode1.Reverse = true;
                                        nxtnode = newnode1;
                                        
                                    }

                                }
                            }
                        }
                    }
                }
            }
            return nxtnode;
        }
        public Node GetNextElemAfterEquipmentDeadEnd(Document doc, Node lastnode,Branch branch, bool mode)
        {
             mode = lastnode.Reverse;
            Node nxtnode = null;
            Element element = doc.GetElement(lastnode.ElementId);
            FamilyInstance familyInstance = element as FamilyInstance;
            MEPModel mEPModel = familyInstance.MEPModel;


            ConnectorSet connectorSet = mEPModel.ConnectorManager.Connectors;
            foreach (Connector connector in connectorSet)
            {
                ConnectorSet nextconnectors = connector.AllRefs;
                foreach (Connector nextconnector in nextconnectors)
                {
                    if (doc.GetElement(nextconnector.Owner.Id) is PipingSystem)
                    {
                        continue;
                    }
                    else if (nextconnector.Owner.Id == lastnode.ElementId)
                    {
                        continue; // Игнорируем те же элементы
                    }


                    /*else if (!doc.GetElement(nextconnector.Owner.Id).LookupParameter("Имя системы").AsString().Contains(SystemName))
                    {
                        continue;
                    }*/
                    else if (connectorSet.Size < 1)
                    {
                        continue;
                    }
                    else
                    {
                        
                        if (mode ==false)
                        {
                            if (nextconnector.Domain == Domain.DomainHvac || nextconnector.Domain == Domain.DomainPiping)
                            {
                                if (lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                                {
                                    if (nextconnector.Direction == FlowDirectionType.In)
                                    {
                                        PipeSystemType systemtype = PipeSystemType.ReturnHydronic;
                                        var nextelement = doc.GetElement(lastnode.ElementId);
                                        if (doc.GetElement(nextelement.Id) is Pipe)
                                        {
                                            //systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                                            string shortsystemname = (doc.GetElement(nextelement.Id) as Pipe).LookupParameter("Сокращение для системы").AsString();
                                            Node newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                            newnode1.NextOwnerId = nextconnector.Owner.Id;
                                            newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                            newnode1.Reverse = false;
                                            nxtnode = newnode1;
                                            if (branch.Nodes.Select(x => x.ElementId).Contains(nxtnode.ElementId))
                                            {
                                                if (nextconnector.Domain == Domain.DomainHvac || nextconnector.Domain == Domain.DomainPiping)
                                                {
                                                    if (lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                                                    {
                                                        if (nextconnector.Direction == FlowDirectionType.Out)
                                                        {
                                                             systemtype = PipeSystemType.ReturnHydronic;
                                                             nextelement = doc.GetElement(lastnode.ElementId);
                                                            if (doc.GetElement(nextelement.Id) is Pipe)
                                                            {
                                                                //systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                                                                 shortsystemname = (doc.GetElement(nextelement.Id) as Pipe).LookupParameter("Сокращение для системы").AsString();
                                                                 newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                                                newnode1.NextOwnerId = nextconnector.Owner.Id;
                                                                newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                                                newnode1.Reverse = true;
                                                                nxtnode = newnode1;


                                                            }
                                                            else
                                                            {
                                                                 shortsystemname = lastnode.ShortSystemName;
                                                                 newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                                                newnode1.NextOwnerId = nextconnector.Owner.Id;
                                                                newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                                                newnode1.Reverse = true;
                                                                nxtnode = newnode1;

                                                            }

                                                        }
                                                    }
                                                }
                                            }

                                        }
                                        else
                                        {
                                            string shortsystemname = lastnode.ShortSystemName;
                                            Node newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                            newnode1.NextOwnerId = nextconnector.Owner.Id;
                                            newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                            newnode1.Reverse = false;
                                            nxtnode = newnode1;
                                            if (branch.Nodes.Select(x => x.ElementId).Contains(nxtnode.ElementId))
                                            {
                                                if (nextconnector.Domain == Domain.DomainHvac || nextconnector.Domain == Domain.DomainPiping)
                                                {
                                                    if (lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                                                    {
                                                        if (nextconnector.Direction == FlowDirectionType.Out)
                                                        {
                                                            systemtype = PipeSystemType.ReturnHydronic;
                                                            nextelement = doc.GetElement(lastnode.ElementId);
                                                            if (doc.GetElement(nextelement.Id) is Pipe)
                                                            {
                                                                //systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                                                                shortsystemname = (doc.GetElement(nextelement.Id) as Pipe).LookupParameter("Сокращение для системы").AsString();
                                                                newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                                                newnode1.NextOwnerId = nextconnector.Owner.Id;
                                                                newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                                                newnode1.Reverse = true;
                                                                nxtnode = newnode1;


                                                            }
                                                            else
                                                            {
                                                                shortsystemname = lastnode.ShortSystemName;
                                                                newnode1 = new Node(doc, doc.GetElement(nextelement.Id), systemtype, shortsystemname, mode);
                                                                newnode1.NextOwnerId = nextconnector.Owner.Id;
                                                                newnode1.PipeSystemType = PipeSystemType.ReturnHydronic;
                                                                newnode1.Reverse = true;
                                                                nxtnode = newnode1;

                                                            }

                                                        }
                                                    }
                                                }
                                            }

                                        }

                                    }
                                }
                            }
                        }
                        else
                        {
                            
                        }
                        
                    }
                }
            }
            return nxtnode;
        }
        public Branch GetDeadEndBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnode, List<Branch> mainnodes)
        {
            Branch branch = new Branch();
            List<Node> controltees = new List<Node>();
            FilteredWorksetCollector collector = new FilteredWorksetCollector(doc);
            IList<Workset> worksets = collector.OfKind(WorksetKind.UserWorkset).ToWorksets();
            WorksetId selected_workset_id = WorksetId.InvalidWorksetId;

            foreach (var workset in worksets)
            {
                if (workset.Name == "(30)_ОВ1_27")
                {
                    selected_workset_id = workset.Id;
                }
            }
            double maxflow = 0;
            int tee_counter = 0;
            int equipment_counter = 0;
            
            PipeSystemType systemtype;
            string shortsystemname;
            string longsystemname = string.Empty;
            bool mode = false;
            Element nextelement = null;
            MEPSystem mepsystem = null;
            ElementSet elementSet = null;
            List<Element> sysfoundedtees = new List<Element>();
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                maxflow = (doc.GetElement(elementId) as Pipe).get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble(); // Добавил логику  для поиска максимального потока
                var longsystemname2 = (doc.GetElement(elementId) as Pipe).LookupParameter("Имя системы").AsValueString();
                if (longsystemname2 == string.Empty)
                {
                    longsystemname = longsystemname;
                }
                else
                {
                    longsystemname = longsystemname2;
                }
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);
                mepsystem = (doc.GetElement(elementId) as Pipe).MEPSystem;
                elementSet = (mepsystem as PipingSystem).PipingNetwork;
                foreach (Element element in elementSet)
                {
                    if (element != null)
                    {
                        if (element is FamilyInstance)
                        {
                            FamilyInstance fI = element as FamilyInstance;
                            MEPModel mepmodel = fI.MEPModel;
                            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                            {
                                if ((mepmodel as MechanicalFitting).PartType == PartType.Tee)
                                {
                                    sysfoundedtees.Add(element);
                                }
                            }

                        }
                    }
                }
            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                try
                {
                    var longsystemname2 = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Имя системы").AsValueString();
                    if (longsystemname2 == string.Empty)
                    {
                        longsystemname = longsystemname;
                    }
                    else
                    {
                        longsystemname = longsystemname2;
                    }
                }
                catch
                {
                    longsystemname = longsystemname;
                }

                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                Connector selectedconnector = null;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);

                    branch.Add(newnode);
                    maxflow = newnode.Connectors.Select(x => x).First().Flow; // Добавил логику  для поиска максимального потока
                    selectedconnector = connector;
                }
                mepsystem = selectedconnector.MEPSystem;
                elementSet = (mepsystem as PipingSystem).PipingNetwork;
                foreach (Element element in elementSet)
                {
                    if (element != null)
                    {
                        if (element is FamilyInstance)
                        {
                            FamilyInstance fI = element as FamilyInstance;
                            MEPModel mepmodel = fI.MEPModel;
                            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                            {
                                if ((mepmodel as MechanicalFitting).PartType == PartType.Tee)
                                {
                                    sysfoundedtees.Add(element);
                                }
                            }

                        }
                    }
                }

            }
            Node lastnode = null;
            Node secnode = null;
            Node VCK1_Node = null;
            Node VCK2_node = null;
            Node firsttee = null;
            Branch firstVCKBranch = new Branch();
            Branch secondVCKBranch = new Branch();
            List<Node> tees = new List<Node>();
            List<Node> nodetees = new List<Node>();
            do
            {

                lastnode = branch.Nodes.Last(); // Get the last added node
                



                // secnode = lastnode;
                try
                {
                    
                    if (lastnode.Element is FamilyInstance)
                    {

                        tee_counter = branch.Nodes.Select(x => x).Where(y => y.IsTee == true).Count();
                        if (lastnode.Connectors.Count >= 3)
                        {
                            if (lastnode.Reverse == true)
                            {
                                var nexteelement = GetManifoldReverseBranch(doc, lastnode, lastnode.PipeSystemType);
                                var newnode = new Node(doc, doc.GetElement(nexteelement.ElementId), nexteelement.PipeSystemType, shortsystemname, mode);

                                branch.Add(newnode);
                                lastnode = newnode;

                            }
                        }
                        if (lastnode.Element.LookupParameter("ADSK_Группирование").AsValueString()=="Воздухоотводчик")
                        {
                            lastnode = branch.Nodes.Select(x => x).Where(x => x.IsTee).Last();
                            var nextelemId = lastnode.Connectors.Where(x => x.IsSelected == false).Select(x => x.NextOwnerId).First();
                            var newnode = new Node(doc, doc.GetElement(nextelemId), lastnode.PipeSystemType, shortsystemname, mode);
                            branch.Add(newnode);
                            lastnode = newnode;
                        }


                        if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {

                            mode = true;
                            Node nxtnode = GetNextElemAfterEquipmentDeadEnd(doc, lastnode, branch, lastnode.Reverse );
                            branch.Add(nxtnode);
                            lastnode = nxtnode;


                        }
                        if (lastnode.IsTee)
                        {
                            



                            var nextelemId = lastnode.Connectors.Where(x => x.IsSelected).Select(x => x.NextOwnerId).First();
                                
                                var newnode = new Node(doc, doc.GetElement(nextelemId), lastnode.PipeSystemType, shortsystemname, mode);

                               
                                
                                branch.Add(newnode);
                            //var unselectedconnecter = branch.Nodes.Where(x => x.IsTee).Select(x => x).First().Connectors.Where(x => !x.IsSelected).Select(x => x.NextOwnerId).First();
                            //firsttee = new Node(doc, doc.GetElement(unselectedconnecter), lastnode.PipeSystemType, shortsystemname, mode);

                            //firsttee = branch.Nodes.Where(x => x.IsTee).Select(x => x).First();
                            //(firstVCKBranch, tees) = GetVCKBranchDeadEnd(doc, firsttee, branch);
                            lastnode = newnode;

                                

                        }




                    }
                }
                catch
                {

                }

                try
                {

                    if (lastnode == null)
                    {

                        break;
                    }
                    else
                    {
                        nextelement = doc.GetElement(lastnode.NextOwnerId);

                        Node newnode = null;

                        newnode = new Node(doc, nextelement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode);
                        // Add the new node to the nodes list
                        // mainnodes.Add(branch); //
                    }



                }
                catch
                {

                    break;
                }

                if (lastnode == null)
                {
                    branch.RemoveNull();
                    break;
                }
            }

            while (lastnode.NextOwnerId != null);
            branch.RemoveNull();

            var ftees = branch.Nodes.Where(x => x.IsTee && x.Connectors.All(c => c.Coefficient != 0)).ToList();


            foreach (var ftee in ftees)
            {

                var sconnector = ftee.Connectors
                    .Where(x => !x.IsSelected)
                    .Select(x => x.NextOwnerId)
                    .FirstOrDefault(); // Изменено на FirstOrDefault для обработка случаев, когда все коннекторы выбраны

                // Проводим проверку, если коннектор не найден (например, если все коннекторы выбраны)
                if (sconnector != null)
                {
                    // Создаем новый узел на основе найденного коннектора
                    Node snode = new Node(doc, doc.GetElement(sconnector), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                    if (branch.Nodes.Select(x=>x.ElementId).Contains(snode.ElementId))
                    {
                        snode = new Node(doc, doc.GetElement(sconnector), lastnode.PipeSystemType, lastnode.ShortSystemName,true);
                    }
                    // Получаем ветвь для тройника
                    (firstVCKBranch, tees) = GetVCKBranchDeadEnd(doc, snode, branch);
                    branch.AddRange(firstVCKBranch);

                    var node = firstVCKBranch.Nodes.Where(x => x.IsTee && x.IsSelected == false).Select(x => x).FirstOrDefault();
                    if (node!=null)
                    {
                     sconnector = node.Connectors
                    .Where(x => !x.IsSelected)
                    .Select(x => x.NextOwnerId)
                    .FirstOrDefault();

                        if (sconnector!=null)
                        {
                             snode = new Node(doc, doc.GetElement(sconnector), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                            if (branch.Nodes.Select(x => x.ElementId).Contains(snode.ElementId))
                            {
                                snode = new Node(doc, doc.GetElement(sconnector), lastnode.PipeSystemType, lastnode.ShortSystemName, true);
                            }
                            // Получаем ветвь для тройника
                            (secondVCKBranch, tees) = GetVCKBranchDeadEnd(doc, snode, branch);
                            branch.AddRange(secondVCKBranch);
                        }
                    }

                    // Добавляем новые элементы в ветвь
                   
                }
            }


            var foundedtees = branch.Nodes.Where(x => x.IsTee ).ToList();

            
            foreach (var tee in foundedtees)
            {
                if (tee.ElementId.IntegerValue == 3546856)
                {
                    Node tee2 = tee;
                }
                var connector = tee.Connectors.Where(x => !x.IsSelected).Select(x => x.NextOwnerId).FirstOrDefault();

                    if(connector!=null)
                {
                    Node snode = new Node(doc, doc.GetElement(connector), lastnode.PipeSystemType, lastnode.ShortSystemName, true);
                    Branch smallbranch = GetSmallBranch(doc, snode, branch);
                    branch.AddRange(smallbranch);
                }
                    //Node snode = new Node(doc, doc.GetElement(connector), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                    
                    
                
            }








            /*  foreach (var node in foundedtees)
              {
                  Branch smallbranch = GetSmallBranch(doc, node, branch);
                  branch.AddRange(smallbranch);
              }*/
            branch.RemoveNull();

            /*(firstVCKBranch, tees) = GetVCKBranchDeadEnd(doc, firsttee, branch);
            branch.AddRange(firstVCKBranch);*/
            return branch;
        }

        public Branch GetTihelmanBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnode, List<Branch> mainnodes)
        {
            List<Node> controltees = new List<Node>();
            FilteredWorksetCollector collector = new FilteredWorksetCollector(doc);
            IList<Workset> worksets = collector.OfKind(WorksetKind.UserWorkset).ToWorksets();
            WorksetId selected_workset_id=WorksetId.InvalidWorksetId;
            
            foreach (var workset in worksets)
            {
                if (workset.Name == "(30)_ОВ1_27")
                {
                    selected_workset_id = workset.Id;
                }
            }
            double maxflow = 0;
            int tee_counter = 0;
            int equipment_counter = 0;
            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            string longsystemname = string.Empty;
            bool mode = false;
            Element nextelement = null;
            MEPSystem mepsystem = null;
            ElementSet elementSet = null;
            List<Element> sysfoundedtees = new List<Element>();
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                maxflow = (doc.GetElement(elementId) as Pipe).get_Parameter(BuiltInParameter.RBS_PIPE_FLOW_PARAM).AsDouble(); // Добавил логику  для поиска максимального потока
                var longsystemname2 = (doc.GetElement(elementId) as Pipe).LookupParameter("Имя системы").AsValueString();
                if (longsystemname2 == string.Empty)
                {
                    longsystemname = longsystemname;
                }
                else
                {
                    longsystemname = longsystemname2;
                }
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);
                mepsystem = (doc.GetElement(elementId) as Pipe).MEPSystem;
                elementSet = (mepsystem as PipingSystem).PipingNetwork;
                foreach (Element element in elementSet)
                {
                    if (element != null)
                    {
                        if (element is FamilyInstance)
                        {
                            FamilyInstance fI = element as FamilyInstance;
                            MEPModel mepmodel = fI.MEPModel;
                            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                            {
                                if ((mepmodel as MechanicalFitting).PartType == PartType.Tee)
                                {
                                    sysfoundedtees.Add(element);
                                }
                            }

                        }
                    }
                }


            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                try
                {
                    var longsystemname2 = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Имя системы").AsValueString();
                    if (longsystemname2 == string.Empty)
                    {
                        longsystemname = longsystemname;
                    }
                    else
                    {
                        longsystemname = longsystemname2;
                    }
                }
                catch
                {
                    longsystemname = longsystemname;
                }

                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                Connector selectedconnector = null;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);

                    branch.Add(newnode);
                    maxflow = newnode.Connectors.Select(x => x).First().Flow; // Добавил логику  для поиска максимального потока
                    selectedconnector = connector;
                }
                mepsystem = selectedconnector.MEPSystem;
                elementSet = (mepsystem as PipingSystem).PipingNetwork;
                foreach (Element element in elementSet)
                {
                    if (element != null)
                    {
                        if (element is FamilyInstance)
                        {
                            FamilyInstance fI = element as FamilyInstance;
                            MEPModel mepmodel = fI.MEPModel;
                            if (element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                            {
                                if ((mepmodel as MechanicalFitting).PartType == PartType.Tee)
                                {
                                    sysfoundedtees.Add(element);
                                }
                            }

                        }
                    }
                }

            }

           


            var equipment = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElementIds();
           
            List<Element> systemequipment = new List<Element>();
            try
            {
                foreach (var equip in equipment)
                {
                    if (equip != null)
                    {
                        if (doc.GetElement(equip).Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {
                            if ((doc.GetElement(equip) as FamilyInstance).LookupParameter("Имя системы") != null)
                            {
                                try
                                {
                                    if (doc.GetElement(equip).get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).AsInteger() !=selected_workset_id.IntegerValue)
                                    {
                                        continue;
                                    }
                                     else if (doc.GetElement(equip).get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM).AsValueString().Contains(longsystemname))
                                    {
                                        systemequipment.Add(doc.GetElement(equip));
                                    }
                                    
                                }
                                catch { continue; }

                            }
                        }


                    }
                }
            }
            catch { }

            equipment_counter = sysfoundedtees.Count();
            Node lastnode = null;
            Node secnode = null;
            Node VCK1_Node = null;
            Node VCK2_node = null;
            Node firsttee = null;
            Branch firstVCKBranch = new Branch();
            Branch secondVCKBranch = new Branch();
            List<Node> tees = new List<Node>();
            List<Node> nodetees = new List<Node>();
            int teenumber = 0;
            foreach(var tee in sysfoundedtees)
            {
                Node nodetee = new Node(doc, doc.GetElement(tee.Id), PipeSystemType.ReturnHydronic, shortsystemname, false);
                nodetee.TeeNumber = teenumber;
                nodetees.Add(nodetee);
                teenumber++;
                
            }
            

            
            
            

            do
            {

                lastnode = branch.Nodes.Last(); // Get the last added node

                


                // secnode = lastnode;
                try
               {
                    if (lastnode.Element is FamilyInstance)
                    {
                        
                        tee_counter = branch.Nodes.Select(x => x).Where(y => y.IsTee == true).Count();
                        if (lastnode.Connectors.Count >= 4)
                        {
                            if (lastnode.Reverse == true)
                            {
                                var nexteelement = GetManifoldReverseBranch(doc, lastnode, lastnode.PipeSystemType);
                                var newnode = new Node(doc, doc.GetElement(nexteelement.ElementId), nexteelement.PipeSystemType, shortsystemname, mode);

                                branch.Add(newnode);
                                lastnode = newnode;

                            }
                        }

                        if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {

                            mode = true;
                            Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                            branch.Add(nxtnode);
                            lastnode = nxtnode;


                        }
                        if(lastnode.IsTee)
                        {
                            

                            double currentflow = lastnode.Connectors.Where(x => x.IsSelected).Select(x => x.Flow).First();
                            if (currentflow>maxflow/2)
                            {
                                var nextelemId = lastnode.Connectors.Where(x => x.IsSelected).Select(x => x.NextOwnerId).First();
                               
                                var newnode = new Node(doc, doc.GetElement(nextelemId), lastnode.PipeSystemType, shortsystemname, mode);
                               
                                firsttee = branch.Nodes.Where(x => x.IsTee).Select(x => x).First();
                                
                                branch.Add(newnode);
                                lastnode = newnode;
                               

                            }
                            else
                            {
                                var nextelemId = lastnode.Connectors.Where(x => !x.IsSelected).Select(x => x.NextOwnerId).First();
                                var newnode = new Node(doc, doc.GetElement(nextelemId), lastnode.PipeSystemType, shortsystemname, mode);
                                var lasttee = lastnode.Connectors.Where(x => x.IsSelected).Select(x => x.NextOwnerId).First();
                                VCK2_node = new Node(doc, doc.GetElement(lasttee), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                                branch.Add(newnode);
                                lastnode = newnode;
                            }
                           
                            
                            
                           
                        }

                       


                    }
                }
                catch
                {

                }
                
                try
                {

                    if (lastnode == null)
                    {
                        
                        break;
                    }
                    else
                    {
                        nextelement = doc.GetElement(lastnode.NextOwnerId);

                        Node newnode = null;



                        newnode = new Node(doc, nextelement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode);
                        // Add the new node to the nodes list
                                             // mainnodes.Add(branch); //
                    }

                    

                }
                catch
                {

                    break;
                }

                if (lastnode == null)
                { 
                    branch.RemoveNull();
                    break;
                }
            }
           
            while (lastnode.NextOwnerId != null);
            branch.RemoveNull();
            lastnode = branch.Nodes.Last();
            foreach (var node in branch.Nodes)
            {
                
                    if (node.PipeSystemType==PipeSystemType.SupplyHydronic)
                    {
                        if (node.IsTee)
                        {
                            if (!node.IsChecked)
                            {
                                tees.Add(node);
                            }

                        }
                    }
                   
            }





            (firstVCKBranch, tees) = GetVCKBranch(doc, firsttee, branch);
            (secondVCKBranch, tees) = GetSecondVCKBranch(doc, VCK2_node, branch);
            branch.AddRange(firstVCKBranch);
            branch.AddRange(secondVCKBranch);
            branch.RemoveNull();
           
            if (lastnode.IsTee && lastnode.PipeSystemType==PipeSystemType.SupplyHydronic)
            {
                tee_counter++;
            }

            List<Node> foundedtees = new List<Node>();
            foreach (var node in branch.Nodes)
            {
                if (node.IsTee && node!=null && node.PipeSystemType ==PipeSystemType.SupplyHydronic)
                {
                    
                        if (node.IsChecked == false)
                        {
                            foundedtees.Add(node);
                        }
                    
                }
            }
           

            foreach (var node in foundedtees)
            {
                
                Branch smallbranch = GetSmallBranch(doc, node, branch);
                branch.AddRange(smallbranch);
            }
               

            return branch;
        }
        private (Branch secondVCKBranch, List<Node> tees) GetSecondVCKBranchDeadEnd(Document doc, Node lasttee, Branch mainbranch)
        {
            double maxflow = 0;
            int counter = 0;
            Branch branch = new Branch();
            List<Node> tees = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            try
            {
                ElementId elementId = lasttee.ElementId;
                bool mode = false;
                branch.Add(lasttee);
                if (doc.GetElement(elementId) is Pipe)
                {
                    systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                        branch.Add(newnode);
                    }
                }
                Node lastnode = null;
                Node secnode = null;
                do
                {
                    lastnode = branch.Nodes.Last(); // Get the last added node

                    if (lastnode.Element is FamilyInstance)
                    {
                        if (lastnode.IsTee)
                        {
                            //CustomConnector selectedconnector = null;
                            var selectedConnector = lastnode.Connectors
                            .OrderByDescending(x => x.Coefficient) // Order by coefficient descending
                            .First(); // Get the highest one, or null if none found
                            var nextElement = doc.GetElement(selectedConnector.NextOwnerId);
                            Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                            branch.Add(newnode);
                            lastnode = newnode;
                        }



                        if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {
                            mode = true;
                            Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                            branch.Add(nxtnode);
                            lastnode = nxtnode;

                        }



                    }



                    try
                    {

                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode); 

                    }
                    catch
                    {
                        break;
                    }
                    counter++;
                    if (mainbranch.Nodes.Select(node => node.ElementId).Contains(lastnode.ElementId))
                    {
                        break;
                    }
                }
                while (lastnode.NextOwnerId != null || counter == 1000);
                Branch newbranch = new Branch();
                foreach (var node in branch.Nodes)
                {
                    if (!mainbranch.Nodes.Select(x => x.ElementId).Contains(node.ElementId))
                    {
                        node.IsChecked = false;
                        newbranch.Nodes.Add(node);
                    }
                }

                return (newbranch, tees);
            }

            catch
            {
                return (branch, tees);
            }




        }
        private (Branch secondVCKBranch, List<Node> tees) GetSecondVCKBranch(Document doc, Node lasttee, Branch mainbranch)
        {
            double maxflow = 0;
            int counter = 0;
            Branch branch = new Branch();
            List<Node> tees = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            try
            {
                ElementId elementId = lasttee.ElementId;
                bool mode = false;
                branch.Add(lasttee);
                if (doc.GetElement(elementId) is Pipe)
                {
                    systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                        branch.Add(newnode);
                    }
                }
                Node lastnode = null;
                Node secnode = null;
                do
                {
                    lastnode = branch.Nodes.Last(); // Get the last added node

                    if (lastnode.Element is FamilyInstance)
                    {
                        if (lastnode.IsTee)
                        {
                            //CustomConnector selectedconnector = null;
                            var selectedConnector = lastnode.Connectors
                            .OrderByDescending(x => x.Coefficient) // Order by coefficient descending
                            .First(); // Get the highest one, or null if none found
                            var nextElement = doc.GetElement(selectedConnector.NextOwnerId);
                            Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                            branch.Add(newnode);
                            lastnode = newnode;
                        }


                       
                        if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {
                            mode = true;
                            Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                            branch.Add(nxtnode);
                            lastnode = nxtnode;

                        }



                    }



                    try
                    {

                        /* double maxflow2 = double.MinValue;
                         CustomConnector selectedconnector = null;
                         foreach (var connector in lastnode.Connectors)
                         {
                             if (connector.Flow > maxflow2)
                             {
                                 selectedconnector = connector;
                             }
                         }*/
                        //var nextelId = selectedconnector.NextOwnerId;





                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode); // Add the new node to the nodes list




                        // mainnodes.Add(branch); //
                    }
                    catch
                    {
                        break;
                    }
                    counter++;
                    if (mainbranch.Nodes.Select(node => node.ElementId).Contains(lastnode.ElementId))
                    {
                        break;
                    }
                }
                while (lastnode.NextOwnerId != null || counter == 1000);
                Branch newbranch = new Branch();
                foreach (var node in branch.Nodes)
                {
                    if (!mainbranch.Nodes.Select(x => x.ElementId).Contains(node.ElementId))
                    {
                        node.IsChecked = false;
                        newbranch.Nodes.Add(node);
                    }
                }

                return (newbranch, tees);
            }
           
            catch
            {
                return (branch, tees);
            }

           

            
        }

        private Branch GetSmallBranch(Document doc, Node node,Branch mainbranch)
        {
            
            List<ElementId> checkedNodesIds = new List<ElementId>();
            foreach(var checknode in mainbranch.Nodes)
            {
                if (checknode!=null)
                {
                    checkedNodesIds.Add(checknode.ElementId);
                }
                
            }
           /* List<ElementId> secondVckBranchIds = new List<ElementId>();
            foreach (var checknode in secondVCKBranch.Nodes)
            {
                if (checknode != null)
                {
                    checkedNodesIds.Add(checknode.ElementId);
                }
            }*/

            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            CustomConnector customConnector = node.Connectors.Select(x => x).OrderByDescending(x => x.Coefficient).Last();
            ElementId elementId = customConnector.NextOwnerId;
            //ElementId elementId = node.Connectors.Where(y => !y.IsSelected).Select(x => x.NextOwnerId).First();

            bool mode=false;
            

            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);
                }
            }
            Node lastnode = null;
            Node secnode = null;

            do
            {

                lastnode = branch.Nodes.Last();
                
                if (lastnode.Element is FamilyInstance)
                {
                    if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                    {
                        mode = true;
                        Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                        branch.Add(nxtnode);
                        lastnode = nxtnode;
                        

                    }
                    else
                    {
                        try
                        {
                            var nextElement = doc.GetElement(lastnode.NextOwnerId);
                            Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                            branch.Add(newnode); // Add the new node to the nodes list
                                                 // mainnodes.Add(branch); //
                            //lastnode = newnode;
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
                else
                {
                    try
                    {
                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode); // Add the new node to the nodes list
                                             // mainnodes.Add(branch); //
                        //lastnode = newnode;
                    }
                    catch
                    {
                        break;
                    }
                }
                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                    branch.Add(newnode); // Add the new node to the nodes list
                                         // mainnodes.Add(branch); //
                                         //lastnode = newnode;
                }
                catch
                {
                    break;
                }
                if (lastnode == null)
                { break; }

                if (checkedNodesIds.Contains(lastnode.ElementId))
                {
                    break;
                }
                

            }
            

            while (lastnode.IsTee==true || lastnode!=null);
            return branch;

        }
        private (Branch, List<Node>) GetVCKBranchDeadEnd(Autodesk.Revit.DB.Document doc, Node vCK_node, Branch mainbranch)
        {
            int counter = 0;
            Branch branch = new Branch();
            List<Node> tees = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
              
                ElementId elementId = vCK_node.ElementId;
            
                bool mode = false;
                branch.Add(vCK_node);
                if (doc.GetElement(elementId) is Pipe)
                {
                    systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                        branch.Add(newnode);
                    }
                }
                Node lastnode = null;
                Node secnode = null;
                do
                {
                    lastnode = branch.Nodes.Last(); // Get the last added node

                    if (lastnode.Element is FamilyInstance)
                    {

                        if (lastnode.IsTee && lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                        {
                            try
                            {
                                var nextnodeEl = lastnode.Connectors
                                .Where(y => y.IsSelected) // Filter to get only unselected connectors
                                .Select(x => x.NextOwnerId) // Select OwnerId
                                .FirstOrDefault();
                                //ElementId nextnodeEl = null;
                                /*foreach (var connector in lastnode.Connectors)
                                {
                                    if ((mainbranch.Nodes.Select(node => node.ElementId).Contains(connector.NextOwnerId)))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        nextnodeEl = connector.NextOwnerId;
                                    }
                                }*/


                                Node nextnode = new Node(doc, doc.GetElement(nextnodeEl), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                                lastnode = nextnode;
                                branch.Add(lastnode);
                            }

                            catch { }


                        }



                        if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                        {
                            mode = true;
                            Node nxtnode = GetNextElemAfterEquipmentDeadEnd(doc, lastnode,branch, lastnode.Reverse);
                            //Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                            branch.Add(nxtnode);
                            lastnode = nxtnode;

                        }



                    }



                    try
                    {


                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode); // Add the new node to the nodes list


                    }
                    catch
                    {
                        break;
                    }
                    counter++;
                    if (mainbranch.Nodes.Select(node => node.ElementId).Contains(lastnode.ElementId))
                    {
                        break;
                    }
                }
                while (lastnode.NextOwnerId != null || counter == 1000);
                Branch newbranch = new Branch();
                foreach (var node in branch.Nodes)
                {
                    if (node != null)
                    {
                        if (!mainbranch.Nodes.Select(x => x.ElementId).Contains(node.ElementId))
                        {
                            node.IsChecked = false;
                            newbranch.Nodes.Add(node);
                        }
                    }

                }
                return (newbranch, tees);
            
           


           
        }
        private (Branch,List<Node>) GetVCKBranch(Autodesk.Revit.DB.Document doc,Node vCK_node, Branch mainbranch)
        {
            int counter = 0;
            Branch branch = new Branch();
            List<Node> tees = new List<Node>();
            PipeSystemType systemtype;
            string shortsystemname;
            ElementId elementId = vCK_node.ElementId;
            bool mode = false;
            branch.Add(vCK_node);
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);
                }
            }
            Node lastnode = null;
            Node secnode = null;
            do
            {
                lastnode = branch.Nodes.Last(); // Get the last added node
                
                if (lastnode.Element is FamilyInstance)
                {
                    
                        if (lastnode.IsTee && lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                        {
                            try
                            {
                               /* var nextnodeEl = lastnode.Connectors
                                .Where(y => !y.IsSelected) // Filter to get only unselected connectors
                                .Select(x => x.NextOwnerId) // Select OwnerId
                                .FirstOrDefault();*/
                                ElementId nextnodeEl = null;
                                foreach (var connector in lastnode.Connectors)
                                {
                                    if ((mainbranch.Nodes.Select(node => node.ElementId).Contains(connector.NextOwnerId)))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        nextnodeEl = connector.NextOwnerId;
                                    }
                                }


                                Node nextnode = new Node(doc, doc.GetElement(nextnodeEl), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                                lastnode = nextnode;
                                branch.Add(lastnode);
                            }

                            catch { }


                        }
                    
                        

                    if (lastnode.Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MechanicalEquipment)
                    {
                        mode = true;
                        Node nxtnode = GetNextElemAfterEquipment(doc, lastnode);
                        branch.Add(nxtnode);
                        lastnode = nxtnode;
                        
                    }

                    

                }
               


                try
                {
                        if (lastnode.IsTee)
                    {
                        double maxflow = double.MinValue;
                        CustomConnector selectedconnector=null;
                        foreach (var connector in lastnode.Connectors)
                        {
                            if (connector.Flow>maxflow)
                            {
                                selectedconnector = connector;
                            }
                        }
                        var nextelId = selectedconnector.NextOwnerId;
                        var nextElement = doc.GetElement(nextelId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode);


                    }
                    else
                    {
                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                        branch.Add(newnode); // Add the new node to the nodes list
                    }
                       
                    
                       
                                         // mainnodes.Add(branch); //
                }
                catch
                {
                    break;
                }
                counter++;
                if (mainbranch.Nodes.Select(node => node.ElementId).Contains(lastnode.ElementId))
                {
                    break;
                }
            }
            while (lastnode.NextOwnerId != null ||counter==1000);
            Branch newbranch = new Branch();
            foreach (var node in branch.Nodes)
            {
                if (node!=null)
                {
                    if (!mainbranch.Nodes.Select(x => x.ElementId).Contains(node.ElementId))
                    {
                        node.IsChecked = false;
                        newbranch.Nodes.Add(node);
                    }
                }
               
            }

            return (newbranch, tees);
        }

        public Branch GetNewBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnode, List<Branch> mainnodes)
        {
            bool mode = false;
            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                branch.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    branch.Add(newnode);
                }
            }
            Node lastnode = null;
            Node secnode = null;
            do
            {
                lastnode = branch.Nodes.Last(); // Get the last added node


               


                try
                {
                    var nextElement = doc.GetElement(lastnode.NextOwnerId);
                    Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
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
        public (List<Branch>, Branch) GetTihelmanBranches(Autodesk.Revit.DB.Document doc, ElementId elementId)
        {



            bool mode = false;
            List<Branch> mainnodes = new List<Branch>();
            Branch additionalNodes = new Branch();
            Branch mainnode = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;

            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();

                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                mainnode.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;

                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                    mainnode.Add(newnode);

                }
            }




            Node lastnode = null;

            do
            {
               
                lastnode = mainnode.Nodes.Last(); // Get the last added node
                if (lastnode.ElementId.IntegerValue == 2894980)
                {
                    lastnode = lastnode;
                }
                PipeSystemType systemtype2;
                string shortsystemname2;
                if (doc.GetElement(elementId) is Pipe)
                {
                    systemtype2 = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname, mode);
                    mainnode.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype2 = connector.PipeSystemType;
                        if (elementId.IntegerValue==2946736)
                        {
                            elementId = elementId;
                        }
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname, mode);
                        mainnode.Add(newnode);

                    }
                }
               
                if (lastnode.Connectors.Count >= 3)
                {
                    Element familyInstance = lastnode.Element;
                    string paramValue = familyInstance.LookupParameter("ADSK_Группирование").AsValueString();

                    if (paramValue!=null)
                    {
                        if (lastnode.ElementId.IntegerValue == 3307606)
                        { lastnode = lastnode; }
                        foreach (var node in mainnode.Nodes)
                        {
                            node.IsOCK = true;
                        }
                        List<Branch> manbranches = new List<Branch>();
                        List<Branch> manifoldbranches = new List<Branch>();
                        if (lastnode.Reverse == false)
                        {
                            manifoldbranches = GetNewManifoldBranches(doc, lastnode, lastnode.PipeSystemType);
                            lastnode.Reverse = true;
                        }
                        else
                        {
                            Node reversenode = GetManifoldReverseBranch(doc, lastnode, lastnode.PipeSystemType);
                            mainnode.Add(reversenode);
                            lastnode = reversenode;

                        }
                        foreach (var manifoldbranch in manifoldbranches)
                        {
                            Branch manbranch = new Branch();

                            foreach (var node in manifoldbranch.Nodes)
                            {

                                // GetNewBranch(doc, node.ElementId, mainnodes);
                                manbranch = GetDeadEndBranch(doc, node.ElementId, manbranch, mainnodes);
                                manbranch.RemoveNull();
                                manbranch.GetPressure();
                                manbranches.Add(manbranch);
                                //mode = false;
                            }

                        }

                        Branch selectedbranch = new Branch();
                        double maxpressure = double.MinValue;
                        foreach (var manifoldbranch in manbranches)
                        {
                            double pressure = manifoldbranch.DPressure;
                            if (pressure > maxpressure)
                            {
                                selectedbranch = manifoldbranch;
                                maxpressure = pressure;

                            }
                        }
                        selectedbranch.IsOCK = true;
                        selectedbranch.OCKCheck();
                        mainnodes.AddRange(manbranches);
                    }    

                    else if (paramValue==null)
                    {

                        foreach (var node in mainnode.Nodes)
                        {
                            node.IsOCK = true;
                        }
                        List<Branch> manbranches = new List<Branch>();
                        List<Branch> manifoldbranches = new List<Branch>();


                        //Отсюда ушел разбираться с коллекторами
                        if (lastnode.Reverse == false)
                        {
                            manifoldbranches = GetNewManifoldBranches(doc, lastnode, lastnode.PipeSystemType);
                            lastnode.Reverse = true;
                        }
                        else
                        {
                            Node reversenode = GetManifoldReverseBranch(doc, lastnode, lastnode.PipeSystemType);
                            mainnode.Add(reversenode);
                            lastnode = reversenode;

                        }

                        foreach (var manifoldbranch in manifoldbranches)
                        {
                            Branch manbranch = new Branch();

                            foreach (var node in manifoldbranch.Nodes)
                            {

                                // GetNewBranch(doc, node.ElementId, mainnodes);
                                manbranch = GetTihelmanBranch(doc, node.ElementId, manbranch, mainnodes);
                                manbranch.RemoveNull();
                                manbranch.GetPressure();
                                manbranches.Add(manbranch);
                                mode = false;
                            }

                        }

                        Branch selectedbranch = new Branch();
                        double maxpressure = double.MinValue;
                        foreach (var manifoldbranch in manbranches)
                        {
                            double pressure = manifoldbranch.DPressure;
                            if (pressure > maxpressure)
                            {
                                selectedbranch = manifoldbranch;
                                maxpressure = pressure;

                            }
                        }
                        selectedbranch.IsOCK = true;
                        selectedbranch.OCKCheck();
                        mainnodes.AddRange(manbranches);
                    }
                    

                    break;

                }
                else
                {
                    try
                    {
                        var nextElement = doc.GetElement(lastnode.NextOwnerId);
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
                       
                        mainnode.Add(newnode);
                        mainnode.IsOCK = true;
                        mainnode.OCKCheck();
                        // Add the new node to the nodes list
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
            List<ElementId> additionalElements = additionalConnectors.Select(x => x).Where(x => x.IsSelected == false).Select(x => x.NextOwnerId).ToList();

            foreach (var addel in additionalElements)
            {
                if (doc.GetElement(addel) is Pipe)
                {
                    systemtype = ((doc.GetElement(addel) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(addel) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname, mode);
                    additionalNodes.Add(newnode);

                }
                else
                {
                    try
                    {
                        shortsystemname = (doc.GetElement(addel) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                        var connectors = ((doc.GetElement(addel) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                        foreach (Connector connector in connectors)
                        {
                            systemtype = connector.PipeSystemType;
                            Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname, mode);
                            additionalNodes.Add(newnode);

                        }
                    }
                    catch { }
                    
                }

            }

            // Continue while NextOwnerId is not null
            return (mainnodes, additionalNodes);
        }

        private Node GetManifoldReverseBranch(Document doc, Node lastnode, PipeSystemType pipeSystemType)
        {
            Node nextnode = null;
            Element element = lastnode.Element;
            FamilyInstance familyInstance = element as FamilyInstance;
            MEPModel mepmodel = familyInstance.MEPModel;
            ConnectorSet connectorSet = mepmodel.ConnectorManager.Connectors;

            foreach (Connector connect in connectorSet)
            {
                if (pipeSystemType==PipeSystemType.ReturnHydronic)
                {
                    if (connect.PipeSystemType==PipeSystemType.ReturnHydronic)
                    {
                        if (connect.Direction==FlowDirectionType.Out)
                        {
                            ConnectorSet nextconnectors = connect.AllRefs;
                            foreach (Connector nextconnect in nextconnectors)
                            {
                                if (doc.GetElement(nextconnect.Owner.Id) is PipingSystem)
                                {
                                    continue;
                                }

                                else if (nextconnect.Owner.Id == lastnode.ElementId)
                                {
                                    continue;
                                }

                                else if (nextconnectors.Size < 1)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (nextconnect.Domain == Autodesk.Revit.DB.Domain.DomainHvac || nextconnect.Domain == Autodesk.Revit.DB.Domain.DomainPiping)
                                    {
                                        

                                         if (pipeSystemType == PipeSystemType.ReturnHydronic)
                                        {
                                            if (nextconnect.Direction == FlowDirectionType.In)
                                            {
                                                 nextnode = new Node(doc, doc.GetElement(nextconnect.Owner.Id), PipeSystemType.ReturnHydronic, lastnode.ShortSystemName, true);

                                            }

                                        }
                                    }
                                }
                            }
                        }

                        
                    }
                    else if (pipeSystemType == PipeSystemType.SupplyHydronic)
                    {
                        if (connect.PipeSystemType == PipeSystemType.SupplyHydronic)
                        {
                            if (connect.Direction == FlowDirectionType.In)
                            {
                                ConnectorSet nextconnectors = connect.AllRefs;
                                foreach (Connector nextconnect in nextconnectors)
                                {
                                    if (doc.GetElement(nextconnect.Owner.Id) is PipingSystem)
                                    {
                                        continue;
                                    }

                                    else if (nextconnect.Owner.Id == lastnode.ElementId)
                                    {
                                        continue;
                                    }

                                    else if (nextconnectors.Size < 1)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        if (nextconnect.Domain == Autodesk.Revit.DB.Domain.DomainHvac || nextconnect.Domain == Autodesk.Revit.DB.Domain.DomainPiping)
                                        {

                                            if (pipeSystemType == PipeSystemType.SupplyHydronic)
                                            {
                                                if (nextconnect.Direction == FlowDirectionType.Out)
                                                {
                                                    nextnode = new Node(doc, doc.GetElement(nextconnect.Owner.Id), PipeSystemType.SupplyHydronic, lastnode.ShortSystemName, true);

                                                }
                                            }
                                        }
                                    }
                                }
                            }


                        }
                    }
                }

                
                


            }
            return nextnode;
        }

        public (List<Branch>, Branch) GetNewBranches(Autodesk.Revit.DB.Document doc, ElementId elementId)
        {
            int counter = 0;
            bool mode = false;
            List<Branch> mainnodes = new List<Branch>();
            Branch additionalNodes = new Branch();
            Branch mainnode = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();
                Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
                mainnode.Add(newnode);

            }
            else
            {
                shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);
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
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname, mode);
                    mainnode.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(elementId) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(elementId) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype2 = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname, mode);
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
                            manbranch = GetNewBranch(doc, node.ElementId, manbranch, mainnodes);
                            manbranch.GetPressure();
                            manbranches.Add(manbranch);
                        }

                    }



                    Branch selectedbranch = new Branch();
                    double maxpressure = double.MinValue;
                    foreach (var manifoldbranch in manbranches)
                    {
                        double pressure = manifoldbranch.DPressure;
                        if (pressure > maxpressure)
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
                        node.IsOCK = false;
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
                        Node newnode = new Node(doc, nextElement, lastnode.PipeSystemType, shortsystemname, mode);
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
            List<ElementId> additionalElements = additionalConnectors.Select(x => x).Where(x => x.IsSelected == false).Select(x => x.NextOwnerId).ToList();

            foreach (var addel in additionalElements)
            {
                if (doc.GetElement(addel) is Pipe)
                {
                    systemtype = ((doc.GetElement(addel) as Pipe).MEPSystem as PipingSystem).SystemType;
                    shortsystemname = (doc.GetElement(addel) as Pipe).LookupParameter("Сокращение для системы").AsString();
                    Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname, mode);
                    additionalNodes.Add(newnode);

                }
                else
                {
                    shortsystemname = (doc.GetElement(addel) as FamilyInstance).LookupParameter("Сокращение для системы").AsString();
                    var connectors = ((doc.GetElement(addel) as FamilyInstance)).MEPModel.ConnectorManager.Connectors;
                    foreach (Connector connector in connectors)
                    {
                        systemtype = connector.PipeSystemType;
                        Node newnode = new Node(doc, doc.GetElement(addel), systemtype, shortsystemname, mode);
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

        public string GetContent(Autodesk.Revit.DB.Document doc, List<Branch> mainnodes)
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
        public void SelectBranches(UIDocument uidoc, List<Branch> mainnodes)
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
                    if (node.IsOCK == true)
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

        public List<Branch> AlgorithmQuartierCollectorsAndRayPipes(Autodesk.Revit.DB.Document doc, List<ElementId> startelements)
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
            foreach (var el in additionalNodes.Nodes)
            {
                el.IsOCK = false;
            }
            foreach (var startelement in additionalNodes.Nodes)
            {
                var nextStartelement = startelement.ElementId;
                (secondarynodes, secAdditionalNodes) = GetNewBranches(doc, nextStartelement);

                foreach (var secondarynode in secondarynodes)
                {
                    Branch branch = new Branch();
                    foreach (var node in secondarynode.Nodes)
                    {
                        node.IsOCK = false;
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
        private List<Branch> AlgorithmFloorCollectorAndTihelman(Document doc, List<ElementId> startelements)
        {
            List<Branch> mainnodes = new List<Branch>(); // тут стояк 
            List<Branch> secondarynodes = new List<Branch>();
            List<Branch> secondarySupernodes = new List<Branch>();
            List<Branch> terciarynodes = new List<Branch>();
            List<Branch> branches = new List<Branch>();
            Branch additionalNodes = new Branch();
            Branch secAdditionalNodes = new Branch();
            List<ModelElement> modelElements = new List<ModelElement>();
            PipeSystemType systemtype;
            string shortsystemname;

            foreach (var startelement in startelements)
            {
                (mainnodes, additionalNodes) = GetTihelmanBranches(doc, startelement);


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

            var totalIds = new HashSet<int>();
            foreach (var el in additionalNodes.Nodes)
            {
                el.IsOCK = false;
            }
            MEPSystem mepSystem = null;
            Node newStartElement = mainnodes.Last().Nodes[0];
            if ( newStartElement is FamilyInstance)
            {
                var elementId = newStartElement.Connectors.Last().OwnerId;

                ConnectorSet sysElemConnectors = ((doc.GetElement(elementId) as FamilyInstance).MEPModel).ConnectorManager.Connectors;
                foreach (Connector sysConnector in sysElemConnectors)
                {
                    mepSystem = sysConnector.MEPSystem;
                }
            }
            else
            {
                var elementId = newStartElement.Connectors.Last().OwnerId;

                mepSystem = ((doc.GetElement(elementId) as Pipe).MEPSystem);
            }
            

            

            var sysElements = (mepSystem as PipingSystem).PipingNetwork;
            var shortsysname = (doc.GetElement(mepSystem.GetTypeId())as MEPSystemType).Abbreviation;
            List<Node> starttees = new List<Node>();
            foreach (Element sysElem in sysElements)
            {
                
                    Node newadditionalnode = new Node(doc, sysElem, PipeSystemType.SupplyHydronic, shortsysname, false);
                    if (newadditionalnode.IsTee && !newadditionalnode.IsOCK )
                    {
                        starttees.Add(newadditionalnode);
                    }
                
            }
          

            foreach (var startelement in starttees)
            {
                if (startelement.ElementId.IntegerValue==2946736)
                {
                    var startelement2 = startelement;
                }
                foreach (var nextstartelement in startelement.Connectors )
                {
                    
                    var nextStartelement = nextstartelement.NextOwnerId;
                    
                    (secondarynodes, secAdditionalNodes) = GetTihelmanBranches(doc, nextStartelement);

                    foreach (var secondarynode in secondarynodes)
                    {
                        Branch branch = new Branch();
                        foreach (var node in secondarynode.Nodes)
                        {
                            node.IsOCK = false;
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
               
            }
                /*foreach (var startelement in additionalNodes.Nodes)
                {

                        var nextStartelement = startelement.ElementId;
                        (secondarynodes, secAdditionalNodes) = GetTihelmanBranches(doc, nextStartelement);

                        foreach (var secondarynode in secondarynodes)
                        {
                            Branch branch = new Branch();
                            foreach (var node in secondarynode.Nodes)
                            {
                                node.IsOCK = false;
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
                }*/


                mainnodes.AddRange(secondarySupernodes);

            return (mainnodes);
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
            MainViewModel mainViewModel = new MainViewModel(doc, window, sysNums);

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
            //List<Branch> mainnodes = AlgorithmQuartierCollectorsAndRayPipes(doc, startelements);
            List<Branch> mainnodes = new List<Branch>();

            //string csvcontent = GetContent(doc, mainnodes);
            //SaveFile(csvcontent);
            //SelectBranches(uIDocument, mainnodes);
            //SelectNodes(uIDocument, mainnodes);
            //SelectAllNodes(uIDocument, mainnodes);
            //SelectAdditionalNodes(uIDocument, mainnodes);


           
            var selectedMode = mainViewModel.CalculationModes
            .FirstOrDefault(x => x.IsMode == true);

            if (selectedMode != null)
            {
                int mode = selectedMode.CalculationId;  // Получаем Id расчета
                                                        // Инициализируем список для главных узлов

                switch (mode)
                {
                    case 0:
                        mainnodes = AlgorithmQuartierCollectorsAndRayPipes(doc, startelements);
                        break;  // Обязательно добавляем break для правильного выполнения

                    case 1:
                        mainnodes = AlgorithmFloorCollectorAndTihelman(doc, startelements);
                        SelectAllNodes(uIDocument, mainnodes);
                        //string csvcontent = GetContent(doc, mainnodes);
                        //SaveFile(csvcontent);
                        break;  // Обязательно добавляем break для правильного выполнения

                    default:  // Обработка случая, если mode не совпадает ни с одним из вышеуказанных
                        throw new InvalidOperationException($"Неизвестный режим расчета: {mode}");
                }
            }




           // string csvcontent2 = GetContent(doc, mainnodes);
            //SaveFile(csvcontent);
            //SelectBranches(uIDocument, mainnodes);
            //SelectNodes(uIDocument, mainnodes);
            //SelectAllNodes(uIDocument, mainnodes);
            //SelectAdditionalNodes(uIDocument, mainnodes);





            return Result.Succeeded;
        }
    }
}

       
    
