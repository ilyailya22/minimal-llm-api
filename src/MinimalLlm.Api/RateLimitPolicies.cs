namespace MinimalLlm;

public static class RateLimitPolicies
{
    /// <summary>Guards the endpoints that make the model generate; see Program.cs for the rationale.</summary>
    public const string Generation = "generation";
}
