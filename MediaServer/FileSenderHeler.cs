using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer
{
    class FileSenderHeler
    {
        private string requestFile;
        private Socket socket;
        private long range;

        public FileSenderHeler(string requestFile, Socket socket, long range)
        {
            this.requestFile = requestFile;
            this.socket = socket;
            this.range = range;
        }

        public string getRequestFile()
        {
            return requestFile;
        }

        public Socket getSocket()
        {
            return socket;
        }

        public long getRange()
        {
            return range;
        }
    }
}
