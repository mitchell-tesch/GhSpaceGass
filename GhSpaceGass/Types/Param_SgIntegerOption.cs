using System;
using System.Drawing;
using System.Windows.Forms;
using GhSpaceGass.Helpers;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

namespace GhSpaceGass.Types;

/// <summary>
///     An integer parameter that adds an "Add [Name] value list" right-click menu item
///     to create a populated GH_ValueList and wire it to itself.
///     Used for component inputs that map to enum-like option sets.
/// </summary>
public class Param_SgIntegerOption : Param_Integer
{
    /// <summary>Parameterless constructor required for Grasshopper serialization.</summary>
    public Param_SgIntegerOption()
    {
    }

    /// <summary>Creates a new integer option parameter with value list configuration.</summary>
    /// <param name="menuName">Display name for the "Add [Name] value list" menu item.</param>
    /// <param name="options">The value list items (label–value pairs).</param>
    /// <param name="defaultIndex">Index of the default-selected item in the value list.</param>
    /// <param name="defaultValue">Optional default integer value for the parameter.</param>
    /// <param name="autoCreate">If true, a value list is auto-created when the component is first placed.</param>
    public Param_SgIntegerOption(
        string menuName,
        ValueListHelper.Option[] options,
        int defaultIndex = 0,
        int? defaultValue = null,
        bool autoCreate = true)
    {
        ValueListName = menuName;
        ValueListOptions = options;
        ValueListDefaultIndex = defaultIndex;
        AutoCreateOnPlacement = autoCreate;
        if (defaultValue.HasValue)
            SetPersistentData(new GH_Integer(defaultValue.Value));
    }

    /// <summary>Display name shown in the "Add [Name] value list" menu item.</summary>
    internal string ValueListName { get; set; } = string.Empty;

    /// <summary>The value list items (label–value pairs).</summary>
    internal ValueListHelper.Option[] ValueListOptions { get; set; }

    /// <summary>Index of the default-selected item when the value list is created.</summary>
    internal int ValueListDefaultIndex { get; set; }

    /// <summary>If true, a value list is auto-created when the owning component is first placed.</summary>
    internal bool AutoCreateOnPlacement { get; set; }

    public override Guid ComponentGuid => new("8A407304-8CF8-4B8B-BBCD-49BA41D48564");

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
        base.AppendAdditionalMenuItems(menu);

        if (ValueListOptions == null || ValueListOptions.Length == 0) return;

        // menu.Items.Add(new ToolStripSeparator());

        var name = ValueListName;
        var options = ValueListOptions;
        var defaultIdx = ValueListDefaultIndex;

        var item = new ToolStripMenuItem($"Add {name} value list", null, (_, _) =>
        {
            var vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Name = name;
            vl.NickName = name;
            vl.ListItems.Clear();
            foreach (var opt in options)
                vl.ListItems.Add(new GH_ValueListItem(opt.Label, opt.Value));
            if (defaultIdx >= 0 && defaultIdx < vl.ListItems.Count)
                vl.SelectItem(defaultIdx);

            vl.Attributes.Pivot = new PointF(
                Attributes.Pivot.X - 200,
                Attributes.Pivot.Y);

            var doc = OnPingDocument();
            if (doc == null) return;
            doc.AddObject(vl, false);
            AddSource(vl);
            ExpireSolution(true);
        });
        item.Enabled = SourceCount == 0;
        menu.Items.Add(item);
    }

}


