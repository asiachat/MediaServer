using System.Net.Sockets;

internal class FileSenderHeler
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

    public void SendFullFile()
    {
        // Implementation for sending the full file
    }

    public void StreamFile()
    {
        // Implementation for streaming the file in chunks
    }
}
