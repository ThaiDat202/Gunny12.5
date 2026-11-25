using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace GunnyRequestStudio.Core
{
    /// <summary>JSON helper (DataContractJsonSerializer) + tinh nang phu tro file.</summary>
    public static class Json
    {
        public static string Serialize<T>(T value, bool pretty = true)
        {
            if (value == null) return "null";
            var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = false, EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Never };
            var ser = new DataContractJsonSerializer(typeof(T), settings);
            using (var ms = new MemoryStream())
            {
                if (pretty)
                {
                    using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false, true, "  "))
                    {
                        ser.WriteObject(writer, value);
                        writer.Flush();
                    }
                }
                else
                {
                    ser.WriteObject(ms, value);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return ser.ReadObject(ms) as T;
            }
        }

        public static T LoadFile<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Deserialize<T>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        public static void SaveFile<T>(string path, T value)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, Serialize(value), Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    public static class Hash
    {
        public static string Sha256File(string path)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var fs = File.OpenRead(path))
                {
                    return ToHex(sha.ComputeHash(fs));
                }
            }
            catch { return null; }
        }

        public static string Sha256Bytes(byte[] data)
        {
            if (data == null) return null;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return ToHex(sha.ComputeHash(data));
            }
        }

        public static string Sha256String(string text)
        {
            return Sha256Bytes(Encoding.UTF8.GetBytes(text ?? string.Empty));
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
