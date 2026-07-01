using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace GhSpaceGass;

public class GhSpaceGassInfo : GH_AssemblyInfo
{
    public override string Name => "GhSpaceGass";

    public override Bitmap Icon => Icons.IconFactory.TabIcon();

    public override string Description =>
        "Grasshopper plug-in for Space Gass structural analysis and design.";

    public override Guid Id => new("609c941f-262a-4a9f-ac27-7c7b598fbb78");

    public override string AuthorName => "Mitchell Tesch";

    public override string AuthorContact => "https://github.com/mitchell-tesch/GhSpaceGass";

    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
}

/// <summary>
///     Handles GH load/unload lifecycle — disposes the SpaceGass session on shutdown (ADR-0007).
/// </summary>
public class GhSpaceGassPriority : GH_AssemblyPriority
{
    public override GH_LoadingInstruction PriorityLoad()
    {
        Instances.DocumentServer.DocumentRemoved += (sender, e) =>
        {
            // When the last document is closed, clean up
            if (Instances.DocumentServer.DocumentCount == 0)
                SpaceGassSessionManager.DisposeSession();
        };

        return GH_LoadingInstruction.Proceed;
    }
}