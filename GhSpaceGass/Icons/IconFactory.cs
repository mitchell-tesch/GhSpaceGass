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
    public static readonly Color Connection = ColorTranslator.FromHtml("#607D8B");
    public static readonly Color Properties = ColorTranslator.FromHtml("#009688");
    public static readonly Color Structure = ColorTranslator.FromHtml("#1976D2");
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
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Structure, 2f);
        using var brush = B(Structure);
        // Draw a simple structural frame: two columns + beam
        g.DrawLine(pen, 4, 20, 4, 6);   // left column
        g.DrawLine(pen, 20, 20, 20, 6); // right column
        g.DrawLine(pen, 4, 6, 20, 6);   // beam
        // Node dots
        using var nodeBrush = B(Connection);
        g.FillEllipse(nodeBrush, 2, 4, 4, 4);
        g.FillEllipse(nodeBrush, 18, 4, 4, 4);
        // Ground hatching
        g.DrawLine(P(Connection, 1f), 2, 21, 6, 21);
        g.DrawLine(P(Connection, 1f), 18, 21, 22, 21);
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
        g.FillRectangle(brush, 9, 6, 6, 12);   // web
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
    // CASES / LOADS PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Load case: folder with "LC" label.</summary>
    public static Bitmap LoadCase()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Folder tab
        g.FillRectangle(brush, 3, 5, 6, 3);
        // Folder body
        g.DrawRectangle(pen, 3, 7, 18, 13);
        g.FillRectangle(B(Color.FromArgb(60, Loads.R, Loads.G, Loads.B)), 4, 8, 17, 12);
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
        using var pen = P(Loads, 1.5f);
        using var brush = B(Loads);
        // Tag shape
        var tag = new PointF[]
        {
            new(6, 4), new(20, 4), new(20, 20), new(6, 20), new(2, 12)
        };
        g.DrawPolygon(pen, tag);
        g.FillPolygon(B(Color.FromArgb(60, Loads.R, Loads.G, Loads.B)), tag);
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
        using var pen = P(Loads, 2.5f);
        // Sigma (Σ) drawn as lines
        g.DrawLine(pen, 18, 3, 6, 3);   // top bar
        g.DrawLine(pen, 6, 3, 12, 12);  // upper diagonal
        g.DrawLine(pen, 12, 12, 6, 21); // lower diagonal
        g.DrawLine(pen, 6, 21, 18, 21); // bottom bar
        return bmp;
    }

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

    /// <summary>Self-weight: gravity "g" with downward arrow.</summary>
    public static Bitmap SelfWeight()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var pen = P(Loads, 2f);
        using var brush = B(Loads);
        // "g" letter
        using var font = new Font("Arial", 11f, FontStyle.Bold);
        g.DrawString("g", font, brush, 2, 1);
        // Downward arrow
        g.DrawLine(pen, 17, 6, 17, 18);
        var head = new PointF[] { new(17, 22), new(14, 17), new(20, 17) };
        g.FillPolygon(brush, head);
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

    /// <summary>Lumped mass: heavy dot with mass "m" label at a node.</summary>
    public static Bitmap LumpedMassLoad()
    {
        var bmp = Create();
        using var g = Setup(bmp);
        using var brush = B(Loads);
        // Large mass dot
        g.FillEllipse(brush, 5, 5, 14, 14);
        // "m" label
        using var font = new Font("Arial", 8f, FontStyle.Bold);
        using var textBrush = B(Color.White);
        g.DrawString("m", font, textBrush, 6, 5);
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
}


