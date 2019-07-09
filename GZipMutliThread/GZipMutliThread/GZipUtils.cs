using GZipUtils.Interfaces;
using GZipUtils.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;


namespace GZipUtils
{
    public class GZipUtils : IArchive
    {


        #region NEW SOLUTION
        #region Property
        uint _DefFileSizePart = (uint)Math.Pow(2, 20);
        uint _DefCountThreads = (uint)Environment.ProcessorCount;

        public uint CountThreads { get => _DefCountThreads; set => _DefCountThreads = value; }
        public uint SizePart { get => _DefFileSizePart; set => _DefFileSizePart = value; }
        #endregion

        private ConcurrentQueue<FileState> CQ_QueueRead = new ConcurrentQueue<FileState>();
        private ConcurrentQueue<FileState> CQ_QueueCompress = new ConcurrentQueue<FileState>();

        byte[][] BuffData;
        byte[][] DataCompress;
        Thread[] ThreadRead;
        Thread[] ThreadCompress;
        Thread[] ThreadArch;
        FileState[] Buff;
        FileStream CompressFile;
        FileStream ArchFile;
        long CompressFilePosition;

        public int Compress(string CompressFileName, string ArchiveFileName)
        {
            try
            {
                BuffData = new byte[_DefCountThreads][];
                DataCompress = new byte[_DefCountThreads][];
                ThreadCompress = new Thread[CountThreads];
                ThreadRead = new Thread[CountThreads];
                ThreadArch = new Thread[CountThreads];
                Buff = new FileState[_DefCountThreads];
                //Open and read file which will be compress
                CompressFile = new FileStream(CompressFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, (int)_DefFileSizePart, true);
                if (_DefFileSizePart > CompressFile.Length)
                {
                    throw new InvalidOperationException("File part size more file myself size");
                }


                ArchFile = new FileStream(ArchiveFileName, FileMode.Create);//)
                int readThreadCount = 0;
                int CountStartedThreadsRead = 0;
                int CountStartedThreadsCompress = 0;
                int CountStartedThreadsWrite = 0;
                //{
                while (CompressFile.Position < CompressFile.Length)
                {
                    
                    for (int PartsCounter = 0; (PartsCounter < (_DefCountThreads)) && (CompressFile.Position < CompressFile.Length); PartsCounter++)
                    {
                        //Если остаток файла меньше либо равен значению дефолтного блока, то мы высчитываем его размер и пишем новое значение
                        if (CompressFile.Length - CompressFile.Position <= _DefFileSizePart)
                        {
                            _DefFileSizePart = (uint)(CompressFile.Length - CompressFile.Position);
                        }
                        BuffData[PartsCounter] = new byte[_DefFileSizePart];

                        /*
                         * Запускаем потоки в которых по кускам читается входной файл и сразу же пишется таким же куском
                         */
                        ThreadRead[PartsCounter] = new Thread(PartForRead);
                        ThreadCompress[PartsCounter] = new Thread(PartForСompression);

                        ThreadRead[PartsCounter].Start(PartsCounter);
                        ThreadCompress[PartsCounter].Start();
                        readThreadCount = PartsCounter;
                        CountStartedThreadsRead++;
                    }

                    for (int PartsCounter = 0; PartsCounter < readThreadCount + 1; PartsCounter++)
                    {
                        ThreadArch[PartsCounter] = new Thread(PartForArch);
                        ThreadArch[PartsCounter].Start(PartsCounter);
                        CountStartedThreadsWrite++;
                    }

                    for (int PartsCounter = 0; PartsCounter < readThreadCount + 1; PartsCounter++)
                    {
                        ThreadArch[PartsCounter].Join();
                        PartsCounter++;
                    }

                }

                for (int PartsCounter = 0; PartsCounter < readThreadCount && (CQ_QueueCompress.Count!=0 || CQ_QueueRead.Count!=0); PartsCounter++)
                {
                    if(ThreadArch[PartsCounter].ThreadState == ThreadState.Stopped)
                    PartsCounter++;
                }
                Console.WriteLine($"Read {CountStartedThreadsRead} Compress {CountStartedThreadsCompress} Write {CountStartedThreadsWrite}");
                ArchFile.Close();
                CompressFile.Close();
                return 0;
            }
            catch (Exception e)
            {
                throw;
            }
        }


        object lockobjRead = new object();
        private void PartForRead(object Index)
        {
            try
            {
                lock (lockobjRead)
                {
                    int idx = (int)Index;
                    CompressFile.Seek(CompressFilePosition,SeekOrigin.Begin);
                    CompressFile.Read(BuffData[idx], 0, BuffData[idx].Length);
                    CQ_QueueRead.Enqueue(new FileState() { Mass = BuffData[idx], Index = idx });
                    CompressFilePosition = CompressFile.Position;
                    Console.WriteLine($"Read Position : {CompressFile.Position} - Index : {idx}");                    
                }
            }
            catch (Exception e )
            {

            }
        }

        object lockobjCompress = new object();
        private void PartForСompression()
        {
            try
            {
                    FileState obj;
                    while (!CQ_QueueRead.TryDequeue(out obj))
                    {

                    }
                    Console.WriteLine($"Compress Position : {CompressFile.Position} - Index : {obj.Index}");
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress))
                        {
                            gzs.Write(obj.Mass, 0, obj.Mass.Length);
                        }
                        CQ_QueueCompress.Enqueue(new FileState() { Mass = ms.ToArray(), Index = obj.Index });
                    }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        int CurrentWriteIndex;
        object lockobjArch = new object();
        private void PartForArch(object Index)
        {
            try
            {
                lock (lockobjArch)
                {
                    if (CurrentWriteIndex >= _DefCountThreads)
                    {
                        CurrentWriteIndex = 0;
                    }
                    FileState obj = null;
                    for (int i = 0; i < Buff.Length; i++)
                    {
                        if (Buff[i] != null && (CurrentWriteIndex == Buff[i].Index))
                        {
                            obj = Buff[i];
                            Buff[i] = null;
                            break;

                        }
                    }
                    if (obj == null)
                    {
                        while (true)
                        {
                            if (CQ_QueueCompress.TryDequeue(out obj))
                            {
                                if (obj.Index == CurrentWriteIndex)
                                {
                                    break;
                                }
                                else
                                {
                                    Buff[obj.Index] = obj;
                                }
                            }
                        }
                    }
                    
                   
                    
                    var Idx = (int)Index;
                    Console.WriteLine($"Arch Position : {CompressFile.Position} - Index : {obj.Index}");
                    //Получаем размер блока и конвертим его для записи в ячейку с информацией о размере блока
                    var PartSize = BitConverter.GetBytes(obj.Mass.Length);
                    /* 
                     * Уменьшаем размер массива на 6 байт
                     * (связано с тем, что мы далее удалим служебную шапку пакета сформированного GZipStream размером 10 байт)
                     * 6 байт потому, что 4 байта мы далее займем информацией о размере пакета
                     */
                    var tmpCompresData = new byte[obj.Mass.Length - 6];
                    //Пишем размер блока в самое начало нового массива
                    PartSize.CopyTo(tmpCompresData, 0);
                    /*
                     * Удаляем из полученного после сжатия массива первые 10 байт(шапка со служебной информацией)
                     * Копируем массив(полученный после выполнения метода Skip) 
                     * в новый массив с 4ой позиции (т.к первые 4 байта заняты размером пакета) 
                     */
                    Ext.Skip(obj.Mass, 10).CopyTo(tmpCompresData, 4);
                    //Пишем итоговый массив в Архивный поток
                    //tmpObj.ArchFile.Write(tmpCompresData, 0, tmpCompresData.Length);
                    ArchFile.Write(tmpCompresData, 0, tmpCompresData.Length);
                    CurrentWriteIndex++;
                }
            }
            catch (Exception e)
            {

                throw;
            }
        }

        byte[][] BuffDataCompress;// = new byte[_DefCountThreads][];
        byte[][] DataUnCompress;
        Thread[] ThreadDeCompress;

        public int Decompress(string ArchiveFileName, string OutFileName)
        {
            int _CustomSizePart;
            int SizeCompressBlock;


            try
            {
                DataUnCompress = new byte[_DefCountThreads][];
                BuffDataCompress = new byte[_DefCountThreads][];
                ThreadDeCompress = new Thread[CountThreads];

                using (FileStream ArchFile = new FileStream(ArchiveFileName, FileMode.OpenOrCreate, FileAccess.Read))
                {
                    using (FileStream DecompressFile = new FileStream(OutFileName, FileMode.Append, FileAccess.Write))
                    {
                        byte[] LengthBuff = new byte[4];
                        while (ArchFile.Position < ArchFile.Length)
                        {
                            for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ArchFile.Position < ArchFile.Length); PartsCounter++)
                            {
                                ArchFile.Read(LengthBuff, 0, 4);
                                SizeCompressBlock = BitConverter.ToInt32(LengthBuff, 0);
                                BuffDataCompress[PartsCounter] = new byte[SizeCompressBlock];

                                /*
                                 * Восстанавливаем шапку каждого пакета в соответсвии со спецификацией.
                                 * Восстанавливаем мы её в связи с тем, что при сжатии мы её удаляли для уменьшения размера пакетов.
                                 */
                                //ФОРМАТ БАЙТА в побитовой значимости
                                //                                +--------+
                                //                                |76543210|
                                //                                +--------+
                                // http://www.zlib.org/rfc-gzip.html#member-format  - спецификация
                                BuffDataCompress[PartsCounter][0] = 31; //ID1
                                BuffDataCompress[PartsCounter][1] = 139;//ID1
                                BuffDataCompress[PartsCounter][2] = 8;  //CM = 8 обозначает метод «спящего» сжатия, который обычно используется gzip и который документирован в другом месте.
                                BuffDataCompress[PartsCounter][3] = 0;  //FLG
                                BuffDataCompress[PartsCounter][4] = 0;
                                BuffDataCompress[PartsCounter][5] = 0;
                                BuffDataCompress[PartsCounter][6] = 0;
                                BuffDataCompress[PartsCounter][7] = 0;
                                //LengthBuff.CopyTo(BuffDataCompress[PartsCounter], 4); //MTIME - время в формате UNIX
                                BuffDataCompress[PartsCounter][8] = 4;  //XFL 2 - максимальная компрессия 4 - быстрое сжатие
                                BuffDataCompress[PartsCounter][9] = 0;  //OS - 0 -FAT filesystem (MS-DOS, OS/2, NT/Win32)  
                                                                        //     1 - Amiga
                                                                        //     2 - VMS(or OpenVMS)
                                                                        //     3 - Unix
                                                                        //     4 - VM / CMS
                                                                        //     5 - Atari TOS
                                                                        //     6 - HPFS filesystem(OS / 2, NT)
                                                                        //     7 - Macintosh
                                                                        //     8 - Z - System
                                                                        //     9 - CP / M
                                                                        //     10 - TOPS - 20
                                                                        //     11 - NTFS filesystem(NT)
                                                                        //     12 - QDOS
                                                                        //     13 - Acorn RISCOS
                                                                        //     255 - unknown
                                ArchFile.Read(BuffDataCompress[PartsCounter], 10, SizeCompressBlock - 10);                             
                                _CustomSizePart = BitConverter.ToInt32(BuffDataCompress[PartsCounter], (SizeCompressBlock - 4));
                                DataUnCompress[PartsCounter] = new byte[_CustomSizePart];
                                ThreadDeCompress[PartsCounter] = new Thread(PartForDecompress);
                                ThreadDeCompress[PartsCounter].Start(PartsCounter);
                            }
                            for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ThreadDeCompress[PartsCounter] != null);)
                            {
                                if (ThreadDeCompress[PartsCounter].ThreadState == ThreadState.Stopped)
                                {
                                    DecompressFile.Write(DataUnCompress[PartsCounter], 0, DataUnCompress[PartsCounter].Length);
                                    PartsCounter++;
                                }
                            }
                        }
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:" + ex.Message);
                return 1;
            }
        }

        private void PartForDecompress(object i)
        {
            int Index = (int)i;
            using (MemoryStream ms = new MemoryStream(BuffDataCompress[Index]))
            {
                using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
                {
                    gzs.Read(DataUnCompress[Index], 0, DataUnCompress[Index].Length);
                }
            }
        }

	#endregion





        #region OLD SOLUTION
        //private static int CountThreads = Environment.ProcessorCount;
        //private static byte[][] Data = new byte[CountThreads][];//= new byte[10][];
        //private static byte[][] DataCompress = new byte[CountThreads][];
        //bool IsCancel = false;

        //public int Compress(string CompressFileName, string ArchiveFileName)
        //{
        //    int DefSizePart = (int)Math.Pow(2, 20);
        //    int _CustomSizePart;
        //    Thread[] ThrdsCmprs;
        //    try
        //    {
        //        using (FileStream CompressFile = new FileStream(CompressFileName, FileMode.OpenOrCreate, FileAccess.Read))
        //        {
        //            using (FileStream ArchFile = new FileStream(ArchiveFileName, FileMode.Create))
        //            {
        //                Console.WriteLine("Compressing");
        //                while (CompressFile.Position < CompressFile.Length)
        //                {
        //                    if (IsCancel)
        //                        break;
        //                    ThrdsCmprs = new Thread[CountThreads];
        //                    for (int PartsCounter = 0; (PartsCounter < CountThreads) && (CompressFile.Position < CompressFile.Length); PartsCounter++)
        //                    {
        //                        if (CompressFile.Length - CompressFile.Position <= DefSizePart)
        //                        {
        //                            _CustomSizePart = (int)(CompressFile.Length - CompressFile.Position);
        //                        }
        //                        else
        //                        {
        //                            _CustomSizePart = DefSizePart;
        //                        }
        //                        Data[PartsCounter] = new byte[_CustomSizePart];
        //                        CompressFile.Read(Data[PartsCounter], 0, _CustomSizePart);

        //                        ThrdsCmprs[PartsCounter] = new Thread(PartForСompression);
        //                        ThrdsCmprs[PartsCounter].Start(PartsCounter);
        //                    }

        //                    for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ThrdsCmprs[PartsCounter] != null);)
        //                    {
        //                        if (ThrdsCmprs[PartsCounter].ThreadState == System.Threading.ThreadState.Stopped)
        //                        {
        //                            var ttt = BitConverter.GetBytes(DataCompress[PartsCounter].Length);
        //                            var tmpCompresData = new byte[DataCompress[PartsCounter].Length - 6];
        //                            ttt.CopyTo(tmpCompresData, 0);
        //                            Ext.Skip(DataCompress[PartsCounter], 10).CopyTo(tmpCompresData, 4);
        //                            ArchFile.Write(tmpCompresData, 0, tmpCompresData.Length);
        //                            PartsCounter++;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        return 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //        return 1;
        //    }
        //}

        //public static void PartForСompression(object i)
        //{
        //    int Index = (int)i;
        //    using (MemoryStream ms = new MemoryStream(Index))
        //    {
        //        using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress))
        //        {
        //            gzs.Write(Data[Index], 0, Data[Index].Length);
        //        }
        //        DataCompress[(int)i] = ms.ToArray();
        //    }
        //}

        //public int Decompress(string ArchiveFileName, string OutFileName)
        //{
        //    int _CustomSizePart;
        //    int SizeCompressBlock;
        //    Thread[] ThrdsDcmp;

        //    try
        //    {
        //        using (FileStream ArchFile = new FileStream(ArchiveFileName, FileMode.OpenOrCreate, FileAccess.Read))
        //        {
        //            using (FileStream DecompressFile = new FileStream(OutFileName, FileMode.Append, FileAccess.Write))
        //            {
        //                Console.WriteLine("Decompressing");
        //                byte[] LengthBuff = new byte[4];
        //                while (ArchFile.Position < ArchFile.Length)
        //                {
        //                    if (IsCancel)
        //                        break;
        //                    ThrdsDcmp = new Thread[CountThreads];
        //                    for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ArchFile.Position < ArchFile.Length); PartsCounter++)
        //                    {
        //                        ArchFile.Read(LengthBuff, 0, 4);
        //                        SizeCompressBlock = BitConverter.ToInt32(LengthBuff, 0);
        //                        DataCompress[PartsCounter] = new byte[SizeCompressBlock];
        //                        //ФОРМАТ БАЙТА в побитовой значимости
        //                        //                                +--------+
        //                        //                                |76543210|
        //                        //                                +--------+
        //                        // http://www.zlib.org/rfc-gzip.html#member-format  - спецификация
        //                        DataCompress[PartsCounter][0] = 31; //ID1
        //                        DataCompress[PartsCounter][1] = 139;//ID1
        //                        DataCompress[PartsCounter][2] = 8;  //CM = 8 обозначает метод «спящего» сжатия, который обычно используется gzip и который документирован в другом месте.
        //                        DataCompress[PartsCounter][3] = 0;  //FLG
        //                        LengthBuff.CopyTo(DataCompress[PartsCounter], 4); //MTIME - время в формате UNIX
        //                        DataCompress[PartsCounter][8] = 4;  //XFL 2 - максимальная компрессия 4 - быстрое сжатие
        //                        DataCompress[PartsCounter][9] = 0;  //OS - 0 -FAT filesystem (MS-DOS, OS/2, NT/Win32)  
        //                                                            //     1 - Amiga
        //                                                            //     2 - VMS(or OpenVMS)
        //                                                            //     3 - Unix
        //                                                            //     4 - VM / CMS
        //                                                            //     5 - Atari TOS
        //                                                            //     6 - HPFS filesystem(OS / 2, NT)
        //                                                            //     7 - Macintosh
        //                                                            //     8 - Z - System
        //                                                            //     9 - CP / M
        //                                                            //     10 - TOPS - 20
        //                                                            //     11 - NTFS filesystem(NT)
        //                                                            //     12 - QDOS
        //                                                            //     13 - Acorn RISCOS
        //                                                            //     255 - unknown
        //                        ArchFile.Read(DataCompress[PartsCounter], 10, SizeCompressBlock - 10);
        //                        _CustomSizePart = BitConverter.ToInt32(DataCompress[PartsCounter], SizeCompressBlock - 4);
        //                        Data[PartsCounter] = new byte[_CustomSizePart];
        //                        ThrdsDcmp[PartsCounter] = new Thread(PartForDecompress);
        //                        ThrdsDcmp[PartsCounter].Start(PartsCounter);
        //                    }
        //                    for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ThrdsDcmp[PartsCounter] != null);)
        //                    {
        //                        if (ThrdsDcmp[PartsCounter].ThreadState == ThreadState.Stopped)
        //                        {
        //                            DecompressFile.Write(Data[PartsCounter], 0, Data[PartsCounter].Length);
        //                            PartsCounter++;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        return 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("ERROR:" + ex.Message);
        //        return 1;
        //    }
        //}

        //public static void PartForDecompress(object i)
        //{
        //    int Index = (int)i;
        //    using (MemoryStream ms = new MemoryStream(DataCompress[Index]))
        //    {
        //        using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
        //        {
        //            gzs.Read(Data[Index], 0, Data[Index].Length);
        //        }
        //    }
        //} 
        #endregion
    }
}
