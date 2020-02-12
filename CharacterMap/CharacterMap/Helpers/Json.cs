using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Storage;

namespace CharacterMap.Helpers
{
    public static class Json
    {
        public static Task<T> ReadAsync<T>(StorageFile file)
        {
            return Task.Run(async () =>
            {
                T result = default;
                var d = JsonSerializer.CreateDefault();
                using (var s = await file.OpenAsync(FileAccessMode.Read).AsTask().ConfigureAwait(false))
                using (var stream = s.AsStreamForRead())
                using (var reader = new StreamReader(stream))
                {
                    result = (T)d.Deserialize(reader, typeof(T));
                }

                return result;
            });
        }

        public static Task WriteAsync<T>(StorageFile file, T obj)
        {
            return Task.Run(async () =>
            {
                T result = default;
                var d = JsonSerializer.CreateDefault();
                using (var s = await file.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false))
                using (var stream = s.AsStreamForWrite())
                using (var writer = new StreamWriter(stream))
                {
                    d.Serialize(writer, obj);
                }

                return result;
            });
        }

        public static Task<T> ToObjectAsync<T>(string value)
        {
            return Task.Run(() => JsonConvert.DeserializeObject<T>(value));
        }

        public static Task<string> StringifyAsync(object value)
        {
            return Task.Run(() => JsonConvert.SerializeObject(value));
        }
    }
}
