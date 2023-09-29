using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakeRouter : IRouter
    {
        readonly List<Func<VirtualPathContext, string?>> _generators = new();

        public Task RouteAsync(RouteContext context)
        {
            return Task.CompletedTask;
        }

        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            foreach (var generator in _generators)
            {
                if (generator(context) is { } path)
                {
                    return new VirtualPathData(this, path);
                }
            }

            return new VirtualPathData(this, "/");
        }

        public void AddVirtualPathGenerator(Func<VirtualPathContext, string?> generator)
        {
            _generators.Add(generator);
        }

        public void MapVirtualPath(string path, object routeValues)
        {
            var rvd = new RouteValueDictionary(routeValues);
            AddVirtualPathGenerator(vpc => {
                // A super simple route generator
                foreach (var (key, value) in rvd)
                {
                    if (!vpc.Values.TryGetValue(key, out var providedVal) || providedVal != value)
                    {
                        return null;
                    }

                    vpc.Values.Remove(key);
                }

                var fullPath = path;
                var separator = "?";
                foreach (var (key, value) in vpc.Values)
                {
                    fullPath += $"{separator}{key}={value}";
                    separator = "&";
                }

                return fullPath;
            });
        }
    }
}
