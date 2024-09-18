using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeTreeV4
{
    public class Node
    {
        public Element Element { get; set; }
        public ElementId ElementId { get; set; }
        public string SystemName { get; set; }
        public PipeSystemType PipeSystemType { get; set; }

        public List<Connector> Connectors { get; set; }

        public Node (Autodesk.Revit.DB.Document doc, Element element, PipeSystemType pipeSystemType)
        {
            Element = element;
            ElementId = Element.Id;
            SystemName = element.LookupParameter("Имя Системы").AsString();
            PipeSystemType = pipeSystemType;

            CustomConnector customConnector = new CustomConnector(doc, ElementId);
            Connectors = GetConnectors(customConnector);
        }

        private List<Connector> GetConnectors(CustomConnector customConnector)
        {
            return customConnector.GetConnectors();
        }
    }

}
