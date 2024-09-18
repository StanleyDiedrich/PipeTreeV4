using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeTreeV4
{
    public class CustomConnector
    {
        public ElementId OwnerId { get; set; }
        public Connector Connector { get; set; }
        public string  Domain { get; set; }
        public string DirectionType { get; set; }
        public ElementId NextOwnerId { get; set; }
        public double  Flow { get; set; }
        List<Connector> Connectors { get; set; } = new List<Connector>();

        public CustomConnector(Autodesk.Revit.DB.Document document,ElementId elementId)
        {
            OwnerId = elementId;
            Element element = document.GetElement(elementId);
            ConnectorSet connectorSet = null;
            if (element is Autodesk.Revit.DB.Plumbing.Pipe)
            {
                Autodesk.Revit.DB.Plumbing.Pipe pipe = element as Pipe;
                connectorSet = pipe.ConnectorManager.Connectors;
            }
            if (element is FamilyInstance)
            {
                FamilyInstance familyInstance = element as FamilyInstance;
                MEPModel mepModel = familyInstance.MEPModel;
                connectorSet = mepModel.ConnectorManager.Connectors;
            }

            foreach (Connector connect in connectorSet)
            {
                if (document.GetElement(connect.Owner.Id) is PipingSystem)
                {
                    continue;
                }
                else if (connect.Owner.Id == elementId)
                {
                    continue; // Игнорируем те же элементы
                }
                else if (connectorSet.Size < 1)
                {
                    continue;
                }
                else
                {

                }
            }
        }
        public List<Connector> GetConnectors()
        {
            
            return Connectors;
        }
    }
}
