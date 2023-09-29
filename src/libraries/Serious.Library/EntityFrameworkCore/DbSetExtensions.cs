using System.Threading.Tasks;
using Serious;

namespace Microsoft.EntityFrameworkCore;

public static class DbSetExtensions
{
    public static ValueTask<T?> FindByIdAsync<T>(this DbSet<T> set, Id<T> id)
        where T : class =>
        set.FindAsync(id.Value);
}
