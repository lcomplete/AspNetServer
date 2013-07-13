using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;

namespace AspNetServer
{
    class WorkerRequest : HttpWorkerRequest
    {
        private SimpleHost _host;

        private HttpProcessor _processor;

        private readonly RequestInfo _requestInfo;

        //输出相关
        private int _statusCode;
        private Dictionary<string, string> _responseHeaders;
        private IList<byte[]> _responseBodyBytes;

        //请求相关
        private string[] _knownRequestHeaders;
        private string[][] _unknownRequestHeaders;

        private bool _isHeaderSent;

        public WorkerRequest(SimpleHost host, HttpProcessor processor, RequestInfo requestInfo)
        {
            _host = host;
            _processor = processor;
            _requestInfo = requestInfo;

            _responseHeaders = new Dictionary<string, string>();
            _responseBodyBytes = new List<byte[]>();

            ParseRequestHeaders();
        }

        private void ParseRequestHeaders()
        {
            _knownRequestHeaders=new string[40];
            NameValueCollection unknownHeaders=new NameValueCollection();
            for (int i = 0; i < _requestInfo.Headers.Count; i++)
            {
                string name = _requestInfo.Headers.Keys[i];
                string value = _requestInfo.Headers[i];
                int index = HttpWorkerRequest.GetKnownRequestHeaderIndex(name);
                if (index >= 0)
                    _knownRequestHeaders[index] =value;
                else
                    unknownHeaders.Add(name,value);
            }

            _unknownRequestHeaders=new string[unknownHeaders.Count][];
            for (int i = 0; i < unknownHeaders.Count; i++)
            {
                _unknownRequestHeaders[i] = new[] {unknownHeaders.Keys[i], unknownHeaders[i]};
            }
        }

        #region vitural method

        public override string GetAppPath()
        {
            return _host.VituralDir;
        }

        public override string GetAppPathTranslated()
        {
            return _host.PhysicalDir;
        }

        public override string GetFilePath()
        {
            return _requestInfo.FilePath;
        }

        public override string GetFilePathTranslated()
        {
            string path = GetFilePath();
            path = path.Substring(_host.VituralDir.Length);
            path = path.Replace('/', '\\');
            return _host.PhysicalDir + path;
        }

        public override byte[] GetPreloadedEntityBody()
        {
            if(_requestInfo.EntityBody==null)
            {
                return null;
            }

            return Encoding.UTF8.GetBytes(_requestInfo.EntityBody);
        }

        public override bool IsEntireEntityBodyIsPreloaded()
        {
            return true;
        }

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            return buffer.Length;
        }

        public override string GetKnownRequestHeader(int index)
        {
            return _knownRequestHeaders[index];
        }

        public override string GetUnknownRequestHeader(string name)
        {
            foreach (string key in _requestInfo.Headers)
            {
                if (string.Compare(name, key, StringComparison.OrdinalIgnoreCase) == 0)
                    return _requestInfo.Headers[key];
            }
            return null;
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            return _unknownRequestHeaders;
        }

        #endregion

        public override string GetUriPath()
        {
            return _requestInfo.FilePath;
        }

        public override string GetQueryString()
        {
            return _requestInfo.QueryString;
        }

        public override string GetRawUrl()
        {
            return _requestInfo.RawUrl;
        }

        public override string GetHttpVerbName()
        {
            return _requestInfo.HttpMethod;
        }

        public override string GetHttpVersion()
        {
            return _requestInfo.Protocol;
        }

        public override string GetRemoteAddress()
        {
            return _requestInfo.RemoteEndPoint.Address.ToString();
        }

        public override int GetRemotePort()
        {
            return _requestInfo.RemoteEndPoint.Port;
        }

        public override string GetLocalAddress()
        {
            return _requestInfo.LocalEndPoint.Address.ToString();
        }

        public override int GetLocalPort()
        {
            return _requestInfo.LocalEndPoint.Port;
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            _statusCode = statusCode;
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            _responseHeaders[HttpWorkerRequest.GetKnownResponseHeaderName(index)] = value;
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            _responseHeaders[name] = value;
        }

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (length > 0)
            {
                byte[] dst = new byte[length];
                Buffer.BlockCopy(data, 0, dst, 0, length);
                _responseBodyBytes.Add(dst);
            }
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
        }

        public override void FlushResponse(bool finalFlush)
        {
            if (!_isHeaderSent)
                _processor.SendHeaders(_statusCode, _responseHeaders, -1, finalFlush);

            for (int i = 0; i < _responseBodyBytes.Count; i++)
            {
                byte[] data = _responseBodyBytes[i];
                _processor.SendResponse(data);
            }

            _responseBodyBytes = new List<byte[]>();
            if (finalFlush)
                _processor.Close();
        }

        public override void EndOfRequest()
        {
        }
    }
}
