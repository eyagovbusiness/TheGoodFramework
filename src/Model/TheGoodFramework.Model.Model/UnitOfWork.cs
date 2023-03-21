using log4net;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TGF.Model
{
    public enum DisposeState
    {
        None,
        Disposing,
        Disposed
    }
    public interface IQueryContext : IDisposable
    {
        IQueryable<T> Query<T>()
            where T : class;
    }

    /// <summary>
    /// Class to perform insert/update/delete operations over one or more dbContext classes, all in one single transaction to ensure Db consistency.
    /// </summary>
    public class UnitOfWork : IQueryContext, IDisposable
    {
        #region Static
        private static readonly ILog mLog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        [ThreadStatic]
        private static UnitOfWork mUnitOfWork;
        public static UnitOfWork Current
        {
            get { return mUnitOfWork; }
        }

        public static UnitOfWork Get<T>([CallerMemberName] string aMemberName = "", [CallerFilePath] string aFileName = "", [CallerLineNumber] int aLineNumber = 0)
            where T : DataContextBase, new()
        {
            DataContextBase lDataContext = new T();
            UnitOfWork lWork = UnitOfWork.Get(lDataContext);

            return lWork;
        }

        public static UnitOfWork Get(DataContextBase aDataContext)
        {
            return new UnitOfWork(() => aDataContext);
        }
        #endregion

        private string mOwnerThreadName;
        private DataContextBase mDataContext;
        private UnitOfWork mParent;

        private UnitOfWork(Func<DataContextBase> aDbContextFactory)
        {
            if (mUnitOfWork != null && mUnitOfWork.mDisposeState != DisposeState.None)
            {
                mLog.WarnFormat("ThreadStatic UnitOfWork is Disposed! Current: {0}, Owner: {1}", Thread.CurrentThread.Name, mUnitOfWork.mOwnerThreadName);
                mUnitOfWork = null;
            }

            mOwnerThreadName = Thread.CurrentThread.Name;

            if (mUnitOfWork == null)
            {
                mDataContext = aDbContextFactory();
            }
            else
            {
                mDataContext = mUnitOfWork.mDataContext;
                mParent = mUnitOfWork;
            }

            // TO-do: Set Command Timeout for the DataContext
            //var lConnectionFact = new ConnectionFactory();
            //var lDataContextType = mDataContext?.GetType();
            //if (lDataContextType != null)
            //{
            //    var lConnectionConfig = lConnectionFact.GetConnectionConfig(lDataContextType);
            //    if (lConnectionConfig?.CommandTimeout != null)
            //        mDataContext.Database.SetCommandTimeout(lConnectionConfig.CommandTimeout);
            //}

            //mLog.TraceFormat("UnitOfWork.ctor - {0}, {1}, {2}", this.GetHashCode(), mDataContext.GetHashCode(), mParent == null);
            mUnitOfWork = this;
        }

        public IQueryable<T> Query<T>()
            where T : class
        {
            return mDataContext.Set<T>();
        }


        public void RevertAll()
        {
            DbEntityEntry[] lEntryList = mDataContext.ChangeTracker.Entries()
                .Where(x => x.State != EntityState.Unchanged)
                .ToArray();

            foreach (DbEntityEntry lEntry in lEntryList)
            {
                switch (lEntry.State)
                {
                    case EntityState.Added:
                        lEntry.State = EntityState.Detached;
                        break;
                    case EntityState.Deleted:
                        lEntry.Reload();
                        break;
                    case EntityState.Modified:
                        lEntry.State = EntityState.Unchanged;
                        break;
                }
            }
        }


        public void Save()
        {
            try
            {
                mDataContext.SaveChanges();
            }
            catch (ValidationException lEx)
            {
                throw new DataException($"{lEx.Message}:\r\n{lEx.ValidationResult.ErrorMessage}");
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool IsChanged(object aEntity)
        {
            DbEntityEntry aEntry = mDataContext.Entry(aEntity);
            if (aEntry != null)
                return aEntry.State != EntityState.Unchanged;
            else
                return false;
        }


        #region IDisposable
        private DisposeState mDisposeState;
        public void Dispose()
        {
            this.DisposeExecute();
            GC.SuppressFinalize(this);
        }
        protected virtual void DisposeExecute()
        {
            if (mDisposeState == DisposeState.None)
            {
                mDisposeState = DisposeState.Disposing;

                if (mUnitOfWork == this)
                    mUnitOfWork = mParent;
                else if (mUnitOfWork != null)
                    mLog.WarnFormat("Disposed UnitOfWork is not last UnitOfWork! {0} != {1}", mUnitOfWork.mOwnerThreadName, this.mOwnerThreadName);

                //mLog.TraceFormat("UnitOfWork.Dispose - {0}, {1}, {2}", this.GetHashCode(), mDataContext.GetHashCode(), mParent == null);

                if (mParent == null)
                {
                    try // catch error if internal context is already Disposed
                    {
                        //mDataContext.ObjectContext().ContextOptions.LazyLoadingEnabled = false;
                        mDataContext.Dispose();
                    }
                    catch { }

                }

                mDataContext = null;

                mDisposeState = DisposeState.Disposed;
            }
        }

        #endregion
    }
}