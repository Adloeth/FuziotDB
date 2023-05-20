using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace FuziotDB
{
    /// <summary>
    /// A class representing an object in the database. Each object has a file and a set of fields that must be serialized in/deserialized from this file.
    /// <br></br>
    /// It's use is purely internal, you must always use the Database class.
    /// </summary>
    internal class DBObject
    {
        #region FIELDS

        /// <summary>The type of the object serialized.</summary>
        private Type type;
        /// <summary>The size in bytes of the header in the file.</summary>
        private uint headerSize = 0;
        /// <summary>The size in bytes of each objects in the file.</summary>
        private ulong objectSize;
        /// <summary>The size of the whole file in bytes.</summary>
        private long fileSize;
        /// <summary>The informations about each fields in the object that are serialized.</summary>
        private List<FieldInfo> infos = new List<FieldInfo>();
        /// <summary>A queue of free IDs. Used to avoid fragmentation when freeing data.</summary>
        private List<ulong> freeIDs = new List<ulong>();
        /// <summary>The path to the object file.</summary>
        private string filePath;
        /// <summary>Whether the object has been registered, in order to avoid using certain methods when the object has been registered.</summary>
        private bool registered;

        #endregion

        #region PROPERTIES

        /// <summary>How many fields this object have.</summary>
        public int Count => infos.Count;
        /// <inheritdoc cref="filePath"/>
        public string FilePath => filePath;

        #endregion

        #region CONSTRUCTORS

        public DBObject(Type type, string databasePath)
        {
            this.type = type;
            string name = type.Name.PascalToSnake();

            if(string.IsNullOrWhiteSpace(name))
                throw new Exception(string.Concat("File name '", name, "' is invalid."));

            if(databasePath[databasePath.Length - 1] != '/')
                this.filePath = string.Concat(databasePath, "/", name, ".dbobj");
            else
                this.filePath = string.Concat(databasePath, name, ".dbobj");

            //There are two bytes at the beginning of the file indicating how many fields there are !
            headerSize = 2;
            //There is one byte per object for the ObjectOptions !
            objectSize = 1;
        }

        #endregion

        #region REGISTER

        /// <summary>
        /// Adds a new field to the object and calculates the increment in header and object sizes.
        /// </summary>
        /// <param name="info">The reflection information about this field in order to get or set the field's value when serializing or deserializing.</param>
        /// <param name="field">The database API field information, containing it's type, size and ASCII name.</param>
        public void Add(System.Reflection.FieldInfo info, Field field)
        {
            if(registered)
                throw new Exception("Cannot add a field to the DBObject when it is registered.");

            infos.Add(new FieldInfo(field, info));
            //1B Type ; 1B Name size ; xB ASCII Name ; 2B Field size
            headerSize += 1u + 1u + (uint)field.Name.Length + 2u;
            this.objectSize += field.Size;
        }

        /// <summary>
        /// Called when the database finished the register process for this object, right before it is added to the database object dictionary.
        /// </summary>
        public void FinalizeRegister()
        {
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
                fileSize = file.Length;

            registered = true;
        }

        /// <summary>
        /// Creates the database file and writes the object header. Does nothing if the file already exists.
        /// </summary>
        public void CreateFile()
        {
            if(registered)
                throw new Exception("Cannot call CreateFile() when the DBObject is registered.");

            if(Exists())
                return;

            if(infos.Count - 1 > ushort.MaxValue)
                throw new Exception(string.Concat("There can only be a maximum of ", ushort.MaxValue + 1, " fields per objects."));

            using(FileStream file = File.Open(filePath, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(BitConverter.GetBytes((ushort)(infos.Count - 1)).ToLittleEndian());

                for(int i = 0; i < infos.Count; i++)
                    file.Write(infos[i].field.CalcHeader());
            }
        }

        #endregion

        #region FILE UTILITIES

        /// <summary>
        /// Checks whether the database file for this object already exists.
        /// </summary>
        public bool Exists() => File.Exists(filePath);

        #endregion

        #region HEADER

        /// <summary>Returns the headers of each fields as specified in the file.</summary>
        public byte[][] GetFileFieldHeaders(FileStream file)
        {
            long curPosition = file.Position;
            file.Position = 0;

            byte[] fieldCountBytes = new byte[2];
            file.Read(fieldCountBytes);
            int fieldCount = BitConverter.ToUInt16(fieldCountBytes.ToCurrentEndian(true)) + 1;

            List<byte[]> headers = new List<byte[]>(fieldCount);

            for (int i = 0; i < fieldCount; i++)
            {
                byte type = (byte)file.ReadByte();
                byte nameSize = (byte)file.ReadByte();
                byte[] name = new byte[nameSize + 1];
                file.Read(name);
                byte[] dataSize = new byte[2];
                file.Read(dataSize);
                
                List<byte> total = new List<byte>(2 + nameSize + 2);
                total.Add(type);
                total.Add(nameSize);
                total.AddRange(name);
                total.AddRange(dataSize);

                headers.Add(total.ToArray());
            }

            file.Position = curPosition;

            return headers.ToArray();
        }

        /// <summary>Returns the header as specified in the file.</summary>
        public byte[] GetFileFullHeader(FileStream file)
        {
            long curPosition = file.Position;
            file.Position = 0;

            List<byte> fullHeader = new List<byte>();

            byte[] fieldCountBytes = new byte[2];
            file.Read(fieldCountBytes);
            int fieldCount = BitConverter.ToUInt16(fieldCountBytes.ToCurrentEndian(true)) + 1;

            fullHeader.AddRange(fieldCountBytes);

            for (int i = 0; i < fieldCount; i++)
            {
                byte type = (byte)file.ReadByte();
                byte nameSize = (byte)file.ReadByte();
                byte[] name = new byte[nameSize + 1];
                file.Read(name);
                byte[] dataSize = new byte[2];
                file.Read(dataSize);
                
                fullHeader.EnsureCapacity(fullHeader.Capacity + 2 + nameSize + 2);
                fullHeader.Add(type);
                fullHeader.Add(nameSize);
                fullHeader.AddRange(name);
                fullHeader.AddRange(dataSize);
            }

            file.Position = curPosition;

            return fullHeader.ToArray();
        }

        /// <summary>Returns the entire header to be written in the file. It is the equivalent of concatenating the results of CalculateCurrentFieldHeaders().</summary>
        public byte[] CalculateCurrentFullHeader()
        {
            byte[][] headers = new byte[infos.Count][];
            int headerSize = 2;

            for(int i = 0; i < infos.Count; i++)
            {
                headers[i] = infos[i].field.CalcHeader();
                headerSize += headers[i].Length;
            }

            byte[] result = new byte[headerSize];

            byte[] fieldCountBytes = BitConverter.GetBytes((ushort)(infos.Count - 1)).ToLittleEndian();
            result[0] = fieldCountBytes[0];
            result[1] = fieldCountBytes[1];

            int pos = 0;
            for(int i = 0; i < infos.Count; i++)
                for (int j = 0; j < headers[i].Length; j++)
                    result[2 + pos++] = headers[i][j];

            return result;
        }

        /// <summary>Returns the header of each field in the object.</summary>
        public byte[][] CalculateCurrentFieldHeaders()
        {
            byte[][] result = new byte[infos.Count][];

            for(int i = 0; i < infos.Count; i++)
                result[i] = infos[i].field.CalcHeader();

            return result;
        }

        /// <summary>
        /// Checks whether the header in the database file is the same as the object's header. 
        /// (Typically it will return false if you added or removed fields in the object)
        /// </summary>
        public bool IsHeaderValid()
        {
            byte[] header;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
                header = GetFileFullHeader(file);

            Field[] fields = Field.FromHeader(header);

            if(infos.Count != fields.Length)
                return false;

            for (int i = 0; i < infos.Count; i++)
            {
                bool atLeastOneValid = false;
                for (int j = 0; j < fields.Length; j++)
                {
                    if(infos[i].field != fields[j])
                        continue;

                    atLeastOneValid = true;
                    break;
                }

                if(!atLeastOneValid)
                    return false;
            }

            return true;
        }

        #endregion

        #region FREE

        /// <summary>
        /// Adds multiple indexes to the queue of available indexes for later allocations to avoid fragmentation.
        /// </summary>
        /// <param name="id">An array of indexes to push to the queue.</param>
        public void PushFreeID(params ulong[] id) => freeIDs.AddRange(id);
        /// <summary>
        /// Adds an index to the queue of available indexes for later allocations to avoid fragmentation.
        /// </summary>
        /// <param name="id">An index to push to the queue.</param>
        public void PushFreeID(ulong id) => freeIDs.Add(id);

        #endregion

        #region PUSH

        /// <summary>
        /// Provides a position in the file where a new object can be written.
        /// It first checks if there are available indexes in the queue to fill in blanks caused by deletion of data, in order to remove fragmentation.
        /// If there are none, the function will return false, which indicates that data must be appended at the end of the file.
        /// </summary>
        /// <param name="offset">The position in bytes where the object bytes must be written.</param>
        /// <returns>False if there are no position in the middle of the file where data can be written. True otherwise.</returns>
        private bool Alloc(out ulong offset)
        {
            if(freeIDs.Count <= 0)
            {
                offset = 0;
                return false;
            }

            offset = freeIDs[0];
            freeIDs.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// Writes an instance of an object to the database file.
        /// </summary>
        /// <param name="instance">The object instance, it must have the exact same type as the DBObject type.</param>
        /// <returns>The position in bytes where the instance has been written.</returns>
        public ulong Push(object instance)
        {
            bool inMiddle = Alloc(out ulong offset);

            using(FileStream file = File.Open(filePath, inMiddle ? FileMode.Open : FileMode.Append, FileAccess.Write))
            {            
                if(inMiddle)
                {
                    if(offset > (ulong)file.Length)
                        throw new Exception("Internal error, allocation offset is invalid, fix this");

                    file.Position = (long)offset;
                }
                else
                    offset = (ulong)file.Length;

                //Write default object options
                file.WriteByte((byte)ObjectOptions.None);

                for(int i = 0; i < infos.Count; i++)
                {
                    DBVariant variant = DBVariant.FromObject(infos[i].info.GetValue(instance), infos[i].field.Size);
                    file.Write(variant.GetBytes());
                }

                fileSize = file.Length;
            }

            return offset;
        }

        #endregion

        #region SET

        /// <summary>
        /// Replaces the data of an object given it's index.
        /// </summary>
        /// <param name="id">The object index.</param>
        /// <param name="instance">The object instance, it must have the exact same type as the DBObject type.</param>
        public void Set(ulong id, object instance)
        {
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Write))
            {
                long pos = (long)(headerSize + id * objectSize);
                if(pos > file.Length)
                    throw new Exception(string.Concat("There are no object with id '", id, "'"));

                file.Position = pos;

                for(int i = 0; i < infos.Count; i++)
                {
                    object value = infos[i].info.GetValue(instance);
                    DBVariant variant = DBVariant.FromObject(value, infos[i].field.Size);
                    file.Write(variant.GetBytes());
                }
            }
        }

        #endregion

        #region FETCH UTILITIES

        /// <summary>
        /// Information about a field when searching through a file.
        /// </summary>
        struct FetchField
        {
            public long offset;
            public ushort size;
            public DBType type;

            public FetchField(long offset, ushort size, DBType type)
            {
                this.offset = offset;
                this.size = size;
                this.type = type;
            }
        }

        /// <summary>
        /// Gives all the requested fields from an object.
        /// </summary>
        /// <param name="fieldsToSearch">The fields names to search, must be ASCII characters.</param>
        /// <returns>An array of FetchFields containing information about each requested fields.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FetchField[] GetFetchFields(string[] fieldsToSearch)
        {
            FetchField[] fetchFields = new FetchField[fieldsToSearch.Length];

            for (int i = 0; i < fieldsToSearch.Length; i++)
            {
                long offset = 0;
                bool setted = false;
                for (int j = 0; j < infos.Count; j++)
                {
                    if(infos[j].field.Name == fieldsToSearch[i])
                    {
                        fetchFields[i] = new FetchField(offset, infos[j].field.Size, infos[j].field.Type);
                        setted = true;
                        break;
                    }

                    offset += infos[j].field.Size;
                }

                if(!setted)
                    throw new Exception(string.Concat("Cannot fetch ; Requested field '", fieldsToSearch[i], "' could not be found in object '", type.FullName, "'."));
            }

            return fetchFields;
        }

        /// <summary>
        /// Gives the variants of each requested fields of the current object.
        /// </summary>
        /// <param name="file">The current file stream to extract the data from.</param>
        /// <param name="objectID">The current object index.</param>
        /// <param name="fetchFields">All fields to recover from the file.</param>
        /// <returns>An array of variants corresponding to each requested field's values. The first element of the array will always be the object index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe DBVariant[] GetObjectVariants(FileStream file, ulong objectID, FetchField[] fetchFields)
        {
            DBVariant[] variants = new DBVariant[fetchFields.Length + 1];
            variants[0] = new DBVariant(objectID);
                    
            byte[] objectBytes = new byte[objectSize];
            file.Read(objectBytes);

            fixed(byte* objectBytesPtr = objectBytes)
            {
                for(int i = 0; i < fetchFields.Length; i++)
                {
                    variants[i + 1] = DBVariant.FromObjectBytes(objectBytesPtr, fetchFields[i].offset, fetchFields[i].size, fetchFields[i].type);
                }
            }

            return variants;
        }

        /// <summary>
        /// Gets necessary data to distribute work between threads when fetching.
        /// </summary>
        /// <param name="length">The amount of bytes in file.</param>
        /// <param name="threadCount">The amount of total threads in the database array</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="objectID">The object index at which to start.</param>
        /// <param name="objectPos">The real offset accounting for the header, this value incremented by `objectSize` when reading the file, which gives the exact position of each read object.</param>
        /// <param name="realCount">The last thread will have a couple more objects to analyze if the distribution of objects between threads is uneven. This way, offset calculation is easy, all threads have the same amount of objects to process except the last one which will do the rest of the file.</param>
        /// <param name="endPos">The end position in bytes at which the thread must stop reading.</param>
        private void GetAsyncFetchThreadInfo(long length, int threadCount, int threadID, out ulong objectID, out long objectPos, out long realCount, out long endPos)
        {
            bool isLastThread = threadID == threadCount - 1;

            //How many objects are in the file
            long   objectCount = (length - (long)headerSize) / (long)objectSize;
            //How many objects there are for each threads (as double, so we can check if there are decimals)
            double      dCount = objectCount / (double)threadCount;
            //How many objects there are for each threads (floored, there may be extra objects that will be given to the last thread)
            long         count = (long)Math.Floor(dCount);
            // The last thread will have a couple more objects to analyze if the distribution of objects between threads is uneven.
            // This way, offset calculation is easy, all threads have the same amount of objects to process except the last one which will do the rest
            // of the file.
                     realCount = isLastThread ? (dCount == count ? count : objectCount - count * (threadCount - 1)) : count;
            // The offset **in bytes** in the file at which the thread should start processing.
            long        offset = count * (long)objectSize * threadID;
            // The real offset accounting for the header, this value incremented by `objectSize` when reading the file, which gives the exact position
            // of each read object.
                    objectPos = headerSize + offset;
            // The end position in bytes at which the thread must stop reading.
                       endPos = offset + realCount * (long)objectSize;

            objectID = (ulong)(count * threadID);
        }

        #endregion

        #region FETCH

        /// <summary>
        /// Go through the whole file sequencially and for each object, calls the provided searchFunction to determine whether or not to insert the object in the result list.
        /// The first element of the returned arrays will be the object index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Variants are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <returns>A list of variant array.</returns>
        public ReadOnlyCollection<DBVariant[]> Fetch(Database.FetchFunc searchFunction, string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<DBVariant[]> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                result = new List<DBVariant[]>((int)((file.Length - (long)headerSize) / (long)objectSize));
                long objectPos = headerSize;
                ulong objectID = 0;

                while(objectPos < file.Length)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants))
                        result.Add(variants);

                    objectID++;
                }
            }

            return new ReadOnlyCollection<DBVariant[]>(result);
        }

        /// <summary>
        /// Go through the whole file sequencially and for each object, calls the provided searchFunction to determine whether or not to insert the object in the result list.
        /// The first element of the returned arrays will be the object index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Variants are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// <br></br>
        /// This search can be cancelled, especially useful if you are searching for one and only one object that you know is unique.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object will be added to the result list. By setting the `cancel` variable to `true`, you can stop the search immediatly.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <returns>A list of variant array.</returns>
        public ReadOnlyCollection<DBVariant[]> Fetch(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<DBVariant[]> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                result = new List<DBVariant[]>((int)((file.Length - (long)headerSize) / (long)objectSize));
                long objectPos = headerSize;
                ulong objectID = 0;
                bool cancel = false;

                while(objectPos < file.Length)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants, ref cancel))
                        result.Add(variants);

                    if(cancel)
                        break;

                    objectID++;
                }
            }

            return new ReadOnlyCollection<DBVariant[]>(result);
        }

        /// <summary>
        /// The asynchronous version of the Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <returns>A list of variant array.</returns>
        public List<DBVariant[]> Fetch(Database.FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<DBVariant[]> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong objectID, out long objectPos, out long realCount, out long endPos);
                result = new List<DBVariant[]>((int)realCount);

                while(objectPos < endPos)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants))
                        result.Add(variants);

                    objectID++;
                }
            }

            return result;
        }

        /// <summary>
        /// The asynchronous version of the cancellable Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="cancel">The shared cancel variable, when set to true, all threads stop their search. Because of the nature of multithreading, it is possible that some thread read several more objects before stopping.</param>
        /// <returns>A list of variant array.</returns>
        public List<DBVariant[]> Fetch(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<DBVariant[]> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong objectID, out long objectPos, out long realCount, out long endPos);

                result = new List<DBVariant[]>((int)realCount);

                while(objectPos < endPos)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants, ref cancel))
                        result.Add(variants);

                    if(cancel)
                        break;

                    objectID++;
                }
            }

            return result;
        }

        #endregion

        #region FETCH COUNT

        /// <summary>
        /// Go through the whole file sequencially and for each object, calls the provided searchFunction to determine whether or not to increment the count.
        /// The first element of the array given to the search function will be the object index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Variants are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <returns>How many objects passed the test.</returns>
        public long FetchCount(Database.FetchFunc searchFunction, params string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                long objectPos = headerSize;
                ulong objectID = 0;

                while(file.Position < file.Length)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants))
                        result++;

                    objectID++;
                }
            }

            return result;
        }

        /// <summary>
        /// Go through the whole file sequencially and for each object, calls the provided searchFunction to determine whether or not to increment the count.
        /// The first element of the array given to the search function will be the object index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Variants are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <returns>How many objects passed the test.</returns>
        public long FetchCount(Database.CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                long objectPos = headerSize;
                ulong objectID = 0;
                bool cancel = false;

                while(file.Position < file.Length)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants, ref cancel))
                        result++;

                    if(cancel)
                        break;

                    objectID++;
                }
            }

            return result;
        }

        /// <summary>
        /// The asynchronous version of the FetchCount function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <returns>How many objects passed the test.</returns>
        public long FetchCount(Database.FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong objectID, out long objectPos, out long realCount, out long endPos);

                while(objectPos < endPos)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants))
                        result++;

                    objectID++;
                }
            }

            return result;
        }

        /// <summary>
        /// The asynchronous version of the cancellable FetchCount function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each object, if it returns true, the object count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the object index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="cancel">The shared cancel variable, when set to true, all threads stop their search. Because of the nature of multithreading, it is possible that some thread read several more objects before stopping.</param>
        /// <returns>How many objects passed the test.</returns>
        public long FetchCount(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong objectID, out long objectPos, out long realCount, out long endPos);

                while(objectPos < endPos)
                {
                    file.Position = objectPos;
                    DBVariant[] variants = GetObjectVariants(file, objectID, fetchFields);
                    objectPos += (long)objectSize;

                    if(searchFunction(variants, ref cancel))
                        result++;

                    if(cancel)
                        break;

                    objectID++;
                }
            }

            return result;
        }

        #endregion
    }
}