namespace AcadMcp.Companion.Agent;

/// <summary>Live progress callbacks raised by the orchestrator while answering a message.</summary>
public interface IAgentObserver
{
    /// <summary>A chunk of assistant text arrived (streaming).</summary>
    void OnTextDelta(string delta);

    /// <summary>The agent is about to run a drawing tool.</summary>
    void OnToolStarted(string toolName, string summary);

    /// <summary>A drawing tool finished.</summary>
    void OnToolCompleted(string toolName, bool isError);

    /// <summary>A high-level status note (e.g. connecting, thinking).</summary>
    void OnStatus(string status);

    /// <summary>An AI-generated image (e.g. a room visualization) is ready to show in the chat.</summary>
    void OnImage(byte[] bytes, string mediaType, string caption);

    /// <summary>The planner produced a plan, or an executor advanced to the next step.</summary>
    void OnPlanUpdate(string text);

    /// <summary>
    /// Start a fresh assistant section (e.g. final summary after plan execution). Resets the
    /// streaming target so the closing answer is not mixed into step-by-step deltas.
    /// </summary>
    void OnSectionBreak();
}
