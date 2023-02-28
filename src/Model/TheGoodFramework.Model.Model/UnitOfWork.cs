using log4net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.ComponentModel.DataAnnotations;
using System.Data;
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
            where T : DbContextBase, new()
        {
            DbContextBase lDataContext = new T();
            UnitOfWork lWork = UnitOfWork.Get(lDataContext);

            return lWork;
        }

        public static UnitOfWork Get(DbContextBase aDataContext)
        {
            return new UnitOfWork(() => aDataContext);
        }
        #endregion

        private string mOwnerThreadName;
        private DbContextBase mDbContext;
        private UnitOfWork mParent;

        private UnitOfWork(Func<DbContextBase> aDbContextFactory)
        {
            if (mUnitOfWork != null && mUnitOfWork.mDisposeState != DisposeState.None)
            {
                mLog.WarnFormat("ThreadStatic UnitOfWork is Disposed! Current: {0}, Owner: {1}", Thread.CurrentThread.Name, mUnitOfWork.mOwnerThreadName);
                mUnitOfWork = null;
            }

            mOwnerThreadName = Thread.CurrentThread.Name;

            if (mUnitOfWork == null)
            {
                mDbContext = aDbContextFactory();
            }
            else
            {
                mDbContext = mUnitOfWork.mDbContext;
                mParent = mUnitOfWork;
            }

            // TO-do: Set Command Timeout for the DataContext
            //var lConnectionFact = new ConnectionFactory();
            //var lDataContextType = mDbContext?.GetType();
            //if (lDataContextType != null)
            //{
            //    var lConnectionConfig = lConnectionFact.GetConnectionConfig(lDataContextType);
            //    if (lConnectionConfig?.CommandTimeout != null)
            //        mDbContext.Database.SetCommandTimeout(lConnectionConfig.CommandTimeout);
            //}

            //mLog.TraceFormat("UnitOfWork.ctor - {0}, {1}, {2}", this.GetHashCode(), mDbContext.GetHashCode(), mParent == null);
            mUnitOfWork = this;
        }

        public IQueryable<T> Query<T>()
            where T : class
        {
            return mDbContext.Set<T>();
        }


        public void RevertAll()
        {
            EntityEntry[] lEntryList = mDbContext.ChangeTracker.Entries()
                .Where(x => x.State != EntityState.Unchanged)
                .ToArray();

            foreach (EntityEntry lEntry in lEntryList)
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
                mDbContext.SaveChanges();
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
            EntityEntry aEntry = mDbContext.Entry(aEntity);
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

                //mLog.TraceFormat("UnitOfWork.Dispose - {0}, {1}, {2}", this.GetHashCode(), mDbContext.GetHashCode(), mParent == null);

                if (mParent == null)
                {
                    try // catch error if internal context is already Disposed
                    {
                        //mDbContext.ObjectContext().ContextOptions.LazyLoadingEnabled = false;
                        mDbContext.Dispose();
                    }
                    catch { }

                }

                mDbContext = null;

                mDisposeState = DisposeState.Disposed;
            }
        }

        #endregion
    }
}