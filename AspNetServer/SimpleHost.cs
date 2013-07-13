using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace AspNetServer
{
    public class SimpleHost : MarshalByRefObject
    {
        public string PhysicalDir { get; private set; }

        public string VituralDir { get; private set; }

        public void Config(string vitrualDir, string physicalDir)
        {
            VituralDir = vitrualDir;
            PhysicalDir = physicalDir;
        }

        public void ProcessRequest(HttpProcessor processor, RequestInfo requestInfo)
        {
            WorkerRequest workerRequest = new WorkerRequest(this, processor, requestInfo);
            HttpRuntime.ProcessRequest(workerRequest);
        }
    }
}
