namespace GhSpaceGass.Core.Models;

/// <summary>
///     Result of disassembling an existing SpaceGass model.
///     Contains the model maps and flat output lists for node, member, and plate data.
/// </summary>
public class SgDisassembledModel
{
    public SgDisassembledModel(SgModelData model)
    {
        Model = model;
    }

    /// <summary>The model with populated ID ↔ geometry maps.</summary>
    public SgModelData Model { get; }

    /// <summary>Node data ordered by node ID.</summary>
    public List<SgDisassembledNode> Nodes { get; } = new();

    /// <summary>Member data ordered by member ID.</summary>
    public List<SgDisassembledMember> Members { get; } = new();

    /// <summary>Plate data ordered by plate ID.</summary>
    public List<SgDisassembledPlate> Plates { get; } = new();

    /// <summary>Warnings generated during disassembly.</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>Node data from the SpaceGass model.</summary>
public readonly record struct SgDisassembledNode(int Id, SgPoint3D Point);

/// <summary>Member data from the SpaceGass model.</summary>
public readonly record struct SgDisassembledMember(
    int Id,
    SgPoint3D Start,
    SgPoint3D End,
    int SectionId,
    int MaterialId,
    string TypeName);

/// <summary>Plate data from the SpaceGass model.</summary>
public readonly record struct SgDisassembledPlate(
    int Id,
    SgPoint3D[] CornerPoints,
    int MaterialId);
