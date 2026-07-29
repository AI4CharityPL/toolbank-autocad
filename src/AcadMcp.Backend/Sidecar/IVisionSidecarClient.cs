// Sanctioned interface for talking to the AcadMcp.Vision Python sidecar.
// See rule 29-acad-vision-architecture.mdc.

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Backend.Sidecar;

public interface IVisionSidecarClient
{
    /// <summary>True iff a /health probe within the last 30s succeeded.</summary>
    bool IsHealthy { get; }

    /// <summary>Resolved sidecar base URL, e.g. http://127.0.0.1:50062.</summary>
    string BaseUrl { get; }

    /// <summary>POST a JSON object to the given relative path and deserialize the response.</summary>
    Task<JsonNode?> PostJsonAsync(string relativePath, JsonNode body, int timeoutMs, CancellationToken ct);

    /// <summary>GET a JSON document from a relative path. Used for /health, /version.</summary>
    Task<JsonNode?> GetJsonAsync(string relativePath, int timeoutMs, CancellationToken ct);
}

/// <summary>Thrown when the Python sidecar is not reachable.</summary>
public sealed class VisionUnavailableException : System.Exception
{
    public string BaseUrl { get; }
    public VisionUnavailableException(string baseUrl, string message, System.Exception? inner = null)
        : base(message, inner)
    {
        BaseUrl = baseUrl;
    }
}

/// <summary>Thrown when the sidecar returned 503 model_not_available with an installHint.</summary>
public sealed class VisionEngineUnavailableException : System.Exception
{
    public string Engine { get; }
    public string InstallHint { get; }
    public VisionEngineUnavailableException(string engine, string installHint)
        : base($"Vision engine '{engine}' is not available. {installHint}")
    {
        Engine = engine;
        InstallHint = installHint;
    }
}

/// <summary>Thrown when the sidecar returned a non-2xx that isn't 503.</summary>
public sealed class VisionToolException : System.Exception
{
    public int StatusCode { get; }
    public VisionToolException(int statusCode, string message)
        : base($"Vision sidecar HTTP {statusCode}: {message}")
    {
        StatusCode = statusCode;
    }
}
