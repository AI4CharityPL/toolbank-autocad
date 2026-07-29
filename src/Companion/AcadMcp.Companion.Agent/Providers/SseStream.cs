using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AcadMcp.Companion.Agent.Providers;

/// <summary>Helpers for reading Server-Sent-Events (text/event-stream) bodies.</summary>
internal static class SseStream
{
    /// <summary>
    /// Yields the raw payload of each <c>data:</c> line in an SSE response. Lines equal to
    /// <c>[DONE]</c> are skipped. Comment/keepalive and other field lines are ignored.
    /// </summary>
    /// <param name="idleTimeout">
    /// Maximum time to wait for the NEXT byte from the provider before giving up. This is the
    /// guard that turns a silently stalled stream (provider hiccup, proxy/VPN holding the socket,
    /// rate-limit pause) into a clean <see cref="TimeoutException"/> instead of a permanent hang.
    /// Pass <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </param>
    public static async IAsyncEnumerable<string> ReadDataLinesAsync(
        HttpResponseMessage response,
        TimeSpan idleTimeout,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        bool hasIdle = idleTimeout > TimeSpan.Zero && idleTimeout != Timeout.InfiniteTimeSpan;

        while (true)
        {
            string? line;
            if (hasIdle)
            {
                using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                idleCts.CancelAfter(idleTimeout);
                try
                {
                    line = await reader.ReadLineAsync(idleCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Model przestał odpowiadać — brak danych w strumieniu przez ponad {idleTimeout.TotalSeconds:N0} s.");
                }
            }
            else
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }

            if (line is null) yield break;
            if (line.Length == 0) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line.Substring(5).Trim();
            if (payload.Length == 0) continue;
            if (payload == "[DONE]") yield break;

            yield return payload;
        }
    }
}
