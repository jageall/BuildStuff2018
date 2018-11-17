using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace AggregateConsistency
{
	interface IKeyStore
	{
		void Destroy(string keyName);
		byte[] Encrypt(string keyName, byte[] data);
		bool TryDecrypt(string keyName, byte[] data, out byte[] decrypted);
		void CreateKeyIfNotExists(string keyName);
	}
	internal class ReallyInsecureAndVeryTemporaryKeyStore : IKeyStore
	{
		private readonly RandomNumberGenerator _rng;
		private readonly Dictionary<string, byte[]> _keys;
		public ReallyInsecureAndVeryTemporaryKeyStore()
		{
			_keys = new Dictionary<string, byte[]>();
			_rng = RandomNumberGenerator.Create();

		}
		
		public void CreateKeyIfNotExists(string keyName)
		{
			if (_keys.TryGetValue(keyName, out _))
			{
				return;
			}

			var key = new byte[32];
			_rng.GetBytes(key, 0, 32);
			_keys.Add(keyName, key);
		}

		public void Destroy(string keyName)
		{
			_keys.Remove(keyName);
		}

		public byte[] Encrypt(string keyName, byte[] data)
		{
			if(!_keys.TryGetValue(keyName, out var key))
				throw new InvalidOperationException("Key does not exist");
			using (var algo = Aes.Create())
			{
				
				algo.Padding = PaddingMode.PKCS7;
				algo.KeySize = 256; 
				algo.Key = key;
				var ms = new MemoryStream();
				ms.Write(algo.IV);
				using (var crypto = new CryptoStream(ms, algo.CreateEncryptor(), CryptoStreamMode.Write))
				{
					crypto.Write(data);
				}

				return ms.ToArray();
			}
		}

		public bool TryDecrypt(string keyName, byte[] data, out byte[] decrypted)
		{
            decrypted = new byte[] { };
            if (!_keys.TryGetValue(keyName, out var key))
                return false;
			using (var algo = Aes.Create())
			{
				algo.Padding = PaddingMode.PKCS7;
				algo.KeySize = 256; 
				algo.Key = key;
				if(data.Length < algo.IV.Length) throw new InvalidOperationException();

				algo.IV = new Span<byte>(data, 0, 16).ToArray();

				var ms = new MemoryStream();
                try
                {
                    using (var crypto = new CryptoStream(ms, algo.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        crypto.Write(data, 16, data.Length - 16);
                    }
                }
                catch(CryptographicException)
                {
                    //Could handle multiple keys....
                    return false;
                }

				decrypted = ms.ToArray();
                return true;
			}
		}
	}
}