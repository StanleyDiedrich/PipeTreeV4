using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeTreeV4
{
    public class Node
    {
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public string SystemName { get; set; }
        public string ShortSystemName { get; set; }
        public PipeSystemType PipeSystemType { get; set; }

        public List<CustomConnector> Connectors { get; set; } = new List<CustomConnector>();
        public List<List<CustomConnector>> ConnectorList { get; set; } = new List<List<CustomConnector>>();

        public ElementId NextOwnerId { get; set; }
        

        public Node (Autodesk.Revit.DB.Document doc, Element element, PipeSystemType pipeSystemType, string shortsystemName)
        {
            Element = element;
            ElementId = Element.Id;
            ShortSystemName = shortsystemName;
            PipeSystemType = pipeSystemType;
            


           
            ConnectorSet connectorSet = null;
            try
            {
                if (element is Autodesk.Revit.DB.Plumbing.Pipe)
                {
                    Autodesk.Revit.DB.Plumbing.Pipe pipe = Element as Pipe;
                    SystemName = pipe.LookupParameter("Имя системы").AsString();
                    connectorSet = pipe.ConnectorManager.Connectors;
                }
                if (element is FamilyInstance)
                {
                    FamilyInstance familyInstance = element as FamilyInstance;
                    SystemName = familyInstance.LookupParameter("Имя системы").AsString();
                    MEPModel mepModel = familyInstance.MEPModel;
                    connectorSet = mepModel.ConnectorManager.Connectors;
                }
                if (connectorSet.Size>=4)
                {
                    List<CustomConnector> customConnectors = new List<CustomConnector>();
                    foreach (Connector connector in connectorSet)
                    {
                        CustomConnector custom = new CustomConnector(doc, ElementId, PipeSystemType);
                        ConnectorSet nextconnectors = connector.AllRefs;
                        foreach (Connector connect in nextconnectors)
                        {
                            string sysname = doc.GetElement(connect.Owner.Id).LookupParameter("Имя системы").AsString();

                            if (doc.GetElement(connect.Owner.Id) is PipingSystem)
                            {
                                continue;
                            }
                            else if (connect.Owner.Id == ElementId)
                            {
                                continue; // Игнорируем те же элементы
                            }
                            else if (connect.Owner.Id == NextOwnerId)
                            {
                                continue;
                            }
                            else if (sysname.Contains(ShortSystemName))
                            {
                                if (connect.Domain == Autodesk.Revit.DB.Domain.DomainHvac || connect.Domain == Autodesk.Revit.DB.Domain.DomainPiping)
                                {

                                    if (pipeSystemType == PipeSystemType.SupplyHydronic)
                                    {
                                        if (connect.Direction == FlowDirectionType.In)
                                        {
                                            custom.Flow = connect.Flow;
                                            custom.Domain = Domain.DomainPiping;
                                            custom.DirectionType = FlowDirectionType.In;
                                            custom.NextOwnerId = connect.Owner.Id;
                                            NextOwnerId = custom.NextOwnerId;

                                            customConnectors.Add(custom);
                                        }
                                    }
                                    else if (pipeSystemType == PipeSystemType.ReturnHydronic)
                                    {
                                        if (connect.Direction == FlowDirectionType.Out)
                                        {
                                            custom.Flow = connect.Flow;
                                            custom.Domain = Domain.DomainPiping;
                                            custom.DirectionType = FlowDirectionType.Out;
                                            custom.NextOwnerId = connect.Owner.Id;
                                            NextOwnerId = custom.NextOwnerId;
                                            customConnectors.Add(custom);
                                        }

                                    }
                                }
                            }
                            /*else if (connectorSet.Size < 1)
                            {

                            }*/
                            /*else
                            {

                                
                            }*/
                        }
                        ConnectorList.Add(customConnectors);
                    }
                   
                }
                else
                {
                    foreach (Connector connector in connectorSet)
                    {
                        CustomConnector custom = new CustomConnector(doc, ElementId, PipeSystemType);
                        ConnectorSet nextconnectors = connector.AllRefs;
                        foreach (Connector connect in nextconnectors)
                        {


                            if (doc.GetElement(connect.Owner.Id) is PipingSystem)
                            {
                                continue;
                            }
                            else if (connect.Owner.Id == ElementId)
                            {
                                continue; // Игнорируем те же элементы
                            }
                            else if (connect.Owner.Id == NextOwnerId)
                            {
                                continue;
                            }
                            else if (!doc.GetElement(connect.Owner.Id).LookupParameter("Имя системы").AsString().Contains(SystemName))
                            {
                                continue;
                            }
                            else if (connectorSet.Size < 1)
                            {
                                continue;
                            }
                            else
                            {

                                if (connect.Domain == Autodesk.Revit.DB.Domain.DomainHvac || connect.Domain == Autodesk.Revit.DB.Domain.DomainPiping)
                                {

                                    if (pipeSystemType == PipeSystemType.SupplyHydronic)
                                    {
                                        if (connect.Direction == FlowDirectionType.In)
                                        {
                                            custom.Flow = connect.Flow;
                                            custom.Domain = Domain.DomainPiping;
                                            custom.DirectionType = FlowDirectionType.In;
                                            custom.NextOwnerId = connect.Owner.Id;
                                            NextOwnerId = custom.NextOwnerId;

                                            Connectors.Add(custom);
                                        }
                                    }
                                    else if (pipeSystemType == PipeSystemType.ReturnHydronic)
                                    {
                                        if (connect.Direction == FlowDirectionType.Out)
                                        {
                                            custom.Flow = connect.Flow;
                                            custom.Domain = Domain.DomainPiping;
                                            custom.DirectionType = FlowDirectionType.Out;
                                            custom.NextOwnerId = connect.Owner.Id;
                                            NextOwnerId = custom.NextOwnerId;
                                            Connectors.Add(custom);
                                        }

                                    }
                                }
                            }
                        }



                    }
                }
            }
                
                
            
            catch
            {

            }
           /* if (Connectors.Count == 1)
            {
                custom.IsSelected = false;
            }*/
            double maxvolume = double.MinValue;
            CustomConnector selectedconnector = null;
            foreach (CustomConnector customConnector in Connectors)
            {
                double flow = customConnector.Flow;
                if (flow > maxvolume)
                {
                    maxvolume = flow;
                    selectedconnector = customConnector;

                }
            }
            if (selectedconnector != null)
            {
                selectedconnector.IsSelected = true;
                NextOwnerId = selectedconnector.NextOwnerId;
            }

        }

        
    }

}
