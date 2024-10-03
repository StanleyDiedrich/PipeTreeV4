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

        public Branch GetTihelmanBranch(Autodesk.Revit.DB.Document doc, ElementId elementId, Branch mainnode, List<Branch> mainnodes)
        {
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

            int tee_counter = 0;
            int equipment_counter = 0;
            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            string longsystemname = string.Empty;
            bool mode = false;
            Element nextelement = null;
            
            if (doc.GetElement(elementId) is Pipe)
            {
                systemtype = ((doc.GetElement(elementId) as Pipe).MEPSystem as PipingSystem).SystemType;
                shortsystemname = (doc.GetElement(elementId) as Pipe).LookupParameter("Сокращение для системы").AsString();

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
                foreach (Connector connector in connectors)
                {
                    systemtype = connector.PipeSystemType;
                    Node newnode = new Node(doc, doc.GetElement(elementId), systemtype, shortsystemname, mode);

                    branch.Add(newnode);
                }
            }

            var equipment = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment).WhereElementIsNotElementType().ToElementIds();
            if (longsystemname == null || longsystemname == string.Empty)
            {
                bool emptystring = true;
            }
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

            equipment_counter = systemequipment.Count();
            Node lastnode = null;
            Node secnode = null;
            Node VCK1_Node = null;
            Node VCK2_node = null;
            Branch firstVCKBranch = new Branch();
            Branch secondVCKBranch = new Branch();
            List<Node> tees = new List<Node>();
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
        int c = equipment_counter - 1;

        if (lastnode.IsTee) // да, тут выбрали тройник.
        {

            if (tee_counter == 1 && lastnode.IsTee)
            {
                var nextElId = lastnode.Connectors
                .Where(y => y.IsSelected) // Filter to get only unselected connectors
                .Select(x => x.NextOwnerId) // Select OwnerId
                .FirstOrDefault();
                Node vCK2_node = new Node(doc, doc.GetElement(nextElId), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                var  nextnodeEl = lastnode.Connectors
                .Where(y => !y.IsSelected) // Filter to get only unselected connectors
                .Select(x => x.NextOwnerId) // Select OwnerId
                .FirstOrDefault();
                Node nextnode= new Node(doc, doc.GetElement(nextnodeEl), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                lastnode.IsSplitter = true;
                lastnode.IsChecked = true;
                lastnode = nextnode;
                branch.Add(lastnode);
                vCK2_node.IsChecked = true;
                (secondVCKBranch, tees) = GetVCKBranch(doc, vCK2_node, false);

            }
            /*else if (tee_counter != 1 && lastnode.IsTee)
            {
                if (lastnode.ElementId.IntegerValue == 2898219)
                {
                    lastnode = lastnode;
                }
                var nextElId = lastnode.Connectors
                .Where(y => !y.IsSelected) // Filter to get only unselected connectors
                .Select(x => x.NextOwnerId) // Select OwnerId
                .FirstOrDefault();
                Node nextnode = new Node(doc, doc.GetElement(lastnode.ElementId), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                nextnode.NextOwnerId = nextElId;
                lastnode.IsSplitter = true;
                *//*Node vcknode = new Node(doc, doc.GetElement(lastnode.NextOwnerId), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                (firstVCKBranch, tees) = GetVCKBranch(doc, vcknode, true);*//*
            }*/
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
           /// var dddd = tees.Select(x => x).Where(x => x.ElementId.IntegerValue == 2897219).First();
            
            var splitnode = tees.Select(x => x).Where(x => x.IsTee).LastOrDefault();
            if (splitnode!=null)
            {
                if (splitnode.ElementId.IntegerValue == 2898219)
                {
                    splitnode = splitnode;
                }
            }
            
            if (splitnode!=null && splitnode.Connectors.Count>1)
            {
                
                var selectednode = splitnode.Connectors.Where(y => !y.IsSelected).Select(x => x).First();
                Node vcknode = new Node(doc, doc.GetElement(selectednode.NextOwnerId), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                vcknode.IsChecked = true;
                (firstVCKBranch, tees) = GetVCKBranch(doc, vcknode, false);
            }



            branch.AddRange(firstVCKBranch);
            branch.AddRange(secondVCKBranch);
            branch.RemoveNull();
            List<Node> foundedtees = new List<Node>();
            foreach (var node in branch.Nodes)
            {
                if (node.IsTee && node != null)
                {
                    if (node.PipeSystemType == PipeSystemType.SupplyHydronic && node.ShortSystemName == lastnode.ShortSystemName)
                    {
                        if (node.IsChecked == false)
                        {
                            foundedtees.Add(node);
                        }
                    }
                }
            }

            foreach (var node in foundedtees)
            {
                Branch smallbranch = GetSmallBranch(doc, node, firstVCKBranch,secondVCKBranch);
                branch.AddRange(smallbranch);
            }

            return branch;
        }

        private Branch GetSmallBranch(Document doc, Node node,Branch firstVckBranch, Branch secondVCKBranch)
        {
            List<ElementId> checkedNodesIds = new List<ElementId>();
            foreach(var checknode in firstVckBranch.Nodes)
            {
                if (checknode!=null)
                {
                    checkedNodesIds.Add(checknode.ElementId);
                }
                
            }
            List<ElementId> secondVckBranchIds = new List<ElementId>();
            foreach (var checknode in secondVCKBranch.Nodes)
            {
                if (checknode != null)
                {
                    checkedNodesIds.Add(checknode.ElementId);
                }
            }

            Branch branch = new Branch();
            PipeSystemType systemtype;
            string shortsystemname;
            ElementId elementId = node.Connectors.Where(y => !y.IsSelected).Select(x => x.NextOwnerId).First();

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

        private (Branch,List<Node>) GetVCKBranch(Autodesk.Revit.DB.Document doc,Node vCK_node, bool firstvck)
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
                    if (firstvck==false)
                    {
                        if (lastnode.IsTee == true)
                        {
                            tees.Add(lastnode);
                        }
                    }
                    else
                    {
                        if (lastnode.IsTee == true)
                        {
                            tees.Add(lastnode);
                        }
                        if (lastnode.IsTee && lastnode.PipeSystemType == PipeSystemType.SupplyHydronic)
                        {
                            try
                            {
                                var nextnodeEl = lastnode.Connectors
                                .Where(y => !y.IsSelected) // Filter to get only unselected connectors
                                .Select(x => x.NextOwnerId) // Select OwnerId
                                .FirstOrDefault();
                                Node nextnode = new Node(doc, doc.GetElement(nextnodeEl), lastnode.PipeSystemType, lastnode.ShortSystemName, false);
                                lastnode = nextnode;
                                branch.Add(lastnode);
                            }

                            catch { }


                        }

                    }
                     
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

                    

                }



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
                counter++;
            }
            while (lastnode.NextOwnerId != null ||counter==1000);


            return (branch, tees);
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
                        Node newnode = new Node(doc, doc.GetElement(elementId), systemtype2, shortsystemname, mode);
                        mainnode.Add(newnode);

                    }
                }

                if (lastnode.Connectors.Count >= 4)
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
                    // Тут мы дошли только до первого уровня и тут покажутся коллекторы поквартирные.


                    /* var secondarymanifolds = manbranches.SelectMany(x => x.Nodes).Select(x => x).Where(x => x.IsManifold == true).ToList();
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
                     selectedsecondbranch = selectedsecondarybranches.OrderByDescending(x => x.DPressure).FirstOrDefault();*/

                    // mainnodes.AddRange(selectedsecondarybranches);

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

            string csvcontent = GetContent(doc, mainnodes);
            //SaveFile(csvcontent);
            //SelectBranches(uIDocument, mainnodes);
            SelectNodes(uIDocument, mainnodes);
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
                        break;  // Обязательно добавляем break для правильного выполнения

                    default:  // Обработка случая, если mode не совпадает ни с одним из вышеуказанных
                        throw new InvalidOperationException($"Неизвестный режим расчета: {mode}");
                }
            }




            string csvcontent2 = GetContent(doc, mainnodes);
            //SaveFile(csvcontent);
            //SelectBranches(uIDocument, mainnodes);
            SelectNodes(uIDocument, mainnodes);
            //SelectAllNodes(uIDocument, mainnodes);
            //SelectAdditionalNodes(uIDocument, mainnodes);





            return Result.Succeeded;
        }
    }
}

       
    
