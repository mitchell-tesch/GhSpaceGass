using System.Drawing;
using GhSpaceGass.Types;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace GhSpaceGass.Helpers;

/// <summary>
///     Reusable helper for value list creation on component inputs.
///     Works with <see cref="Param_SgIntegerOption" /> to provide:
///     1. Auto-create on placement: value lists appear wired to enum inputs when the component is first placed.
///     2. Right-click create: "Add [Name] value list" on the input parameter grip creates, positions, and wires a value list.
/// </summary>
public static class ValueListHelper
{
    /// <summary>Label–value pair for a value list item.</summary>
    public readonly struct Option
    {
        public readonly string Label;
        public readonly string Value;

        public Option(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    // ── Member options ──────────────────────────────────────────────

    public static readonly Option[] MemberTypeOptions =
    {
        new("Beam", "0"), new("Truss", "1"), new("Cable", "2"),
        new("Compression Only", "3"), new("Tension Only", "4")
    };

    public static readonly Option[] DirectionAxisOptions =
    {
        new("Not Applicable", "0"), new("X Axis", "1"), new("Y Axis", "2"),
        new("Z Axis", "3"), new("Negative X Axis", "4"),
        new("Negative Y Axis", "5"), new("Negative Z Axis", "6")
    };

    // ── Analysis options ────────────────────────────────────────────

    public static readonly Option[] AnalysisTypeOptions =
    {
        new("Linear Static", "0"), new("Non-linear Static", "1"),
        new("Buckling", "2"), new("Dynamic Frequency", "3")
    };

    public static readonly Option[] ForceModeOptions =
    {
        new("End Forces", "0"), new("Intermediate", "1")
    };

    // ── Load options ────────────────────────────────────────────────

    public static readonly Option[] PositionUnitsOptions =
    {
        new("Actual", "0"), new("Percent", "1")
    };

    public static readonly Option[] LoadAxesOptions =
    {
        new("Local", "0"), new("Global", "1")
    };

    // ── Restraint friction options ──────────────────────────────────

    public static readonly Option[] FrictionNormalAxisOptions =
    {
        new("None", "0"), new("X Axis", "1"), new("Y Axis", "2"), new("Z Axis", "3")
    };

    public static readonly Option[] FrictionNormalDirectionOptions =
    {
        new("Either", "0"), new("Positive Only", "1"), new("Negative Only", "2")
    };

    // ── Analysis settings (shared across Static, Buckling, Dynamic) ─

    public static readonly Option[] PlateTypeOptions =
    {
        new("BCPlates", "0"), new("DLPlates", "1")
    };

    public static readonly Option[] SolverTypeOptions =
    {
        new("Paradise", "0"), new("Wavefront", "1"), new("SGX", "2")
    };

    public static readonly Option[] TensionCompressionOptions =
    {
        new("Activated", "0"), new("No Reversal", "1"),
        new("Deactivated", "2"), new("Gradual Activation", "3")
    };

    public static readonly Option[] OptimizationMethodOptions =
    {
        new("None", "0"), new("Auto", "1"), new("General", "2"),
        new("Linear", "3"), new("Circular", "4")
    };

    public static readonly Option[] OptimizationAxisOptions =
    {
        new("X", "0"), new("Y", "1"), new("Z", "2"), new("Vector", "3")
    };

    // ── Static settings only ────────────────────────────────────────

    public static readonly Option[] LoadingTypeOptions =
    {
        new("Full", "0"), new("Residual", "1")
    };

    public static readonly Option[] MatrixTypeOptions =
    {
        new("Secant", "0"), new("Tangent", "1")
    };

    // ── Buckling settings only ──────────────────────────────────────

    public static readonly Option[] BucklingTheoryOptions =
    {
        new("Signcount Eigensolver", "0"), new("Classic Eigensolver", "1")
    };

    public static readonly Option[] AxialForceDistributionOptions =
    {
        new("Linear", "0"), new("NonLinear", "1")
    };

    public static readonly Option[] ConstraintAxesOptions =
    {
        new("Global", "0"), new("Inclined", "1")
    };

    public static readonly Option[] OffsetAxesOptions =
    {
        new("Local", "0"), new("Global", "1")
    };

    public static readonly Option[] PlateTheoryOptions =
    {
        new("Kirchoff", "0"), new("Mindlin", "1")
    };

    public static readonly Option[] PlateForceModeOptions =
    {
        new("Element Forces", "0"), new("Nodal Forces", "1")
    };

    // ── Assembly options ────────────────────────────────────────────

    public static readonly Option[] AssemblyModeOptions =
    {
        new("Rebuild", "0"), new("Append", "1")
    };

    // ── Methods ─────────────────────────────────────────────────────


    /// <summary>
    ///     For each <see cref="Param_SgIntegerOption" /> input with
    ///     <see cref="Param_SgIntegerOption.AutoCreateOnPlacement" /> = true and no sources,
    ///     creates a populated value list and wires it to the input.
    ///     Call from <see cref="GH_DocumentObject.AddedToDocument" />.
    /// </summary>
    public static void AutoCreateOnPlacement(GH_Component component, GH_Document document)
    {
        for (var i = 0; i < component.Params.Input.Count; i++)
        {
            if (component.Params.Input[i] is Param_SgIntegerOption
                {
                    AutoCreateOnPlacement: true, ValueListOptions: not null
                } optParam
                && optParam.SourceCount == 0)
            {
                var vl = new GH_ValueList();
                vl.CreateAttributes();
                vl.Name = optParam.ValueListName;
                vl.NickName = optParam.ValueListName;
                vl.ListItems.Clear();
                foreach (var opt in optParam.ValueListOptions)
                    vl.ListItems.Add(new GH_ValueListItem(opt.Label, opt.Value));
                if (optParam.ValueListDefaultIndex >= 0 &&
                    optParam.ValueListDefaultIndex < vl.ListItems.Count)
                    vl.SelectItem(optParam.ValueListDefaultIndex);

                vl.Attributes.Pivot = new PointF(
                    component.Attributes.Pivot.X - 200,
                    component.Attributes.Pivot.Y + i * 20);

                document.AddObject(vl, false);
                optParam.AddSource(vl);
            }
        }
    }
}
