using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AggregateConsistency.Infrastructure
{
	internal static class JsonExtension
	{
		private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

		public static byte[] ToBytes(this JObject json) {
			using(var ms = new MemoryStream())
			using(var jw = new JsonTextWriter(new StreamWriter(ms, Encoding))) {
				json.WriteTo(jw);
				jw.Flush();
				return ms.ToArray();
			}
		}
	}
}