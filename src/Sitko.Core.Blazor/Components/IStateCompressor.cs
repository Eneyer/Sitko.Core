#if NET6_0_OR_GREATER
using System.Threading.Tasks;

namespace Sitko.Core.Blazor.Components;

public interface IStateCompressor
{
    Task<byte[]> ToGzipAsync<T>(T value);

    Task<T> FromGzipAsync<T>(byte[] bytes);
}
#endif
