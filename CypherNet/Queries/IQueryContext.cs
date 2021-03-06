﻿using CypherNet.Graph;

namespace CypherNet.Queries
{
    public interface IQueryContext<out TVariables>
    {
        TVariables Vars { get; }
    }

    public interface IReturnQueryContext<out TVariables> : IQueryContext<TVariables>, IEntityPropertyAccessor
    {
    }

    public interface IWhereQueryContext<out TVariables> : IQueryContext<TVariables>, IEntityPropertyAccessor
    {

    }

    public interface IEntityPropertyAccessor
    {
        [ParseToCypher("{0}.{1}")]
        TProp Prop<TProp>(
            [ArgumentEvaluator(typeof (MemberNameArgumentEvaluator))] IGraphEntity entity,
            [ArgumentEvaluator(typeof (ValueArgumentEvaluator))] string property);

        [ParseToCypher("{0}.{1}")]
        object Prop(
            [ArgumentEvaluator(typeof(MemberNameArgumentEvaluator))] IGraphEntity entity,
            [ArgumentEvaluator(typeof(ValueArgumentEvaluator))] string property);
    }

    public interface IUpdateQueryContext<out TVariables> : IQueryContext<TVariables>
    {
        [ParseToCypher("{0}.{1} = {2}")]
        ISetResult Set<TEntity, TValue>(
            [ArgumentEvaluator(typeof(MemberNameArgumentEvaluator))]TEntity entity,
            [ArgumentEvaluator(typeof(ValueArgumentEvaluator))]string property,
            [ArgumentEvaluator(typeof(StringWrapperArgumentEvaluator))]TValue newValue) where TEntity : IGraphEntity;
    }

    public interface ISetResult
    {
    }
}