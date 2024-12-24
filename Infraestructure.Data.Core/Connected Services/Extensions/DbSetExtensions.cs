using System;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Infraestructure.Data.Core.Extensions
{
    public static class DbSetExtensions
    {
        public static async Task<IEnumerable<T>> FromSqlQueryAsync<T>(this DbContext dbContext,
            string sqlQuery, object parameters = null, IDbTransaction transaction = null,
            int? commandTimeout = null, CommandType? commandType = null)
        {
            var connection = dbContext.Database;
            return await connection.QueryAsync<T>(sqlQuery, parameters, transaction, commandTimeout, commandType);
        }

        public static async Task<IEnumerable<TReturn>> FromSqlQueryAsync<TFirst, TSecond, TReturn>(this DbContext dbContext,
            string sql, Func<TFirst, TSecond, TReturn> map, object param = null, IDbTransaction transaction = null,
            bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
        {
            var connection = dbContext.Database.GetDbConnection();
            return await connection.QueryAsync(sql, map, param, transaction, buffered, splitOn, commandTimeout, commandType);
        }
        public static async Task ExecuteNonQueryAsync(this DbContext dbContext, string sqlQuery,
            object parameters = null, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var connection = dbContext.Database..GetDbConnection();

            await connection.ExecuteAsync(sqlQuery, parameters, transaction, commandTimeout, CommandType.Text);
        }
    }
}