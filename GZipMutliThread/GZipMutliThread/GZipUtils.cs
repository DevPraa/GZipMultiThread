using GZipUtils.Interfaces;
using GZipUtils.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;


namespace GZipUtils
{
    public class GZipUtils : IArchive
    {
        #region Property
        uint _DefFileSizePart = (uint)Math.Pow(2, 10);
        uint _DefCountThreads = (uint)Environment.ProcessorCount;

        public uint CountThreads { get => _DefCountThreads; set => _DefCountThreads = value; }
        public uint SizePart { get => _DefFileSizePart; set => _DefFileSizePart = value; }
        #endregion


        byte[][] BuffData;// = new byte[_DefCountThreads][];
        byte[][] DataCompress;
        Thread[] ThreadCompress;


        public int Compress(string CompressFileName, string ArchiveFileName)
        {
            try
            {
                BuffData = new byte[_DefCountThreads][];
                DataCompress = new byte[_DefCountThreads][];
                ThreadCompress = new Thread[CountThreads];
                //Open and read file which will be compress
                using (FileStream CompressFile = new FileStream(CompressFileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, (int)_DefFileSizePart, true))
                {
                    if (_DefFileSizePart > CompressFile.Length)
                    {
                        throw new InvalidOperationException("File part size more file myself size");
                    }

                    //Create archive file
                    using (FileStream ArchFile = new FileStream(ArchiveFileName, FileMode.Create))
                    {
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

                                FileState FS = new FileState()
                                {
                                    ArchFile = ArchFile,
                                    ComressStream = CompressFile,
                                    Index = PartsCounter
                                };

                                /*
                                 * Запускаем потоки в которых по кускам читается входной файл и сразуже пишется таким же куском
                                 */
                                ThreadCompress[PartsCounter] = new Thread(PartForСompression);
                                ThreadCompress[PartsCounter].Priority = ThreadPriority.Highest;
                                ThreadCompress[PartsCounter].Start(FS);

                            }

                            for (int PartsCounter = 0; (PartsCounter < CountThreads) && (ThreadCompress[PartsCounter] != null);)
                            {
                                ThreadCompress[PartsCounter].Join();
                                PartsCounter++;
                            }
                        }
                    }
                }
                return 0;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        private void PartForСompression(object FileState)
        {
            try
            {
                var tmpObj = (FileState)FileState;
                var tmp = tmpObj.ComressStream.Read(BuffData[tmpObj.Index], 0, BuffData[tmpObj.Index].Length);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress))
                    {
                        gzs.Write(BuffData[tmpObj.Index], 0, BuffData[tmpObj.Index].Length);
                    }
                    //Получаем размер блока и конвертим его для записи в ячейку с информацией о размере блока
                    var PartSize = BitConverter.GetBytes(ms.ToArray().Length);
                    var tmpCompresData = new byte[ms.ToArray().Length - 6];
                    //Пишем размер блока в самое начало массива
                    PartSize.CopyTo(tmpCompresData, 0);
                    Ext.Skip(ms.ToArray(), 10).CopyTo(tmpCompresData, 4);
                    tmpObj.ArchFile.Write(tmpCompresData, 0, tmpCompresData.Length);








                    //tmpObj.ArchFile.Write(ms.ToArray(), 0, ms.ToArray().Length);
                }
            }
            catch(Exception e)
            {
                throw;
            }
        }















        public int Decompress(string ArchiveFileName, string OutFileName)
        {
            try
            {
                return 0;
            }
            catch
            {
                throw;
            }
        }
    }
}
