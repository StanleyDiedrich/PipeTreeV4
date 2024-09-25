﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace PipeTreeV4
{
    public class Branch
    {
        private static int _counter = 0;
        public int Number { get; set; }
        public double DPressure { get; set; }
         public List<Node> Nodes { get; set; }

        public Branch ()
        {
            Number = ++_counter;
            Nodes = new List<Node>();
            
        }

        public void  Add(Node node)
        {
            Nodes.Add(node);
        }

        public List<Node> GetNodes ()
        {
            return Nodes;
        }

        public double GetPressure() // Эту шляпу определили с целью поиска общей потери давления на ответвлении
        {
            double pressure = 0;

            foreach (var node in Nodes)
            {
                Element element = node.Element;
                if (element is Pipe)
                {
                    double dpressure = (element as Pipe).LookupParameter("Падение давления").AsDouble();
                    pressure += dpressure;
                }
            }
            DPressure = pressure;
            return DPressure;
        }
    }
}
