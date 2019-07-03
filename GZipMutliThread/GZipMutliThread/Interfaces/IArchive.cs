using System;
using System.Collections.Generic;
using System.Text;


namespace GZipUtils.Interfaces
{
    interface IArchive
    {
        uint CountThreads { get; set; }

        uint SizePart { get; set; }

        int Compress(string CompressFileName, string ArchiveFileName);

        int Decompress(string ArchiveFileName, string OutFileName);
    }
}
