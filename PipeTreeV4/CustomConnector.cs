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
        public Domain  Domain { get; set; }
        public FlowDirectionType DirectionType { get; set; }
        public ElementId NextOwnerId { get; set; }
        public ElementId Neighbourg { get; set; }
        public double  Flow { get; set; }
        public bool IsSelected { get; set; }
        List<CustomConnector> Connectors { get; set; } = new List<CustomConnector>();

        public CustomConnector(Autodesk.Revit.DB.Document document,ElementId elementId, PipeSystemType pipeSystemType)
        {
            OwnerId = elementId;

            
        }
        
    }
}
