using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GhSpaceGass.Icons;

/// <summary>
///     Generates 24×24 pixel icons for Grasshopper components.
///     Panel colour scheme follows the SpaceGass tab layout.
/// </summary>
public static class IconFactory
{
    // ── Panel Colours ──────────────────────────────────────────────
    public static readonly Color Connection = ColorTranslator.FromHtml("#009688");
    public static readonly Color Properties = ColorTranslator.FromHtml("#607D8B");
    public static readonly Color Structure = ColorTranslator.FromHtml("#1976D2");
    public static readonly Color Cases = ColorTranslator.FromHtml("#795548");
    public static readonly Color Loads = ColorTranslator.FromHtml("#FF9800");
    public static readonly Color Model = ColorTranslator.FromHtml("#4CAF50");
    public static readonly Color Analysis = ColorTranslator.FromHtml("#E53935");
    public static readonly Color Results = ColorTranslator.FromHtml("#7B1FA2");

    // ── Shared Helpers ─────────────────────────────────────────────

    private static Bitmap Create()
    {
        return new Bitmap(24, 24);
    }

    private static Graphics Setup(Bitmap bmp)
    {
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        return g;
    }

    private static Pen P(Color c, float w = 1.5f) => new(c, w);
    private static SolidBrush B(Color c) => new(c);

    // ── Tab Icon ───────────────────────────────────────────────────

    /// <summary>Tab icon — stylised "SG" monogram.</summary>
    public static Bitmap TabIcon()
    {
        using var stream = typeof(IconFactory).Assembly
            .GetManifestResourceStream("GhSpaceGass.Icons.icon.png");
        if (stream != null)
            return new Bitmap(Image.FromStream(stream), 24, 24);

        // Fallback if resource not found
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2f);
        g.DrawLine(pen, 4, 20, 4, 6);
        g.DrawLine(pen, 20, 20, 20, 6);
        g.DrawLine(pen, 4, 6, 20, 6);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // CONNECTION PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hero: Lightning bolt connection symbol.</summary>
    public static Bitmap Connect()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Connection, 2f);
        using var brush = B(Connection);
        // Lightning bolt
        var bolt = new PointF[]
        {
            new(14, 2), new(6, 11), new(13, 11),
            new(10, 22), new(18, 13), new(11, 13), new(14, 2)
        };
        g.FillPolygon(brush, bolt);
        g.DrawPolygon(P(Color.White, 0.5f), bolt);
        return bmp;
    }

    /// <summary>Info document / "i" glyph.</summary>
    public static Bitmap JobInfo()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Connection, 1.5f);
        using var brush = B(Connection);
        // Document outline
        g.DrawRectangle(pen, 5, 2, 14, 20);
        // "i" glyph
        g.FillEllipse(brush, 10, 5, 4, 3);  // dot
        g.FillRectangle(brush, 10, 10, 4, 9); // stem
        return bmp;
    }

    /// <summary>Save / floppy disk symbol.</summary>
    public static Bitmap SaveJob()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Connection, 1.5f);
        using var brush = B(Connection);
        // Floppy disk body
        g.DrawRectangle(pen, 3, 3, 18, 18);
        // Label area (top inset)
        g.FillRectangle(brush, 7, 3, 10, 7);
        // Media slot (bottom centre)
        g.DrawRectangle(pen, 7, 14, 10, 7);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // PROPERTIES PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>I-beam cross-section silhouette.</summary>
    public static Bitmap Section()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Properties);
        // I-beam shape (top flange, web, bottom flange)
        g.FillRectangle(brush, 4, 3, 16, 3);   // top flange
        g.FillRectangle(brush, 9, 6, 3, 12);   // web
        g.FillRectangle(brush, 4, 18, 16, 3);  // bottom flange
        return bmp;
    }

    /// <summary>Material cube / block.</summary>
    public static Bitmap Material()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Properties, 1.5f);
        using var brush = B(Properties);
        // Isometric cube
        var top = new PointF[] { new(12, 2), new(22, 7), new(12, 12), new(2, 7) };
        var left = new PointF[] { new(2, 7), new(12, 12), new(12, 22), new(2, 17) };
        var right = new PointF[] { new(12, 12), new(22, 7), new(22, 17), new(12, 22) };
        g.FillPolygon(B(Color.FromArgb(180, Properties.R, Properties.G, Properties.B)), top);
        g.FillPolygon(brush, left);
        g.FillPolygon(B(Color.FromArgb(120, Properties.R, Properties.G, Properties.B)), right);
        g.DrawPolygon(pen, top);
        g.DrawPolygon(pen, left);
        g.DrawPolygon(pen, right);
        return bmp;
    }

    /// <summary>I-beam cross-section with query indicator (list lines).</summary>
    public static Bitmap GetSectionProperties()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Properties);
        using var pen = P(Properties, 1.5f);
        // I-beam shape (smaller, offset left)
        g.FillRectangle(brush, 2, 4, 12, 2);   // top flange
        g.FillRectangle(brush, 6, 6, 2, 8);    // web
        g.FillRectangle(brush, 2, 14, 12, 2);  // bottom flange
        // List lines (right side, indicating "query/list")
        g.DrawLine(pen, 16, 6, 22, 6);
        g.DrawLine(pen, 16, 10, 22, 10);
        g.DrawLine(pen, 16, 14, 22, 14);
        return bmp;
    }

    /// <summary>Material cube with query indicator (list lines).</summary>
    public static Bitmap GetMaterialProperties()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Properties, 1.5f);
        using var brush = B(Properties);
        // Small cube (offset left)
        var top = new PointF[] { new(8, 3), new(15, 6), new(8, 9), new(1, 6) };
        var left = new PointF[] { new(1, 6), new(8, 9), new(8, 16), new(1, 13) };
        var right = new PointF[] { new(8, 9), new(15, 6), new(15, 13), new(8, 16) };
        g.FillPolygon(B(Color.FromArgb(180, Properties.R, Properties.G, Properties.B)), top);
        g.FillPolygon(brush, left);
        g.FillPolygon(B(Color.FromArgb(120, Properties.R, Properties.G, Properties.B)), right);
        g.DrawPolygon(pen, top);
        g.DrawPolygon(pen, left);
        g.DrawPolygon(pen, right);
        // List lines (right side)
        g.DrawLine(pen, 17, 6, 23, 6);
        g.DrawLine(pen, 17, 10, 23, 10);
        g.DrawLine(pen, 17, 14, 23, 14);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // STRUCTURE PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hero: Beam member between two node dots.</summary>
    public static Bitmap Member()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2.5f);
        using var nodeBrush = B(Structure);
        // Beam line
        g.DrawLine(pen, 3, 18, 21, 6);
        // Node circles at each end
        g.FillEllipse(nodeBrush, 1, 16, 5, 5);
        g.FillEllipse(nodeBrush, 19, 4, 5, 5);
        // Local axis hint (small perpendicular tick at midpoint)
        using var axisPen = P(Color.FromArgb(160, Structure.R, Structure.G, Structure.B), 1f);
        g.DrawLine(axisPen, 10, 10, 14, 14);
        return bmp;
    }

    /// <summary>Triangle support (standard structural engineering restraint symbol).</summary>
    public static Bitmap Restraint()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 1.5f);
        using var brush = B(Structure);
        // Triangle
        var tri = new PointF[] { new(12, 6), new(4, 18), new(20, 18) };
        g.DrawPolygon(pen, tri);
        g.FillPolygon(B(Color.FromArgb(80, Structure.R, Structure.G, Structure.B)), tri);
        // Ground line
        g.DrawLine(pen, 2, 19, 22, 19);
        // Hatching below ground
        using var hatchPen = P(Structure, 1f);
        for (var x = 4; x <= 20; x += 3)
            g.DrawLine(hatchPen, x, 19, x - 2, 22);
        return bmp;
    }

    /// <summary>Hinge circle (release symbol).</summary>
    public static Bitmap Release()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2f);
        // Member line with gap for hinge
        g.DrawLine(pen, 2, 12, 9, 12);
        g.DrawLine(pen, 15, 12, 22, 12);
        // Hinge circle
        g.DrawEllipse(pen, 9, 8, 6, 6);
        // Small rotation arrow hint
        using var arrowPen = P(Structure, 1f);
        g.DrawArc(arrowPen, 7, 16, 10, 6, 200, 140);
        return bmp;
    }

    /// <summary>Spring zigzag.</summary>
    public static Bitmap RestraintStiffness()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 1.5f);
        // Fixed end (top)
        g.DrawLine(pen, 6, 2, 18, 2);
        // Spring coils
        var spring = new PointF[]
        {
            new(12, 2), new(16, 5), new(8, 8), new(16, 11),
            new(8, 14), new(16, 17), new(12, 20)
        };
        g.DrawLines(pen, spring);
        // Fixed end (bottom)
        g.DrawLine(pen, 6, 20, 18, 20);
        // Ground hatching at bottom
        using var hatchPen = P(Structure, 1f);
        for (var x = 7; x <= 17; x += 3)
            g.DrawLine(hatchPen, x, 20, x - 2, 23);
        return bmp;
    }

    /// <summary>Friction: node with hatching below.</summary>
    public static Bitmap RestraintFriction()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 1.5f);
        // Roller circle
        g.DrawEllipse(pen, 8, 6, 8, 8);
        // Ground line
        g.DrawLine(pen, 3, 15, 21, 15);
        // Friction hatching (dense)
        using var hatchPen = P(Structure, 1f);
        for (var x = 4; x <= 20; x += 2)
            g.DrawLine(hatchPen, x, 15, x - 2, 19);
        // Horizontal friction arrows
        using var arrPen = P(Color.FromArgb(200, Analysis.R, Analysis.G, Analysis.B), 1f);
        g.DrawLine(arrPen, 3, 3, 10, 3);
        g.DrawLine(arrPen, 10, 3, 8, 1);
        g.DrawLine(arrPen, 10, 3, 8, 5);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // CASES PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Load case: folder with "LC" label.</summary>
    public static Bitmap LoadCase()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Cases, 1.5f);
        using var brush = B(Cases);
        // Folder tab
        g.FillRectangle(brush, 3, 5, 6, 3);
        // Folder body
        g.DrawRectangle(pen, 3, 7, 18, 13);
        g.FillRectangle(B(Color.FromArgb(60, Cases.R, Cases.G, Cases.B)), 4, 8, 17, 12);
        // "LC" text
        // using var font = new Font("Arial", 7f, FontStyle.Bold);
        // g.DrawString("LC", font, brush, 5, 10);
        return bmp;
    }

    /// <summary>Load category: tag label.</summary>
    public static Bitmap LoadCategory()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Cases, 1.5f);
        using var brush = B(Cases);
        // Tag shape
        var tag = new PointF[]
        {
            new(6, 4), new(20, 4), new(20, 20), new(6, 20), new(2, 12)
        };
        g.DrawPolygon(pen, tag);
        g.FillPolygon(B(Color.FromArgb(60, Cases.R, Cases.G, Cases.B)), tag);
        // Hole in tag
        g.FillEllipse(B(Color.White), 4, 10, 4, 4);
        g.DrawEllipse(pen, 4, 10, 4, 4);
        return bmp;
    }

    /// <summary>Combination: Sigma Σ symbol.</summary>
    public static Bitmap CombinationLoadCase()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Cases, 2.5f);
        // Sigma (Σ) drawn as lines
        g.DrawLine(pen, 18, 3, 6, 3);   // top bar
        g.DrawLine(pen, 6, 3, 12, 12);  // upper diagonal
        g.DrawLine(pen, 12, 12, 6, 21); // lower diagonal
        g.DrawLine(pen, 6, 21, 18, 21); // bottom bar
        return bmp;
    }

    /// <summary>Load case folder with list lines (query variant).</summary>
    public static Bitmap GetLoadCases()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Cases, 1.5f);
        using var brush = B(Cases);
        // Smaller folder tab (offset left)
        g.FillRectangle(brush, 1, 5, 5, 3);
        // Smaller folder body
        g.DrawRectangle(pen, 1, 7, 13, 13);
        g.FillRectangle(B(Color.FromArgb(60, Cases.R, Cases.G, Cases.B)), 2, 8, 12, 12);
        // List lines (right side)
        g.DrawLine(pen, 17, 9, 23, 9);
        g.DrawLine(pen, 17, 13, 23, 13);
        g.DrawLine(pen, 17, 17, 23, 17);
        return bmp;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // LOADS PANEL
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Node load: downward arrow at a point.</summary>
    public static Bitmap NodeLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Arrow shaft
        g.DrawLine(pen, 12, 2, 12, 16);
        // Arrow head
        var head = new PointF[] { new(12, 20), new(8, 14), new(16, 14) };
        g.FillPolygon(brush, head);
        // Node dot at bottom
        g.FillEllipse(brush, 10, 19, 4, 4);
        return bmp;
    }

    /// <summary>Distributed load: arrows along a beam (UDL symbol).</summary>
    public static Bitmap DistributedLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var beamPen = P(Structure, 1.5f);
        using var brush = B(Loads);
        // Beam line
        g.DrawLine(beamPen, 2, 20, 22, 20);
        // Top line of UDL
        g.DrawLine(pen, 2, 4, 22, 4);
        // Vertical arrows
        for (var x = 4; x <= 20; x += 4)
        {
            g.DrawLine(pen, x, 4, x, 17);
            // Arrow heads
            var head = new PointF[] { new(x, 19), new(x - 2, 15), new(x + 2, 15) };
            g.FillPolygon(brush, head);
        }
        return bmp;
    }
    
    /// <summary>Self-weight: apple (Newton's gravity).</summary>
    public static Bitmap SelfWeight()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Stem
        g.DrawLine(pen, 12, 2, 12, 6);
        // Leaf
        g.DrawArc(pen, 12, 1, 6, 5, 180, 90);
        // Apple body (ellipse)
        g.FillEllipse(brush, 5, 6, 14, 15);
        g.DrawEllipse(pen, 5, 6, 14, 15);
        return bmp;
    }

    /// <summary>Self-weight apple with list lines (query variant).</summary>
    public static Bitmap GetSelfWeightLoads()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Stem
        g.DrawLine(pen, 8, 3, 8, 6);
        // Leaf
        g.DrawArc(pen, 8, 2, 5, 4, 180, 90);
        // Apple body (smaller, offset left)
        g.FillEllipse(brush, 2, 6, 12, 14);
        g.DrawEllipse(pen, 2, 6, 12, 14);
        // List lines (right side)
        g.DrawLine(pen, 17, 8, 23, 8);
        g.DrawLine(pen, 17, 13, 23, 13);
        g.DrawLine(pen, 17, 18, 23, 18);
        return bmp;
    }

    /// <summary>Node load arrow with list lines (query variant).</summary>
    public static Bitmap GetNodeLoads()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Arrow shaft (smaller, offset left)
        g.DrawLine(pen, 7, 2, 7, 13);
        // Arrow head
        var head = new PointF[] { new(7, 17), new(4, 12), new(10, 12) };
        g.FillPolygon(brush, head);
        // Node dot
        g.FillEllipse(brush, 5, 17, 4, 4);
        // List lines (right side)
        g.DrawLine(pen, 16, 6, 22, 6);
        g.DrawLine(pen, 16, 12, 22, 12);
        g.DrawLine(pen, 16, 18, 22, 18);
        return bmp;
    }

    /// <summary>Distributed load (UDL) with list lines (query variant for member loads).</summary>
    public static Bitmap GetMemberLoads()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var beamPen = P(Structure, 1.5f);
        using var brush = B(Loads);
        // Beam line (smaller, offset left)
        g.DrawLine(beamPen, 1, 18, 14, 18);
        // Top line of UDL
        g.DrawLine(pen, 1, 5, 14, 5);
        // Vertical arrows (fewer)
        for (var x = 3; x <= 12; x += 4)
        {
            g.DrawLine(pen, x, 5, x, 15);
            var head = new PointF[] { new(x, 17), new(x - 1.5f, 14), new(x + 1.5f, 14) };
            g.FillPolygon(brush, head);
        }

        // List lines (right side)
        g.DrawLine(pen, 17, 6, 23, 6);
        g.DrawLine(pen, 17, 12, 23, 12);
        g.DrawLine(pen, 17, 18, 23, 18);
        return bmp;
    }

    /// <summary>Plate pressure arrows with list lines (query variant for plate loads).</summary>
    public static Bitmap GetPlateLoads()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var platePen = P(Structure, 1f);
        using var loadPen = P(Loads, 1.5f);
        using var loadBrush = B(Loads);
        // Plate outline (smaller, offset left)
        g.DrawLine(platePen, 1, 16, 14, 16);
        g.DrawLine(platePen, 1, 16, 2, 20);
        g.DrawLine(platePen, 14, 16, 13, 20);
        // Pressure arrows
        for (var x = 4; x <= 11; x += 4)
        {
            g.DrawLine(loadPen, x, 4, x, 13);
            var head = new PointF[] { new(x, 15), new(x - 1.5f, 12), new(x + 1.5f, 12) };
            g.FillPolygon(loadBrush, head);
        }

        // List lines (right side)
        g.DrawLine(loadPen, 17, 6, 23, 6);
        g.DrawLine(loadPen, 17, 12, 23, 12);
        g.DrawLine(loadPen, 17, 18, 23, 18);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // MODEL PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hero: Assembly / network of connected nodes.</summary>
    public static Bitmap AssembleModel()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Model, 1.5f);
        using var brush = B(Model);
        // Network edges
        g.DrawLine(pen, 4, 18, 12, 4);
        g.DrawLine(pen, 12, 4, 20, 18);
        g.DrawLine(pen, 4, 18, 20, 18);
        g.DrawLine(pen, 12, 4, 4, 10);
        g.DrawLine(pen, 12, 4, 20, 10);
        g.DrawLine(pen, 4, 10, 4, 18);
        g.DrawLine(pen, 20, 10, 20, 18);
        // Nodes
        g.FillEllipse(brush, 10, 2, 4, 4);   // top
        g.FillEllipse(brush, 2, 8, 4, 4);    // left-mid
        g.FillEllipse(brush, 18, 8, 4, 4);   // right-mid
        g.FillEllipse(brush, 2, 16, 4, 4);   // bottom-left
        g.FillEllipse(brush, 18, 16, 4, 4);  // bottom-right
        return bmp;
    }

    /// <summary>Hero: Disassemble / exploded version of the assembly network — same 5 nodes pushed outward.</summary>
    public static Bitmap DisassembleModel()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var dashPen = P(Model, 1f);
        dashPen.DashStyle = DashStyle.Dash;
        using var brush = B(Model);
        // Same network edges as AssembleModel but dashed (ghost of original)
        g.DrawLine(dashPen, 4, 18, 12, 4);
        g.DrawLine(dashPen, 12, 4, 20, 18);
        g.DrawLine(dashPen, 4, 18, 20, 18);
        g.DrawLine(dashPen, 12, 4, 4, 10);
        g.DrawLine(dashPen, 12, 4, 20, 10);
        g.DrawLine(dashPen, 4, 10, 4, 18);
        g.DrawLine(dashPen, 20, 10, 20, 18);
        // Same 5 nodes but pushed outward from centre (12, 12)
        g.FillEllipse(brush, 10, 0, 4, 4);   // top — pushed up
        g.FillEllipse(brush, 0, 6, 4, 4);    // left-mid — pushed left
        g.FillEllipse(brush, 20, 6, 4, 4);   // right-mid — pushed right
        g.FillEllipse(brush, 0, 18, 4, 4);   // bottom-left — pushed out
        g.FillEllipse(brush, 20, 18, 4, 4);  // bottom-right — pushed out
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // ANALYSIS PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hero: Play/run triangle.</summary>
    public static Bitmap RunAnalysis()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Analysis);
        // Play triangle
        var play = new PointF[] { new(6, 3), new(6, 21), new(20, 12) };
        g.FillPolygon(brush, play);
        // White highlight edge
        using var highlightPen = P(Color.FromArgb(80, 255, 255, 255), 0.5f);
        g.DrawPolygon(highlightPen, play);
        return bmp;
    }

    /// <summary>Static settings: gear/cog.</summary>
    public static Bitmap StaticSettings()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Analysis, 1.5f);
        using var brush = B(Analysis);
        // Simplified gear: circle with teeth
        g.FillEllipse(brush, 7, 7, 10, 10);
        // Teeth (4 cardinal + 4 diagonal)
        foreach (var angle in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            var rad = angle * System.Math.PI / 180.0;
            var cx = 12 + (float)(System.Math.Cos(rad) * 9);
            var cy = 12 + (float)(System.Math.Sin(rad) * 9);
            g.FillRectangle(brush, cx - 2, cy - 2, 4, 4);
        }
        // Center hole
        g.FillEllipse(B(Color.White), 9, 9, 6, 6);
        return bmp;
    }

    /// <summary>Buckling settings: buckled column shape.</summary>
    public static Bitmap BucklingSettings()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Analysis, 2f);
        // Straight column (dashed)
        using var dashPen = new Pen(Color.FromArgb(100, Analysis.R, Analysis.G, Analysis.B), 1f)
            { DashStyle = DashStyle.Dash };
        g.DrawLine(dashPen, 12, 2, 12, 22);
        // Buckled shape (sine curve)
        var points = new PointF[12];
        for (var i = 0; i < 12; i++)
        {
            var t = i / 11f;
            var y = 2 + t * 20;
            var x = 12 + (float)(System.Math.Sin(t * System.Math.PI) * 6);
            points[i] = new PointF(x, y);
        }
        g.DrawCurve(pen, points, 0.5f);
        // End nodes
        using var nodeBrush = B(Analysis);
        g.FillEllipse(nodeBrush, 10, 0, 4, 4);
        g.FillEllipse(nodeBrush, 10, 20, 4, 4);
        return bmp;
    }

    /// <summary>Dynamic frequency: sine wave.</summary>
    public static Bitmap DynamicFrequencySettings()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Analysis, 2f);
        // Sine wave
        var points = new PointF[20];
        for (var i = 0; i < 20; i++)
        {
            var t = i / 19f;
            var x = 2 + t * 20;
            var y = 12 + (float)(System.Math.Sin(t * 2 * System.Math.PI) * 8);
            points[i] = new PointF(x, y);
        }
        g.DrawCurve(pen, points, 0.5f);
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════
    // RESULTS PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Hero: Upward reaction arrows from support.</summary>
    public static Bitmap NodeReactions()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 2f);
        using var brush = B(Results);
        // Support triangle (small)
        var tri = new PointF[] { new(12, 18), new(8, 22), new(16, 22) };
        g.FillPolygon(B(Color.FromArgb(100, Results.R, Results.G, Results.B)), tri);
        g.DrawPolygon(P(Results, 1f), tri);
        // Vertical reaction arrow (up)
        g.DrawLine(pen, 12, 16, 12, 4);
        var head = new PointF[] { new(12, 2), new(9, 6), new(15, 6) };
        g.FillPolygon(brush, head);
        // Horizontal reaction arrow (right)
        g.DrawLine(P(Results, 1.5f), 14, 18, 21, 18);
        var headH = new PointF[] { new(22, 18), new(19, 16), new(19, 20) };
        g.FillPolygon(brush, headH);
        return bmp;
    }

    /// <summary>Hero: Node with displacement delta arrows.</summary>
    public static Bitmap NodeDisplacements()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 1.5f);
        using var brush = B(Results);
        // Original node position (hollow)
        g.DrawEllipse(P(Color.FromArgb(120, Results.R, Results.G, Results.B), 1f), 5, 5, 5, 5);
        // Displacement arrow
        using var dashPen = new Pen(Results, 1f) { DashStyle = DashStyle.Dot };
        g.DrawLine(dashPen, 8, 8, 16, 16);
        // Displaced node (filled)
        g.FillEllipse(brush, 14, 14, 5, 5);
        // Delta symbol "δ"
        // using var font = new Font("Symbol", 9f, FontStyle.Bold);
        // g.DrawString("d", font, brush, 14, 0);
        return bmp;
    }

    /// <summary>Hero: Shear/moment force diagram along a beam.</summary>
    public static Bitmap MemberForces()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 2f);
        using var beamPen = P(Structure, 1.5f);
        // Beam line
        g.DrawLine(beamPen, 2, 12, 22, 12);
        // Force diagram (filled area above/below beam)
        var diagram = new PointF[]
        {
            new(2, 12), new(6, 6), new(12, 4), new(18, 8), new(22, 12),
            new(22, 12), new(18, 16), new(12, 20), new(6, 16), new(2, 12)
        };
        g.FillPolygon(B(Color.FromArgb(80, Results.R, Results.G, Results.B)), diagram);
        g.DrawCurve(pen, new PointF[] { new(2, 12), new(6, 6), new(12, 4), new(18, 8), new(22, 12) }, 0.4f);
        g.DrawCurve(pen, new PointF[] { new(2, 12), new(6, 16), new(12, 20), new(18, 16), new(22, 12) }, 0.4f);
        // End nodes
        using var nodeBrush = B(Structure);
        g.FillEllipse(nodeBrush, 1, 10, 3, 3);
        g.FillEllipse(nodeBrush, 20, 10, 3, 3);
        return bmp;
    }

    /// <summary>Hero: Deflected beam (dashed original, curved displaced).</summary>
    public static Bitmap MemberDisplacements()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 2f);
        // Original beam (dashed)
        using var dashPen = new Pen(Color.FromArgb(100, Results.R, Results.G, Results.B), 1f)
            { DashStyle = DashStyle.Dash };
        g.DrawLine(dashPen, 3, 8, 21, 8);
        // Deflected shape (cubic curve below)
        var deflected = new PointF[]
        {
            new(3, 8), new(8, 14), new(14, 18), new(21, 8)
        };
        g.DrawCurve(pen, deflected, 0.5f);
        // Fill under curve
        var fill = new PointF[]
        {
            new(3, 8), new(8, 14), new(14, 18), new(21, 8), new(21, 8), new(3, 8)
        };
        g.FillClosedCurve(B(Color.FromArgb(40, Results.R, Results.G, Results.B)), fill, FillMode.Winding, 0.3f);
        // Support indicators
        using var nodeBrush = B(Results);
        g.FillEllipse(nodeBrush, 1, 6, 4, 4);
        g.FillEllipse(nodeBrush, 19, 6, 4, 4);
        return bmp;
    }

    /// <summary>Hero: Buckling mode shape (S-curve column).</summary>
    public static Bitmap BucklingResults()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 2f);
        // Original column (dashed vertical)
        using var dashPen = new Pen(Color.FromArgb(100, Results.R, Results.G, Results.B), 1f)
            { DashStyle = DashStyle.Dash };
        g.DrawLine(dashPen, 12, 2, 12, 22);
        // Buckled mode shape (S-curve for mode 2)
        var points = new PointF[14];
        for (var i = 0; i < 14; i++)
        {
            var t = i / 13f;
            var y = 2 + t * 20;
            var x = 12 + (float)(System.Math.Sin(t * 2 * System.Math.PI) * 5);
            points[i] = new PointF(x, y);
        }
        g.DrawCurve(pen, points, 0.5f);
        using var brush = B(Results);
        // Lambda "λ" symbol
        // using var font = new Font("Arial", 7f, FontStyle.Bold);
        // g.DrawString("λ", font, brush, 17, 1);
        // End supports
        g.FillEllipse(brush, 10, 0, 4, 4);
        g.FillEllipse(brush, 10, 20, 4, 4);
        return bmp;
    }

    /// <summary>Hero: Dynamic frequency — multi-mode sine waves with frequency label.</summary>
    public static Bitmap DynamicFrequencyResults()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 2f);
        // Mode 1 — single sine wave
        var points1 = new PointF[16];
        for (var i = 0; i < 16; i++)
        {
            var t = i / 15f;
            var x = 2 + t * 20;
            var y = 8 + (float)(System.Math.Sin(t * System.Math.PI) * 6);
            points1[i] = new PointF(x, y);
        }
        g.DrawCurve(pen, points1, 0.5f);
        // Mode 2 — double sine wave (lighter)
        using var pen2 = P(Color.FromArgb(120, Results.R, Results.G, Results.B), 1.5f);
        var points2 = new PointF[16];
        for (var i = 0; i < 16; i++)
        {
            var t = i / 15f;
            var x = 2 + t * 20;
            var y = 16 + (float)(System.Math.Sin(t * 2 * System.Math.PI) * 4);
            points2[i] = new PointF(x, y);
        }
        g.DrawCurve(pen2, points2, 0.5f);
        // "f" frequency label
        // using var font = new Font("Arial", 7f, FontStyle.Italic);
        // using var brush = B(Results);
        // g.DrawString("f", font, brush, 18, 0);
        return bmp;
    }

    /// <summary>Lumped mass: kettlebell/weight shape at a node.</summary>
    public static Bitmap LumpedMassLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Handle (arc at top)
        g.DrawArc(pen, 7, 1, 10, 10, 180, 180);
        // Neck (narrow section)
        g.FillRectangle(brush, 9, 8, 6, 4);
        // Weight body (trapezoid)
        var body = new PointF[] { new(5, 12), new(19, 12), new(21, 22), new(3, 22) };
        g.FillPolygon(brush, body);
        g.DrawPolygon(pen, body);
        return bmp;
    }

    /// <summary>Prescribed displacement: arrow with delta symbol.</summary>
    public static Bitmap PrescribedDisplacement()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Horizontal arrow
        g.DrawLine(pen, 4, 12, 18, 12);
        // Arrow head
        var head = new PointF[] { new(20, 12), new(16, 8), new(16, 16) };
        g.FillPolygon(brush, head);
        // Delta "δ" hint — small dot at start
        g.FillEllipse(brush, 2, 10, 4, 4);
        return bmp;
    }

    /// <summary>Concentrated load: single downward arrow at midspan of a beam.</summary>
    public static Bitmap ConcentratedLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var beamPen = P(Structure, 1.5f);
        using var brush = B(Loads);
        // Beam line
        g.DrawLine(beamPen, 2, 20, 22, 20);
        // Single arrow at midspan
        g.DrawLine(pen, 12, 2, 12, 16);
        var head = new PointF[] { new(12, 19), new(9, 14), new(15, 14) };
        g.FillPolygon(brush, head);
        return bmp;
    }

    /// <summary>Prestress load: opposing horizontal arrows along a beam.</summary>
    public static Bitmap PrestressLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var beamPen = P(Structure, 1.5f);
        using var brush = B(Loads);
        // Beam line
        g.DrawLine(beamPen, 2, 12, 22, 12);
        // Left inward arrow
        g.DrawLine(pen, 2, 12, 9, 12);
        var headL = new PointF[] { new(10, 12), new(6, 9), new(6, 15) };
        g.FillPolygon(brush, headL);
        // Right inward arrow
        g.DrawLine(pen, 22, 12, 15, 12);
        var headR = new PointF[] { new(14, 12), new(18, 9), new(18, 15) };
        g.FillPolygon(brush, headR);
        return bmp;
    }

    /// <summary>Node constraint: two dots connected by a rigid link line.</summary>
    public static Bitmap NodeConstraint()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2f);
        using var brush = B(Structure);
        // Master node (filled)
        g.FillEllipse(brush, 2, 9, 6, 6);
        // Slave node (open)
        g.DrawEllipse(pen, 16, 9, 6, 6);
        // Link line
        g.DrawLine(pen, 8, 12, 16, 12);
        return bmp;
    }

    /// <summary>Member offset: beam with offset indicator.</summary>
    public static Bitmap MemberOffset()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2f);
        using var lightPen = P(Color.FromArgb(120, Structure.R, Structure.G, Structure.B), 2f);
        using var dashPen = P(Color.FromArgb(120, Structure.R, Structure.G, Structure.B), 1f);
        dashPen.DashStyle = DashStyle.Dash;
        // Original beam (dashed)
        g.DrawLine(lightPen, 2, 18, 22, 18);
        // Offset beam (solid, shifted up)
        g.DrawLine(pen, 2, 8, 22, 8);
        // Offset indicators (vertical lines at ends)
        g.DrawLine(dashPen, 4, 8, 4, 18);
        g.DrawLine(dashPen, 20, 8, 20, 18);
        return bmp;
    }

    /// <summary>Plate element: filled quadrilateral.</summary>
    public static Bitmap PlateElement()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 1.5f);
        using var brush = new SolidBrush(Color.FromArgb(60, Structure.R, Structure.G, Structure.B));
        // Filled quad shape
        var pts = new PointF[]
        {
            new(4, 6), new(20, 4), new(22, 18), new(2, 20)
        };
        g.FillPolygon(brush, pts);
        g.DrawPolygon(pen, pts);
        return bmp;
    }

    /// <summary>Plate pressure load: plate with downward arrows.</summary>
    public static Bitmap PlatePressureLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var platePen = P(Structure, 1f);
        using var loadPen = P(Loads, 1.5f);
        using var loadBrush = B(Loads);
        // Plate outline
        g.DrawLine(platePen, 2, 18, 22, 18);
        g.DrawLine(platePen, 2, 18, 4, 22);
        g.DrawLine(platePen, 22, 18, 20, 22);
        // Pressure arrows
        for (var x = 6; x <= 18; x += 6)
        {
            g.DrawLine(loadPen, x, 4, x, 15);
            var head = new PointF[] { new(x, 17), new(x - 2, 13), new(x + 2, 13) };
            g.FillPolygon(loadBrush, head);
        }
        return bmp;
    }

    /// <summary>Thermal load: thermometer symbol.</summary>
    /// <summary>Thermal load: thermometer icon.</summary>
    public static Bitmap ThermalLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Thermometer stem
        g.DrawLine(pen, 12, 3, 12, 16);
        // Thermometer bulb
        g.FillEllipse(brush, 8, 15, 8, 8);
        // Temperature marks
        g.DrawLine(pen, 14, 6, 17, 6);
        g.DrawLine(pen, 14, 10, 17, 10);
        return bmp;
    }

    /// <summary>Moving load scenario: simplified vehicle silhouette on a beam.</summary>
    public static Bitmap MovingLoadScenario()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var beamPen = P(Structure, 1.5f);
        using var brush = B(Loads);
        // Beam / travel path along the bottom
        g.DrawLine(beamPen, 2, 20, 22, 20);
        // Vehicle body (rectangle) sitting on the beam
        var body = new Rectangle(5, 9, 14, 7);
        g.FillRectangle(brush, body);
        // Cab notch (drawn as a triangle back on top-left)
        var cab = new PointF[] { new(5, 9), new(11, 5), new(13, 9) };
        g.FillPolygon(brush, cab);
        // Two wheels
        g.FillEllipse(brush, 6, 16, 4, 4);
        g.FillEllipse(brush, 14, 16, 4, 4);
        // Motion arrow ahead of the vehicle
        var head = new PointF[] { new(23, 12), new(19, 10), new(19, 14) };
        g.FillPolygon(brush, head);
        return bmp;
    }

    /// <summary>Moving load vehicle: truck body with two wheels + wheel-load markers.</summary>
    public static Bitmap MovingLoadVehicle()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Loads);
        using var accentPen = P(Loads, 1.5f);
        // Vehicle body
        var body = new Rectangle(3, 8, 18, 8);
        g.FillRectangle(brush, body);
        // Cab
        var cab = new PointF[] { new(3, 8), new(10, 3), new(14, 8) };
        g.FillPolygon(brush, cab);
        // Two wheels
        g.FillEllipse(brush, 4, 16, 5, 5);
        g.FillEllipse(brush, 15, 16, 5, 5);
        // Wheel-load force arrows (small vertical ticks above the wheels)
        g.DrawLine(accentPen, 6, 1, 6, 6);
        g.DrawLine(accentPen, 17, 1, 17, 6);
        return bmp;
    }

    /// <summary>Moving load pressure: rectangular footprint with pressure arrows.</summary>
    public static Bitmap MovingLoadPressure()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Loads);
        using var pen = P(Loads, 2f);
        using var outlinePen = P(Loads, 1.5f);
        // Rectangular pressure footprint (outline only)
        g.DrawRectangle(outlinePen, 3, 12, 18, 8);
        // Three downward pressure arrows on top of the footprint
        for (var x = 6; x <= 18; x += 6)
        {
            g.DrawLine(pen, x, 2, x, 10);
            var head = new PointF[] { new(x, 12), new(x - 2, 8), new(x + 2, 8) };
            g.FillPolygon(brush, head);
        }
        return bmp;
    }

    /// <summary>Moving load travel path: polyline path with station dots.</summary>
    public static Bitmap MovingLoadTravelPath()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Polyline path — four segments zig-zagging across the canvas
        var pts = new PointF[]
        {
            new(3, 18), new(8, 6), new(13, 14), new(18, 5), new(22, 12)
        };
        for (var i = 0; i < pts.Length - 1; i++)
            g.DrawLine(pen, pts[i], pts[i + 1]);
        // Station dots at each vertex
        foreach (var p in pts)
            g.FillEllipse(brush, p.X - 2, p.Y - 2, 4, 4);
        return bmp;
    }

    /// <summary>Moving load: small vehicle glyph riding on top of a path arrow.</summary>
    public static Bitmap MovingLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // Travel-path arrow along the bottom
        g.DrawLine(pen, 3, 20, 20, 20);
        var head = new PointF[] { new(23, 20), new(19, 17), new(19, 23) };
        g.FillPolygon(brush, head);
        // Vehicle body riding on the path
        g.FillRectangle(brush, 6, 9, 12, 6);
        var cab = new PointF[] { new(6, 9), new(11, 5), new(13, 9) };
        g.FillPolygon(brush, cab);
        // Two small wheels
        g.FillEllipse(brush, 7, 15, 3, 3);
        g.FillEllipse(brush, 14, 15, 3, 3);
        return bmp;
    }

    /// <summary>Moving load settings: cog wheel on the Loads-colour background.</summary>
    public static Bitmap MovingLoadSettings()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Cog teeth — eight rectangular teeth around a central hub
        var cx = 12f;
        var cy = 12f;
        var outerR = 10f;
        var innerR = 7f;
        var teeth = 8;
        for (var i = 0; i < teeth; i++)
        {
            var a1 = i * 2 * Math.PI / teeth - Math.PI / teeth * 0.35;
            var a2 = i * 2 * Math.PI / teeth + Math.PI / teeth * 0.35;
            var pts = new PointF[]
            {
                new((float)(cx + innerR * Math.Cos(a1)), (float)(cy + innerR * Math.Sin(a1))),
                new((float)(cx + outerR * Math.Cos(a1)), (float)(cy + outerR * Math.Sin(a1))),
                new((float)(cx + outerR * Math.Cos(a2)), (float)(cy + outerR * Math.Sin(a2))),
                new((float)(cx + innerR * Math.Cos(a2)), (float)(cy + innerR * Math.Sin(a2)))
            };
            g.FillPolygon(brush, pts);
        }
        // Filled hub with a small centre hole for contrast
        g.FillEllipse(brush, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
        g.FillEllipse(new SolidBrush(Color.Transparent), cx - 2.5f, cy - 2.5f, 5, 5);
        g.DrawEllipse(pen, cx - 2.5f, cy - 2.5f, 5, 5);
        return bmp;
    }

    /// <summary>Generate Moving Loads: play triangle above a moving-load motif.</summary>
    public static Bitmap GenerateMovingLoads()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Loads);
        using var pen = P(Loads, 2f);
        // Play triangle at the top-left
        var play = new PointF[] { new(3, 3), new(3, 13), new(11, 8) };
        g.FillPolygon(brush, play);
        // Path arrow along the bottom
        g.DrawLine(pen, 3, 21, 20, 21);
        var head = new PointF[] { new(23, 21), new(19, 18), new(19, 24) };
        g.FillPolygon(brush, head);
        // Small vehicle body sitting on the path
        g.FillRectangle(brush, 13, 14, 8, 5);
        g.FillEllipse(brush, 14, 18, 3, 3);
        g.FillEllipse(brush, 18, 18, 3, 3);
        return bmp;
    }



    /// <summary>Plate forces: plate with force arrows.</summary>
    public static Bitmap PlateForces()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 1.5f);
        using var brush = B(Results);
        var pts = new PointF[] { new(4, 6), new(20, 4), new(22, 18), new(2, 20) };
        g.DrawPolygon(pen, pts);
        g.DrawLine(pen, 12, 8, 12, 16);
        var head = new PointF[] { new(12, 18), new(10, 14), new(14, 14) };
        g.FillPolygon(brush, head);
        return bmp;
    }

    /// <summary>Steel member check — I-beam profile with a check mark.</summary>
    public static Bitmap SteelMemberCheck()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Results, 1.5f);
        using var brush = B(Results);
        // I-beam profile (top flange, web, bottom flange)
        g.DrawLine(pen, 4, 5, 14, 5);   // top flange
        g.DrawLine(pen, 9, 5, 9, 19);   // web
        g.DrawLine(pen, 4, 19, 14, 19); // bottom flange
        // Check mark (green traffic-light hint)
        using var checkPen = new Pen(Color.FromArgb(76, 175, 80), 2f);
        g.DrawLine(checkPen, 15, 13, 18, 17);
        g.DrawLine(checkPen, 18, 17, 22, 9);
        return bmp;
    }
}
