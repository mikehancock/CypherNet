using CypherNet.Serialization;

namespace CypherNet.Transaction
{
    #region

    using CypherNet.Core;

    using Http;

    #endregion

    internal class DefaultCypherSessionFactory : ICypherSessionFactory
    {
        private readonly GraphStore store;

        public DefaultCypherSessionFactory(string baseUrl)
        {
            this.store = new GraphStore(baseUrl);
            this.store.Initialize();
        }

        public ICypherSession Create()
        {
            return new CypherSession(this.store.GetClient());
        }
    }
}