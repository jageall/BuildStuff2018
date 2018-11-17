using System;
using System.Collections.Generic;
using AggregateConsistency;

namespace AggregateConsistencyTests
{
    internal class TestKeyStore : IKeyStore
    {
        private readonly HashSet<string> _keys;

        public TestKeyStore()
        {
            _keys = new HashSet<string>();
        }
        public void Destroy(string keyName)
        {
            _keys.Remove(keyName);
        }

        public byte[] Encrypt(string keyName, byte[] data)
        {
            if (!_keys.Contains(keyName))
            {
                throw new InvalidOperationException();
            }

            return data;
        }

        public bool TryDecrypt(string keyName, byte[] data, out byte[] decrypted)
        {
            if (!_keys.Contains(keyName))
            {
                decrypted = new byte[] { };
                return false;
            }

            decrypted = data;
            return true;
        }

        public void CreateKeyIfNotExists(string keyName)
        {
            _keys.Add(keyName);
        }
    }
}