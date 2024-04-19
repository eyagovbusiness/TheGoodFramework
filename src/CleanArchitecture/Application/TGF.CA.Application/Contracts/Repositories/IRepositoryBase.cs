using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TGF.Common.ROP.HttpResult;

namespace TGF.CA.Application.Contracts.Repositories
{
    /// <summary>
    /// Interface of the base default CRUD implementations in TGF.CA.Infrastructure.DB.Repository.RepositoryBase class.
    /// </summary>
    public interface IRepositoryBase
    {
        public Task<IHttpResult<T>> AddAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class;
        public Task<IHttpResult<T>> GetByIdAsync<T,TKey>(TKey aEntityId, CancellationToken aCancellationToken = default)
            where T : class
            where TKey : struct, IEquatable<TKey>;
        public Task<IHttpResult<T>> UpdateAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class;
        public Task<IHttpResult<T>> DeleteAsync<T>(T aEntity, CancellationToken aCancellationToken = default)
            where T : class;
    }
}
