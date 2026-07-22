using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace GhSpaceGass.Types;

public class Param_SgSection : GH_Param<GH_SgSection>
{
    public Param_SgSection()
        : base("Section", "Sec", "A SpaceGass cross-section profile.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.Section();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000001");
}

public class Param_SgMaterial : GH_Param<GH_SgMaterial>
{
    public Param_SgMaterial()
        : base("Material", "Mat", "A SpaceGass material definition.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }
    
    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.Material();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000002");
}

public class Param_SgMember : GH_Param<GH_SgMember>
{
    public Param_SgMember()
        : base("Member", "Mem", "A SpaceGass structural member.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.Member();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000003");
}

public class Param_SgModel : GH_Param<GH_SgModel>
{
    public Param_SgModel()
        : base("Model", "Mod", "A compiled SpaceGass structural model.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.AssembleModel();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000004");
}

public class Param_SgRestraint : GH_Param<GH_SgRestraint>
{
    public Param_SgRestraint()
        : base("Restraint", "Rst", "A SpaceGass node restraint (boundary condition).",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.Restraint();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000005");
}

public class Param_SgLoadCase : GH_Param<GH_SgLoadCase>
{
    public Param_SgLoadCase()
        : base("Load Case", "LC", "A SpaceGass load case definition.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.LoadCase();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000006");
}

public class Param_SgNodeLoad : GH_Param<GH_SgNodeLoad>
{
    public Param_SgNodeLoad()
        : base("Node Load", "NL", "A SpaceGass node load (concentrated force/moment).",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.NodeLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000007");
}

public class Param_SgLoadCategory : GH_Param<GH_SgLoadCategory>
{
    public Param_SgLoadCategory()
        : base("Load Category", "Cat", "A SpaceGass load category definition.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.LoadCategory();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000008");
}

public class Param_SgMemberDistributedLoad : GH_Param<GH_SgMemberDistributedLoad>
{
    public Param_SgMemberDistributedLoad()
        : base("Member Distributed Load", "DL", "A SpaceGass member distributed load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.DistributedLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000009");
}

public class Param_SgSelfWeightLoad : GH_Param<GH_SgSelfWeightLoad>
{
    public Param_SgSelfWeightLoad()
        : base("Self-Weight Load", "SW", "A SpaceGass self-weight load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.SelfWeight();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000010");
}

public class Param_SgRelease : GH_Param<GH_SgRelease>
{
    public Param_SgRelease()
        : base("Release", "Rel", "A SpaceGass member end release.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.Release();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000011");
}

public class Param_SgCombinationLoadCase : GH_Param<GH_SgCombinationLoadCase>
{
    public Param_SgCombinationLoadCase()
        : base("Combination Load Case", "CLC", "A SpaceGass combination load case.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.CombinationLoadCase();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000012");
}

public class Param_SgRestraintStiffness : GH_Param<GH_SgRestraintStiffness>
{
    public Param_SgRestraintStiffness()
        : base("Restraint Stiffness", "RstK", "Spring stiffness parameters for a restraint.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.RestraintStiffness();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000013");
}

public class Param_SgRestraintFriction : GH_Param<GH_SgRestraintFriction>
{
    public Param_SgRestraintFriction()
        : base("Restraint Friction", "RstF", "Friction parameters for a restraint.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.RestraintFriction();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000014");
}

public class Param_SgAnalysisSettings : GH_Param<GH_SgAnalysisSettings>
{
    public Param_SgAnalysisSettings()
        : base("Analysis Settings", "ASet", "SpaceGass analysis settings.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.StaticSettings();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000015");
}

public class Param_SgLumpedMassLoad : GH_Param<GH_SgLumpedMassLoad>
{
    public Param_SgLumpedMassLoad()
        : base("Lumped Mass Load", "LM", "A SpaceGass lumped mass load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.LumpedMassLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000016");
}

public class Param_SgPrescribedDisplacement : GH_Param<GH_SgPrescribedDisplacement>
{
    public Param_SgPrescribedDisplacement()
        : base("Prescribed Displacement", "PD", "A SpaceGass prescribed node displacement.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.PrescribedDisplacement();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000017");
}

public class Param_SgMemberConcentratedLoad : GH_Param<GH_SgMemberConcentratedLoad>
{
    public Param_SgMemberConcentratedLoad()
        : base("Member Concentrated Load", "CL", "A SpaceGass member concentrated load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.ConcentratedLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000018");
}

public class Param_SgMemberPrestressLoad : GH_Param<GH_SgMemberPrestressLoad>
{
    public Param_SgMemberPrestressLoad()
        : base("Member Prestress Load", "PL", "A SpaceGass member prestress load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.PrestressLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000019");
}

public class Param_SgNodeConstraint : GH_Param<GH_SgNodeConstraint>
{
    public Param_SgNodeConstraint()
        : base("Node Constraint", "NC", "A SpaceGass node constraint (master-slave link).",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.NodeConstraint();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000020");
}

public class Param_SgMemberOffset : GH_Param<GH_SgMemberOffset>
{
    public Param_SgMemberOffset()
        : base("Member Offset", "Off", "A SpaceGass member offset.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MemberOffset();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000021");
}

public class Param_SgPlate : GH_Param<GH_SgPlate>
{
    public Param_SgPlate()
        : base("Plate", "Plt", "A SpaceGass plate element.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.PlateElement();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000022");
}

public class Param_SgPlatePressureLoad : GH_Param<GH_SgPlatePressureLoad>
{
    public Param_SgPlatePressureLoad()
        : base("Plate Pressure Load", "PPL", "A SpaceGass plate pressure load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.PlatePressureLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000023");
}

public class Param_SgThermalLoad : GH_Param<GH_SgThermalLoad>
{
    public Param_SgThermalLoad()
        : base("Thermal Load", "TL", "A SpaceGass thermal load.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.ThermalLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000024");
}

public class Param_SgMovingLoadScenario : GH_Param<GH_SgMovingLoadScenario>
{
    public Param_SgMovingLoadScenario()
        : base("Moving Load Scenario", "MLS", "A SpaceGass moving load scenario.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadScenario();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000025");
}

public class Param_SgMovingLoadVehicle : GH_Param<GH_SgMovingLoadVehicle>
{
    public Param_SgMovingLoadVehicle()
        : base("Moving Load Vehicle", "MLV", "A SpaceGass moving load vehicle.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadVehicle();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000026");
}

public class Param_SgMovingLoadPressure : GH_Param<GH_SgMovingLoadPressure>
{
    public Param_SgMovingLoadPressure()
        : base("Moving Load Pressure", "MLP", "A SpaceGass moving load pressure.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadPressure();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000027");
}

public class Param_SgMovingLoadTravelPath : GH_Param<GH_SgMovingLoadTravelPath>
{
    public Param_SgMovingLoadTravelPath()
        : base("Moving Load Travel Path", "MLTP", "A SpaceGass moving load travel path.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadTravelPath();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000028");
}

public class Param_SgMovingLoad : GH_Param<GH_SgMovingLoad>
{
    public Param_SgMovingLoad()
        : base("Moving Load", "ML", "A SpaceGass moving load (vehicle or pressure on a travel path).",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoad();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000029");
}

public class Param_SgMovingLoadSettings : GH_Param<GH_SgMovingLoadSettings>
{
    public Param_SgMovingLoadSettings()
        : base("Moving Load Settings", "MLSet", "Job-level moving-load engine settings.",
            "SpaceGass", "Parameters", GH_ParamAccess.item)
    {
    }

    public override GH_Exposure Exposure => GH_Exposure.hidden;
    protected override Bitmap Icon => Icons.IconFactory.MovingLoadSettings();
    public override Guid ComponentGuid => new("B1000001-0000-0000-0000-000000000030");
}
