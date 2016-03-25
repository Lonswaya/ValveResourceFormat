﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ValveResourceFormat
{
    public class CompiledShader : IDisposable
    {
        public const int MAGIC = 0x32736376; // "vcs2"

        private BinaryReader Reader;

        /// <summary>
        /// Releases binary reader.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Reader != null)
            {
                Reader.Dispose();
                Reader = null;
            }
        }

        /// <summary>
        /// Opens and reads the given filename.
        /// The file is held open until the object is disposed.
        /// </summary>
        /// <param name="filename">The file to open and read.</param>
        public void Read(string filename)
        {
            var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            Read(fs);
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        public void Read(Stream input)
        {
            Reader = new BinaryReader(input);

            if (Reader.ReadUInt32() != MAGIC)
            {
                throw new InvalidDataException("Given file is not a vcs2.");
            }

            // This is static across all files, is it version?
            if (Reader.ReadUInt32() != 0x0000003E)
            {
                throw new InvalidDataException("Not 3E.");
            }

            var wtf = Reader.ReadUInt32();

            // TODO: Odd assumption, features.vcs seems to have this
            if (wtf < 100)
            {
                Console.WriteLine("wtf: {0}", wtf);
                ReadFeatures();
            }
            else
            {
                Reader.BaseStream.Position -= 4;
                ReadShader();
            }
        }

        private void ReadFeatures()
        {
            var name = Encoding.UTF8.GetString(Reader.ReadBytes(Reader.ReadInt32()));

            Console.WriteLine("Name: {0}", name);
        }

        private void ReadShader()
        {
            var fileIdentifier = Reader.ReadBytes(16);

            // This appears to always be CD B1 D4 A1 82 20 D5 E2 D5 A3 78 2E C8 0F D7 7C 0E 00 00 00
            // Including aperture robot repair and dota 2
            var staticIdentifier = Reader.ReadBytes(20);

            Console.WriteLine("File identifier: {0}", BitConverter.ToString(fileIdentifier));
            Console.WriteLine("Static identifier: {0}", BitConverter.ToString(staticIdentifier));

            // 2
            var count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            for (var i = 0; i < count; i++)
            {
                var previousPosition = Reader.BaseStream.Position;

                var name = Reader.ReadNullTermString(Encoding.UTF8);

                Reader.BaseStream.Position = previousPosition + 128;

                var a = Reader.ReadInt32();
                var b = Reader.ReadInt32();
                var c = Reader.ReadInt32();
                var d = Reader.ReadInt32();
                var e = Reader.ReadInt32();
                var f = Reader.ReadInt32();

                Console.WriteLine($"{a} {b} {c} {d} {e} {f} {name}");
            }

            // 3
            count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            for (var i = 0; i < count; i++)
            {
                Reader.BaseStream.Position += 118 * 4;
            }

            // 4
            count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            for (var i = 0; i < count; i++)
            {
                var previousPosition = Reader.BaseStream.Position;

                var name = Reader.ReadNullTermString(Encoding.UTF8);

                Reader.BaseStream.Position = previousPosition + 128;

                var a = Reader.ReadInt32();
                var b = Reader.ReadInt32();
                var c = Reader.ReadInt32();
                var d = Reader.ReadInt32();
                var e = Reader.ReadInt32();
                var f = Reader.ReadInt32();

                Console.WriteLine($"{a} {b} {c} {d} {e} {f} {name}");
            }

            // 5
            count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            for (var i = 0; i < count; i++)
            {
                Reader.BaseStream.Position += 118 * 4;
            }

            // 6
            count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            for (var i = 0; i < count; i++)
            {
                var previousPosition = Reader.BaseStream.Position;

                var name = Reader.ReadNullTermString(Encoding.UTF8);

                Reader.BaseStream.Position = previousPosition + 200; // ??

                var type = Reader.ReadInt32();
                var b = Reader.ReadInt32();

                Reader.BaseStream.Position = previousPosition + 480; // wrong

                Console.WriteLine($"{type} {b} {name}");
            }

            // 7
            count = Reader.ReadUInt32();

            Console.WriteLine("Count: {0} - Offset: {1}", count, Reader.BaseStream.Position);

            //ReadShaderChunk();
        }

        private void ReadShaderChunk()
        {
            // Steam\steamapps\common\dota 2 beta\game\dota\shaders\vfx\hero_pc_40_ps.vcs
            Reader.BaseStream.Position = 90272; // Offset to number of LZMA chunks
            var lzmaCount = Reader.ReadUInt32();

            Reader.BaseStream.Position = 237892; // Start of offsets
            var lzmaOffsets = new uint[lzmaCount];

            for (int i = 0; i < lzmaCount; i++)
            {
                lzmaOffsets[i] = Reader.ReadUInt32();
            }

            // Yes, well aware that I can do reading of offsets and parsing in one go, but this is super temp
            for (int i = 0; i < lzmaCount; i++)
            {
                Reader.BaseStream.Position = lzmaOffsets[i];

                var chunkSize = Reader.ReadUInt32();

                if (Reader.ReadUInt32() != 0x414D5A4C)
                {
                    throw new InvalidDataException("Not LZMA?");
                }

                var uncompressedSize = Reader.ReadUInt32();
                var compressedSize = Reader.ReadUInt32();

                Console.WriteLine("Chunk size: {0}", chunkSize);
                Console.WriteLine("Compressed size: {0}", compressedSize);
                Console.WriteLine("Uncompressed size: {0} ({1:P2} compression)", uncompressedSize, (uncompressedSize - compressedSize) / (double)uncompressedSize);

                var decoder = new SevenZip.Compression.LZMA.Decoder();
                decoder.SetDecoderProperties(Reader.ReadBytes(5));

                var compressedBuffer = Reader.ReadBytes((int)compressedSize);

                using (var inputStream = new MemoryStream(compressedBuffer))
                using (var outStream = new MemoryStream((int)uncompressedSize))
                {
                    decoder.Code(inputStream, outStream, compressedBuffer.Length, uncompressedSize, null);

                    var outData = outStream.ToArray();
                    // Let's not dump all shaders to desktop shall we
                    if (i == 1)
                    {
                        File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "shader_out_" + i + ".bin"), outData);
                    }
                }
            }
        }
    }
}
