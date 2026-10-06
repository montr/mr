using System.Text.Json;
using Monoreport.Models;

namespace Monoreport.Services
{
    public sealed class DataResolver
    {
        public JsonElement Resolve(string path, string code, string location, MergeContext context)
        {
            var value = context.Values.Peek();
            
            foreach (var segment in path.Split('.'))
            {
                if (value.ValueKind != JsonValueKind.Object || value.TryGetProperty(segment, out value) == false)
                {
                    var message = $"Unknown template field: {path}";
                    if (context.Options.MissingValues == MissingValueBehavior.Error)
                    {
                        throw new TemplateException(message, path, code, location);
                    }

                    if (context.Options.MissingValues == MissingValueBehavior.Warning)
                    {
                        context.Warnings.Add(new(message, path, code, location));
                    }

                    return default;
                }
            }

            return value;
        }
    }
}
