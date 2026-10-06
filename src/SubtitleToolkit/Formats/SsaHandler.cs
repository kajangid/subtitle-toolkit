using SubtitleToolkit.Model;

namespace SubtitleToolkit.Formats;

/// <summary>
/// Parser and serializer for SubStation Alpha v4.00 (.ssa) subtitle files.
/// </summary>
public sealed class SsaHandler : AssHandler
{
    public override SubtitleFormat Format => SubtitleFormat.Ssa;

    protected override bool IsSsa => true;
}
