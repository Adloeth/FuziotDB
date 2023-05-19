# FuziotDB

Yet another file format project of mine. FuziotDB is an embedded relational database written in C# that doesn't use SQL. I needed an embedded relational database for a HTTP server project but most popular solutions (like SQLite) are using SQL which slows down the program because of the interpretation behind it. Ultimately I made it for fun, to understand how these types of data storage work and I was curious to see how much better a database system I make in a week could perform against SQLite.

I definitly need to do a proper benchmark but with the default settings of SQLite (There are no good documentation for Microsoft.Data.Sqlite unfortunatly), on my machine, SQLite took 4 hours to insert one million objects in the database while this system took 16 seconds and this database is usually 1.5 to 2 times faster to fetch information.

Obviously I am missing a lot of features most SQL implementations have, but for my current project, I don't need more than saving and fetching.

## Variants

The database can only serialize specific C# data-types, all of which are handled by the DBVariant class :
- bool, sbyte and byte
- ushort, short and half
- uint, int and single
- ulong, long and double
- Guid and Numerics.BigInteger
- string

I have also made a few custom types for specific uses :
- ASCIIS (For ASCII String) is a simple wrapper of a C# string but guarantees that the string is an ASCII string, because C# strings are UTF-16 (2 bytes per character).
- Blob is a wrapper for a byte[] that is serialized as-is (not changed depending on the system's endianness) to handle big sets of bytes or even files.
- VarUInt and VarInt are experimental, I wanted to see if I could make an integer type that can have any amount of bytes so I could save an RGB value with a 3 byte integer or a 4x4 single floating point matrix with a 64 byte integer.

## File format

Each fields in a C# class can have the attribute `DBSerialize`, a class that has at least one field with this attribute can be registered to a `Database` (by calling `Database.Register<Type>()`) this will create a file for the class, each class has one and one file only.

The file begins with a header where for each field, there is 1 byte for a type enum, 1 byte for the byte length of the field's name (in ASCII) (meaning that the name can have a maximum of 256 characters (256 and not 255 because a value of `0` means `1`, a field **must** have a name, so we don't waste !)), a set of bytes for the name and 2 bytes for the size of the field's data in bytes (minus one, same reason than for the field's name).

For example, if we want to serialize an `int test` field, it will generate a header with `DBType.Int` as a type, `3` for the name length, 4 bytes for the name `t`, `e`, `s`, `t` and 2 bytes for the value `3`, because an `int` is 4 bytes long. This will result in this header :
```
01 03 74 65 73 74 03 00
↑  ↑  └────┬────┘ └─┬─┘
│  │      name     size
│  └ name length
└ DBType.Int
```

Then, when an instance of this class is written, fields are written one after the other in the same order as defined in the header. So in the above example it will simply be 4 bytes per object.

Everything must be little endian, on big endian systems a conversion must be made.

## Fetch

In order to retreive information from an object, you can use the `Database.Fetch<T>` methods for synchrnous search or `Database.AsyncFetch<T>` coupled with `Database.WaitForResult()` for multithreaded search. In any case, you must provide a fetch function delegate which receives as an argument an array of `DBVariant`. This delegate will be called for every object in the database, the first element of the variant array is the current object's index, you can optionally give a set of strings to the fetch method corresponding to the names of the fields you want to retreive, these fields will appear in the variant array in the same order you asked for them.

### Example

Here is a simple class example :

```cs
class Test
{
    [DBSerialize("a")] private int test0;
    [DBSerialize("b")] private byte test1;
    [DBSerialize("c")] private double test2;
}
```

And here is how you can search through the database. In this example, only objects with an ID greater or equal to `100` and with `Test.test2` less than `0.125` will be retreived in the list.

```cs
Database db = new Database("path/to/database/folder/");
db.Register<Test>();

ReadOnlyCollection<DBVariant[]> result = db.Fetch<Test>((DBVariant[] fields) => 
{
    //First element of the array is the object index in the file.
    if(fields[0] < 100ul)
        return false;

    //The other indexes are the requested fields in the order you asked for.
    //In this case, index 1 is the field "c", or Test.test2, a double.
    return fields[1] < 0.125;
    
}, 
//The requested fields params
"c", "a", "b");
```

Note that this fetch function is not optimal, we are asking to retreive the fields `a`, `b` and `c` (all of them in this case), but we are only using `c`, removing the others will speed the search.

## Multithreading example

Multithreading is simple enough, using the exact same example :

```cs
Database db = new Database("path/to/database/folder/");
db.Register<Test>();

FetchThreadInfo info = db.FetchAsync<Test>((DBVariant[] fields) => 
{
    //First element of the array is the object index in the file.
    if(fields[0] < 100ul)
        return false;

    //The other indexes are the requested fields in the order you asked for.
    //In this case, index 1 is the field "c", or Test.test2, a double.
    return fields[1] < 0.125;
    
}, 
//The fields params
"c", "a", "b");

//We pause the current thread and wait for the fetch to finish.
DBVariant[][] data = info.WaitForResult();
```

This method is drastically faster, you can specify how many thread the database can use when creating it :
`new Database("path/to/database/folder/", nbOfThread)`, you can set it to 0 threads to remove the multithreading capability entirely (if you want to spare a little memory for the whole thread part). If you don't set it altogether, it will default to `Environment.ProcessorCount` (How many virtual cores you have).