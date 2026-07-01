using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Models;

/// <summary>
///     In-memory representation of a plate element (3 or 4 node shell element).
///     No API call — pure data. References corner points and material by Goo reference.
/// </summary>
public class SgPlateData
{
    public SgPlateData(
        SgPoint3D[] nodes,
        SgMaterialData material,
        double actualThickness,
        double? bendingThickness = null,
        double? membraneThickness = null,
        double? shearThickness = null,
        double offset = 0,
        PlateTheory? theory = null)
    {
        if (nodes == null || nodes.Length < 3 || nodes.Length > 4)
            throw new ArgumentException(
                "Plate must have exactly 3 or 4 corner nodes.",
                nameof(nodes));

        Nodes = nodes;
        Material = material ?? throw new ArgumentNullException(nameof(material));
        ActualThickness = actualThickness;
        BendingThickness = bendingThickness;
        MembraneThickness = membraneThickness;
        ShearThickness = shearThickness;
        Offset = offset;
        Theory = theory;
    }

    /// <summary>Corner nodes (3 for tri, 4 for quad plate).</summary>
    public SgPoint3D[] Nodes { get; }

    /// <summary>The material assigned to this plate.</summary>
    public SgMaterialData Material { get; }

    /// <summary>Actual plate thickness.</summary>
    public double ActualThickness { get; }

    /// <summary>Bending thickness override (null = use actual).</summary>
    public double? BendingThickness { get; }

    /// <summary>Membrane thickness override (null = use actual).</summary>
    public double? MembraneThickness { get; }

    /// <summary>Shear thickness override (null = use actual).</summary>
    public double? ShearThickness { get; }

    /// <summary>Offset from the nodal plane.</summary>
    public double Offset { get; }

    /// <summary>Plate theory (Kirchoff or Mindlin). Null = SpaceGass default.</summary>
    public PlateTheory? Theory { get; }

    /// <summary>True if this is a triangular plate (3 nodes).</summary>
    public bool IsTriangle => Nodes.Length == 3;
}

