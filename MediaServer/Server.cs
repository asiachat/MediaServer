using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;


namespace MediaServer
{
    class Server
    {
        private const int MAXCONNECTIONS = 10;
        private int connections;
        private Socket socServer;
        private string ip;
        private int port;
        private bool running;
        private FileStream servedFile = null;
        ILogger logger;
        private AvailableMedia availableMedia;
        private Semaphore maxNumberAcceptedClients;
        private IPEndPoint IPE;

        private Server()
        {
            socServer = null;
            connections = 0;
            maxNumberAcceptedClients = new Semaphore(MAXCONNECTIONS, MAXCONNECTIONS);
        }
        public Server(string ip, int port, string mediaDir) : this()
        {
            availableMedia = new AvailableMedia(mediaDir);

            using ILoggerFactory factory = LoggerFactory.Create(builder => builder
            .AddFilter("MediaServer.Server", LogLevel.Debug)
            .AddConsole());

            logger = factory.CreateLogger<Server>();
            socServer = null;
            this.ip = ip;
            this.port = port;
            socServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPE = new IPEndPoint(IPAddress.Parse(this.ip), this.port);
        }

        public void Start()
        {
            socServer.Bind(IPE);
            socServer.Listen(0);
            running = true;
            Thread thread = new Thread(Listen);
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            Thread.Sleep(100);
            if (this.servedFile != null)
            { try { servedFile.Close(); } catch {; } }
            if (socServer != null && socServer.Connected) socServer.Shutdown(SocketShutdown.Both);
        }

        //TODO: Finish Implementation (done)
        private Socket listenSocket;
        public void Start(IPEndPoint localEndPoint)
        {
            // Create the socket which listens for incoming connections
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            // Start the server with a listen backlog of 100 connections
            listenSocket.Listen(100);

            // Post accepts on the listening socket
            SocketAsyncEventArgs acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            StartAccept(acceptEventArg);

            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }


        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            throw new NotImplementedException();
        }

        private void Listen()
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += AcceptCallback;

            bool pending = false;
            while (this.running && !pending)
            {
                Console.WriteLine("Waiting for connection ...");
                maxNumberAcceptedClients.WaitOne();

                // TODO: Accept the next connection asynchronously (done)
                Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenSocket.Bind(new IPEndPoint(IPAddress.Any, 8080)); // Adjust your port as needed
                listenSocket.Listen(10);


                pending = listenSocket.AcceptAsync(e); // This handles the request asynchronously
            }
        }

        /// <summary>
        /// Accepts the request and starts to process it on a new thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AcceptCallback(object sender, SocketAsyncEventArgs e)
        {
            Thread thread = new Thread(ProcessAccept);
            thread.Start(e);
        }

        private void ProcessAccept(object eObj)
        {
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)eObj;
            try
            {
                Interlocked.Increment(ref connections);
                Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", connections);
                Socket sock = e.AcceptSocket;
                byte[] buffer = new byte[3000];
                int receivedSize = 0;
                var sb = new StringBuilder();
                MemoryStream ms;

                while (sock.Available > 0)
                {
                    receivedSize = sock.Receive(buffer, SocketFlags.None);

                    ms = new MemoryStream();
                    ms.Write(buffer, 0, receivedSize);
                    string toAdd = UTF8Encoding.UTF8.GetString(ms.ToArray());
                    sb.Append(toAdd);
                    logger.LogDebug("Received {0} bytes", receivedSize);
                }
                string requestData = sb.ToString();

                BusinessLogic(requestData, sock);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error processing request");
            }
            finally
            {
                Listen();
            }

        }

        private void CloseClientSocket(Socket socket)
        {
            // close the socket associated with the client
            try
            {
                socket.Shutdown(SocketShutdown.Send);
            }
            // throws if client process has already closed
            catch (Exception) { }
            socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref connections);

            maxNumberAcceptedClients.Release();
            Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", connections);
        }

        //TODO: Finish implementation. Analyze through the entire method. done
        private void BusinessLogic(string request, Socket handler)
        {

            string[] requestLines = GetRequestLines(request);
            List<KeyValuePair<string, string>> headers = GetHeaders(requestLines);
            KeyValuePair<string, string> methodAndPath = GetMethodAndPath(requestLines);

            //TODO: You probably want to do something here
            String methPath = String.Format("Method: {0} | path: {1}", methodAndPath.Key, methodAndPath.Value);

            if (methodAndPath.Value.ToLower().Contains("favicon.ico"))
            {
                CloseClientSocket(handler);
                return;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(methPath + Environment.NewLine);
                foreach (KeyValuePair<string, string> pair in headers)
                {
                    sb.Append(String.Format("{0}:{1}" + Environment.NewLine, pair.Key, pair.Value));
                }
                logger.LogDebug(sb.ToString());
                if (methodAndPath.Key.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    HandleHead(handler, headers, methodAndPath.Value);
                }
                else if (methodAndPath.Key.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    HandleGet(handler, headers, methodAndPath.Value);
                }
                else
                {
                    logger.LogWarning($"Unexpected HTTP method: {methodAndPath.Key}");
                    CloseClientSocket(handler);

                }

            }
        }

        //TODO: Finish implementation (done)
        /// <summary>
        /// This method takes the raw request splits it based on end of line, and returns each line in a string array
        /// </summary>
        /// <param name="request">The raw request</param>
        /// <returns>A string array with each cell being a line in the request</returns>
        private string[] GetRequestLines(string request)
        {
            if (string.IsNullOrWhiteSpace(request))
            {
                return Array.Empty<string>(); // Return an empty array if the request is null or whitespace
            }

            return request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        }

        //TODO: Implement (done)
        /// <summary>
        /// Thus methods returns the HTTP method used in the request and the requested path in key value pair
        /// </summary>
        /// <param name="requestLines">The request lines</param>
        /// <returns>Key Value Pair containing the method as a key and the path as the value</returns>
        private KeyValuePair<string, string> GetMethodAndPath(string[] requestLines)
        {
            if (requestLines == null || requestLines.Length == 0)
            {
                return new KeyValuePair<string, string>("UNKNOWN", "UNKNOWN"); // Fallback in case of empty request
            }

            string[] firstLineParts = requestLines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (firstLineParts.Length < 2)
            {
                return new KeyValuePair<string, string>("INVALID", "INVALID"); // Indicates malformed request
            }

            string method = firstLineParts[0].ToUpperInvariant();
            string path = firstLineParts[1];

            return new KeyValuePair<string, string>(method, path);
        }

        //TODO: Implement (done)
        /// <summary>
        /// This method returns the headers submitted in the request as a list of key value pairs
        /// </summary>
        /// <param name="requestLines">The request as a string array</param>
        /// <returns>List of key value pairs where the each header name is key and their contents is their value</returns>
        private List<KeyValuePair<string, string>> GetHeaders(string[] requestLines)
        {
            List<KeyValuePair<string, string>> headers = new List<KeyValuePair<string, string>>();

            try
            {
                // Headers typically start after the first line (which contains the method and path)
                for (int i = 1; i < requestLines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(requestLines[i]))
                        break; // Stop reading at the first empty line (end of headers)

                    string[] headerParts = requestLines[i].Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (headerParts.Length == 2)
                    {
                        string key = headerParts[0].Trim();
                        string value = headerParts[1].Trim();
                        headers.Add(new KeyValuePair<string, string>(key, value));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Exception getting headers");
            }

            return headers;
        }

        private int GetIndexFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                logger.LogDebug("Received an empty or root path.");
                return -1; // No valid index
            }

            string requestIndex = path.TrimStart('/');
            logger.LogDebug("Attempting to find match for: {0}", requestIndex);

            if (!int.TryParse(requestIndex, out int index))
            {
                logger.LogDebug("Index parsing from path failed.");
                return -1;
            }

            return index;
        }

        //TODO: Finish implementation (done)
        /// <summary>
        /// This method returns the header information of a particular file
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="headers"></param>
        /// <param name="path"></param>
        private void HandleHead(Socket handler, List<KeyValuePair<string, string>> headers, string path)
        {
            int index = GetIndexFromPath(path);
            string requestFile = availableMedia.getAbsolutePath(index);

            logger.LogDebug("HEAD requested for file: {0}", requestFile);

            FileInfo fileInfo = new FileInfo(requestFile);
            if (fileInfo.Exists)
            {
                StringBuilder response = new StringBuilder();
                response.AppendLine("HTTP/1.1 200 OK");
                response.AppendLine($"Content-Length: {fileInfo.Length}");
                response.AppendLine($"Last-Modified: {fileInfo.LastWriteTimeUtc:R}");
                response.AppendLine("Content-Type: application/octet-stream");
                response.AppendLine(); // End of headers

                byte[] responseBytes = Encoding.ASCII.GetBytes(response.ToString());
                handler.Send(responseBytes);
            }
            else
            {
                SendNotFound(handler);
            }

            CloseClientSocket(handler);
        }

        private void HandleGet(Socket handler, List<KeyValuePair<string, string>> headers, string path)
        {
            int index = GetIndexFromPath(path);
            if (index == -1)
            {
                ReturnList(handler); // Return a directory listing or another response
            }
            else // File possibly requested
            {
                string requestFile = availableMedia.getAbsolutePath(index);
                logger.LogDebug("GET requested for file: {0}", requestFile);

                FileInfo fileInfo = new FileInfo(requestFile);
                if (fileInfo.Exists)
                {
                    ServeFile(handler, headers, requestFile);
                }
                else
                {
                    SendNotFound(handler);
                }
            }
        }

        //TODO: Finish implementation (done)
        /// <summary>
        /// This method returns a  webpage contain with a list of media found in the media dir. Each entry on the page must
        /// be clickable via an anchor tag which shows the name of the media file with its href attribute set to the index
        /// number of the file. Clicking the link must open the media item.
        /// The names of the file must not show the root path to the media dir
        /// 
        /// </summary>
        /// <param name="handler">The socket to write the webpage to</param>
        private void ReturnList(Socket handler)
        {
            string template = File.ReadAllText("template.txt"); // Assume this contains an HTML skeleton
            StringBuilder mediaListHtml = new StringBuilder();
            string[] files = availableMedia.getAvailableFiles().ToArray();

            // Generate HTML anchor tags for each media file
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]); // Get name without full path
                mediaListHtml.AppendLine($"<a href=\"/{i}\">{fileName}</a><br>");
            }

            // Merge into the template (assuming `{{MEDIA_LIST}}` is a placeholder in the HTML file)
            string finalHtml = template.Replace("{{MEDIA_LIST}}", mediaListHtml.ToString());

            // Prepare HTTP response headers
            string contentType = "text/html";
            string httpHeader = $"HTTP/1.1 200 OK{Environment.NewLine}" +
                                $"Server: VLC{Environment.NewLine}" +
                                $"Content-Type: {contentType}{Environment.NewLine}" +
                                $"Last-Modified: {GMTTime(DateTime.Now)}{Environment.NewLine}" +
                                $"Date: {GMTTime(DateTime.Now)}{Environment.NewLine}" +
                                $"Accept-Ranges: bytes{Environment.NewLine}" +
                                $"Content-Length: {Encoding.UTF8.GetByteCount(finalHtml)}{Environment.NewLine}" +
                                $"Connection: close{Environment.NewLine}{Environment.NewLine}";

            // Send response
            handler.Send(Encoding.UTF8.GetBytes(httpHeader), SocketFlags.None);
            handler.Send(Encoding.UTF8.GetBytes(finalHtml), SocketFlags.None);
            CloseClientSocket(handler);
        }

        //TODO: Finish implementation (done)
        /// <summary>
        /// This method determines if to stream a file or send it in its entirety.
        /// </summary>
        /// <param name="handler">The socket to send the file to</param>
        /// <param name="headers">The request headers</param>
        /// <param name="requestFile">The requested file</param>
        private void ServeFile(Socket handler, List<KeyValuePair<string, string>> headers, string requestFile)
        {
            long tempRange = 0;
            bool hasRange = false;
            KeyValuePair<string, string> rangeHeader = headers.Find(e => e.Key.Contains("Range", StringComparison.OrdinalIgnoreCase));

            if (!rangeHeader.Equals(default(KeyValuePair<string, string>)))
            {
                hasRange = true;
                string range = rangeHeader.Value.ToLower()
                    .Replace("bytes=", "")
                    .Split('-')[0]; // Extract start range
                long.TryParse(range, out tempRange);
            }

            FileSenderHeler fsHelper = new FileSenderHeler(requestFile, handler, tempRange);

            if (!hasRange || requestFile.ToLower().EndsWith(".jpg") || requestFile.ToLower().EndsWith(".png") ||
                requestFile.ToLower().EndsWith(".gif") || requestFile.ToLower().EndsWith(".mp3"))
            {
                // Send the file in its entirety
                fsHelper.SendFullFile();
            }
            else
            {
                // Stream the file in chunks for range requests
                fsHelper.StreamFile();
            }
        }


        //TODO: Finish implementation (done)
        /// <summary>
        /// Sends entire file to requestor since it is small
        /// </summary>
        /// <param name="fsHelperObj">Helper object containing data relevant for thread execution</param>
        private void NoRangeSend(object fsHelperObj)
        {
            FileSenderHeler fsHelper = (FileSenderHeler)fsHelperObj;
            Socket handler = fsHelper.getSocket();
            string requestFile = fsHelper.getRequestFile();

            if (!File.Exists(requestFile))
            {
                handler.Close();
                return;
            }

            FileInfo fInfo = new FileInfo(requestFile);
            long chunkSize = fInfo.Length > 8000000 ? 500000 : 50000; // Adjust for larger files
            long bytesSent = 0;

            string contentType = GetContentType(requestFile.ToLower());
            logger.LogDebug("Sending file: {0}", requestFile);

            // Prepare the HTTP header
            string reply = $"HTTP/1.1 200 OK{Environment.NewLine}" +
                           $"Server: VLC{Environment.NewLine}" +
                           $"Content-Type: {contentType}{Environment.NewLine}" +
                           $"Connection: close{Environment.NewLine}" +
                           $"Content-Length: {fInfo.Length}{Environment.NewLine}{Environment.NewLine}";

            handler.Send(Encoding.UTF8.GetBytes(reply), SocketFlags.None);

            // Start reading and sending file in chunks
            using (FileStream fsFile = new FileStream(requestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buffer = new byte[chunkSize];
                int bytesRead;

                while (this.running && handler.Connected && (bytesRead = fsFile.Read(buffer, 0, buffer.Length)) > 0)
                {
                    handler.Send(buffer, 0, bytesRead, SocketFlags.None);
                    bytesSent += bytesRead;
                }
            }

            CloseClientSocket(handler);
        }

        //TODO: Finish implementation (done)
        /// <summary>
        /// Streams a movie to the requestors size they are too big to go all at once
        /// </summary>
        /// <param name="fsHelperObj">Helper object containing data relevant for thread execution</param>
        private void SendWithRange(object fsHelperObj)
        {
            FileSenderHeler fsHelper = (FileSenderHeler)fsHelperObj;
            Socket handler = fsHelper.getSocket();
            string requestFile = fsHelper.getRequestFile();

            logger.LogDebug("Streaming movie: {0}", requestFile);

            long chunkSize = 500000; // Adjust chunk size dynamically if needed
            long range = fsHelper.getRange(); // Get requested range
            long bytesSent = range;
            long bytesToSend = 1;

            string contentType = GetContentType(requestFile.ToLower());
            FileInfo fInfo = new FileInfo(requestFile);
            long fileLength = fInfo.Length;

            using (FileStream fs = new FileStream(requestFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                string reply = ContentString(range, contentType, fileLength);
                handler.Send(Encoding.UTF8.GetBytes(reply), SocketFlags.None);

                byte[] buffer = new byte[chunkSize];
                if (fs.CanSeek)
                    fs.Seek(range, SeekOrigin.Begin);

                while (this.running && handler.Connected && bytesToSend > 0)
                {
                    int bytesRead = fs.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    handler.Send(buffer, 0, bytesRead, SocketFlags.None);
                    bytesSent += bytesRead;

                    // Check if the requested range has been fully transmitted
                    if (bytesSent >= fileLength)
                        break;
                }
            }

            CloseClientSocket(handler);
        }

        //INSPIRED BY ORIGINAL PROJECT
        private string EncodeUrlPaths(string Value)
        {//Encode requests sent to the DLNA device
            if (Value == null) return null;
            return Value.Replace("%", "&percnt;").Replace("&", "&amp;").Replace("\\", "/");
        }

        //FROM ORIGINAL PROJECT
        private bool IsMusicOrImage(string fileName)
        {//We don't want to use byte-ranges for music or image data so we test the filename here
            if (fileName.ToLower().EndsWith(".jpg") || fileName.ToLower().EndsWith(".png") || fileName.ToLower().EndsWith(".gif") || fileName.ToLower().EndsWith(".mp3"))
                return true;
            return false;
        }

        //FROM ORIGINAL PROJECT
        private string GetContentType(string FileName)
        {//Based on the file type we create our content type for the reply to the TV/DLNA device
            string ContentType = "audio/mpeg";
            if (FileName.ToLower().EndsWith(".jpg")) ContentType = "image/jpg";
            else if (FileName.ToLower().EndsWith(".png")) ContentType = "image/png";
            else if (FileName.ToLower().EndsWith(".gif")) ContentType = "image/gif";
            else if (FileName.ToLower().EndsWith(".avi")) ContentType = "video/avi";
            if (FileName.ToLower().EndsWith(".mp4")) ContentType = "video/mp4";
            return ContentType;
        }

        //INSPIRED FROM ORIGINAL PROJECT
        private string GMTTime(DateTime Time)
        {//Covert date to GMT time/date
            string GMT = Time.ToString("ddd, dd MMM yyyy HH':'mm':'ss 'GMT'");
            return GMT;//Example "Sat, 25 Jan 2014 12:03:19 GMT";
        }

        //FROM ORIGINAL PROJECT
        private string ContentString(long Range, string ContentType, long FileLength)
        {//Builds up our HTTP reply string for byte-range requests
            string Reply = "";
            Reply = "HTTP/1.1 206 Partial Content" + Environment.NewLine + "Server: VLC" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
            Reply += "Accept-Ranges: bytes" + Environment.NewLine;
            Reply += "Date: " + GMTTime(DateTime.Now) + Environment.NewLine;
            if (Range == 0)
            {
                Reply += "Content-Length: " + FileLength + Environment.NewLine;
                Reply += "Content-Range: bytes 0-" + (FileLength - 1) + "/" + FileLength + Environment.NewLine;
            }
            else
            {
                Reply += "Content-Length: " + (FileLength - Range) + Environment.NewLine;
                Reply += "Content-Range: bytes " + Range + "-" + (FileLength - 1) + "/" + FileLength + Environment.NewLine;
            }
            return Reply + Environment.NewLine;
        }
        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // Process the accepted connection
                ProcessAccept(e);
            }
            else
            {
                logger.LogError($"Socket error occurred: {e.SocketError}");
            }

            // Start accepting the next connection
            StartAccept(e);
        }
        private void SendNotFound(Socket handler)
        {
            string response = "HTTP/1.1 404 Not Found\r\n" +
                              "Content-Type: text/plain\r\n" +
                              "Content-Length: 13\r\n" +
                              "Connection: close\r\n\r\n" +
                              "404 Not Found";
            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
            handler.Send(responseBytes);
            CloseClientSocket(handler);
        }
    }
}
