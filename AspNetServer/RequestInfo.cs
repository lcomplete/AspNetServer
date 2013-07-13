using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace AspNetServer
{
    public class RequestInfo:MarshalByRefObject
    {
        public string RawUrl { get; private set; }

        public string Protocol { get; private set; }

        public string FilePath { get; private set; }

        public string QueryString { get; private set; }

        public string HttpMethod { get; private set; }

        public NameValueCollection Headers { get; private set; }

        public IPEndPoint RemoteEndPoint { get; set; }

        public IPEndPoint LocalEndPoint { get; set; }

        public string EntityBody { get; private set; }

        private string _rawRequestHeaders;

        private bool _parsed;

        public RequestInfo(string requestHeaders)
        {
            _rawRequestHeaders = requestHeaders;
        }

        public void ParseHeaders()
        {
            if (!_parsed)
            {
                DoParse();
                _parsed = true;
            }
        }

        private void DoParse()
        {
            string[] lines = _rawRequestHeaders.Split(new[] { "\r\n" }, StringSplitOptions.None);

            string[] actions = lines[0].Split(' ');
            HttpMethod = actions[0];
            RawUrl = actions[1];
            Protocol = actions[2];

            string[] path = RawUrl.Split('?');
            FilePath =HttpUtility.UrlDecode(path[0]);
            if (path.Length == 2)
                QueryString = path[1];

            Headers = new NameValueCollection();

            bool headerComplete = false;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if(string.IsNullOrEmpty(line))
                {
                    headerComplete = true;
                    continue;
                }

                if(headerComplete)
                {
                    EntityBody = line;
                }
                else
                {
                    int separator = line.IndexOf(":");
                    Headers.Add(line.Substring(0, separator),
                                line.Substring(separator + 1, line.Length - separator - 1).TrimStart());
                }
            }
        }
    }
}
