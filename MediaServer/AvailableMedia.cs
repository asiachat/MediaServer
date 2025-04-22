using System.Collections.Generic;
using System.IO;

namespace MediaServer
{
    public class AvailableMedia
    {
        static private string[] fileArray;
        static string path;

        public AvailableMedia(string path)
        {
            path = path.ToLower();
            if (AvailableMedia.path == null || !AvailableMedia.path.Equals(path))
            {
                AvailableMedia.path = path;
                fileArray = Directory.GetFiles(path, "*.mp3", SearchOption.AllDirectories).Union(Directory.GetFiles(path, "*.mp4", SearchOption.AllDirectories)).ToArray();
                //TODO: ADD jpg, png, and gif to the fileArray 
            }
        }
        public IEnumerable<string> getAvailableFiles()
        {
            return fileArray;
        }

        public string stripPath(string filename)
        {
            return filename.ToLower().Replace(AvailableMedia.path, "");
        }

        public string getAbsolutePath(int index)
        {
            return fileArray[index];
        }
    }
}
