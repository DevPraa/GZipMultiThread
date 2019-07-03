using System.IO;

namespace GZipUtils
{
    internal class FileState
    {

        public FileStream ArchFile { get; set; }
        public int Index { get; set; }
        public FileStream ComressStream { get; set; }
    }
}