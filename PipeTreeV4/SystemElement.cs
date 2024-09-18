using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace PipeTreeV4
{
    public class SystemElement
    {
        public ElementId ElementId { get; set; }
        public Element Element { get; set; }
        public string SystemName { get; set; }

        public SystemElement (  Element el)
        {
            
            Element = el;
            ElementId = el.Id;
            
            
        }

       
    }
}
