using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipeTreeV4
{
    public class SystemNumber
    {
        public string SystemName { get; set; }
        public bool IsSelected { get; set; }

        public SystemNumber (string systemname)
        {
            SystemName = systemname;
        }
    }
}
