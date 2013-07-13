using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace AspNetServer
{
    public class HttpProcessor : MarshalByRefObject
    {
        private Socket _socket;

        private bool _isClosed;

        private SimpleHost _host;

        private static readonly Dictionary<string, string> staticFileContentType = new Dictionary<string, string>()
                                                                   {
                                                                       {"htm", "text/html"},
                                                                       {"html", "text/html"},
                                                                       {"xml", "text/xml"},
                                                                       {"txt", "text/plain"},
                                                                       {"css", "text/css"},
                                                                       {"js", "application/x-javascript"},
                                                                       {"png", "image/png"},
                                                                       {"gif", "image/gif"},
                                                                       {"jpg", "image/jpg"},
                                                                       {"jpeg", "image/jpeg"},
                                                                       {"zip", "application/zip"}
                                                                   };

        public HttpProcessor(SimpleHost host, Socket socket)
        {
            _host = host;
            _socket = socket;
        }

        public void ProcessRequest()
        {
            try
            {
                RequestInfo requestInfo = ParseRequest();
                if (requestInfo != null)
                {
                    string staticContentType = GetStaticContentType(requestInfo);
                    if (!string.IsNullOrEmpty(staticContentType))
                    {
                        WriteFileResponse(requestInfo.FilePath, staticContentType);
                    }
                    else if (requestInfo.FilePath.EndsWith("/"))
                    {
                        WriteDirResponse(requestInfo.FilePath);
                    }
                    else
                    {
                        _host.ProcessRequest(this, requestInfo);
                        //WorkerRequest workerRequest = new WorkerRequest(this, requestInfo);
                        //HttpRuntime.ProcessRequest(workerRequest);
                    }
                }
                else
                {
                    SendErrorResponse(400);
                }
            }
            finally
            {
                Close();//确保连接关闭
            }
        }

        private string GetStaticContentType(RequestInfo requestInfo)
        {
            int dotStart = requestInfo.FilePath.LastIndexOf('.') + 1;
            string contentType = string.Empty;
            if (dotStart > 0)
            {
                string extension = requestInfo.FilePath.Substring(dotStart, requestInfo.FilePath.Length - dotStart);
                foreach (KeyValuePair<string, string> fileType in staticFileContentType)
                {
                    if (extension.ToLower() == fileType.Key)
                    {
                        contentType = fileType.Value;
                        break;
                    }
                }
            }
            return contentType;
        }

        private void WriteFileResponse(string filePath, string contentType)
        {
            string fullPath = Path.Combine(_host.PhysicalDir, filePath.TrimStart('/'));
            if (!File.Exists(fullPath))
                SendErrorResponse(404);
            else
            {
                try
                {
                    using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                    {
                        int length = (int)fs.Length;
                        byte[] buffer = new byte[length];
                        int contentLength = fs.Read(buffer, 0, length);
                        string headers = BuildHeader(200,
                                                     new Dictionary<string, string>() { { "Content-Type", contentType } },
                                                     contentLength, false);
                        _socket.Send(Encoding.UTF8.GetBytes(headers));
                        _socket.Send(buffer, 0, contentLength, SocketFlags.None);
                    }
                }
                finally
                {
                    Close();
                }
            }
        }

        private void WriteDirResponse(string filePath)
        {
            string dir = Path.Combine(_host.PhysicalDir, filePath.TrimStart('/'));
            if (!Directory.Exists(dir))
                SendErrorResponse(404);
            else
            {
                string[] files = Directory.GetFileSystemEntries(dir);
                StringBuilder builder = new StringBuilder(files.Length + 2);
                builder.Append("<ol>");
                foreach (string file in files)
                {
                    string filename = Path.GetFileName(file);
                    bool isDir = Directory.Exists(file);
                    builder.AppendFormat("<li><a href=\"{0}\">{1}</a>{2}</li>", isDir ? filename + "/" : filename,
                                         filename, isDir ? "↓" : "");
                }
                builder.Append("</ol>");
                SendResponse(200, builder.ToString(), new Dictionary<string, string>() { { "Content-Type", "text/html" } });
            }
        }

        private RequestInfo ParseRequest()
        {
            try
            {
                string requestHeaders = GetRequestHeaders();
                if (!string.IsNullOrEmpty(requestHeaders))
                {
                    Console.WriteLine(requestHeaders);

                    RequestInfo requestInfo = new RequestInfo(requestHeaders);
                    requestInfo.ParseHeaders();
                    IPEndPoint remoteEndPoint = (IPEndPoint)_socket.RemoteEndPoint;
                    requestInfo.RemoteEndPoint = remoteEndPoint;
                    IPEndPoint localEndPoint = (IPEndPoint)_socket.LocalEndPoint;
                    requestInfo.LocalEndPoint = localEndPoint;

                    return requestInfo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        private string GetRequestHeaders()
        {
            int bufferSize = 0x8000;
            byte[] receiveBytes = new byte[0];
            while (true)
            {
                byte[] bytes = ReceiveBytes(bufferSize);
                if (bytes == null || bytes.Length == 0)
                    break;
                if (receiveBytes.Length == 0)
                    receiveBytes = bytes;
                else
                {
                    int receiveBytesLength = receiveBytes.Length + bytes.Length;
                    byte[] dst = new byte[receiveBytesLength];
                    Buffer.BlockCopy(receiveBytes, 0, dst, 0, receiveBytes.Length);
                    Buffer.BlockCopy(bytes, 0, dst, receiveBytes.Length, bytes.Length);
                }
            }

            string requestHeaders = Encoding.UTF8.GetString(receiveBytes);
            return requestHeaders;
        }

        private byte[] ReceiveBytes(int length)
        {
            int available = GetRequestAvailable();
            available = available > length ? length : available;
            byte[] buffer = null;
            if (available > 0)
            {
                buffer = new byte[available];
                int count = _socket.Receive(buffer, available, SocketFlags.None);
                if (count < available)
                {
                    byte[] dst = new byte[count];
                    if (count > 0)
                        Buffer.BlockCopy(buffer, 0, dst, 0, count);
                    buffer = dst;
                }
            }

            return buffer;
        }

        public int GetRequestAvailable()
        {
            int available = 0;
            try
            {
                if (_socket.Available == 0)
                {
                    _socket.Poll(10000, SelectMode.SelectRead);
                    if (_socket.Available == 0 && _socket.Connected)
                        _socket.Poll(10000, SelectMode.SelectRead);
                }

                available = _socket.Available;
            }
            catch
            {
                //ignore
            }

            return available;
        }

        private void SendErrorResponse(int statusCode)
        {
            SendResponse(statusCode, statusCode.ToString());
        }

        public void SendResponse(int statusCode, string body, Dictionary<string, string> headers = null, bool keepAlive = false)
        {
            SendResponse(statusCode, Encoding.UTF8.GetBytes(body), headers, keepAlive);
        }

        public void SendResponse(int statusCode, byte[] responseBodyBytes, Dictionary<string, string> headers = null, bool keepAlive = false)
        {
            SendHeaders(statusCode, headers, responseBodyBytes.Length, keepAlive);
            _socket.Send(responseBodyBytes);

            if (!keepAlive)
                Close();
        }

        public void SendResponse(byte[] data)
        {
            _socket.Send(data);
        }

        public void SendHeaders(int statusCode, Dictionary<string, string> headers, int contentLength, bool keepAlive)
        {
            string header = BuildHeader(statusCode, headers, contentLength, keepAlive);
            _socket.Send(Encoding.UTF8.GetBytes(header));
        }

        private string BuildHeader(int statusCode, Dictionary<string, string> headers, int contentLength, bool keepAlive)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("HTTP/1.1 {0} {1}\r\n", statusCode, HttpWorkerRequest.GetStatusDescription(statusCode));
            builder.AppendFormat("Server: AspNet Simple Server/1.0\r\n");
            builder.AppendFormat("Date: {0}\r\n",
                                 DateTime.Now.ToUniversalTime().ToString("R", DateTimeFormatInfo.InvariantInfo));
            if (contentLength > 0)
                builder.AppendFormat("Content-Length: {0}\r\n", contentLength);
            if (keepAlive)
                builder.Append("Connection: keep-alive\r\n");
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> pair in headers)
                {
                    builder.AppendFormat("{0}: {1}\r\n", pair.Key, pair.Value);
                }
            }
            builder.Append("\r\n");

            return builder.ToString();
        }

        public void Close()
        {
            try
            {
                if (!_isClosed)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();

                    _isClosed = true;
                }
            }
            catch
            {
                //ignore
            }
        }
    }
}