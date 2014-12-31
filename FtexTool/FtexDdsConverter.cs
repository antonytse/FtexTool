﻿using System;
using System.Collections.Generic;
using System.Linq;
using FtexTool.Dds;
using FtexTool.Dds.Enum;
using FtexTool.Ftex;
using FtexTool.Ftex.Enum;
using FtexTool.Ftexs;

namespace FtexTool
{
    internal static class FtexDdsConverter
    {
        public static DdsFile ConvertToDds(FtexFile file)
        {
            DdsFile result = new DdsFile
            {
                Header = new DdsFileHeader
                {
                    Size = DdsFileHeader.DefaultHeaderSize,
                    Flags = DdsFileHeaderFlags.Texture | DdsFileHeaderFlags.MipMap,
                    Height = file.Height,
                    Width = file.Width,
                    Depth = file.Depth,
                    MipMapCount = file.MipMapCount,
                    Caps = DdsSurfaceFlags.Texture | DdsSurfaceFlags.MipMap
                }
            };

            switch (file.PixelFormatType)
            {
                case 0:
                    result.Header.PixelFormat = DdsPixelFormat.DdsPfA8R8G8B8();
                    result.Header.Flags = result.Header.Flags | DdsFileHeaderFlags.Volume;
                    break;
                case 1:
                    result.Header.PixelFormat = DdsPixelFormat.DdsLuminance();
                    break;
                case 2:
                    result.Header.PixelFormat = DdsPixelFormat.DdsPfDxt1();
                    break;
                case 4:
                    result.Header.PixelFormat = DdsPixelFormat.DdsPfDxt5();
                    break;
                default:
                    throw new NotImplementedException(String.Format("Unknown PixelFormatType {0}", file.PixelFormatType));
            }

            result.Data = file.Data;
            return result;
        }

        public static FtexFile ConvertToFtex(DdsFile file)
        {
            FtexFile result = new FtexFile();
            if (file.Header.PixelFormat.Equals(DdsPixelFormat.DdsPfA8R8G8B8()))
                result.PixelFormatType = 0;
            else if (file.Header.PixelFormat.Equals(DdsPixelFormat.DdsLuminance()))
                result.PixelFormatType = 1;
            else if (file.Header.PixelFormat.Equals(DdsPixelFormat.DdsPfDxt1()))
                result.PixelFormatType = 2;
            else if (file.Header.PixelFormat.Equals(DdsPixelFormat.DdsPfDxt5()))
                result.PixelFormatType = 4;
            else
                throw new NotImplementedException(String.Format("Unknown PixelFormatType {0}", file.Header.PixelFormat));
           
            result.Height = Convert.ToInt16(file.Header.Height);
            result.Width = Convert.ToInt16(file.Header.Width);
            result.Depth = Convert.ToInt16(file.Header.Depth);
            result.MipMapCount = Convert.ToByte(file.Header.MipMapCount);
            result.NrtFlag = 2;

            var mipMapData = GetMipMapData(file);
            var mipMaps = GetMipMapInfos(mipMapData);
            var ftexsFiles = GetFtexsFiles(mipMaps, mipMapData);
            result.AddMipMapInfos(mipMaps);
            result.AddFtexsFiles(ftexsFiles);
            result.FtexsFileCount = Convert.ToByte(ftexsFiles.Count());
            result.AdditionalFtexsFileCount = Convert.ToByte(ftexsFiles.Count() - 1);
            
            // TODO: Check if the texture is not a diffuse map.
            result.TextureType = FtexTextureType.DiffuseMap;
            
            // TODO: Handle the DDS depth flag.
            
            return result;
        }

        private static List<FtexsFile> GetFtexsFiles(List<FtexFileMipMapInfo> mipMapInfos, List<byte[]> mipMapDatas)
        {
            Dictionary<byte, FtexsFile> ftexsFiles = new Dictionary<byte, FtexsFile>();

            foreach (var mipMapInfo in mipMapInfos)
            {
                if (ftexsFiles.ContainsKey(mipMapInfo.FtexsFileNumber) == false)
                {
                    FtexsFile ftexsFile = new FtexsFile
                    {
                        FileNumber = mipMapInfo.FtexsFileNumber
                    };
                    ftexsFiles.Add(mipMapInfo.FtexsFileNumber, ftexsFile);
                }
            }

            for (int i = 0; i < mipMapInfos.Count; i++)
            {
                FtexFileMipMapInfo mipMapInfo = mipMapInfos[i];
                FtexsFile ftexsFile = ftexsFiles[mipMapInfo.FtexsFileNumber];
                byte[] mipMapData = mipMapDatas[i];
                FtexsFileMipMap ftexsFileMipMap = new FtexsFileMipMap();
                List<FtexsFileChunk> chunks = GetFtexsChunks(mipMapInfo, mipMapData);
                ftexsFileMipMap.AddChunks(chunks);
                ftexsFile.AddMipMap(ftexsFileMipMap);
            }
            return ftexsFiles.Values.ToList();
        }

        private static List<FtexsFileChunk> GetFtexsChunks(FtexFileMipMapInfo mipMapInfo, byte[] mipMapData)
        {
            List<FtexsFileChunk> ftexsFileChunks = new List<FtexsFileChunk>();
            const int maxChunkSize = short.MaxValue;
            int requiredChunks = (int) Math.Ceiling((double) mipMapData.Length/maxChunkSize);
            int mipMapDataOffset = 0;
            for (int i = 0; i < requiredChunks; i++)
            {
                FtexsFileChunk chunk = new FtexsFileChunk();
                int chunkSize = Math.Min(mipMapData.Length - mipMapDataOffset, maxChunkSize);
                byte[] chunkData = new byte[chunkSize];
                Array.Copy(mipMapData, mipMapDataOffset, chunkData, 0, chunkSize);
                chunk.ChunkData = chunkData;
                chunk.CompressedChunkSize = Convert.ToInt16(chunkSize);
                chunk.DecompressedChunkSize = Convert.ToInt16(chunkSize);
                ftexsFileChunks.Add(chunk);
                mipMapDataOffset += chunkSize;
            }
            return ftexsFileChunks;
        }

        private static List<FtexFileMipMapInfo> GetMipMapInfos(List<byte[]> levelData)
        {
            List<FtexFileMipMapInfo> mipMapsInfos = new List<FtexFileMipMapInfo>();
            for (int i = 0; i < levelData.Count; i++)
            {
                FtexFileMipMapInfo mipMapInfo = new FtexFileMipMapInfo();
                int fileSize = levelData[i].Length;
                mipMapInfo.DecompressedFileSize = fileSize;
                mipMapInfo.Index = Convert.ToByte(i);
                mipMapsInfos.Add(mipMapInfo);
            }

            SetMipMapFileNumber(mipMapsInfos);
            return mipMapsInfos;
        }

        private static void SetMipMapFileNumber(ICollection<FtexFileMipMapInfo> mipMapsInfos)
        {
            int fileSize = 0;
            byte nextFileNumber = 1;
            foreach (var mipMapInfo in mipMapsInfos.OrderBy(m => m.DecompressedFileSize))
            {
                mipMapInfo.FtexsFileNumber = nextFileNumber;
                fileSize += mipMapInfo.DecompressedFileSize;
                nextFileNumber = GetFtexsFileNumber(fileSize);
            }
        }

        private static byte GetFtexsFileNumber(int fileSize)
        {
            // TODO: When the mipmap count is 1-5 then always 1
            if (fileSize <= 16384)
                return 1;
            if (fileSize <= 65536)
                return 2;
            if (fileSize <= 262144)
                return 3;
            if (fileSize <= 1048576)
                return 4;
            return 5;
        }
        private static List<byte[]> GetMipMapData(DdsFile file)
        {
            List<byte[]> mipMapDatas = new List<byte[]>();
            byte[] data = file.Data;
            int dataOffset = 0;
            int size = DdsPixelFormat.CalculateImageSize(file.Header.PixelFormat, file.Header.Width, file.Header.Height);
            int minimumSize = DdsPixelFormat.GetMinimumImageSize(file.Header.PixelFormat);
            for (int i = 0; i < file.Header.MipMapCount; i++)
            {
                var buffer = new byte[size];
                Array.Copy(data, dataOffset, buffer, 0, size);
                mipMapDatas.Add(buffer);

                dataOffset += size; ;
                // BUG: In some files the size gets divided by 2 to get the size of the 2 smallest mipmaps (e.g. 128-64-32).
                size = size/4;
                if (size < minimumSize)
                    size = minimumSize;
            }
            return mipMapDatas;
        }
    }
}