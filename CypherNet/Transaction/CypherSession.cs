namespace CypherNet.Transaction
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    using CypherNet.Core;

    using Dynamic;
    using Graph;
    using Queries;
    using Serialization;
    using StaticReflection;

    #endregion

    public class CypherSession : ICypherSession
    {
        private static readonly string NodeVariableName = ReflectOn<SingleNodeResult>.Member(a => a.NewNode).Name;

        private static readonly string CreateNodeClauseFormat =
            String.Format(@"CREATE ({0}{{0}} {{1}}) RETURN {0} as {{2}}, id({0}) as {{3}}, labels({0}) as {{4}};",
                          NodeVariableName);

        private readonly IWebSerializer _webSerializer;
        private readonly IEntityCache _entityCache;
        private readonly INeoClient _webClient;

        internal CypherSession(INeoClient webClient)
        {
            _webClient = webClient;
            _entityCache = new DictionaryEntityCache();
            _webSerializer = new DefaultJsonSerializer(_entityCache);
        }

        #region ICypherSession Members

        public ICypherQueryStart<TVariables> BeginQuery<TVariables>()
        {
            return new FluentCypherQueryBuilder<TVariables>(this._webClient);
        }

        public ICypherQueryStart<TVariables> BeginQuery<TVariables>(
            Expression<Func<ICypherPrototype, TVariables>> variablePrototype)
        {
            return new FluentCypherQueryBuilder<TVariables>(this._webClient);
        }

        public Node CreateNode(object properties)
        {
            return CreateNode(properties, null);
        }

        public Node CreateNode(object properties, string label)
        {
            var props = _webSerializer.Serialize(properties);
            var propNames = new EntityReturnColumns(NodeVariableName);
            var clause = string.Format(
                CreateNodeClauseFormat,
                string.IsNullOrEmpty(label) ? "" : ":" + label,
                props,
                propNames.PropertiesPropertyName,
                propNames.IdPropertyName,
                propNames.LabelsPropertyName);
           
            var result = this._webClient.QueryAsync(clause).Result;
            return result.Read() ? result.Get<Node>(0) : null;
        }


        public Relationship CreateRelationship(Node node1, Node node2, string type, object relationshipProperties = null)
        {
            var query = BeginQuery(n => new {node1 = n.Node, node2 = n.Node, rel = n.Rel})
                .Start(ctx => ctx
                                  .StartAtId(ctx.Vars.node1, node1.Id)
                                  .StartAtId(ctx.Vars.node2, node2.Id))
                .Create(ctx => ctx.CreateRel(ctx.Vars.node1, ctx.Vars.rel, type, relationshipProperties, ctx.Vars.node2))
                .Return(ctx => ctx.Vars.rel)
                .Fetch();

            return query.FirstOrDefault();
        }

        public Node GetNode(long id)
        {
            if (_entityCache.Contains<Node>(id))
            {
                return _entityCache.GetEntity<Node>(id);
            }
            else
            {
                var query = BeginQuery(n => new {newNode = n.Node})
                    .Start(v => v.StartAtId(v.Vars.newNode, id))
                    .Return(ctx => new {ctx.Vars.newNode})
                    .Fetch();
                var firstRow = query.FirstOrDefault();
                return firstRow == null ? null : firstRow.newNode;
            }
        }

        public void Delete(long nodeId)
        {
            BeginQuery(n => new {newNode = n.Node})
                .Start(v => v.StartAtId(v.Vars.newNode, nodeId))
                .Delete(v => v.newNode)
                .Execute();
            _entityCache.Remove<Node>(nodeId);
        }

        public void Delete(Node node)
        {
            Delete(node.Id);
        }

        public void Clear()
        {
            _entityCache.Clear();
        }

        private static readonly MethodInfo SetMethodInfo = typeof(IUpdateQueryContext<SingleNodeResult>).GetMethod("Set");
        private static readonly PropertyInfo VarsProperty =
                (PropertyInfo)ReflectOn<IUpdateQueryContext<SingleNodeResult>>.Member(c => c.Vars).MemberInfo;
        private static readonly PropertyInfo NewNodeProperty =
            (PropertyInfo) ReflectOn<SingleNodeResult>.Member(c => c.NewNode).MemberInfo;

        public void Save(Node node)
        {
            var props = node as IDynamicMetaData;
            var vals = props.GetAllValues().Where(kvp => !Node.NodePropertyNames.Contains(kvp.Key));
            
            var setActions = new List<Expression<Func<IUpdateQueryContext<SingleNodeResult>, ISetResult>>>();
            foreach (var val in vals)
            {
                var param = Expression.Parameter(typeof(IUpdateQueryContext<SingleNodeResult>));
                var propType = val.Value.GetType();
                var method = SetMethodInfo.MakeGenericMethod(new[] { typeof(Node), propType });
                var member = Expression.Property(Expression.Property(param, VarsProperty), NewNodeProperty);
                var call = Expression.Call(param, method, member, Expression.Constant(val.Key), Expression.Constant(val.Value));
                var lambda = Expression.Lambda<Func<IUpdateQueryContext<SingleNodeResult>, ISetResult>>(call, param);
                setActions.Add(lambda);
            }

            BeginQuery<SingleNodeResult>().Start(v => v.StartAtId(v.Vars.NewNode, node.Id))
                                          .Update(setActions.ToArray())
                                          .Execute();
        }

        #endregion

        internal class SingleNodeResult
        {
            public SingleNodeResult(Node newNode)
            {
                NewNode = newNode;
            }

            public Node NewNode { get; private set; }
        }
    }
}