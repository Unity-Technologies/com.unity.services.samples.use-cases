using System;
using Newtonsoft.Json;

namespace SampleGameModule.Models;

/// <summary>
/// Exception whose Message is the structured JSON error shape every sample client
/// parses: { status, name, message, retryAfter, details }. The Cloud Code SDK
/// surfaces it to the client inside a CloudCodeException message, and each
/// sample's CloudCodeManager reads the status and name from there.
/// </summary>
public class ModuleError : Exception
{
    public int Status { get; }
    public string Name { get; }

    public ModuleError(int status, string name, string message)
        : base(JsonConvert.SerializeObject(new
        {
            status,
            name,
            message,
            retryAfter = (int?)null,
            details = Array.Empty<string>(),
        }))
    {
        Status = status;
        Name = name;
    }
}
