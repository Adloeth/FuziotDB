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

        private ThreadInfoBase StartThreads(DatabaseAction action)
        {
            if(!IsMultithreadedCompatible)
                throw new Exception("Cannot use multithreading on this database.");

            while(!ActionDone);

            cancel = false;

            if(action is FetchAction)
                this.currentInfo = new FetchAsyncInfo(threads.Length);
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

        public void Register<T>() where T : new() => Register(typeof(T));

        private void Register(Type type)
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
                    throw new Exception(string.Concat("Cannot serialize type '", type.FullName, "' because it doesn't have the same header signature."));
            }
            else
                obj.CreateFile();

            obj.FinalizeRegister();

            objects.Add(type, obj);
            Console.WriteLine(string.Concat("Type '", type.FullName, "' is registered to the database (with ", obj.Count, " fields) !"));
        }

        #endregion
    
        #region FREE

        public void Free<T>(ulong id) => Free(typeof(T), id);

        private void Free(Type type, ulong id)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            obj.PushFreeID(id);
        }

        public void Free<T>(params ulong[] id) => Free(typeof(T), id);

        private void Free(Type type, params ulong[] id)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            obj.PushFreeID(id);
        }

        #endregion

        #region PUSH

        public void Push<T>(T instance) where T : new() => Push(typeof(T), instance);
        private void Push(Type type, object instance)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            obj.Push(instance);
        }

        #endregion

        #region FETCH

        public ReadOnlyCollection<DBVariant[]> Fetch<T>(FetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Fetch(typeof(T), searchFunction, fieldsToSearch);
        private ReadOnlyCollection<DBVariant[]> Fetch(Type type, FetchFunc searchFunction, params string[] fieldsToSearch)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch);

        public ReadOnlyCollection<DBVariant[]> Fetch<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Fetch(typeof(T), searchFunction, fieldsToSearch);
        private ReadOnlyCollection<DBVariant[]> Fetch(Type type, CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch);

        public FetchAsyncInfo FetchAsync<T>(FetchFunc searchFunction, params string[] fieldsToSearch)
            => (FetchAsyncInfo)StartThreads(new FetchAction(typeof(T), searchFunction, fieldsToSearch));

        public FetchAsyncInfo FetchAsync<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
            => (FetchAsyncInfo)StartThreads(new CancellableFetchAction(typeof(T), searchFunction, fieldsToSearch));

        private List<DBVariant[]> Fetch(Type type, FetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch, threadCount, threadID);

        private List<DBVariant[]> Fetch(Type type, CancellableFetchFunc searchFunction, string[] fieldsToSearch, int threadCount, int threadID, ref bool cancel)
            => GetObject(type).Fetch(searchFunction, fieldsToSearch, threadCount, threadID, ref cancel);

        #endregion

        #region COUNT

        public long Count<T>(FetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Count(typeof(T), searchFunction, fieldsToSearch);
        private long Count(Type type, FetchFunc searchFunction, params string[] fieldsToSearch)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            return obj.FetchCount(searchFunction, fieldsToSearch);
        }

        public long Count<T>(CancellableFetchFunc searchFunction, params string[] fieldsToSearch) where T : new() => Count(typeof(T), searchFunction, fieldsToSearch);
        private long Count(Type type, CancellableFetchFunc searchFunction, params string[] fieldsToSearch)
        {
            if(!objects.TryGetValue(type, out DBObject obj))
                throw new Exception(string.Concat("Type '", type.FullName, "' wasn't registered."));

            return obj.FetchCount(searchFunction, fieldsToSearch);
        }

        #endregion
    
        #endregion
    }
}