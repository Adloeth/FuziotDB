using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;

namespace FuziotDB
{
    /// <summary>
    /// The class that handles all database interactions.
    /// </summary>
    public class Database : IDisposable
    {
        #region FIELDS

        private Dictionary<Type, DBObject> objects = new Dictionary<Type, DBObject>();
        private string databasePath;
        private bool cancel;

        #region MULTITHREADING

        private DBThread[] threads;
        private ManualResetEvent threadLock;
        private DatabaseAction action;
        private ThreadInfoBase currentInfo;
        private bool closeThreads;

        #endregion

        #endregion

        #region PROPERTIES

        public bool IsMultithreadedCompatible => threads != null;

        public bool ActionDone
        {
            get
            {
                for (int i = 0; i < threads.Length; i++)
                    if(!threads[i].IsAvailable)
                        return false;

                action = null;
                return true;
            }
        }

        #endregion

        #region CONSTRUCTORS & DESTRUCTORS

        public Database(string databasePath) : this(databasePath, Environment.ProcessorCount) { }

        public Database(string databasePath, int threadCount)
        {
            this.databasePath = databasePath;

            if(threadCount > 0)
            {
                threads = new DBThread[threadCount];
                threadLock = new ManualResetEvent(false);

                for (int i = 0; i < threadCount; i++)
                {
                    int index = i;
                    threads[i] = new DBThread(threadLock, () => 
                    {
                        while(!threads[index].Closing)
                        {
                            threads[index].IsAvailable = true;

                            threadLock.WaitOne();

                            if(threads[index].Closing)
                                break;

                            if(action == null)
                                continue;

                            if(!objects.TryGetValue(action.type, out DBObject obj))
                            {
                                Console.WriteLine(string.Concat("Type '", action.type.FullName, "' wasn't registered."));
                                continue;
                            }

                            if(action is FetchAction fetchAction)
                                ((FetchAsyncInfo)currentInfo).SetResult(index, obj.Fetch(fetchAction.fetchFunc, fetchAction.fieldsToSearch, threadCount, index));

                            else if(action is CancellableFetchAction cancellableFetchAction)
                                ((FetchAsyncInfo)currentInfo).SetResult(index, obj.Fetch(cancellableFetchAction.fetchFunc, cancellableFetchAction.fieldsToSearch, threadCount, index, ref cancel));

                            //Console.WriteLine(string.Concat("Thread ", index, " finished with ", ((FetchResult)this.threads[index].Result).result.Count, " objects"));
                        }
                    });
                    threads[i].Start();
                }
            }
            else
            {
                threads = null;
                threadLock = null;
                action = null;
                currentInfo = null;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < threads.Length; i++)
                threads[i].Dispose();
        }

        #endregion

        #region METHODS

        #region UTILITIES

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DBObject GetObject(Type type)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            return obj;
        }

        #region MULTITHREADING

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Database WaitForActionDone()
        {
            if(IsMultithreadedCompatible)
                while(!ActionDone);

            return this;
        }

        private ThreadInfoBase StartThreads(DatabaseAction action)
        {
            if(!IsMultithreadedCompatible)
                throw new Exception("Cannot use multithreading on this database.");

            while(!ActionDone);

            cancel = false;

            if(action is FetchAction || action is CancellableFetchAction)
                this.currentInfo = new FetchAsyncInfo(threads.Length);
            else if(action is FetchCountAction || action is CancellableFetchCountAction)
                this.currentInfo = new CountAsyncInfo(threads.Length);
            else
                throw new Exception(string.Concat("Action '", action.GetType().Name, "' is unsupported"));

            for(int i = 0; i < threads.Length; i++)
                threads[i].IsAvailable = false;

            this.action = action;

            threadLock.Set();
            threadLock.Reset();

            return this.currentInfo;
        }

        #endregion

        #endregion

        #region DELEGATES

        /// <summary>
        /// Called when using the fetch method, used to determines whether or not an object should be ignored.
        /// </summary>
        /// <param name="fields">The value of the fields of the current object, in the same order as inputed in the fetch method. The first element of the array will always be the current object index in the file.</param>
        /// <returns>False if the object should be ignored from the search.</returns>
        public delegate bool FetchFunc(DBVariant[] fields);
        /// <summary>
        /// Called when using the fetch method, used to determines whether or not an object should be ignored.
        /// </summary>
        /// <param name="fields">The value of the fields of the current object, in the same order as inputed in the fetch method. The first element of the array will always be the current object index in the file.</param>
        /// <param name="cancel">When set to true, the whole search is stopped.</param>
        /// <returns>False if the object should be ignored from the search.</returns>
        public delegate bool CancellableFetchFunc(DBVariant[] fields, ref bool cancel);

        #endregion

        #region REGISTER

        /// <summary>
        /// Registers an object to the database. This will create a file for each object. Once registered you can use other methods of the database class
        /// to add, remove, search and count instances in the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Register<T>() where T : new() => Register(typeof(T), false);
        /// <summary>
        /// Registers an object to the database. This will create a file for each object. Once registered you can use other methods of the database class
        /// to add, remove, search and count instances in the database.
        /// </summary>
        /// <param name="upgrade">
        /// If this object was already registered and the header of the file isn't the same as the object's header (the software was updated and data has 
        /// been added or removed from the class), setting this to false will generate an error, while setting it to true will rewrite the entire file accounting 
        /// for the new header. This means that data can be permanently lost of a field was removed, it is your responsibility to use backups if you set this 
        /// parameter to true.
        /// </param>
        /// <typeparam name="T"></typeparam>
        public void Register<T>(bool upgrade) where T : new() => Register(typeof(T), upgrade);

        private void Register(Type type, bool upgrade)
        {
            DBObject obj = new DBObject(type, databasePath);
            foreach(System.Reflection.FieldInfo info in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                DBSerializeAttribute attribute = info.GetCustomAttribute<DBSerializeAttribute>();

                if(attribute == null)
                    continue;

                Type fieldType = info.FieldType;

                if(!DBVariant.IsSupported(fieldType))
                    throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because field '", info.Name, "' type ('", fieldType, "') is not supported."));

                DBType dbType = DBVariant.GetDBType(fieldType);
                if((dbType == DBType.UTF16 || dbType == DBType.ASCII || dbType == DBType.Variable))
                {
                    if(!attribute.HasSize)
                        throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because field '", info.Name, "' needs it's size attribute to be set."));
                    else if(attribute.Size <= 0)
                        throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because the size of field '", info.Name, "' cannot be lower than 1."));
                    else if(attribute.Size >= ushort.MaxValue + 1)
                        throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because the size of field '", info.Name, "' cannot be higher than 65536."));
                }
                else if(attribute.HasSize)
                    throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because field '", info.Name, "' cannot have it's size attribute set."));

                ASCIIS alias = attribute.Alias;
                if(ASCIIS.IsNullOrEmpty(alias))
                {
                    if(!info.Name.IsASCIICompatible())
                        throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because field '", info.Name, "' alias isn't compatible with ASCII encoding."));
                    
                    alias = new ASCIIS(info.Name);
                }

                Field field = new Field(dbType, alias, (ushort)((attribute.HasSize ? attribute.Size : (ushort)DBVariant.GetSize(fieldType)) - 1));

                obj.Add(info, field);
            }

            if(obj.Count <= 0)
                throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because no fields are marked with the DBSerialize attribute."));

            if(obj.Exists())
            {
                if(!obj.IsHeaderValid())
                {
                    if(!upgrade)
                        throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because it doesn't have the same header signature."));
                    else
                        Upgrade(type, obj);
                }
            }
            else
                obj.CreateFile();

            obj.FinalizeRegister();

            objects.Add(type, obj);
            Console.WriteLine(string.Concat("Type '", type.FullName, "' is registered to the database (with ", obj.Count, " fields) !"));
        }

        /// <summary>
        /// Rewrites the entire file adding or removing fields depending on the current type signature.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="obj"></param>
        private void Upgrade(Type type, DBObject obj)
        {
            string tmpPath = string.Concat(obj.FilePath, ".tmp");

            if(File.Exists(tmpPath))
                File.Delete(tmpPath);

            byte[][] currentHeaders = obj.CalculateCurrentFieldHeaders();
            Field[] newFields = Field.FromHeader(obj.CalculateCurrentFullHeader());

            using(FileStream old = File.Open(obj.FilePath, FileMode.Open, FileAccess.Read))
            {
                byte[] oldHeader = obj.GetFileFullHeader(old);
                Field[] oldFields = Field.FromHeader(oldHeader);
                long oldObjectSize = 1; //First byte is for ObjectOptions

                for (int i = 0; i < oldFields.Length; i++)
                    oldObjectSize += oldFields[i].Size + 1; //Don't forget that a value of 0 means a length of 1

                old.Position = oldHeader.Length;

                using(FileStream tmp = File.Open(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    //Write the field count at the beginning of the file.
                    tmp.Write(BitConverter.GetBytes((ushort)(currentHeaders.Length - 1)).ToLittleEndian());

                    for (int i = 0; i < currentHeaders.Length; i++)
                        tmp.Write(currentHeaders[i]);

                    while(old.Position < old.Length)
                    {
                        //Get the first byte (the object options) to ignore the field if it is a deleted field.
                        ObjectOptions objectOptions = (ObjectOptions)old.ReadByte();

                        if(objectOptions.HasFlag(ObjectOptions.Deleted))
                        {
                            old.Position += oldObjectSize;
                            continue;
                        }

                        //Get all the old fields bytes.
                        byte[][] oldObjectFields = new byte[oldFields.Length][];

                        //Fill them
                        for (int i = 0; i < oldFields.Length; i++)
                        {
                            oldObjectFields[i] = new byte[oldFields[i].Size + 1];
                            old.Read(oldObjectFields[i]);
                        }

                        //Check if the new fields were already in the old file, in order to copy them.
                        for (int i = 0; i < newFields.Length; i++)
                        {
                            int fieldIndex = -1;
                            for (int j = 0; j < oldFields.Length; j++)
                                if(newFields[i] == oldFields[j])
                                {
                                    fieldIndex = j;
                                    break;
                                }

                            //If a new field was inserted (the field didn't exist in the old file), the default value will be set (all bytes at 0).
                            if(fieldIndex == -1)
                                tmp.Write(new byte[newFields[i].Size + 1]);
                            //Otherwise, writes the field as in the old file.
                            else
                                tmp.Write(oldObjectFields[fieldIndex]);
                        }
                    }
                }
            }
        }

        #endregion
    
        #region FREE

        /// <summary>
        /// Deletes an object in the database.
        /// </summary>
        /// <param name="id">The index of the object to remove.</param>
        public void Free<T>(ulong id) => Free(typeof(T), id);

        private void Free(Type type, ulong id)
            => WaitForActionDone().GetObject(type).PushFreeID(id);

        /// <summary>
        /// Deletes multiple objects in the database.
        /// </summary>
        /// <param name="id">The indexes of the objects to remove.</param>
        public void Free<T>(params ulong[] id) => Free(typeof(T), id);

        private void Free(Type type, params ulong[] ids)
            => WaitForActionDone().GetObject(type).PushFreeID(ids);

        #endregion

        #region PUSH

        /// <summary>
        /// Adds an object to the database.
        /// </summary>
        /// <param name="instance"></param>
        /// <typeparam name="T"></typeparam>
        public void Push<T>(T instance) where T : new() => Push(typeof(T), instance);
        private void Push(Type type, object instance)
            => WaitForActionDone().GetObject(type).Push(instance);

        #endregion

        #region FETCH

        /// <summary>
        /// Synchronously fetch requested fields that passed the test in the search function.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>A list of all objects that passed the test in the search function.</returns>
        public ReadOnlyCollection<DBVariant[]> Fetch<T>(FetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Fetch(typeof(T), searchFunction, fieldsToSearch);
        private ReadOnlyCollection<DBVariant[]> Fetch(Type type, FetchFunc searchFunction, params string[] fieldsToSearch)
            => WaitForActionDone().GetObject(type).Fetch(searchFunction, fieldsToSearch);

        /// <summary>
        /// Synchronously fetch requested fields that passed the test in the search function.
        /// If `cancel` is set to true in the search function, the search will be stopped, mainly useful to search for one object only.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>A list of all objects that passed the test in the search function.</returns>
        public ReadOnlyCollection<DBVariant[]> Fetch<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Fetch(typeof(T), searchFunction, fieldsToSearch);
        private ReadOnlyCollection<DBVariant[]> Fetch(Type type, CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
            => WaitForActionDone().GetObject(type).Fetch(searchFunction, fieldsToSearch);

        #region MULTITHREADING

        /// <summary>
        /// Asynchronously fetch requested fields that passed the test in the search function.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>A ThreadInfo that can be used to wait for the result.</returns>
        public FetchAsyncInfo FetchAsync<T>(FetchFunc searchFunction, params string[] fieldsToSearch)
            => (FetchAsyncInfo)StartThreads(new FetchAction(typeof(T), searchFunction, fieldsToSearch));

        /// <summary>
        /// Asynchronously fetch requested fields that passed the test in the search function. 
        /// If `cancel` is set to true in the search function, all threads will stop their search, mainly useful to search for one object only.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>A ThreadInfo that can be used to wait for the result.</returns>
        public FetchAsyncInfo FetchAsync<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
            => (FetchAsyncInfo)StartThreads(new CancellableFetchAction(typeof(T), searchFunction, fieldsToSearch));

        private List<DBVariant[]> Fetch(Type type, FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch, threadCount, threadID);

        private List<DBVariant[]> Fetch(Type type, CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch, threadCount, threadID, ref cancel);

        #endregion

        #endregion

        #region COUNT

        /// <summary>
        /// Synchronously counts objects that passed the test in the search function.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>How many objects passed the test in the search function.</returns>
        public long Count<T>(FetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Count(typeof(T), searchFunction, fieldsToSearch);
        private long Count(Type type, FetchFunc searchFunction, params string[] fieldsToSearch)
            => WaitForActionDone().GetObject(type).FetchCount(searchFunction, fieldsToSearch);

        /// <summary>
        /// Synchronously counts objects that passed the test in the search function.
        /// If `cancel` is set to true in the search function, the search will be stopped, mainly useful to search for one object only.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>How many objects passed the test in the search function.</returns>
        public long Count<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Count(typeof(T), searchFunction, fieldsToSearch);
        private long Count(Type type, CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
            => WaitForActionDone().GetObject(type).FetchCount(searchFunction, fieldsToSearch);

        #region MULTITHREADING

        /// <summary>
        /// Asynchronously  counts objects that passed the test in the search function.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>How many objects passed the test in the search function.</returns>
        public CountAsyncInfo CountAsync<T>(FetchFunc searchFunction, params string[] fieldsToSearch) where T : new()
            => (CountAsyncInfo)StartThreads(new FetchCountAction(typeof(T), searchFunction, fieldsToSearch));

        /// <summary>
        /// Asynchronously counts objects that passed the test in the search function.
        /// If `cancel` is set to true in the search function, all threads will stop their search, mainly useful to search for one object only.
        /// </summary>
        /// <param name="searchFunction">Called for each object in the database, if it returns true, the object will be added to the result list.</param>
        /// <param name="fieldsToSearch">Fields to return and pass to the search function.</param>
        /// <returns>How many objects passed the test in the search function.</returns>
        public CountAsyncInfo CountAsync<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch) where T : new()
            => (CountAsyncInfo)StartThreads(new CancellableFetchCountAction(typeof(T), searchFunction, fieldsToSearch));

        private long Count(Type type, FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
            => GetObject(type).FetchCount(searchFunction, fieldsToSearch, threadCount, threadID);

        private long Count(Type type, CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
            => GetObject(type).FetchCount(searchFunction, fieldsToSearch, threadCount, threadID, ref cancel);

        #endregion

        #endregion
    
        #endregion
    }
}