using System.Collections.Concurrent;

namespace CharacterMap.Helpers;

internal static class Singleton<T> where T : new()
{
    private static ConcurrentDictionary<Type, T> _instances => field ??= [];

    public static T Instance => field ??= _instances.GetOrAdd(typeof(T), (t) => new T());
}
