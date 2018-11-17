using AggregateConsistency.Infrastructure;

namespace AggregateConsistency
{
    internal static class KeyStoreExtensions
    {
        public static Preprocessor DecryptFactory(this IKeyStore store, string scopeIdentity)
        {
            return new Wrapper(store, scopeIdentity).TryDecrypt;
        }
        //Wrapper to deal with the fact that Func does not do out....
        private struct Wrapper
        {
            private readonly IKeyStore _store;
            private readonly string _scopeIdentity;

            public Wrapper(IKeyStore store, string scopeIdentity)
            {
                _store = store;
                _scopeIdentity = scopeIdentity;
            }

            public bool TryDecrypt(byte[] input, out byte[] output)
            {
                return _store.TryDecrypt(_scopeIdentity, input, out output);
            }
        }
    }
}