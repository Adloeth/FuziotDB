using System;

namespace FuziotDB
{
    [Flags] public enum InstanceOptions : byte
    {
        None = 0,
        //Option0 = 1,
        //Option1 = 2,
        //Option2 = 4,
        //Option3 = 8,
        //Option4 = 16,
        //Option5 = 32,
        //Option6 = 64,
        Deleted = 128
    }
}