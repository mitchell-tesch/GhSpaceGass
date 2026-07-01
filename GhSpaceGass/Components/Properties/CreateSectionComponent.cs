using System;
using System.Drawing;
using GhSpaceGass.Core.Models;
using GhSpaceGass.Types;
using Grasshopper.Kernel;

namespace GhSpaceGass.Components.Properties;

public class CreateSectionComponent : GH_Component
{
    private int _inAreaFactor, _inIyFactor, _inIzFactor, _inTorsionFactor;
    private int _inAy, _inAz, _inPrincipalAngle;
    private int _inLibrary, _inName, _inArea, _inIy, _inIz, _inJ;
    private int _inMark, _inTransposed;
    
    private int _outSection;

    public CreateSectionComponent()
        : base("SG Section", "sgSection",
            "Create a SpaceGass section. Connect Library for library lookup, or omit for a custom user-defined section.",
            "SpaceGass", "2 | Properties")
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override Bitmap Icon => Icons.IconFactory.Section();
    public override Guid ComponentGuid => new("DDEA3958-E4B0-437B-B3CF-336783E89834");

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        _inMark = pManager.AddTextParameter("Mark", "Mk",
            "Optional mark/label.",
            GH_ParamAccess.item);
        _inLibrary = pManager.AddTextParameter("Library", "L",
            "SpaceGass section library (e.g. \"Aust300\"). Omit for custom section.",
            GH_ParamAccess.item);
        _inName = pManager.AddTextParameter("Name", "N",
            "Section name (library lookup or custom label).",
            GH_ParamAccess.item);
        _inArea = pManager.AddNumberParameter("Area", "A",
            "Cross-sectional area (custom mode).",
            GH_ParamAccess.item);
        _inIy = pManager.AddNumberParameter("Iy", "Iy",
            "Second moment of area about Y (custom mode).",
            GH_ParamAccess.item);
        _inIz = pManager.AddNumberParameter("Iz", "Iz",
            "Second moment of area about Z (custom mode).",
            GH_ParamAccess.item);
        _inJ = pManager.AddNumberParameter("J", "J",
            "Torsion constant (custom mode).",
            GH_ParamAccess.item);
        _inAy = pManager.AddNumberParameter("Ay", "Ay",
            "Shear area in local Y direction.",
            GH_ParamAccess.item);
        _inAz = pManager.AddNumberParameter("Az", "Az",
            "Shear area in local Z direction.",
            GH_ParamAccess.item);
        _inPrincipalAngle = pManager.AddNumberParameter("Principal Angle", "PA",
            "Principal axis rotation angle (custom mode).",
            GH_ParamAccess.item);
        _inAreaFactor = pManager.AddNumberParameter("Area Factor", "Af",
            "Area modification factor (must be > 0).",
            GH_ParamAccess.item);
        _inIyFactor = pManager.AddNumberParameter("Iy Factor", "Iyf",
            "Iy modification factor (must be > 0).",
            GH_ParamAccess.item);
        _inIzFactor = pManager.AddNumberParameter("Iz Factor", "Izf",
            "Iz modification factor (must be > 0).",
            GH_ParamAccess.item);
        _inTorsionFactor = pManager.AddNumberParameter("Torsion Factor", "Jf",
            "Torsion constant modification factor (must be > 0).",
            GH_ParamAccess.item);
        _inTransposed = pManager.AddBooleanParameter("Transposed", "Tr",
            "Whether the section is transposed.",
            GH_ParamAccess.item);

        // All optional except Name
        pManager[_inLibrary].Optional = true;
        for (var i = 2; i < pManager.ParamCount; i++)
            pManager[i].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        _outSection = pManager.AddParameter(new Param_SgSection(),
            "Section", "S",
            "The SpaceGass section.",
            GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
        var name = string.Empty;
        if (!da.GetData(_inName, ref name) || string.IsNullOrWhiteSpace(name))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Section name is required.");
            return;
        }

        // Check if Library is connected → library mode
        var library = string.Empty;
        var hasLibrary = da.GetData(_inLibrary, ref library) && !string.IsNullOrWhiteSpace(library);

        SgSectionData section;
        if (hasLibrary)
        {
            section = new SgSectionData(library, name);
        }
        else
        {
            // Custom mode — read properties
            double v = 0;
            double? area = null, iy = null, iz = null, j = null;
            double? ay = null, az = null, principalAngle = null;
            if (da.GetData(_inArea, ref v)) area = v;
            if (da.GetData(_inIy, ref v)) iy = v;
            if (da.GetData(_inIz, ref v)) iz = v;
            if (da.GetData(_inJ, ref v)) j = v;
            if (da.GetData(_inAy, ref v)) ay = v;
            if (da.GetData(_inAz, ref v)) az = v;
            if (da.GetData(_inPrincipalAngle, ref v)) principalAngle = v;

            section = new SgSectionData(name, area, iy, iz, j, ay, az, principalAngle);
        }

        // Read common optional parameters (apply in both modes)
        double fv = 0;
        if (da.GetData(_inAreaFactor, ref fv))
        {
            if (fv <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Area Factor must be > 0.");
                return;
            }

            section.AreaFactor = fv;
        }

        if (da.GetData(_inIyFactor, ref fv))
        {
            if (fv <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Iy Factor must be > 0.");
                return;
            }

            section.IyFactor = fv;
        }

        if (da.GetData(_inIzFactor, ref fv))
        {
            if (fv <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Iz Factor must be > 0.");
                return;
            }

            section.IzFactor = fv;
        }

        if (da.GetData(_inTorsionFactor, ref fv))
        {
            if (fv <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Torsion Factor must be > 0.");
                return;
            }

            section.TorsionFactor = fv;
        }

        // Shear areas in library mode
        if (hasLibrary)
        {
            if (da.GetData(_inAy, ref fv)) section.Ay = fv;
            if (da.GetData(_inAz, ref fv)) section.Az = fv;
        }

        var mark = string.Empty;
        if (da.GetData(_inMark, ref mark) && !string.IsNullOrWhiteSpace(mark))
            section.Mark = mark;

        var transposed = false;
        if (da.GetData(_inTransposed, ref transposed))
            section.Transposed = transposed;

        da.SetData(_outSection, new GH_SgSection(section));
    }
}