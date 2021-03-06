// #define USE_SORT_ORDER
using System;
using System.Collections.Generic;
using System.IO;
#if USE_SORT_ORDER
using System.Linq;
#endif

namespace YunoArc
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2)
                return Error($"Usage: {AppDomain.CurrentDomain.FriendlyName} <-u|-p> path [output-path]");

            var mode = args[0];
            var path = args[1];

            switch (mode)
            {
                case "-u":
                    var outputDirectory = args.Length >= 3 ? args[2] : Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_UNPACK");

                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(stream))
                    {
                        // Header
                        var count = reader.ReadUInt16();
                        var magic = reader.ReadUInt16(); // 0x5501

                        if (magic != 0x5501)
                            throw new InvalidDataException();

                        // List files
                        var files = new List<FileData>();

                        for (var i = 0; i < count; ++i)
                        {
                            var name = reader.ReadStringEncrypted(12);
                            var sizeHigh = reader.ReadUInt16Encrypted();
                            var positionLow = reader.ReadUInt16Encrypted();
                            var positionHigh = reader.ReadUInt16Encrypted();
                            var sizeLow = reader.ReadUInt16Encrypted();
                            var size = (sizeHigh << 16) | sizeLow;
                            var position = ((positionHigh << 16) | positionLow) + (4 + count * 20);

                            files.Add(new FileData(name, position, size));
                        }

                        // Write files
                        for (var i = 0; i < files.Count; ++i)
                        {
                            var file = files[i];

                            stream.Position = file.Position;

                            var bytes = reader.ReadBytes(file.Size);
                            var extension = Path.GetExtension(file.Name);

                            switch (extension.ToUpper())
                            {
                                case ".MES":
                                    bytes = ElfLZSS.Decompress(bytes);

                                    break;
                            }

#if USE_SORT_ORDER
                            File.WriteAllBytes(Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(file.Name)}@{i}{extension}"), bytes);
#else
                            File.WriteAllBytes(Path.Combine(outputDirectory, file.Name), bytes);
#endif
                        }
                    }

                    break;

                case "-p":
                    var outputPath = args.Length >= 3 ? args[2] : path + "_PACK";
                    var inputFilePaths =
#if USE_SORT_ORDER
                        Directory.GetFiles(path)
                        .Select(x => GetPackOrderByFileName(x))
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Name)
                        .Select(x => x.Name)
                        .ToArray();
#else
                        Directory.GetFiles(path);
#endif

                    using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write((ushort)inputFilePaths.Length);
                        writer.Write((ushort)0x5501);

                        var basePosition = stream.Position + inputFilePaths.Length * 20;
                        var currentPosition = basePosition;

                        // Files
                        foreach (var inputFilePath in inputFilePaths)
                        {
                            // Contents
                            var bytes = File.ReadAllBytes(inputFilePath);
                            var extension = Path.GetExtension(inputFilePath);
                            var previousPosition = stream.Position;

                            switch (extension.ToUpper())
                            {
                                case ".MES":
                                    bytes = ElfLZSS.Compress(bytes);

                                    break;
                            }

                            stream.Position = currentPosition;

                            writer.Write(bytes);

                            stream.Position = previousPosition;

                            // Meta
                            var name = Path.GetFileName(inputFilePath);
                            var size = bytes.Length;
                            var position = currentPosition - basePosition;

#if USE_SORT_ORDER
                            if (name.Contains("@"))
                                name = name.Substring(0, name.IndexOf("@")) + Path.GetExtension(name);
#endif

                            writer.WriteStringEncrypted(name, 12);
                            writer.WriteUInt16Encrypted((ushort)((size >> 16) & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)(position & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)((position >> 16) & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)(size & 0xFFFF));

                            currentPosition += size;
                        }
                    }

                    break;

                default:
                    return Error($"Unknown parameter: {args[0]}");
            }

            return 0;
        }

#if USE_SORT_ORDER
        private static OrderData GetPackOrderByFileName(string path)
        {
            // "name@order.ext"
            var order = int.MaxValue;
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (fileName.Contains("@"))
            {
                var seperatorPosition = fileName.IndexOf("@");
                var orderString = fileName.Substring(seperatorPosition + 1);

                if (int.TryParse(orderString, out var o))
                    order = o;
            }

            return new OrderData(path, order);
        }
#endif

        private static int Error(string message)
        {
            Console.WriteLine(message);

            return -1;
        }
    }
}
