using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FuziotDB
{
    /// <summary>
    /// A class representing a type in the database. Each type has a file and a header defining the fields that must be serialized in/deserialized from 
    /// this file. Instances can be pushed to the file and each field's values are written to it in the same order as declared in the header.
    /// Instances can be freed (deleted), modified, fetched (searched).
    /// <br></br>
    /// It's use is purely internal, you must always use the Database class.
    /// </summary>
    internal class DBType
    {
        #region FIELDS

        /// <summary>The type to serialized.</summary>
        private Type type;
        /// <summary>The size in bytes of the header in the file.</summary>
        private uint headerSize = 0;
        /// <summary>The size in bytes of each instances in the file.</summary>
        private ulong instanceSize;
        /// <summary>The size of the whole file in bytes.</summary>
        private long fileSize;
        /// <summary>The informations about each fields the type has.</summary>
        private List<FieldInfo> infos = new List<FieldInfo>();
        /// <summary>A queue of free IDs. Used to avoid fragmentation when freeing data.</summary>
        private List<ulong> freeIDs = new List<ulong>();
        /// <summary>The path to the type file.</summary>
        private string filePath;
        /// <summary>Whether the type has been registered, in order to avoid using certain methods when the type has been registered.</summary>
        private bool registered;

        #region MULTITHREADING

        private bool isWriting;
        private int threadsReading;

        #endregion

        #endregion

        #region PROPERTIES

        /// <summary>How many fields this type have.</summary>
        public int FieldCount => infos.Count;
        /// <inheritdoc cref="filePath"/>
        public string FilePath => filePath;

        public bool IsReading => threadsReading > 0;
        public bool IsWriting => isWriting;

        #endregion

        #region CONSTRUCTORS

        public DBType(Type type, string databasePath)
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
            //There is one byte per instance for the ObjectOptions !
            instanceSize = 1;
        }

        #endregion

        #region FILE STREAMS

        /// <summary>
        /// Lock for reading (see <see cref="LockRead"/>) and opens the type file so that it can read from it.
        /// </summary>
        private FileStream OpenFileRead()
        {
            LockRead();
            return File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        /// <summary>
        /// Lock for writing (see <see cref="LockWrite"/>) and opens the type file so that it can write to it.
        /// </summary>
        private FileStream OpenFileWrite()
        {
            LockWrite();
            return File.Open(FilePath, FileMode.Open, FileAccess.Write, FileShare.None);
        }

        /// <summary>
        /// Lock for writing (see <see cref="LockWrite"/>) and opens the type file so that it can read and write at the same time.
        /// </summary>
        private FileStream OpenFileReadWrite()
        {
            LockWrite();
            return File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        /// <summary>
        /// Waits for all threads writing to the file to finish.
        /// This is made so that only one thread can write at any time and no threads can read while another thread is writing to avoid conflicts while searching or writing.
        /// <br></br>
        /// Don't forget to call <see cref="ReleaseRead"/> when the work is done.
        /// </summary>
        private void LockRead()
        {
            while(isWriting);

            threadsReading++;
        }

        /// <summary>
        /// Waits for all threads writing to the file to finish, then claims that this thread is writing and waits for any reading threads to finish.
        /// This is made so that only one thread can write at any time and no threads can read while another thread is writing to avoid conflicts while searching or writing.
        /// <br></br>
        /// Don't forget to call <see cref="ReleaseWrite"/> when the work is done.
        /// </summary>
        private void LockWrite()
        {
            while(isWriting);

            isWriting = true;

            while(IsReading);
        }

        /// <summary>
        /// <see cref="LockRead"/>
        /// </summary>
        private void ReleaseRead()
        {
            threadsReading--;
        }

        /// <summary>
        /// <see cref="LockWrite"/>
        /// </summary>
        private void ReleaseWrite()
        {
            isWriting = false;
        }

        #endregion

        #region REGISTER

        /// <summary>
        /// Adds a new field to the type and calculates the increment in header and type sizes.
        /// </summary>
        /// <param name="info">The reflection information about this field in order to get or set the field's value when serializing or deserializing.</param>
        /// <param name="field">The database API field information, containing it's type, size and ASCII name.</param>
        public void Add(System.Reflection.FieldInfo info, Field field)
        {
            if(registered)
                throw new Exception("Cannot add a field to the DBType when it is registered.");

            infos.Add(new FieldInfo(field, info));
            //1B Name size ; xB ASCII Name ; 2B Field size
            headerSize += 1u + (uint)field.Name.Length + 2u;
            this.instanceSize += (ulong)(field.Size + 1);
        }

        /// <summary>
        /// Called when the database finished the register process for this type, right before it is added to the database type dictionary.
        /// </summary>
        public void FinalizeRegister()
        {
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
                fileSize = file.Length;

            registered = true;
        }

        /// <summary>
        /// Creates the database file and writes the type's header. Does nothing if the file already exists.
        /// </summary>
        public void CreateFile()
        {
            if(registered)
                throw new Exception("Cannot call CreateFile() when the DBType is registered.");

            if(Exists())
                return;

            if(infos.Count - 1 > ushort.MaxValue)
                throw new Exception(string.Concat("There can only be a maximum of ", ushort.MaxValue + 1, " fields per types."));

            using(FileStream file = File.Open(filePath, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(BitConverter.GetBytes((ushort)(infos.Count - 1)).ToLittleEndian());

                for(int i = 0; i < infos.Count; i++)
                    file.Write(infos[i].field.CalcHeader());
            }
        }

        /// <summary>
        /// Order of field in a class shouldn't matter. This method orders the type's fields so that it match the fields declared in the header 
        /// of the file. This way we can trust the fields to be in the right order when serializing/deserializing.
        /// </summary>
        public void OrderFields()
        {
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                Field[] fields = Field.FromHeader(GetFileFullHeader(file));
                FieldInfo tmp;

                for (int i = 0; i < infos.Count; i++)
                    for (int j = 0; j < fields.Length; j++)
                        if(infos[i].field == fields[j])
                        {
                            tmp = infos[j];
                            infos[j] = infos[i];
                            infos[i] = tmp;
                        }
            }
        }

        #endregion

        #region FILE UTILITIES

        /// <summary>
        /// Checks whether the database file for this type already exists.
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
                byte nameSize = (byte)file.ReadByte();
                byte[] name = new byte[nameSize + 1];
                file.Read(name);
                byte[] dataSize = new byte[2];
                file.Read(dataSize);
                
                List<byte> total = new List<byte>(1 + nameSize + 2);
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
                byte nameSize = (byte)file.ReadByte();
                byte[] name = new byte[nameSize + 1];
                file.Read(name);
                byte[] dataSize = new byte[2];
                file.Read(dataSize);
                
                fullHeader.EnsureCapacity(fullHeader.Capacity + 1 + nameSize + 2);
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

        /// <summary>Returns the header of each field this type has.</summary>
        public byte[][] CalculateCurrentFieldHeaders()
        {
            byte[][] result = new byte[infos.Count][];

            for(int i = 0; i < infos.Count; i++)
                result[i] = infos[i].field.CalcHeader();

            return result;
        }

        /// <summary>
        /// Checks whether the header in the database file is the same as the type's header. 
        /// (Typically it will return false if you added or removed fields to/from the type)
        /// </summary>
        public bool IsHeaderValid()
        {
            byte[] header;
            using(FileStream file = OpenFileRead())
                header = GetFileFullHeader(file);
            ReleaseRead();

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
        /// Sets the Deleted flag of the specified instances and add them to the queue of available indexes for later allocations to avoid fragmentation.
        /// </summary>
        /// <param name="ids">An array of indexes to push to the queue.</param>
        public void FreeID(params ulong[] ids)
        {
            freeIDs.AddRange(ids);

            using(FileStream file = OpenFileWrite())
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    file.Position = (long)(headerSize + ids[i] * instanceSize);
                    InstanceOptions options = (InstanceOptions)file.ReadByte();
                    file.Position--;
                    file.WriteByte((byte)(options | InstanceOptions.Deleted));   
                }
            }
            ReleaseWrite();
        }
        /// <summary>
        /// Sets the Deleted flag of the specified instances and add them to the queue of available indexes for later allocations to avoid fragmentation.
        /// </summary>
        /// <param name="id">An index to push to the queue.</param>
        public void FreeID(ulong id)
        {
            freeIDs.Add(id);

            using(FileStream file = OpenFileWrite())
            {
                file.Position = (long)(headerSize + id * instanceSize);
                InstanceOptions options = (InstanceOptions)file.ReadByte();
                file.Position--;
                file.WriteByte((byte)(options | InstanceOptions.Deleted));
            }
            ReleaseWrite();
        }

        /// <summary>
        /// Search the whole file for instances having the Deleted flag enabled to reconstruct the freeIDs queue.
        /// </summary>
        public void GetFreedIDs()
        {
            using(FileStream file = OpenFileRead())
            {
                file.Position = headerSize;

                ulong id = 0;
                while(file.Position < file.Length)
                {
                    InstanceOptions options = (InstanceOptions)file.ReadByte();

                    if(options.HasFlag(InstanceOptions.Deleted))
                    {
                        freeIDs.Add(id);
                    }

                    file.Position += (long)instanceSize - 1;
                    id++;
                }
            }
            ReleaseRead();
        }

        #endregion

        #region PURGE

        /// <summary>
        /// Go through every freed instances and sets all their data to 0. This clears the data without rewriting the entire file.
        /// The file size doesn't change.
        /// </summary>
        public void PurgeKeep()
        {
            using(FileStream file = OpenFileWrite())
            {
                for (int i = 0; i < freeIDs.Count; i++)
                {
                    file.Position = (long)(headerSize + freeIDs[i] * instanceSize + 1);
                    file.Write(new byte[instanceSize - 1]);
                }
            }
            ReleaseWrite();
        }

        /// <summary>
        /// Rewrites the entire file removing the freed instances. The file size is reduced.
        /// </summary>
        public void Purge()
        {
            string newFilePath = string.Concat(filePath, ".tmp");

            using(FileStream oldFile = OpenFileReadWrite())
            {
                using(FileStream newFile = File.Open(newFilePath, FileMode.Open, FileAccess.Write, FileShare.Write))
                {
                    byte[] header = new byte[headerSize];
                    oldFile.Read(header);
                    newFile.Write(header);

                    while(oldFile.Position < oldFile.Length)
                    {
                        InstanceOptions options = (InstanceOptions)oldFile.ReadByte();

                        if(options.HasFlag(InstanceOptions.Deleted))
                            continue;

                        oldFile.Position--;
                        byte[] data = new byte[instanceSize];
                        oldFile.Read(data);
                        newFile.Write(data);
                    }
                }    
            }

            File.Delete(filePath);
            File.Move(newFilePath, filePath);

            ReleaseWrite();
        }

        #endregion

        #region PUSH

        /// <summary>
        /// Provides a position in the file where a new instance can be written.
        /// It first checks if there are available indexes in the queue to fill in blanks caused by deletion of data, in order to remove fragmentation.
        /// If there are none, the function will return false, which indicates that data must be appended at the end of the file.
        /// </summary>
        /// <param name="offset">The position in bytes where the instance bytes must be written.</param>
        /// <returns>False if there are no position in the middle of the file where data can be written. True otherwise.</returns>
        private bool Alloc(out ulong id, out long offset)
        {
            if(freeIDs.Count <= 0)
            {
                offset = 0;
                id = 0;
                return false;
            }

            id = freeIDs[0];
            offset = (long)(headerSize + id * instanceSize);
            freeIDs.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// Writes an instance of the type to the database file.
        /// </summary>
        /// <param name="instance">The type instance, it must have the exact same type as the DBType's type.</param>
        /// <returns>The position in bytes where the instance has been written.</returns>
        public long Push(object instance)
        {
            LockWrite();
            bool inMiddle = Alloc(out ulong id, out long offset);

            using(FileStream file = File.Open(filePath, inMiddle ? FileMode.Open : FileMode.Append, FileAccess.Write, FileShare.None))
            {            
                if(inMiddle)
                {
                    if(offset > file.Length)
                        throw new Exception("Internal error, allocation offset is invalid, fix this");

                    file.Position = (long)offset;
                }
                else
                    offset = file.Length;

                //Write default instance options
                file.WriteByte((byte)InstanceOptions.None);

                for(int i = 0; i < infos.Count; i++)
                {
                    object value = infos[i].info.GetValue(instance);
                    byte[] bytes;

                    if(infos[i].field.Translator.IsFlexible)
                        bytes = infos[i].field.Translator.FlexibleTranslateFrom(value, infos[i].field.Size);
                    else
                        bytes = infos[i].field.Translator.FixedTranslateFrom(value);

                    if(infos[i].field.Translator.EndianSensitive)
                        DBUtils.ToLittleEndian(ref bytes);

                    file.Write(bytes);
                }

                fileSize = file.Length;
            }
            ReleaseWrite();

            return offset;
        }

        #endregion

        #region SET

        /// <summary>
        /// Replaces the data of an instance given it's index.
        /// </summary>
        /// <param name="id">The instance index.</param>
        /// <param name="instance">The type instance, it must have the exact same type as the DBObject type.</param>
        public void Set(ulong id, object instance)
        {
            using(FileStream file = OpenFileWrite())
            {
                long pos = (long)(headerSize + id * instanceSize);
                if(pos > file.Length)
                    throw new Exception(string.Concat("There are no instance with id '", id, "'"));

                file.Position = pos;

                for(int i = 0; i < infos.Count; i++)
                {
                    object value = infos[i].info.GetValue(instance);
                    byte[] bytes;

                    if(infos[i].field.Translator.IsFlexible)
                        bytes = infos[i].field.Translator.FlexibleTranslateFrom(value, infos[i].field.Size);
                    else
                        bytes = infos[i].field.Translator.FixedTranslateFrom(value);

                    if(infos[i].field.Translator.EndianSensitive)
                        DBUtils.ToLittleEndian(ref bytes);

                    file.Write(bytes);
                }
            }
            ReleaseWrite();
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
            public TranslatorBase translator;

            public FetchField(long offset, ushort size, TranslatorBase translator)
            {
                this.offset = offset;
                this.size = size;
                this.translator = translator;
            }
        }

        /// <summary>
        /// Gives all the requested fields from an instance.
        /// </summary>
        /// <param name="fieldsToSearch">The fields names to search, must be ASCII characters.</param>
        /// <returns>An array of FetchFields containing information about each requested fields.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FetchField[] GetFetchFields(string[] fieldsToSearch)
        {
            FetchField[] fetchFields = new FetchField[fieldsToSearch.Length];

            for (int i = 0; i < fieldsToSearch.Length; i++)
            {
                long offset = 1; // Skip the option byte
                bool setted = false;
                for (int j = 0; j < infos.Count; j++)
                {
                    if(infos[j].field.Name == fieldsToSearch[i])
                    {
                        fetchFields[i] = new FetchField(offset, infos[j].field.Size, infos[j].field.Translator);
                        setted = true;
                        break;
                    }

                    offset += infos[j].field.Size + 1;
                }

                if(!setted)
                    throw new Exception(string.Concat("Cannot fetch ; Requested field '", fieldsToSearch[i], "' could not be found in object '", type.FullName, "'."));
            }

            return fetchFields;
        }

        /// <summary>
        /// Gives the value of each requested fields of the current instance as well as it's current ID.
        /// </summary>
        /// <param name="file">The current file stream to extract the data from.</param>
        /// <param name="instanceID">The current instance index.</param>
        /// <param name="fetchFields">All fields to recover from the instance.</param>
        /// <returns>An array of objects corresponding to each requested field's values. The first element of the array will always be the instance index (ulong).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe object[] GetInstanceFieldValues(FileStream file, ulong instanceID, FetchField[] fetchFields)
        {
            object[] values = new object[fetchFields.Length + 1];
            values[0] = instanceID;
                    
            byte[] instanceBytes = new byte[instanceSize - 1];
            file.Read(instanceBytes);

            for(int i = 0; i < fetchFields.Length; i++)
            {
                byte[] fieldBytes = instanceBytes.Extract(fetchFields[i].offset, fetchFields[i].size + 1);

                if(fetchFields[i].translator.EndianSensitive)
                    DBUtils.ToCurrentEndian(fieldBytes, true);

                if(fetchFields[i].translator.IsFlexible)
                    values[i + 1] = fetchFields[i].translator.FlexibleTranslateTo(fieldBytes, fetchFields[i].size);
                else
                    values[i + 1] = fetchFields[i].translator.FixedTranslateTo(fieldBytes);
            }

            return values;
        }

        /// <summary>
        /// Gets necessary data to distribute work between threads when fetching.
        /// </summary>
        /// <param name="length">The amount of bytes in file.</param>
        /// <param name="threadCount">The amount of total threads in the database array</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="instanceID">The instance index at which to start.</param>
        /// <param name="instancePos">The real offset accounting for the header, this value incremented by <see cref="instanceSize"/> when reading the file, which gives the exact position of each read instance.</param>
        /// <param name="realCount">The last thread will have a couple more instances to analyze if the distribution of instances between threads is uneven. This way, offset calculation is easy, all threads have the same amount of instances to process except the last one which will do the rest of the file.</param>
        /// <param name="endPos">The end position in bytes at which the thread must stop reading.</param>
        private void GetAsyncFetchThreadInfo(long length, int threadCount, int threadID, out ulong instanceID, out long instancePos, out long realCount, out long endPos)
        {
            bool isLastThread = threadID == threadCount - 1;

            //How many instances are in the file
            long instanceCount = (length - (long)headerSize) / (long)instanceSize;
            //How many instances there are for each threads (as double, so we can check if there are decimals)
            double      dCount = instanceCount / (double)threadCount;
            //How many instances there are for each threads (floored, there may be extra instances that will be given to the last thread)
            long         count = (long)Math.Floor(dCount);
            // The last thread will have a couple more instances to analyze if the distribution of instances between threads is uneven.
            // This way, offset calculation is easy, all threads have the same amount of instances to process except the last one which will do the rest
            // of the file.
                     realCount = isLastThread ? (dCount == count ? count : instanceCount - count * (threadCount - 1)) : count;
            // The offset **in bytes** in the file at which the thread should start processing.
            long        offset = count * (long)instanceSize * threadID;
            // The real offset accounting for the header, this value incremented `instanceSize` when reading the file, which gives the exact position
            // of each read instance.
                   instancePos = headerSize + offset;
            // The end position in bytes at which the thread must stop reading.
                        endPos = offset + realCount * (long)instanceSize;

            instanceID = (ulong)(count * threadID);
        }

        #endregion

        #region FETCH

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to insert the instance in the result list.
        /// The first element of the returned arrays will be the instance index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Values are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<object[]> Fetch(Database.FetchFunc searchFunction, string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<object[]> result = null;
            using(FileStream file = OpenFileRead())
            {
                result = new List<object[]>((int)((file.Length - (long)headerSize) / (long)instanceSize));
                file.Position = headerSize;
                ulong instanceID = 0;

                while(file.Position < file.Length)
                {
                    if(((InstanceOptions)file.ReadByte()).HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);

                    if(searchFunction(values))
                        result.Add(values);

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to insert the instance in the result list.
        /// The first element of the returned arrays will be the instance index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Values are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// <br></br>
        /// This search can be cancelled, especially useful if you are searching for one and only one instance that you know is unique.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list. By setting the `cancel` variable to `true`, you can stop the search immediatly.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<object[]> Fetch(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<object[]> result = null;
            using(FileStream file = OpenFileRead())
            {
                result = new List<object[]>((int)((file.Length - (long)headerSize) / (long)instanceSize));
                file.Position = headerSize;
                ulong objectID = 0;
                bool cancel = false;

                while(file.Position < file.Length)
                {
                    if(((InstanceOptions)file.ReadByte()).HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    object[] values = GetInstanceFieldValues(file, objectID, fetchFields);
                    
                    if(searchFunction(values, ref cancel))
                        result.Add(values);

                    if(cancel)
                        break;

                    objectID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<object[]> Fetch(Database.FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<object[]> result = null;
            LockRead();
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);
                result = new List<object[]>((int)realCount);

                file.Position = instancePos;

                while(instancePos < endPos)
                {
                    if(((InstanceOptions)file.ReadByte()).HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);

                    if(searchFunction(values))
                        result.Add(values);

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the cancellable Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="cancel">The shared cancel variable, when set to true, all threads stop their search. Because of the nature of multithreading, it is possible that some thread read several more instances before stopping.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<object[]> Fetch(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            List<object[]> result = null;
            LockRead();
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);

                result = new List<object[]>((int)realCount);
                file.Position = instancePos;

                while(instancePos < endPos)
                {
                    if(((InstanceOptions)file.ReadByte()).HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);

                    if(searchFunction(values, ref cancel))
                        result.Add(values);

                    if(cancel)
                        break;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        #endregion

        #region FETCH FULL

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to insert the instance in the result list.
        /// This function requests all fields and returns an object instance. This method is slower than requesting fields with <see cref="Fetch"/> but you can work directly 
        /// with C# class instance.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<T> FetchFull<T>(Database.FetchFunc<T> searchFunction)
        {
            List<T> result = null;
            using(FileStream file = OpenFileRead())
            {
                result = new List<T>((int)((file.Length - (long)headerSize) / (long)instanceSize));
                ulong instanceID = 0;

                file.Position = headerSize;

                while(file.Position < file.Length)
                {
                    T obj = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);;

                    InstanceOptions options = (InstanceOptions)file.ReadByte();

                    if(options.HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    for(int i = 0; i < infos.Count; i++)
                    {
                        byte[] fieldBytes = new byte[infos[i].field.Size + 1];
                        file.Read(fieldBytes);
                        
                        if(infos[i].field.Translator.EndianSensitive)
                            DBUtils.ToCurrentEndian(fieldBytes, true);

                        object value;
                        if(infos[i].field.Translator.IsFlexible)
                            value = infos[i].field.Translator.FlexibleTranslateTo(fieldBytes, infos[i].field.Size);
                        else
                            value = infos[i].field.Translator.FixedTranslateTo(fieldBytes);
                        infos[i].info.SetValue(obj, value);
                    }

                    if(searchFunction(obj))
                        result.Add(obj);

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to insert the instance in the result list.
        /// The first element of the returned arrays will be the instance index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Values are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// <br></br>
        /// This search can be cancelled, especially useful if you are searching for one and only one instance that you know is unique.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list. By setting the `cancel` variable to `true`, you can stop the search immediatly.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<T> FetchFull<T>(Database.CancellableFetchFunc<T> searchFunction)
        {
            List<T> result = null;
            bool cancel = false;
            using(FileStream file = OpenFileRead())
            {
                result = new List<T>((int)((file.Length - (long)headerSize) / (long)instanceSize));
                ulong instanceID = 0;

                file.Position = headerSize;

                while(file.Position < file.Length)
                {
                    T obj = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);;

                    InstanceOptions options = (InstanceOptions)file.ReadByte();

                    if(options.HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    for(int i = 0; i < infos.Count; i++)
                    {
                        byte[] fieldBytes = new byte[infos[i].field.Size + 1];
                        file.Read(fieldBytes);
                        
                        if(infos[i].field.Translator.EndianSensitive)
                            DBUtils.ToCurrentEndian(fieldBytes, true);

                        object value;
                        if(infos[i].field.Translator.IsFlexible)
                            value = infos[i].field.Translator.FlexibleTranslateTo(fieldBytes, infos[i].field.Size);
                        else
                            value = infos[i].field.Translator.FixedTranslateTo(fieldBytes);
                        infos[i].info.SetValue(obj, value);
                    }

                    if(searchFunction(obj, ref cancel))
                        result.Add(obj);

                    if(cancel)
                        break;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<T> FetchFull<T>(Database.FetchFunc<T> searchFunction, int threadCount, int threadID)
        {
            List<T> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);
                result = new List<T>((int)realCount);

                file.Position = instancePos;

                while(file.Position < file.Length)
                {
                    T obj = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);;

                    InstanceOptions options = (InstanceOptions)file.ReadByte();

                    if(options.HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    for(int i = 0; i < infos.Count; i++)
                    {
                        byte[] fieldBytes = new byte[infos[i].field.Size + 1];
                        file.Read(fieldBytes);
                        
                        if(infos[i].field.Translator.EndianSensitive)
                            DBUtils.ToCurrentEndian(fieldBytes, true);

                        object value;
                        if(infos[i].field.Translator.IsFlexible)
                            value = infos[i].field.Translator.FlexibleTranslateTo(fieldBytes, infos[i].field.Size);
                        else
                            value = infos[i].field.Translator.FixedTranslateTo(fieldBytes);
                        infos[i].info.SetValue(obj, value);
                    }

                    instancePos += (long)instanceSize;

                    if(searchFunction(obj))
                        result.Add(obj);

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the cancellable Fetch function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance will be added to the result list.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="cancel">The shared cancel variable, when set to true, all threads stop their search. Because of the nature of multithreading, it is possible that some thread read several more instances before stopping.</param>
        /// <returns>A list of object array, each object array represents an instance that passed the searchFunction test.</returns>
        public List<T> FetchFull<T>(Database.CancellableFetchFunc<T> searchFunction, int threadCount, int threadID, ref bool cancel)
        {
            List<T> result = null;
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);
                result = new List<T>((int)realCount);

                file.Position = instancePos;

                while(file.Position < file.Length)
                {
                    T obj = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);;

                    InstanceOptions options = (InstanceOptions)file.ReadByte();

                    if(options.HasFlag(InstanceOptions.Deleted))
                    {
                        file.Position += (long)instanceSize - 1;
                        continue;
                    }

                    for(int i = 0; i < infos.Count; i++)
                    {
                        byte[] fieldBytes = new byte[infos[i].field.Size + 1];
                        file.Read(fieldBytes);
                        
                        if(infos[i].field.Translator.EndianSensitive)
                            DBUtils.ToCurrentEndian(fieldBytes, true);

                        object value;
                        if(infos[i].field.Translator.IsFlexible)
                            value = infos[i].field.Translator.FlexibleTranslateTo(fieldBytes, infos[i].field.Size);
                        else
                            value = infos[i].field.Translator.FixedTranslateTo(fieldBytes);
                        infos[i].info.SetValue(obj, value);
                    }

                    instancePos += (long)instanceSize;

                    if(searchFunction(obj, ref cancel))
                        result.Add(obj);

                    if(cancel)
                        break;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        #endregion

        #region FETCH COUNT

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to increment the count.
        /// The first element of the array given to the search function will be the instance index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Values are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>How many instances passed the test.</returns>
        public long FetchCount(Database.FetchFunc searchFunction, params string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = OpenFileRead())
            {
                long instancePos = headerSize;
                ulong instanceID = 0;

                while(file.Position < file.Length)
                {
                    file.Position = instancePos;
                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);
                    instancePos += (long)instanceSize;

                    if(searchFunction(values))
                        result++;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// Go through the whole file sequencially and for each instance, calls the provided searchFunction to determine whether or not to increment the count.
        /// The first element of the array given to the search function will be the instance index in the file, meaning that you don't have to request any fields. The next elements will be the requested fields.
        /// Values are provided in the same order as requested fields. For example, asking for fields "c", "a" and "b" will gives the values of each of those fields in the same order.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <returns>How many instances passed the test.</returns>
        public long FetchCount(Database.CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            using(FileStream file = OpenFileRead())
            {
                long instancePos = headerSize;
                ulong instanceID = 0;
                bool cancel = false;

                while(file.Position < file.Length)
                {
                    file.Position = instancePos;
                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);
                    instancePos += (long)instanceSize;

                    if(searchFunction(values, ref cancel))
                        result++;

                    if(cancel)
                        break;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the FetchCount function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <returns>How many instances passed the test.</returns>
        public long FetchCount(Database.FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            LockRead();
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);

                while(instancePos < endPos)
                {
                    file.Position = instancePos;
                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);
                    instancePos += (long)instanceSize;

                    if(searchFunction(values))
                        result++;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        /// <summary>
        /// The asynchronous version of the cancellable FetchCount function. Is is called on every thread and distribute work among them so each thread analyses only part of the file at the same time.
        /// </summary>
        /// <param name="searchFunction">A delegate called for each instance, if it returns true, the instance count will be incremented.</param>
        /// <param name="fieldsToSearch">The name of the fields to search, this optimises performance and especially memory as you only list the fields you will actively work with. This array can be empty, in which case only the instance index will be given.</param>
        /// <param name="threadCount">The total amount of threads in the database.</param>
        /// <param name="threadID">The current thread index.</param>
        /// <param name="cancel">The shared cancel variable, when set to true, all threads stop their search. Because of the nature of multithreading, it is possible that some thread read several more instances before stopping.</param>
        /// <returns>How many instances passed the test.</returns>
        public long FetchCount(Database.CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
        {
            FetchField[] fetchFields = GetFetchFields(fieldsToSearch);

            long result = 0;
            LockRead();
            using(FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                GetAsyncFetchThreadInfo(file.Length, threadCount, threadID, 
                out ulong instanceID, out long instancePos, out long realCount, out long endPos);

                while(instancePos < endPos)
                {
                    file.Position = instancePos;
                    object[] values = GetInstanceFieldValues(file, instanceID, fetchFields);
                    instancePos += (long)instanceSize;

                    if(searchFunction(values, ref cancel))
                        result++;

                    if(cancel)
                        break;

                    instanceID++;
                }
            }
            ReleaseRead();

            return result;
        }

        #endregion
    }
}