using System;

namespace YunoArc
{
    public class FileData
    {
        public string Name;
        public int Position;
        public int Size;

        public FileData(string name, int position, int size)
        {
            Name = name;
            Position = position;
            Size = size;
        }
    }
}
