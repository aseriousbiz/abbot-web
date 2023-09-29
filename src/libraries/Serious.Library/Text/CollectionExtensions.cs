using System.Collections.Generic;
using System.Linq;
using Serious.Cryptography;

namespace Serious.Text;

public static class CollectionExtensions
{
    static readonly CryptoRandom CryptoRandom = new();

    public static T Random<T>(this ICollection<T> items)
    {
        return items.ElementAt(CryptoRandom.Next(items.Count));
    }
}
