﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using rgvlee.Core.Common.Helpers;
using ProjectExpressionHelper = EntityFrameworkCore.Testing.Common.Helpers.ExpressionHelper;

namespace EntityFrameworkCore.Testing.Common
{
    /// <inheritdoc />
    public class AsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private static readonly ILogger Logger = LoggingHelper.CreateLogger<AsyncQueryProvider<T>>();

        public AsyncQueryProvider(IEnumerable<T> enumerable)
        {
            Source = enumerable.AsQueryable();
        }

        /// <summary>
        ///     The query provider source.
        /// </summary>
        public virtual IQueryable<T> Source { get; set; }

        /// <inheritdoc />
        /// <remarks>In this implementation it is just a wrapper for <see cref="AsyncQueryProvider{T}.CreateQuery{T}(Expression)" /></remarks>
        public virtual IQueryable CreateQuery(Expression expression)
        {
            Logger.LogDebug("CreateQuery: invoked");

            //Handles cases where we are projecting to another type
            if (expression is MethodCallExpression mce)
            {
                var returnType = mce.Method.ReturnType;
                if (returnType.GetGenericTypeDefinition() != typeof(IQueryable<>))
                {
                    throw new InvalidOperationException($"Expected IQueryable<>; actual {returnType.FullName}");
                }

                var createQueryMethod = typeof(IQueryProvider).GetMethods().Single(x => x.Name.Equals(nameof(IQueryProvider.CreateQuery)) && x.IsGenericMethod);

                var createQueryResult = createQueryMethod.MakeGenericMethod(returnType.GetGenericArguments().Single()).Invoke(this, new[] { expression });

                return (IQueryable) Activator.CreateInstance(typeof(AsyncEnumerable<>).GetGenericTypeDefinition().MakeGenericType(returnType.GetGenericArguments().Single()),
                    createQueryResult);
            }

            return CreateQuery<T>(expression);
        }

        /// <inheritdoc />
        public virtual IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            Logger.LogDebug("CreateQuery<TElement>: invoked");

            if (expression is FromSqlQueryRootExpression)
            {
                Logger.LogDebug("CreateQuery: catch all exception invoked");
                throw new NotSupportedException();
            }

            ProjectExpressionHelper.ThrowIfExpressionIsNotSupported(expression);

            return new AsyncEnumerable<TElement>(Source.Provider.CreateQuery<TElement>(EnsureExpressionCanBeResolvedBySourceProvider(expression)));
        }

        /// <inheritdoc />
        public virtual object Execute(Expression expression)
        {
            Logger.LogDebug("Execute: invoked");
            ProjectExpressionHelper.ThrowIfExpressionIsNotSupported(expression);
            return Source.Provider.Execute(EnsureExpressionCanBeResolvedBySourceProvider(expression));
        }

        /// <inheritdoc />
        public virtual TResult Execute<TResult>(Expression expression)
        {
            Logger.LogDebug("Execute<TResult>: invoked");
            ProjectExpressionHelper.ThrowIfExpressionIsNotSupported(expression);
            return Source.Provider.Execute<TResult>(EnsureExpressionCanBeResolvedBySourceProvider(expression));
        }

        /// <inheritdoc />
        public virtual TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            //TResult is a Task<T>. The provider requires T.
            return (TResult) typeof(AsyncQueryProvider<T>).GetMethod(nameof(WrapExecuteAsync), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(TResult).GetGenericArguments())
                .Invoke(this, new object[] { expression, cancellationToken });
        }

        private Task<TResult> WrapExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute<TResult>(expression));
        }

        private Expression EnsureExpressionCanBeResolvedBySourceProvider(Expression expression)
        {
            Logger.LogDebug("EnsureExpressionCanBeResolvedBySourceProvider: invoked");

            if (expression is MethodCallExpression mce && mce.Arguments[0] is Microsoft.EntityFrameworkCore.Query.QueryRootExpression)
            {
                for (var i = 0; i < mce.Arguments.Count; i++)
                {
                    Logger.LogDebug("argument[{i}]: {argument}", i, mce.Arguments[i].ToString());
                }

                //This ensures that Source provider will always be able to resolve the expression
                var arguments = new List<Expression>();
                arguments.Add(Source.Expression);
                arguments.AddRange(mce.Arguments.Skip(1));

                Logger.LogDebug("sourceExpression: {sourceExpression}", Source.Expression.ToString());

                for (var i = 0; i < mce.Arguments.Count; i++)
                {
                    Logger.LogDebug("argument[{i}]: {argument}", i, mce.Arguments[i].ToString());
                }

                return Expression.Call(mce.Method, arguments);
            }

            return expression;
        }
    }
}