# CONTEXT — GhSpaceGass

Shared vocabulary and domain definitions for the GhSpaceGass Grasshopper plug-in.

---

## Product

**GhSpaceGass** — a Grasshopper 8 plug-in that provides a full round-trip workflow between Grasshopper and SpaceGass via the SpaceGass REST API.

## Domain Terms

### SpaceGass API Service
The local REST API server (`SpaceGassApi.exe`) that provides programmatic access to SpaceGass structural analysis. Runs on `http://localhost:<port>` (default port 34560). Requires a licensed SpaceGass installation.

**Port configuration**: The `--urls http://localhost:<port>` CLI flag is the working method to set a custom port. The `--port` flag documented by SpaceGass does not work in the current beta (v14.50.134-beta1). The plug-in uses `--urls` when launching the service.

### Job
A single SpaceGass project file (`.sg`). The API supports one open job at a time. A job contains structure, loads, analysis settings, and results.

### Singleton Client
A single shared `SpaceGassApiClient` instance that lives for the lifetime of the Grasshopper session. All components access SpaceGass through this instance. No explicit connection wire on the canvas.

### Node
A point in 3D space within a SpaceGass structural model, identified by an integer ID assigned by SpaceGass. Nodes are the endpoints of members and the locations where restraints and point loads are applied.

### Member
A straight structural element connecting two nodes (Node A → Node B). Has a type (beam, truss, cable, etc.), a section, a material, an optional orientation direction, and optional end releases. Polyline geometry produces multiple members sharing intermediate nodes.

### Member Release
A per-end release condition applied to a member, controlling which of the 6 degrees of freedom (Fx, Fy, Fz, Mx, My, Mz) transfer forces through the connection at that end. Each DOF can be Fixed (`F` — force transfers), Released (`R` — no force transfer), or Spring (`S` — partial release with a stiffness value). Represented as a 6-character code string with optional per-DOF stiffness. The `Create Release` builder component uses fixity booleans (true = Fixed, false = Released — same convention as Create Restraint); when a spring stiffness is provided for a DOF, the boolean is ignored and the code is set to `S`. An all-fixed release (`FFFFFF` with no stiffness) is a no-op and is skipped during assembly to optimise API calls. Created by the `Create Release` builder component and assigned to members via the `Create Member` component's optional Release A / Release B inputs (ADR-0013).

### Member Offset
A rigid offset applied at each end of a member, shifting the structural member axis away from the node location. Has X/Y/Z offset values at both end A and end B, in local or global axes (defaults to local). Used to model eccentric connections (e.g., beam connected to column face rather than centreline). Created by the `Create Member Offset` builder component and assigned to members via the `Create Member` component's optional Offset input. Pushed via `Job.Structure.MemberOffsets.Bulk.PostAsync` after members are created. All-zero offsets are skipped to optimise API calls.

### Section
A cross-section profile assigned to members. Can be sourced from the SpaceGass library (by name) or user-defined (with explicit geometric properties: area, Ix, Iy, etc.).

### Material
A material definition assigned to members. Can be sourced from the SpaceGass library (by name) or user-defined (with explicit properties: E, G, density, fy, etc.).

### Restraint
A boundary condition applied at a node, defining which of the 6 degrees of freedom (Fx, Fy, Fz, Mx, My, Mz) are fixed, free, or spring-supported.

### Node Constraint
A master-slave rigid link between two nodes, constraining specified degrees of freedom of the slave node to follow the master node. Has a 6-character constraint code (F=Constrained, R=Free) for the 6 DOFs (TX, TY, TZ, RX, RY, RZ). Default `FFFFFF` = fully rigid link. Supports global or inclined axes (with optional direction vector for inclined). Created by the `Create Node Constraint` builder component and pushed via `Job.Structure.NodeConstraints.Bulk.PostAsync`. Assembled after restraints, before loads.

### Plate
A 3 or 4 node shell/plate finite element. Triangular plates have 3 corner nodes; quadrilateral plates have 4. Has a material (same Material Goo as members), actual thickness, optional bending/membrane/shear thickness overrides, an offset from the nodal plane, and a plate theory (Kirchoff thin plate or Mindlin thick plate). Created by the `Create Plate` builder component from Mesh geometry — each mesh face becomes one plate element. Pushed via `Job.Structure.Plates.Bulk.PostAsync`. Plate corner nodes are deduplicated with member endpoints in the shared node pool. Materials are deduplicated across both members and plates.

### Load Case
A named container for a set of loads (e.g., "Dead Load", "Live Load"). Identified by an integer ID assigned by SpaceGass. The API supports multiple types: Primary and Combination. Primary load cases hold direct loads; Combination load cases reference other load cases with scale factors.

### Combination Load Case
A load case that combines other load cases (primary or combination) with scale factors (e.g., 1.2×Dead + 1.5×Live, or 1.0×ULS + 0.7×Wind_Combo). Created via the `Create Combination Load Case` builder component, which accepts both primary Load Cases and other Combination Load Cases as constituents. The API accepts a title, an optional note, and a list of constituent (load case ID, factor) pairs. Only linear combination is supported. Assembled after primary load cases in dependency order; when combinations reference other combinations, they are created in topological order (dependencies first).

### Load Category
A classification label applied to loads to indicate their source or purpose (e.g., "Dead", "Live", "Wind"). Used for filtering and code-compliance grouping. Optional — node loads may reference a category but are not required to. Created via the API and referenced by ID on individual load entries.

### Node Load
A concentrated force and/or moment applied at a specific node, within a specific load case. Defined in global axes.

### Member Distributed Load
A distributed force and/or moment applied along the length of a member, within a specific load case. Defaults to local member axes; optionally applied in global axes. Forces (Fx, Fy, Fz) and moments (Mx, My, Mz) can be specified at start and end of the loaded region independently — set different values for trapezoidal loading. Forces and moments are separate API types: forces pushed via `Job.Loads.MemberDistributedLoads.Bulk.PostAsync`, moments pushed via `Job.Loads.MemberDistributedMoments.Bulk.PostAsync`. A single `Create Member Distributed Load` component handles both; the assembler splits them into the correct API calls.

### Member Concentrated Load
A concentrated force and/or moment applied at a specific position along a member, within a specific load case. Has force components (Fx, Fy, Fz), moment components (Mx, My, Mz), a position along the member, position units (Percent or Actual), and axis system (Local or Global — defaults to Local, ADR-0011). Created by the `Create Member Concentrated Load` builder component and pushed via `Job.Loads.MemberConcentratedLoads.Bulk.PostAsync`.

### Member Prestress Load
An axial prestress force applied to a member, within a specific load case. Used to model post-tensioning or pre-compression in structural members. Has a single `Prestress` value representing the axial force. Created by the `Create Member Prestress Load` builder component and pushed via `Job.Loads.MemberPrestressLoads.Bulk.PostAsync`.

### Plate Pressure Load
A uniform pressure applied to a plate element, within a specific load case. Has pressure components (Px, Py, Pz) in local or global axes (defaults to local, ADR-0011). References a plate element via the Plate Goo input — corner nodes are used to resolve the plate ID during assembly. Created by the `Create Plate Pressure Load` builder component and pushed via `Job.Loads.PlatePressureLoads.Bulk.PostAsync`.

### Thermal Load
A temperature change and/or thermal gradient applied to a member or plate element, within a specific load case. Has a uniform temperature change, Y thermal gradient, and Z thermal gradient. Applies to both members and plates — the same API type (`ThermalLoadCreate`) with an `ElementType` discriminator (Member or Plate). Created by the `Create Thermal Load` builder component, which auto-detects the element type from the input (Line → member, Plate Goo → plate). Pushed via `Job.Loads.ThermalLoads.Bulk.PostAsync`.

### Self-Weight Load
An automatic gravity load applied to all members based on their section area, material density, and a gravity factor, within a specific load case.

### Lumped Mass Load
A concentrated mass applied at a specific node, within a specific load case. Used for dynamic frequency analysis to represent non-structural mass (e.g., equipment, floor mass) that doesn't come from member self-weight. Has translational mass components (Tmx, Tmy, Tmz) and rotational mass components (Rmx, Rmy, Rmz). Created by the `Create Lumped Mass Load` builder component and pushed via `Job.Loads.LumpedMassLoads.Bulk.PostAsync`.

### Prescribed Displacement
An imposed displacement and/or rotation applied at a specific node, within a specific load case. Used to model support settlements or forced displacements (e.g., differential settlement of a foundation). Has translation components (Tx, Ty, Tz) and rotation components (Rx, Ry, Rz). Created by the `Create Prescribed Displacement` builder component and pushed via `Job.Loads.NodeDisplacements.Bulk.PostAsync`.

### Assemble Model
The compile step that collects all in-memory Goo objects (nodes, members, sections, materials, restraints, loads), deduplicates coincident geometry, resolves geometry-to-ID mappings, and pushes the model to SpaceGass via bulk API calls in dependency order. Supports two modes: Rebuild (default — clears existing data first) and Append (adds to existing model content without clearing).

### Disassemble Model
The reverse of Assemble Model: reads an existing SpaceGass model from the open job via List GET endpoints and outputs the structural geometry (Points, Lines, Meshes) plus a populated SgModel for downstream chaining to Run Analysis and Results. Does not modify the job — read-only.

### SgModel (Model Object)
The Goo type output by both Assemble Model and Disassemble Model. Encapsulates the ID ↔ geometry mappings and job context. Flows downstream to Analysis and Results components.

### Builder Component
A synchronous Grasshopper component that constructs an in-memory Goo object (e.g., SgNode, SgMember) without making any API calls. Pure data construction.

### Async Component
A Grasshopper component that performs API calls on a background thread using the GrasshopperAsyncComponent pattern. The canvas remains responsive during execution. Used by all components that make API calls — Connect, Assemble Model, Disassemble Model, Save Job, Job Info, Run Analysis, all Results components, and all Get query components. Runtime messages are deferred to the SetData phase (UI thread) via `WorkerInstance.AddRuntimeMessage` to survive the Grasshopper re-solve message clear.

### Clear & Rebuild
The default strategy used by Assemble Model: clear all existing model data in the open job (via `Job.Data.Delete(force=true)`) and push the entire model from scratch. The Grasshopper graph is the single source of truth (see ADR-0001). An optional Append mode (Mode input = 1) skips the clear, adding data alongside existing job content — useful for augmenting a model opened via Connect.

### Orphan Point
A Point3d referenced by a restraint or load that does not coincide (within tolerance) with any member endpoint. Assemble Model creates a standalone node for it and emits a warning (see ADR-0002).

### Coincidence Tolerance
The distance threshold used to determine whether two points are the same node. Defaults to the Rhino document tolerance. Configurable.

### Deduplication
The process by which Assemble Model identifies coincident points (within tolerance) and merges them into a single node, ensuring shared connectivity between members, restraints, and loads.

### Analysis Settings
A configuration object that controls analysis behaviour for non-linear static and buckling analyses. Created by the `Create Analysis Settings` builder component and fed as an optional input to `Run Analysis`. When omitted, SpaceGass defaults apply. Settings include number of buckling modes and other solver parameters. Applied to the job immediately before each analysis run (ADR-0014).

### Intermediate Member Forces
Forces (Fx, Fy, Fz, Mx, My, Mz) at stations along the length of a member, not just at the end nodes. Queried via the API's `List Member Intermediate Forces` endpoint. Output as a two-level data tree `{load_case; member}` with station results ordered by position (ADR-0015).

### Intermediate Member Displacements
Translations (TxGlobal, TyGlobal, TzGlobal, TxLocal, TyLocal, TzLocal) at stations along the length of a member. Queried via the API's `List Member Intermediate Displacements` endpoint, which provides global and local translations — no rotations. Output as a two-level data tree `{load_case; member}` with station results ordered by position (ADR-0015).

### Buckling Load Factor
A scalar multiplier indicating the load level at which elastic buckling occurs for a given load case and mode. Returned per load case and mode from the API's `List Buckling Load Factors` endpoint. Output in a data tree `{load_case; mode}`.

### Effective Length
The equivalent length used for buckling capacity calculations, returned per member, per load case, per mode, per axis (Ley, Lez). Also includes the critical buckling load (Pcr) and member length. Queried via the API's `List Buckling Effective Lengths` endpoint. Output in a data tree `{load_case; mode}` with member results as list items within each branch.

### Plate Element Forces
Average in-plane forces (Fx, Fy, Fxy), bending moments (Mx, My, Mxy, MxTop, MxBtm, MyTop, MyBtm), and transverse shears (Vxz, Vyz) for a plate element at a specific load case. One result per plate per load case. Queried via `List Plate Element Forces` endpoint. Output in a data tree `{load_case}` with plate results as list items.

### Plate Nodal Forces
Per-node forces (Fx, Fy, Fz) and moments (Mx, My, Mz) at each corner node of a plate element for a specific load case. Multiple results per plate (one per corner node). Queried via `List Plate Nodal Forces` endpoint. Output in a data tree `{load_case; plate}` with node results as list items within each branch.

---

## Architecture References

Detailed decisions are recorded in `adr/0001–0015`. Key choices:

- **Round-trip workflow**: GH → SpaceGass (push model) → run analysis → GH (pull results)
- **Grasshopper tab**: `SpaceGass` (no space)
- **Panel layout**: `Connection` (Connect, Job Info, Save Job), `Properties` (Create Section, Create Material, Get Sections, Get Materials), `Structure` (Create Member, Create Restraint, Create Release, Create Member Offset, Create Node Constraint, Create Plate), `Cases` (Create Load Case, Create Combination Load Case, Create Load Category, Get Load Cases), `Loads` (Create Node Load, Create Member Distributed Load, Create Member Concentrated Load, Create Member Prestress Load, Create Self-Weight Load, Create Lumped Mass Load, Create Prescribed Displacement, Create Plate Pressure Load, Create Thermal Load, Get Self-Weight Loads, Get Node Loads, Get Member Loads, Get Plate Loads), `Model` (Assemble Model, Disassemble Model), `Analysis` (Create Analysis Settings, Run Analysis), `Results` (Get Node Reactions, Get Node Displacements, Get Member Forces, Get Member Displacements, Get Buckling Results, Get Dynamic Frequency Results, Get Plate Forces)
- **Library vs Custom naming**: Unified components — `Create Section` and `Create Material` handle both library (when Library input is connected) and custom (when Library is omitted) modes. Same Goo types for both.
- **Fine-grained components**: one component per concept
- **Deferred push**: builder components produce Goo; Assemble Model compiles and sends
- **Server-assigned IDs**: no manual ID allocation; SpaceGass returns IDs on creation
- **Async via GrasshopperAsyncComponent**: source-included, not NuGet
- **Results**: all results output data trees with Load Cases, Node IDs, and Member IDs in matching tree structures for easy correlation. Static results use `{load_case}` for node results, `{load_case; member}` for member results, `{load_case}` for plate element forces, and `{load_case; plate}` for plate nodal forces (ADR-0008, ADR-0015). Buckling and dynamic frequency results use `{load_case; mode}`. All results components include Load Cases filter input supporting both primary and combination load case names. Viewport preview with auto-scale (ADR-0009): results components draw preview geometry (arrows, displaced shapes, force diagrams) in the Rhino viewport; auto-scale by default, optional Scale input override. Forces and moments auto-scale independently (different units). Show Values input toggles numeric labels.
- **Error handling**: all async components use `ModelAssembler.FormatApiError` to extract meaningful messages from `ErrorResponse` (Title, Detail, HTTP status code) and `ApiException`. Errors are surfaced as both a component-level error badge (via `AddRuntimeMessage`) and in the Status output.
- **Member releases**: separate builder component (ADR-0013)
- **Analysis settings**: builder Goo fed into Run Analysis (ADR-0014)

---

## Slice Board

- [x] **Slice 1+2** — Connect → service health check + job lifecycle

**Delivered (Slices 1+2):** `SpaceGass Connect` component (`SpaceGass > Connection` panel).
Inputs: `Connect?` (bool, default false — prevents auto-start), `Port` (int, default 34560), `File Path` (optional `.sg` path), `Show Console` (bool, default true — shows the SpaceGass API console window), `Force Option` (value list: None=0 / Open Previous Saved=1 / Open Unsaved Most Recent=2, default None — force open when file is locked or has unsaved changes), `Install Path` (optional custom path to SpaceGassApi.exe).
Outputs: `Connected?` (bool), `URL` (string — the API service base URL), `Version` (string — SpaceGass version from the service), `Status` (string with connection and job info).
Behaviour: launches `SpaceGassApi.exe` via `--urls`, probes with `Service.Info.GetAsync()` (2s probe timeout for fast failure when nothing is listening), opens existing file or creates new job (temp file if no path given). Toggle `Connect? = false` to disconnect and kill the service (ADR-0007). Reuses an already-running service without launching (ADR-0004). After opening a file, checks `AccessMode` — warns if read-only, errors if no access. API errors surfaced via `FormatApiError` with error badge on the component. Core layer in `GhSpaceGass.Core` with 17 passing unit tests.

- [x] **Slice 3** — Create Member + Section + Material + Assemble Model → structure in SpaceGass

**Delivered (Slice 3):** Four components across three panels.
`Create Library Section` (`SpaceGass > Properties`): Inputs: Library, Name, + optional overrides (Mark, AreaFactor, IyFactor, IzFactor, TorsionFactor, Ay, Az, Transposed). Output: Section Goo.
`Create Library Material` (`SpaceGass > Properties`): Inputs: Library, Name. Output: Material Goo.
`Create Member` (`SpaceGass > Structure`): Inputs: Line, Section, Material, Type (value list: Beam/Truss/Cable/CompressionOnly/TensionOnly, default Beam — ADR-0012). Output: Member Goo.
`Assemble Model` (`SpaceGass > Model`, async): Inputs: Assemble? (bool, default false — prevents auto-run), Members (list), Tolerance (optional, default doc tolerance). Outputs: Model Goo (ID↔geometry maps), Status. Behaviour: clears job data (ADR-0001), deduplicates sections/materials by full key including overrides (ADR-0006), deduplicates nodes via O(n) spatial grid, pushes in dependency order (materials → sections → nodes → members), warns on duplicates and multiple instances (ADR-0005), validates bulk results, reports step-level API errors. Core: `ModelAssembler` in `GhSpaceGass.Core` with 35 passing unit tests total.

- [x] **Slice 4** — Create Restraint → Assemble Model applies restraints

**Delivered (Slice 4):** One new component + updated Assemble Model.
`Create Restraint` (`SpaceGass > Structure`): Inputs: Point (Point3d), Fx/Fy/Fz (bool, default true), Mx/My/Mz (bool, default false). Output: Restraint Goo. Builds a 6-character restraint code (F=Fixed, R=Released); validates each character is exactly F or R. Default: `FFFRRR` (pinned support). Pure data construction — no API calls.
`Assemble Model` — new optional Restraints input (list). Description updated to reflect all input types. After creating members, resolves each restraint point to a node ID via the spatial deduplication grid. Orphan points (ADR-0002) create standalone nodes with a warning. Warns when multiple restraints target the same node (consistent with ADR-0006 deduplicate+warn pattern). Pushes via `Job.Structure.NodeRestraints.Bulk.PostAsync`. Validates bulk result. Status output includes restraint count. `SgModelData.RestraintMap` maps point → restraint code. Dependency order: clear → materials → sections → nodes → members → restraints.
Core: `SgRestraintData` model, `ISpaceGassApi.CreateNodeRestraintsAsync`, extended `ModelAssembler`. 58 passing unit tests total (23 new).

- [x] **Slice 5** — Create Load Case + Create Load Category + Create Node Load → Assemble Model applies loads

**Delivered (Slice 5):** Three new builder components + updated Assemble Model.
`Create Load Case` (`SpaceGass > Loads`): Inputs: Name (string, required), Notes (string, optional). Output: Load Case Goo. Validates non-empty name.
`Create Load Category` (`SpaceGass > Loads`): Inputs: Name (string, required), Notes (string, optional). Output: Load Category Goo. Validates non-empty name. A classification label for loads (e.g. "Dead", "Live", "Wind") — optional tag on individual node loads.
`Create Node Load` (`SpaceGass > Loads`): Inputs: Point (Point3d), Load Case (Load Case Goo), Load Category (Load Category Goo, optional), Fx/Fy/Fz/Mx/My/Mz (number, default 0). Output: Node Load Goo. Always global axes (ADR-0011). Warns if all components are zero.
`Assemble Model` — new optional Node Loads input (list). After creating restraints, collects load cases from node loads, deduplicates by name (ADR-0006 pattern), creates via `Job.Loads.LoadCases.Bulk.PostAsync`. Collects load categories (if any), deduplicates by name, creates via `Job.Loads.LoadCategories.Bulk.PostAsync`. Resolves node load points to node IDs via spatial grid. Orphan load points (ADR-0002) create standalone nodes with warning. Creates node loads via `Job.Loads.NodeLoads.Bulk.PostAsync` with optional category ID. Validates bulk results. Status includes load case, category, and node load counts. Dependency order: clear → materials → sections → nodes → members → restraints → load cases → load categories → node loads.
Core: `SgLoadCaseData`, `SgLoadCategoryData`, `SgNodeLoadData` (with optional LoadCategory) models, `ISpaceGassApi.CreateLoadCasesAsync`, `ISpaceGassApi.CreateLoadCategoriesAsync`, `ISpaceGassApi.CreateNodeLoadsAsync`, extended `ModelAssembler`. `SgModelData.LoadCaseMap`, `SgModelData.LoadCategoryMap`, `SgModelData.NodeLoadCount`. 104 passing unit tests total (46 new).

- [x] **Slice 6** — Run Static Analysis

**Delivered (Slice 6):** One new async component + core analysis support.
`Run Analysis` (`SpaceGass > Analysis`, async): Inputs: Model (SgModel Goo, required), Run? (bool, default false — prevents auto-run). Outputs: Model (pass-through for downstream chaining), Success (bool), Status (string with elapsed time, run ID, warnings/errors). Behaviour: calls `Job.Analysis.Static.RunLinear.PostAsync` to submit analysis, then polls `Job.Analysis.Runs[runId].GetAsync()` at 500ms intervals until terminal status (Completed/Failed/Cancelled) — matching the SpaceGass API documentation pattern. Displays live progress (step/percentage/load case status) on the component message. Maps `AnalysisRunStatus.Completed` → `Success = true`; Failed/Cancelled → `Success = false` with error message. SpaceGass analysis warnings surfaced as GH warnings. API exceptions wrapped in `InvalidOperationException` with formatted message. All async components use `async Task DoWork` (not `.GetAwaiter().GetResult()`) for proper async execution and cancellation. Model Goo passed through to enable downstream chaining to Results components.
Core: `SgAnalysisResult` domain model (Succeeded, RunId, ElapsedTime, ErrorMessage, Warnings), `ISpaceGassApi.RunStaticAnalysisAsync`, `ISpaceGassApi.GetAnalysisRunAsync`, `SpaceGassApiWrapper` implementation, `SpaceGassSession.RunStaticAnalysisAsync` with connection guard, async polling, progress callback, and error formatting. 112 passing unit tests total (8 new).

- [x] **Slice 7** — Get Node Reactions (data only) — first results back

**Delivered (Slice 7):** One new async component + core results query support.
`Get Node Reactions` (`SpaceGass > Results`, async): Inputs: Model (SgModel Goo, required), Points (List<Point3d>, optional — filter to specific node locations), Load Cases (List<string>, optional — filter to specific load case names, supports both primary and combination). Outputs: Load Cases (DataTree — one name per branch), Nodes (DataTree — integer node IDs per branch), Points, Fx, Fy, Fz, Mx, My, Mz (all DataTree, branched by load case — ADR-0008, ordered by node ID within each branch). Behaviour: calls `Job.Query.Analysis.Static.NodeReactions.GetAsync()` with optional server-side filtering via `Nodes` and `LoadCases` query parameters. Filter names resolved to load case IDs via both `LoadCaseMap` and `CombinationLoadCaseMap`. Unmatched filter values emit warnings and are skipped. Empty results emit warning "No node reactions found". API exceptions wrapped with `ModelAssembler.FormatApiError`.
Core: `SgNodeReactionData` model, `SgNodeReactionsResult` container, `ISpaceGassApi.GetNodeReactionsAsync`, `SpaceGassApiWrapper` implementation, `SpaceGassSession.GetNodeReactionsAsync` with connection guard, filter resolution, and error formatting. 131 passing unit tests total (14 new).

- [x] **Slice 8** — Units + Headings inputs on Connect

**Delivered (Slice 8):** One new async component + core job info support + Connect enhancements.
`Job Info` (`SpaceGass > Connection`, async): Inputs: Refresh? (bool, default true), Heading (string, optional), Project Heading (string, optional), Designer Initials (string, optional), Notes (string, optional). Outputs: Heading, Project Heading, Designer Initials, Notes, Vertical Axis (display: "Y"/"Z"), Units (formatted with standard engineering abbreviations: mm, kN, kN·m, MPa, °C, kg, etc.), Status (full multiline job summary). Behaviour: calls `Job.Status.GetAsync()` which returns the full `JobStatus` including `Job.Headings`, `Job.Settings.VerticalAxis`, `Job.Units` (11 unit types), `Structure` counts, `Loads` counts, and `Analysis` result flags — all in one API call. If any heading inputs are provided, PATCHes headings via `Job.Headings.PatchAsync(JobHeadingsUpdate)` first, then re-queries full status. Separated from Connect to allow re-querying at any time without disrupting the connection. Units are read-only in the API (no PATCH/PUT). Display formatting maps raw enum names to standard engineering abbreviations via `SgJobInfo.DisplayUnit()`.
`SpaceGass Connect` — enhanced with two new outputs: `URL` (API service base URL) and `Version` (SpaceGass version from `ServiceInfo`).
Core: `SgJobInfo` domain model (headings, settings, 11 unit strings, state, structure/loads/analysis summaries, `FormatUnits()`, `FormatStatus()`, `DisplayUnit()`, `FormatVerticalAxis()` display helpers), `ISpaceGassApi.GetFullJobStatusAsync`, `ISpaceGassApi.UpdateHeadingsAsync`, `SpaceGassApiWrapper` implementation, `SpaceGassSession.GetJobInfoAsync` with connection guard and `MapJobStatus` mapping, `SpaceGassSession.UpdateHeadingsAsync` with partial update support, `SpaceGassSession.ServiceUrl` and `SpaceGassSession.SpaceGassVersion` properties. 179 passing unit tests total (48 new).

- [x] **Slice 9** — Create Member Distributed Load + Create Self-Weight Load → Assemble

**Delivered (Slice 9):** Two new builder components + updated Assemble Model.
`Create Member Distributed Load` (`SpaceGass > Loads`): Inputs: Member (Line — the member geometry), Load Case (required), Load Category (optional), Fx Start/Fy Start/Fz Start (number, default 0), Fx End/Fy End/Fz End (number, default 0 — set different from start for trapezoidal loads), Start Position (number, default 0), End Position (number, default 100), Position Units (value list: Actual/Percent, default Percent), Axes (value list: Local/Global, default Local — ADR-0011). Output: Member Distributed Load Goo. Warns if all force components are zero.
`Create Self-Weight Load` (`SpaceGass > Loads`): Inputs: Load Case (required), Load Category (optional), Acceleration X (number, default 0), Acceleration Y (number, default -9.81), Acceleration Z (number, default 0). Output: Self-Weight Load Goo. Direct axis inputs map 1:1 to the API. Warns if all accelerations are zero.
`Assemble Model` — two new optional inputs: Distributed Loads (list), Self-Weight Loads (list). Load case and load category collection now gathers from all three load sources (node loads, distributed loads, self-weight loads) and deduplicates together. Distributed loads resolve member geometry (start→end line) to member ID via MemberMap; unmatched members emit warning and skip. Self-weight loads pushed via `Job.Loads.SelfWeightLoads.Bulk.PostAsync`. Status includes distributed load and self-weight load counts. Dependency order: clear → materials → sections → nodes → members → restraints → load cases → load categories → node loads → member distributed loads → self-weight loads. **Updated in later session:** the three separate load inputs (Node Loads, Distributed Loads, Self-Weight Loads) were consolidated into a single unified `Loads` (L) generic input that accepts all load types. The component sorts items by Goo type internally.
Core: `SgMemberDistributedLoadData`, `SgSelfWeightLoadData` models, `ISpaceGassApi.CreateMemberDistributedLoadsAsync`, `ISpaceGassApi.CreateSelfWeightLoadsAsync`, `SpaceGassApiWrapper` implementations, extended `ModelAssembler` with `TryResolveCanonicalPoint` for safe member lookup, `SgModelData.MemberDistributedLoadCount`, `SgModelData.SelfWeightLoadCount`. 196 passing unit tests total (17 new).

- [x] **Slice 10** — Get Node Displacements + Get Member End Forces (data only)

**Delivered (Slice 10):** Two new async results components + core query support.
`Get Node Displacements` (`SpaceGass > Results`, async): Inputs: Model (SgModel Goo, required), Points (List<Point3d>, optional filter), Load Cases (List<string>, optional filter — supports both primary and combination). Outputs: Load Cases (DataTree — one name per branch), Nodes (DataTree — integer node IDs per branch), Points, Tx, Ty, Tz, Rx, Ry, Rz (all DataTree, branched by load case — ADR-0008, ordered by node ID). Identical pattern to Get Node Reactions. Calls `Job.Query.Analysis.Static.NodeDisplacements.GetAsync()` with optional `Nodes` and `LoadCases` query params. Filter names resolved via both `LoadCaseMap` and `CombinationLoadCaseMap`.
`Get Member End Forces` (`SpaceGass > Results`, async): Inputs: Model (SgModel Goo, required), Members (List<Line>, optional — filter by member geometry), Load Cases (List<string>, optional filter). Outputs: Lines (member geometry, 2 entries per member), Points (node location at each end), Fx, Fy, Fz, Mx, My, Mz (all DataTree, branched by load case, ordered by member ID then node ID), Load Cases (List<string>). The API returns `MemberEndForce` with per-end lists (typically 2 values per record); the domain model flattens these to individual `SgMemberEndForceData` records. Member filter resolves Line geometry to member IDs via MemberMap. Unmatched members/load cases warn and skip. **Note:** This component was later reworked in Slice 18 into `Get Member Forces` with End Forces / Intermediate mode support.
Core: `SgNodeDisplacementData`, `SgNodeDisplacementsResult`, `SgMemberEndForceData`, `SgMemberEndForcesResult` models, `ISpaceGassApi.GetNodeDisplacementsAsync`, `ISpaceGassApi.GetMemberEndForcesAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetNodeDisplacementsAsync` and `SpaceGassSession.GetMemberEndForcesAsync` with connection guards, filter resolution, and error formatting. 214 passing unit tests total (18 new).

- [x] **Slice 11** — Create Member Release + assign releases on Create Member

**Delivered (Slice 11):** One new builder component + updated Create Member + core model.
`Create Release` (`SpaceGass > Structure`, builder): Inputs: Fx/Fy/Fz/Mx/My/Mz (bool, default true = Fixed — same fixity convention as Create Restraint), Kx/Ky/Kz/Kmx/Kmy/Kmz (number, optional — per-DOF spring stiffness, overrides bool to 'S' code). Output: Release Goo. Builds a 6-character release code (F=Fixed, R=Released, S=Spring). Default: `FFFFFF` (fully rigid). Pure data construction — no API calls (ADR-0013).
`Create Member` — two new optional inputs: Release A (Release Goo), Release B (Release Goo). When provided, releases are carried on `SgMemberData` and pushed to SpaceGass as part of member creation via `MemberCreate.Releases` (`MemberReleaseUpdate`). When omitted (or when both ends are fully fixed `FFFFFF` with no stiffness), `Releases` is null — skipped to optimise API calls.
Stiffness mapping: `KTx`→`TxStiffnessAtA/B`, `KTy`→`TyStiffnessAtA/B`, `KTz`→`TzStiffnessAtA/B`, `KRx`→`RxStiffnessAtA/B`, `KRy`→`RyStiffnessAtA/B`, `KRz`→`RzStiffnessAtA/B`. Releases are set inline on `MemberCreate` — no separate API call needed.
Core: `SgReleaseData` model (6-char code accepting F/R/S + 6 optional stiffness values + `IsFullyFixed` helper), updated `SgMemberData` (optional `ReleaseA`, `ReleaseB`), updated `ModelAssembler` to populate `MemberCreate.Releases` when releases have structural effect (skip fully-fixed). New Goo: `GH_SgRelease`, `Param_SgRelease`. 249 passing unit tests total (35 new).

- [x] **Slice 12** — Create Member from Polyline

**Delivered (Slice 12):** Updated Create Member component to accept Polyline geometry (ADR-0003).
`Create Member` (`SpaceGass > Structure`): Geometry input changed from `Line` to `Curve` (name "Curve", nickname "C"). Accepts `Line`, `LineCurve`, and `Polyline`. A Polyline with N vertices produces N-1 Member Goo objects (one per segment), all inheriting the same section, material, type, and releases. Output access changed to `GH_ParamAccess.list`. Zero-length segments emit a warning and are skipped; valid segments still produced. Arcs, NURBS, and other non-linear curves are rejected with error: "Curve geometry is not supported. Use Line or Polyline, or explode/discretise curves first." Line input continues to work as before (one line → one member).
No core model changes — polyline splitting happens in the component. `ModelAssembler` already deduplicates coincident intermediate nodes via the spatial grid. `ExtractLines` checks `TryGetPolyline` before `IsLinear` to preserve intermediate nodes on collinear polylines. 257 passing unit tests total (8 new polyline assembly pattern tests).

- [x] **Slice 13** — Member Direction (orientation)

**Delivered (Slice 13):** New core model + updated Create Member + updated ModelAssembler (ADR-0010).
`Create Member` — three new optional direction inputs with priority: Direction Node (Point3d, nickname "DN" — highest priority, overrides all) > Direction Axis (integer/value list: NotApplicable/XAxis/YAxis/ZAxis/NegativeXAxis/NegativeYAxis/NegativeZAxis, nickname "DX") > Direction Angle (number in degrees, default 0, nickname "DA"). When multiple inputs are connected, higher-priority input wins and a warning is emitted. When no direction inputs are connected (or all defaults), `MemberCreate.Direction` is null and SpaceGass automatic orientation applies. Direction Axis value list auto-populates. All polyline segments inherit the same direction.
`SgDirectionData` model with factory methods: `FromAngle(double)`, `FromAxis(DirectionAxis)`, `FromNode(SgPoint3d)`. `IsDefault` property returns true when angle=0, axis=NotApplicable, no node — used to skip unnecessary API payload.
`ModelAssembler` — direction node points added to the node pool for deduplication and creation as SpaceGass nodes. Direction mapped to `MemberCreate.Direction` (`DirectionUpdate`) with `DirAngle`, `DirAxis`, `DirNode` (resolved to node ID via spatial grid). Default direction skipped.
Core: `SgDirectionData` model, updated `SgMemberData` (optional `Direction`), updated `ModelAssembler`. 272 passing unit tests total (15 new).

- [x] **Slice 14** — Create Combination Load Case + Assemble Model update

**Delivered (Slice 14):** One new builder component + updated Assemble Model + core model and API.
`Create Combination Load Case` (`SpaceGass > Loads`, builder): Inputs: Name (string, required), Load Cases (generic list — accepts both primary Load Case Goo and Combination Load Case Goo as constituents), Factors (list of number — matching scale factors), Notes (string, optional). Output: Combination Load Case Goo. Validates non-empty name, matching list lengths, at least one constituent, and non-null load case entries. ToString: "Combination: ULS = 1.2×Dead + 1.5×Live". Pure data construction — no API calls.
`Assemble Model` — new optional Combination Load Cases input (list). Constituent load cases collected into the primary load case pool (created even without direct loads). After creating primary load cases, step 7c: deduplicates combinations by name (ADR-0006), resolves constituent names to SpaceGass IDs via `LoadCaseMap` or `CombinationLoadCaseMap`, builds `CombinationLoadCaseCreate` with `CombinationItems` (load case ID + multiplying factor), pushes via `Job.Loads.CombinationLoadCases.Bulk.PostAsync`, validates bulk result. Combinations referencing other combinations are created in topological order (dependencies first); circular references emit a warning and are skipped. Warns if constituent name not found. Status includes combination count. Dependency order: ... → load cases → **combination load cases** → load categories → node loads → ...
Core: `SgCombinationLoadCaseData` model (Name, Notes, list of `SgCombinationConstituent`: LoadCase + Factor), `SgModelData.CombinationLoadCaseMap`, `ISpaceGassApi.CreateCombinationLoadCasesAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgCombinationLoadCase`, `Param_SgCombinationLoadCase`. 289 passing unit tests total (17 new).

- [x] **Slice 15** — Create Section / Create Material (merged, library + custom)

**Delivered (Slice 15):** Merged library and custom section/material into unified components.
`Create Section` (`SpaceGass > Properties`, builder — replaces `Create Library Section`, same GUID): Inputs: Library (string, optional — omit for custom), Name (string, required), Area/Iy/Iz/J/Ay/Az (number, optional — custom properties), Principal Angle (number, optional), Area Factor/Iy Factor/Iz Factor/Torsion Factor (number, optional — must be > 0), Mark (string, optional), Transposed (bool, optional). When Library connected → library mode (SpaceGass lookup + optional overrides). When Library omitted → custom mode (user-defined properties). Output: Section Goo (same type, interchangeable downstream).
`Create Material` (`SpaceGass > Properties`, builder — replaces `Create Library Material`, same GUID): Inputs: Library (string, optional), Name (string, required), E (Young's modulus), Poissons Ratio, Density, Thermal Coefficient, Concrete Strength (all number, optional — custom properties). Same library/custom mode logic.
`ModelAssembler` — partitions unique sections/materials by `IsLibrary`. Library items → existing `CreateSectionsFromLibraryAsync` / `CreateMaterialsFromLibraryAsync`. Custom items → new `CreateSectionsFromUserAsync` (`Job.Structure.Sections.Bulk.PostAsync`) / `CreateMaterialsFromUserAsync` (`Job.Structure.Materials.Bulk.PostAsync`). Both populate the same `SectionMap`/`MaterialMap`. Deduplication applies across both modes by key (ADR-0006).
Core: updated `SgSectionData` (nullable Library, new Area/Iy/Iz/J/PrincipalAngle properties, IsLibrary flag, dual-mode Key), updated `SgMaterialData` (nullable Library, new YoungsModulus/PoissonsRatio/MassDensity/ThermalCoeff/ConcreteStrength, IsLibrary flag, dual-mode Key), `ISpaceGassApi.CreateSectionsFromUserAsync` + `CreateMaterialsFromUserAsync`, `SpaceGassApiWrapper` implementations. 306 passing unit tests total (17 new).

- [x] **Slice 16** — Advanced Restraints (spring stiffness + friction)

**Delivered (Slice 16):** Two new builder components + updated Create Restraint + core models.
Restraint code extended to full API vocabulary: F=Fixed, R=Released, S=Spring, P=Plastic, N=Friction, V=Variable.
`Create Restraint Stiffness` (`SpaceGass > Structure`, builder): Inputs: Kx/Ky/Kz/Kmx/Kmy/Kmz (number, all optional). Output: Restraint Stiffness Goo. Warns if no stiffness provided.
`Create Restraint Friction` (`SpaceGass > Structure`, builder): Inputs: Factor X/Y/Z (number, optional), Normal Axis X/Y/Z (value list: None/XAxis/YAxis/ZAxis), Normal Direction X/Y/Z (value list: Either/PositiveOnly/NegativeOnly). Output: Restraint Friction Goo. Warns if no friction defined.
`Create Restraint` — two new optional inputs: Stiffness (Restraint Stiffness Goo), Friction (Restraint Friction Goo). Code building: booleans set F/R, then stiffness overrides DOFs to 'S', then friction overrides translational DOFs to 'N'. Warns if friction and stiffness conflict on same DOF (friction wins).
`ModelAssembler` — maps stiffness to `NodeRestraintCreate.TxStiffness/TyStiffness/...` and friction to `XFrictionFactor/XFrictionNormalAxis/XFrictionNormalDirection/...`.
Core: `SgRestraintStiffnessData` model (6 optional stiffness values + HasAnyStiffness), `SgRestraintFrictionData` + `SgFrictionAxisData` models (per-axis factor/normalAxis/normalDirection), updated `SgRestraintData` (accepts S/P/N/V codes, carries optional Stiffness + Friction). New Goo: `GH_SgRestraintStiffness`, `GH_SgRestraintFriction`, `Param_SgRestraintStiffness`, `Param_SgRestraintFriction`. 325 passing unit tests total (19 new).

- [x] **Slice 17** — Create Analysis Settings + Run Analysis expansion

**Delivered (Slice 17):** Three new settings builder components + updated Run Analysis + core dispatch.
`Create Static Analysis Settings` (`SpaceGass > Analysis`, builder): 27 optional inputs mapping 1:1 to `StaticSettingsUpdate` API type (P-Delta, load steps, convergence, damping, solver, optimization, etc.). Output: Analysis Settings Goo. Used for both Linear Static and Non-linear Static analysis. Pure data construction (ADR-0014).
`Create Buckling Analysis Settings` (`SpaceGass > Analysis`, builder): 21 optional inputs mapping to `BucklingSettingsUpdate` (modes, theory, tolerance, limits, axial force distribution, etc.). Output: Analysis Settings Goo.
`Create Dynamic Frequency Settings` (`SpaceGass > Analysis`, builder): 18 optional inputs mapping to `DynamicFrequencySettingsUpdate` (modes, frequency shift, tolerance, limits, etc.). Output: Analysis Settings Goo.
`Run Analysis` — updated: new description "Run an analysis on the assembled SpaceGass model (Linear Static, Non-linear Static, Buckling, or Dynamic Frequency)." Two new optional inputs: Type (value list: 0=Linear Static, 1=Non-linear Static, 2=Buckling, 3=Dynamic Frequency, default 0), Settings (Analysis Settings Goo). Dispatches to the correct API endpoint based on Type. Polling and progress pattern unchanged. Status output includes analysis type name. Value list auto-populates.
Optimized approach: no intermediate domain model — API settings types (`StaticSettingsUpdate`, `BucklingSettingsUpdate`, `DynamicFrequencySettingsUpdate`) wrapped directly in unified `SgAnalysisSettingsData` container with factory methods (`ForLinearStatic`, `ForNonlinearStatic`, `ForBuckling`, `ForDynamicFrequency`).
Core: `SgAnalysisSettingsData` + `SgAnalysisType` enum, `ISpaceGassApi.RunNonlinearAnalysisAsync` + `RunBucklingAnalysisAsync` + `RunDynamicFrequencyAnalysisAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.RunAnalysisAsync` with type dispatch + backward-compat `RunStaticAnalysisAsync`. New Goo: `GH_SgAnalysisSettings`, `Param_SgAnalysisSettings`. 337 passing unit tests total (12 new dispatch tests).
**Note:** Future post-release slice — use Grasshopper 8 Zoom UI feature to show only common inputs by default on settings components, with full inputs revealed on zoom.

- [x] **Slice 18** — Get Member Forces (rework) + Get Member Displacements

**Delivered (Slice 18):** One reworked component + one new async component + core query support.
`Get Member Forces` (`SpaceGass > Results`, async — replaces `Get Member End Forces`, same GUID): Inputs: Model (SgModel Goo, required), Members (List<Line>, optional filter), Load Cases (List<string>, optional filter — supports both primary and combination load case names), Mode (value list: End Forces=0 / Intermediate=1, default End Forces). Both modes use two-level data tree `{load_case; member}` (ADR-0015). End Forces mode: outputs Points (node locations), Nodes (node IDs), Fx–Mz; no Lines or Stations. Intermediate mode: outputs Lines (one per branch), Stations, Fx–Mz; no Points or Nodes. Outputs: Load Cases (tree, one per branch), Members (integer IDs, one per branch), Lines/Points/Nodes/Stations (conditional on mode), Fx/Fy/Fz/Mx/My/Mz (DataTree). Value list auto-populates on Mode input.
`Get Member Displacements` (`SpaceGass > Results`, async): Inputs: Model (required), Members (optional filter), Load Cases (optional filter — supports both primary and combination). Calls `Job.Query.Analysis.Static.MemberIntermediateDisplacements.GetAsync()`. Outputs: Load Cases (tree), Members (integer IDs, one per branch), Lines (one per branch), Stations, TxGlobal/TyGlobal/TzGlobal/TxLocal/TyLocal/TzLocal (DataTree `{load_case; member}`). The API provides translations in both global and local axes — both are output.
Core: `SgMemberIntermediateForceData` (station, location, 6 force components), `SgMemberIntermediateForcesResult`, `SgMemberDisplacementData` (station, location, 6 translation components: global + local), `SgMemberDisplacementsResult` models, `ISpaceGassApi.GetMemberIntermediateForcesAsync` + `GetMemberIntermediateDisplacementsAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetMemberIntermediateForcesAsync` + `GetMemberDisplacementsAsync` with connection guards, filter resolution (refactored to shared `ResolveMemberFilter`/`ResolveLoadCaseFilter` helpers — both check `LoadCaseMap` and `CombinationLoadCaseMap`), and error formatting. 360 passing unit tests total (23 new).

- [x] **Slice 19** — Get Buckling Results

**Delivered (Slice 19):** One new async component + core query support.
`Get Buckling Results` (`SpaceGass > Results`, async): Inputs: Model (SgModel Goo, required), Members (List<Line>, optional — filter effective lengths), Modes (List<int>, optional — filter by mode number), Load Cases (List<string>, optional — filter by load case name, supports both primary and combination). Combines two API queries: `List Buckling Load Factors` (no server-side filter, modes and load cases filtered client-side) and `List Buckling Effective Lengths` (server-side Members and Modes filters, load cases filtered client-side). Null Mode/Member records skipped.
Outputs (all branched by `{load_case; mode}`): Load Cases (string names), Modes (integer mode numbers), Load Factors, Node At Max Translation (Point3d), Translation Axis (string), Node At Max Rotation (Point3d), Rotation Axis (string), Members (integer IDs — list items), Lines (member geometry — list items), Length (member length — list items), Pcr (critical buckling load — list items), Ley (effective length about Y — list items), Lez (effective length about Z — list items).
Core: `SgBucklingLoadFactorData` (LoadCaseId, Mode, LoadFactor, NodeAtMaxTranslation as SgPoint3d?, TranslationAxis, NodeAtMaxRotation as SgPoint3d?, RotationAxis — node IDs resolved to point geometry via model's NodeMap), `SgBucklingEffectiveLengthData` (LoadCaseId, MemberId, Mode, Ley, Lez, Pcr, Length), `SgBucklingResultsResult` container, `ISpaceGassApi.GetBucklingLoadFactorsAsync` + `GetBucklingEffectiveLengthsAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetBucklingResultsAsync` with connection guard, member/mode/load case filter resolution, and error formatting. 374 passing unit tests total (14 new).

- [x] **Slice 20** - Clean up creation of a value list for component inputs - right click on input create value list to left of component.

**Delivered (Slice 20):** Unified value list UX across all components via `Param_SgIntegerOption` custom parameter and `ValueListHelper` static helper.
`Param_SgIntegerOption` (`GhSpaceGass.Types`): Extends `Param_Integer`. Stores value list options, default selection index, and auto-create flag. Overrides `AppendAdditionalMenuItems` to add an "Add [Name] value list" item to the input parameter's own right-click context menu — creates a populated `GH_ValueList`, positions it to the left, wires it, and expires the solution. Disabled when the input already has a source.
`ValueListHelper` (`GhSpaceGass.Helpers`): Static helper with shared option arrays (MemberType, DirectionAxis, AnalysisType, ForceMode, PositionUnits, LoadAxes, FrictionNormalAxis/Direction, PlateType, SolverType, TensionCompression, OptimizationMethod/Axis, LoadingType, MatrixType, BucklingTheory, AxialForceDistribution). One lifecycle method: `AutoCreateOnPlacement(component, document)` for `AddedToDocument` — creates and wires populated value lists for all `Param_SgIntegerOption` inputs with no existing sources. Deferred via `document.ScheduleSolution(0, ...)` to avoid expiring objects during the active solution.
Components updated (8 total): `CreateMember` (Type, Direction Axis), `RunAnalysis` (Type), `GetMemberForces` (Mode), `CreateMemberDistributedLoad` (Position Units, Axes), `CreateRestraintFriction` (Normal Axis/Direction × 3 axes), `CreateStaticAnalysisSettings` (7 enum inputs), `CreateBucklingAnalysisSettings` (7 enum inputs), `CreateDynamicFrequencySettings` (4 enum inputs). All auto-create value lists on first placement.
Behaviour: on component placement, populated value lists appear automatically wired to each enum input. Right-click any enum input grip → "Add [Name] value list" creates and wires a new list on demand (for re-creation after deletion or copy-paste). ADR-0011 updated: member distributed loads now default to local axes. 374 passing unit tests (unchanged).

- [x] **Slice 21** — Get Dynamic Frequency Results

**Delivered (Slice 21):** One new async component + core query support.
`Get Dynamic Frequency Results` (`SpaceGass > Results`, async): Inputs: Model (SgModel Goo, required), Points (List<Point3d>, optional — filter mode shapes to specific node locations), Modes (List<int>, optional — filter both natural frequencies and mode shapes by mode number). Combines two API queries: `Job.Query.Analysis.Dynamic.NaturalFrequencies.GetAsync()` and `Job.Query.Analysis.Dynamic.ModeShapes.GetAsync()`. Modes filter passed server-side to both endpoints via `Modes` query parameter. Points filter resolved to node IDs via model's NodeMap, passed server-side to mode shapes endpoint via `Nodes` query parameter. Null Mode records skipped.
Outputs (all branched by `{load_case; mode}`): Load Cases (string names), Modes (integer mode numbers), Frequency (Hz), Period (seconds), Mass Part X/Y/Z (mass participation ratios), Nodes (integer node IDs — list items for mode shapes), Points (Point3d node locations — list items), Tx/Ty/Tz (translations — list items), Rx/Ry/Rz (rotations — list items).
Empty results emit warning "No dynamic frequency results found". Unmatched filter nodes emit warning and are skipped. API exceptions wrapped with `ModelAssembler.FormatApiError`.
Core: `SgNaturalFrequencyData` model (LoadCaseId, Mode, Frequency, Period, MassPartX/Y/Z), `SgModeShapeNodeData` model (LoadCaseId, Mode, NodeId, Tx/Ty/Tz/Rx/Ry/Rz), `SgDynamicFrequencyResultsResult` container, `ISpaceGassApi.GetNaturalFrequenciesAsync` + `GetModeShapesAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetDynamicFrequencyResultsAsync` with connection guard, mode/node/load case filter resolution, and error formatting. 389 passing unit tests total (15 new).

- [x] **Slice 22** - Add Lumped Mass Loads and Node Displacements

**Delivered (Slice 22):** Two new builder components + updated Assemble Model + core models and API.
`Create Lumped Mass Load` (`SpaceGass > Loads`, builder): Inputs: Point (Point3d), Load Case (required), Load Category (optional), Tmx/Tmy/Tmz (translational mass, default 0), Rmx/Rmy/Rmz (rotational mass, default 0). Output: Lumped Mass Load Goo. Warns if all mass components are zero. Pure data construction — no API calls.
`Create Prescribed Displacement` (`SpaceGass > Loads`, builder): Inputs: Point (Point3d), Load Case (required), Load Category (optional), Tx/Ty/Tz (translations, default 0), Rx/Ry/Rz (rotations, default 0). Output: Prescribed Displacement Goo. Warns if all components are zero. Pure data construction — no API calls.
`Assemble Model` — unified `Loads` input now also accepts `GH_SgLumpedMassLoad` and `GH_SgPrescribedDisplacement` Goo types. Load case and category collection gathers from all 5 load sources (node loads, distributed loads, self-weight loads, lumped mass loads, prescribed displacements) and deduplicates together. Lumped mass loads and prescribed displacements resolve point → node ID via spatial grid; orphan points create standalone nodes with warning (ADR-0002 pattern). Lumped mass loads pushed via `Job.Loads.LumpedMassLoads.Bulk.PostAsync`. Prescribed displacements pushed via `Job.Loads.NodeDisplacements.Bulk.PostAsync`. Status includes lumped mass load and prescribed displacement counts. Dependency order: … → self-weight loads → **lumped mass loads → prescribed displacements**.
Core: `SgLumpedMassLoadData` model (Point, LoadCase, LoadCategory, Tmx/Tmy/Tmz/Rmx/Rmy/Rmz, IsZero), `SgPrescribedDisplacementData` model (Point, LoadCase, LoadCategory, Tx/Ty/Tz/Rx/Ry/Rz, IsZero), `SgModelData.LumpedMassLoadCount` + `PrescribedDisplacementCount`, `ISpaceGassApi.CreateLumpedMassLoadsAsync` + `CreatePrescribedDisplacementsAsync`, `SpaceGassApiWrapper` implementations, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgLumpedMassLoad`, `GH_SgPrescribedDisplacement`, `Param_SgLumpedMassLoad`, `Param_SgPrescribedDisplacement`. 424 passing unit tests total (29 new).

- [x] **Slice 23** - Add Member Concentrated Loads, and add distributed moment support to the existing Member Distributed Loads.

**Delivered (Slice 23):** One new builder component + updated existing component + core models and API.
`Create Member Concentrated Load` (`SpaceGass > Loads`, builder): Inputs: Member (Line), Load Case (required), Load Category (optional), Fx/Fy/Fz (force, default 0), Mx/My/Mz (moment, default 0), Position (default 50), Position Units (value list: Actual/Percent, default Percent), Axes (value list: Local/Global, default Local — ADR-0011). Output: Member Concentrated Load Goo. Warns if all components are zero. Auto-creates value lists on placement. Pure data construction — no API calls.
`Create Member Distributed Load` — updated with 6 new optional moment inputs: Mx Start/My Start/Mz Start/Mx End/My End/Mz End (all default 0). `SgMemberDistributedLoadData` extended with moment fields + `HasForces`/`HasMoments` helpers. `IsZero` now checks both forces AND moments. During assembly, forces pushed via `MemberDistributedLoadCreate` (existing), moments pushed via `MemberDistributedMomentCreate` (new separate API call). A single Goo with both forces and moments produces both API calls; a Goo with only moments skips the force API call and vice versa.
`Assemble Model` — unified `Loads` input now also accepts `GH_SgMemberConcentratedLoad` Goo type. Load case and category collection gathers from all 7 load sources. Concentrated loads resolve member geometry to member ID via MemberMap (same pattern as distributed loads); unmatched members warn and skip. Pushed via `Job.Loads.MemberConcentratedLoads.Bulk.PostAsync`. Distributed moments pushed via `Job.Loads.MemberDistributedMoments.Bulk.PostAsync`. Member lookup built once and shared between distributed and concentrated load steps. Status includes concentrated load and distributed moment counts.
Core: `SgMemberConcentratedLoadData` model (MemberStart/End, LoadCase, LoadCategory, Fx/Fy/Fz/Mx/My/Mz, Position, PositionUnits, Axes, IsZero), updated `SgMemberDistributedLoadData` (6 moment fields, HasForces, HasMoments), `SgModelData.MemberConcentratedLoadCount` + `MemberDistributedMomentCount`, `ISpaceGassApi.CreateMemberConcentratedLoadsAsync` + `CreateMemberDistributedMomentsAsync`, `SpaceGassApiWrapper` implementations, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgMemberConcentratedLoad`, `Param_SgMemberConcentratedLoad`. 449 passing unit tests total (25 new).

- [x] **Slice 24** - Add Member prestress load application

**Delivered (Slice 24):** One new builder component + updated Assemble Model + core model and API.
`Create Member Prestress Load` (`SpaceGass > Loads`, builder): Inputs: Member (Line), Load Case (required), Load Category (optional), Prestress (number, default 0). Output: Member Prestress Load Goo. Warns if prestress is zero. Pure data construction — no API calls.
`Assemble Model` — unified `Loads` input now also accepts `GH_SgMemberPrestressLoad` Goo type. Load case and category collection gathers from all 8 load sources. Prestress loads resolve member geometry to member ID via MemberMap (same pattern as distributed/concentrated); unmatched members warn and skip. Pushed via `Job.Loads.MemberPrestressLoads.Bulk.PostAsync`. Status includes prestress load count.
Core: `SgMemberPrestressLoadData` model (MemberStart/End, LoadCase, LoadCategory, Prestress, IsZero), `SgModelData.MemberPrestressLoadCount`, `ISpaceGassApi.CreateMemberPrestressLoadsAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgMemberPrestressLoad`, `Param_SgMemberPrestressLoad`. 460 passing unit tests total (11 new).

- [x] **Slice 25** - Add creating of Node Constraints (structure)

**Delivered (Slice 25):** One new builder component + updated Assemble Model + core model and API.
`Create Node Constraint` (`SpaceGass > Structure`, builder): Inputs: Slave Point (Point3d), Master Point (Point3d), Fx/Fy/Fz/Mx/My/Mz (bool, default true = constrained), Axes (value list: Global/Inclined, default Global), X Vector/Y Vector/Z Vector (number, optional — for inclined axes). Output: Node Constraint Goo. Builds 6-character constraint code (F=Constrained, R=Free). Default `FFFFFF` = fully rigid link. Warns if inclined axes with zero direction vector. Auto-creates value list on placement. Pure data construction — no API calls.
`Assemble Model` — new optional Constraints input (list of Node Constraint Goo, separate from Loads). Both slave and master points resolved to node IDs via spatial deduplication grid. Orphan points (ADR-0002) create standalone nodes with warning. Warns when multiple constraints target the same slave node. Pushed via `Job.Structure.NodeConstraints.Bulk.PostAsync`. Status includes constraint count. Dependency order: … → members → restraints → **constraints** → load cases → …
Core: `SgNodeConstraintData` model (SlavePoint, MasterPoint, ConstraintCode, Axes, XVector/YVector/ZVector), `SgModelData.ConstraintCount`, `ISpaceGassApi.CreateNodeConstraintsAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgNodeConstraint`, `Param_SgNodeConstraint`. 477 passing unit tests total (17 new).

- [x] **Slice 26** - Add creating of Member offsets (feeds into Create Member before Assemble Model)

**Delivered (Slice 26):** One new builder component + updated Create Member + core model and API.
`Create Member Offset` (`SpaceGass > Structure`, builder): Inputs: X Offset A/Y Offset A/Z Offset A (default 0), X Offset B/Y Offset B/Z Offset B (default 0), Axes (value list: Local/Global, default Local). Output: Member Offset Goo. Warns if all offsets are zero. Auto-creates value list on placement. Pure data construction — no API calls.
`Create Member` — new optional `Offset` input (Member Offset Goo). When provided, offset data is carried on `SgMemberData`. All polyline segments inherit the same offset (consistent with releases/direction pattern).
`Assemble Model` — after creating members (step 5b), creates offsets via `Job.Structure.MemberOffsets.Bulk.PostAsync` for members with non-zero offsets. All-zero offsets skipped to optimise API calls. Dependency order: … → members → **member offsets** → restraints → constraints → …
Core: `SgMemberOffsetData` model (XOffsetAtA/YOffsetAtA/ZOffsetAtA/XOffsetAtB/YOffsetAtB/ZOffsetAtB, Axes, IsZero), updated `SgMemberData` (optional `Offset`), `ISpaceGassApi.CreateMemberOffsetsAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler`. New Goo: `GH_SgMemberOffset`, `Param_SgMemberOffset`. 489 passing unit tests total (12 new).

- [x] **Slice 27** - Add creation of Plate elements

**Delivered (Slice 27):** One new builder component + updated Assemble Model + core model and API.
`Create Plate` (`SpaceGass > Structure`, builder): Inputs: Mesh (required — each face → one plate), Material (Material Goo, required), Thickness (number, required), Bending Thickness/Membrane Thickness/Shear Thickness (optional overrides), Offset (default 0), Theory (value list: Kirchoff/Mindlin, default Kirchoff). Output: list of Plate Goo (one per mesh face). Tri faces → 3-node plates, quad faces → 4-node plates. Auto-creates value list on placement. Pure data construction — no API calls.
`Assemble Model` — new optional Plates input (list of Plate Goo). Members input now optional (plates-only models are valid). Early exit requires both members AND plates to be empty. Plate corner points added to node pool for deduplication alongside member endpoints. Plate materials collected alongside member materials and deduplicated (ADR-0006). Plates pushed via `Job.Structure.Plates.Bulk.PostAsync` after members/offsets. `SgModelData.PlateMap` maps plate ID → corner points. Status includes plate count. Dependency order: … → members → member offsets → **plates** → restraints → constraints → …
Core: `SgPlateData` model (Nodes[3-4], Material, ActualThickness, BendingThickness?, MembraneThickness?, ShearThickness?, Offset, Theory?, IsTriangle), `SgModelData.PlateMap`, `ISpaceGassApi.CreatePlatesAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` (sections/members guarded by members.Count>0, materials collected from both members+plates). New Goo: `GH_SgPlate`, `Param_SgPlate`. 507 passing unit tests total (18 new).

- [x] **Slice 28** - Add plate pressure load application

**Delivered (Slice 28):** One new builder component + updated Assemble Model + core model and API.
`Create Plate Pressure Load` (`SpaceGass > Loads`, builder): Inputs: Plate (Plate Goo, required — carries corner nodes for plate ID resolution), Load Case (required), Load Category (optional), Px/Py/Pz (pressure, default 0), Axes (value list: Local/Global, default Local — ADR-0011). Output: Plate Pressure Load Goo. Warns if all pressure components are zero. Auto-creates value list on placement. Pure data construction — no API calls.
`Assemble Model` — unified `Loads` input now also accepts `GH_SgPlatePressureLoad` Goo type. Load case and category collection gathers from all 9 load sources. Plate pressure loads resolve plate corner points → plate ID via PlateMap (iterate and match resolved node IDs); unmatched plates warn and skip. Pushed via `Job.Loads.PlatePressureLoads.Bulk.PostAsync`. Status includes plate pressure load count.
Core: `SgPlatePressureLoadData` model (PlateNodes[], LoadCase, LoadCategory, Px/Py/Pz, Axes, IsZero), `SgModelData.PlatePressureLoadCount`, `ISpaceGassApi.CreatePlatePressureLoadsAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgPlatePressureLoad`, `Param_SgPlatePressureLoad`. 518 passing unit tests total (11 new).

- [x] **Slice 29** - Add Thermal Load application

**Delivered (Slice 29):** One new builder component + updated Assemble Model + core model and API.
`Create Thermal Load` (`SpaceGass > Loads`, builder): Inputs: Element (generic — accepts Line for member or Plate Goo for plate, auto-detects element type), Load Case (required), Load Category (optional), Temperature (default 0), Y Gradient (default 0), Z Gradient (default 0). Output: Thermal Load Goo. Line input → member thermal load (carries member start/end for ID resolution). Plate Goo input → plate thermal load (carries corner nodes for plate ID resolution). Unrecognised input → error. Warns if all thermal values are zero. Pure data construction — no API calls.
`Assemble Model` — unified `Loads` input now also accepts `GH_SgThermalLoad` Goo type. Load case and category collection gathers from all 10 load sources. Member thermal loads resolve via MemberMap (same pattern as distributed/concentrated); plate thermal loads resolve via PlateMap (same pattern as plate pressure). All pushed via single `Job.Loads.ThermalLoads.Bulk.PostAsync` call. Status includes thermal load count.
Core: `SgThermalLoadData` model with factory methods `ForMember(...)` and `ForPlate(...)` (ElementType, MemberStart/End or PlateNodes, LoadCase, LoadCategory, ThermalLoad, YGradient, ZGradient, IsZero), `SgModelData.ThermalLoadCount`, `ISpaceGassApi.CreateThermalLoadsAsync`, `SpaceGassApiWrapper` implementation, extended `ModelAssembler` (memberLookup moved to method scope, thermal step resolves both member and plate elements) and `SpaceGassSession.AssembleModelAsync`. New Goo: `GH_SgThermalLoad`, `Param_SgThermalLoad`. 531 passing unit tests total (13 new).

- [x] **Slice 30** - Get Plate Element forces

**Delivered (Slice 30):** One new async results component + core query support.
`Get Plate Forces` (`SpaceGass > Results`, async): Inputs: Model (required), Plates (List<Plate Goo>, optional filter), Load Cases (List<string>, optional filter — supports primary and combination), Mode (value list: Element Forces=0 / Nodal Forces=1, default Element Forces). Element Forces mode: calls `PlateElementForces.GetAsync()`, outputs Load Cases, Plates, Fx/Fy/Fxy/Mx/My/Mxy/Vxz/Vyz branched by `{load_case}`. Nodal Forces mode: calls `PlateNodalForces.GetAsync()`, outputs Load Cases, Plates, Nodes, Fx/Fy/Fz/Mx/My/Mz branched by `{load_case; plate}`. Plate filter resolves Plate Goo corner nodes → plate IDs via PlateMap. Load case filter resolves via LoadCaseMap + CombinationLoadCaseMap. Empty results emit warning.
Core: `SgPlateElementForceData` (PlateId, LoadCaseId, Fx/Fy/Fxy/Mx/My/Mxy/MxTop/MxBtm/MyTop/MyBtm/Vxz/Vyz), `SgPlateNodalForceData` (PlateId, LoadCaseId, NodeId, Fx/Fy/Fz/Mx/My/Mz), `SgPlateElementForcesResult` + `SgPlateNodalForcesResult` containers, `ISpaceGassApi.GetPlateElementForcesAsync` + `GetPlateNodalForcesAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetPlateElementForcesAsync` + `GetPlateNodalForcesAsync` with connection guards, plate/load case filter resolution (new `ResolvePlateFilter` helper), and error formatting. 541 passing unit tests total (10 new).

- [x] **Slice 30.5** — Save Job component

**Delivered (Slice 30.5):** One new async component + core session method.
`Save Job` (`SpaceGass > Connection`, async): Inputs: `Save?` (bool, default false — prevents auto-save), `File Path` (optional `.sg` path — if omitted, saves to the current job file path from Connect).
Outputs: `Saved?` (bool), `File Path` (string — path the job was saved to), `Status` (string — summary with file path, structure counts, load counts, analysis state).
Behaviour: requires active connection via singleton session. When `Save? = false`: outputs `Saved? = false` and idle status. When `Save? = true` with explicit path: saves to that path. When `Save? = true` without path: resolves the current file path from the job state; errors if no file is associated. After saving, queries full job status for the summary output. Component message: "Saved" / "Error" / empty.
Core: `SpaceGassSession.SaveAndGetInfoAsync(string? filePath, CancellationToken)` — resolves path → saves → queries status → returns `SgJobInfo`. 562 passing unit tests total (11 new).

- [x] **Slice 31** - Add an append mode to the Assemble Model component, so that it adds to an existing model opened from the Connect component.

**Delivered (Slice 31):** Updated Assemble Model component + core assembler + ADR-0001 amendment.
`Assemble Model` (`SpaceGass > Model`, async): New optional input: Mode (value list: Rebuild=0 / Append=1, nickname "MD", default Rebuild). Rebuild mode: unchanged — clears all existing job data before pushing (ADR-0001 Clear & Rebuild). Append mode: skips `ClearJobDataAsync`, pushes materials, sections, nodes, members, restraints, loads etc. on top of whatever exists in the open SpaceGass job. Append mode emits a warning on every run: "Append mode: data is added to existing job content. Recomputes will create duplicates." Status output prefix: "Assembled:" in Rebuild mode, "Appended:" in Append mode. Component message: "Assembled (3N, 2M)" or "Appended (3N, 2M)". Component description updated to mention both modes. Value list auto-creates on placement (existing `Param_SgIntegerOption` pattern). All deduplication, validation, warnings, orphan handling, and dependency ordering within the pushed data remain identical in both modes. ADR-0005 multi-instance warning still applies.
Core: `ModelAssembler.AssembleAsync` — new `bool appendMode = false` parameter; clear step guarded by `if (!appendMode)`. `SpaceGassSession.AssembleModelAsync` — passes through `appendMode` flag. ADR-0001 amended to document the opt-in Append mode. CONTEXT.md vocabulary updated (Clear & Rebuild, Assemble Model definitions). 573 passing unit tests total (9 new).

- [x] **Slice 32** - Add components to disassemble a model from an open existing model (multiple query components pattern).
  - [x] **Slice 32.1** — Disassemble Model: core component querying nodes, members, plates + building SgModel with all maps for downstream chaining.

  **Delivered (Slice 32.1):** One new async component + core query support + domain model.
  `Disassemble Model` (`SpaceGass > Model`, async): Input: Disassemble? (bool, default false). Outputs: Model (SgModel Goo — NodeMap, MemberMap, PlateMap, SectionMap, MaterialMap, LoadCaseMap, CombinationLoadCaseMap populated from the live job), Points (List<Point3d> — node locations ordered by ID), Node IDs, Lines (member geometry A→B), Member IDs, Member Types (display names: Beam, Truss, Cable, etc.), Member Sections (section ID per member), Member Materials (material ID per member), Meshes (one mesh per plate — tri or quad face), Plate IDs, Status. Behaviour: requires active connection; queries nodes → sections → materials → members → plates → load cases from SpaceGass via List GET endpoints; builds SgModelData with populated maps; resolves member/plate geometry via node ID→coordinate lookups; empty model emits warning. Section/material map keys: `Library::Name` for library items, `Name` for custom. Load cases partitioned into LoadCaseMap (Primary) and CombinationLoadCaseMap (Combination) by type. Component message: "Disassembled (3N, 2M, 1P)".
  Core: `SgDisassembledModel` domain model (SgModelData + Lists of SgDisassembledNode/Member/Plate + Warnings), `ISpaceGassApi.ListNodesAsync` + `ListMembersAsync` + `ListSectionsAsync` + `ListMaterialsAsync` + `ListPlatesAsync` + `ListLoadCasesAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.DisassembleModelAsync` with connection guard, error formatting, and member type mapping. 598 passing unit tests total (25 new).

  - [x] **Slice 32.2** — Get Section Properties + Get Material Properties query components.

  **Delivered (Slice 32.2):** Two new async query components + core session methods + domain models.
  `Get Sections` (`SpaceGass > Properties`, async): Input: Model (SgModel Goo, required). Outputs: IDs, Names, Libraries, Sources ("Library"/"User"), Area, Iy, Iz, J, Ay, Az, Principal Angle, Mark, Area Factor, Iy Factor, Iz Factor, Torsion Factor, Transposed, Angle Type, Status — all parallel lists ordered by section ID. Calls existing `ListSectionsAsync`. Nullable API values default to 0.0 / "" / false. Null IDs skipped. Empty results emit warning.
  `Get Materials` (`SpaceGass > Properties`, async): Input: Model (SgModel Goo, required). Outputs: IDs, Names, Libraries, Sources, Youngs Modulus (E), Poissons Ratio (PR), Density (D), Thermal Coefficient (TC), Concrete Strength (fc), Status — all parallel lists ordered by material ID. Same patterns as section properties.
  Core: `SgSectionPropertiesResult` + `SgSectionPropertyData` (18 fields), `SgMaterialPropertiesResult` + `SgMaterialPropertyData` (9 fields) domain models, `SpaceGassSession.GetSectionPropertiesAsync` + `GetMaterialPropertiesAsync` with connection guards, `MapPropertySource` + `MapAngleType` helpers. 610 passing unit tests total (12 new).

  - [x] **Slice 32.3** — Get Load Cases, Combination Load Cases, Load Categories, and Load Groups.

  **Delivered (Slice 32.3):** One new async query component + core session method + domain model + 2 new API methods.
  `Get Load Cases` (`SpaceGass > Cases`, async): Input: Model (SgModel Goo, required). Outputs: IDs, Names, Types ("Primary"/"Combination"/"Step"/"Unused"), Notes, Combination Items (DataTree — branch per load case with "Factor×LoadCaseName" strings), Category IDs, Categories, Group IDs, Groups, Group Cases (DataTree — branch per group with comma-separated ID list), Status. Queries load cases, load categories, and load case groups from SpaceGass. Combination item load case IDs resolved to names via the queried data; unresolved IDs shown as "LC{id}". Null IDs skipped. Empty results emit warning.
  Core: `SgLoadCaseDataResult` + `SgLoadCaseInfo` + `SgLoadCategoryInfo` + `SgLoadCaseGroupInfo` domain models, `ISpaceGassApi.ListLoadCategoriesAsync` + `ListLoadCaseGroupsAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetLoadCaseDataAsync` + `MapLoadCaseType` helper. 621 passing unit tests total (11 new).

  - [x] **Slice 32.4** — Get all Self-Weight Loads.

  **Delivered (Slice 32.4):** One new async query component + core session method + domain model + 1 new API method.
  `Get Self-Weight Loads` (`SpaceGass > Loads`, async): Input: Model (SgModel Goo, required — used to resolve load case IDs to names). Outputs: Load Cases (int IDs), Load Case Names (resolved via LoadCaseMap/CombinationLoadCaseMap, fallback "LC{id}"), Categories (int IDs, 0 if none), Acceleration X (AccX) / Y (AccY) / Z (AccZ), Status — all parallel lists. Nicknames match Create Self-Weight Load component. Null load cases skipped. Empty results emit warning.
  Core: `SgSelfWeightLoadsDataResult` + `SgSelfWeightLoadInfo` domain models, `ISpaceGassApi.ListSelfWeightLoadsAsync`, `SpaceGassApiWrapper` implementation, `SpaceGassSession.GetSelfWeightLoadsDataAsync` + shared `BuildLoadCaseIdToNameMap` / `ResolveLoadCaseName` helpers (O(1) lookup via reverse dictionary). 630 passing unit tests total (9 new).

  - [x] **Slice 32.5** — Get all Node Loads. This includes Node Lumped Mass Loads, Prescribed Displacements.

  **Delivered (Slice 32.5):** One new async query component + core session method + domain models + 3 new API methods.
  `Get Node Loads` (`SpaceGass > Loads`, async): Input: Model (SgModel Goo, required — resolves node IDs → Points and load case IDs → names). Outputs: all DataTree branched by node (one branch per unique loaded node, ordered by node ID). Shared outputs: Node IDs, Points (one per branch). Per-type outputs prefixed NL/LM/PD: Load Case IDs (int), Load Cases (name), Categories (ID), then 6 value components each. Node loads: Fx/Fy/Fz/Mx/My/Mz. Lumped mass: Tmx/Tmy/Tmz/Rmx/Rmy/Rmz. Prescribed displacements: Tx/Ty/Tz/Rx/Ry/Rz. Nodes without a particular load type get empty branches. Unresolved node IDs warn and skip. Empty results emit warning. 31 outputs total + Status.
  Core: `SgNodeLoadsDataResult` + `SgNodeLoadEntry` + `SgNodeLoadInfo` (with LoadCaseId) + `SgLumpedMassLoadInfo` (with LoadCaseId) + `SgPrescribedDisplacementInfo` (with LoadCaseId) domain models, `ISpaceGassApi.ListNodeLoadsAsync` + `ListLumpedMassLoadsAsync` + `ListPrescribedDisplacementsAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetNodeLoadsDataAsync` + `BuildNodeIdToPointMap` helper. 641 passing unit tests total (11 new).

  - [x] **Slice 32.6** — Get all Member Loads. This is to include Concentrated Loads, Member Distributed Loads/Moments, Member Prestress Loads and Member Thermal Loads.

  **Delivered (Slice 32.6):** One new async query component + core session method + domain models + 5 new API methods.
  `Get Member Loads` (`SpaceGass > Loads`, async): Input: Model (SgModel Goo, required — resolves member IDs → Lines and load case IDs → names). Outputs: all DataTree branched by member (one branch per unique loaded member, ordered by member ID). Shared outputs: Member IDs, Lines (one per branch). Five load type groups prefixed CL/DL/DM/PL/TL, each with Load Case ID, Load Case Name, Category ID, then type-specific values. CL: Fx-Mz, Position, Position Units, Axes. DL: FxStart-FzFinish, Start/Finish Position, Position Units, Axes. DM: MxStart-MzFinish, Start/Finish Position, Position Units, Axes. PL: Prestress. TL: Temperature, Y Gradient, Z Gradient. Empty branches for load types not applied to that member. Thermal loads filtered to ElementType=Member only (plate thermals → Slice 32.7). Unresolved member IDs warn and skip. Position Units mapped to "Actual"/"Percent". Axes mapped to "Local"/"Global Inclined"/"Global Projected".
  Core: `SgMemberLoadsDataResult` + `SgMemberLoadEntry` + 5 load info records, `ISpaceGassApi.ListMemberConcentratedLoadsAsync` + `ListMemberDistributedLoadsAsync` + `ListMemberDistributedMomentsAsync` + `ListMemberPrestressLoadsAsync` + `ListThermalLoadsAsync`, `SpaceGassApiWrapper` implementations, `SpaceGassSession.GetMemberLoadsDataAsync` + `MapPositionUnits` + `MapLoadAxes` helpers. 653 passing unit tests total (12 new).

  - [x] **Slice 32.7** — Get all Plate Loads. This is to include Plate Pressure Loads and Plate Thermal Loads.

  **Delivered (Slice 32.7):** One new async query component + core session method + domain models + 1 new API method.
  `Get Plate Loads` (`SpaceGass > Loads`, async): Input: Model (SgModel Goo, required — resolves plate IDs → corner points and load case IDs → names). Outputs: all DataTree branched by plate (one branch per unique loaded plate, ordered by plate ID). Shared outputs: Plate IDs (one per branch), Plate Points (3 or 4 corner points per branch). Pressure loads (PP prefix): Load Case ID/Name, Category, Px/Py/Pz, Axes. Thermal loads (TL prefix): Load Case ID/Name, Category, Temperature, Y Gradient, Z Gradient. Empty branches for load types not applied to a plate. Thermal loads filtered to ElementType=Plate only. Unresolved plate IDs warn and skip. 16 outputs total + Status.
  Core: `SgPlateLoadsDataResult` + `SgPlateLoadEntry` + `SgPlatePressureLoadInfo` + `SgPlateThermalLoadInfo` domain models, `ISpaceGassApi.ListPlatePressureLoadsAsync`, `SpaceGassApiWrapper` implementation, `SpaceGassSession.GetPlateLoadsDataAsync` (reuses `ListThermalLoadsAsync` + `MapLoadAxes`). 663 passing unit tests total (10 new).

- 
- [x] **Slice 33** — Results viewport preview — reaction arrows

**Delivered (Slice 33):** Updated existing component + two new Core models.
`Get Node Reactions` (`SpaceGass > Results`, async — same GUID): Two new optional inputs: Scale (number, nickname "Sc" — auto-scale per ADR-0009 when omitted, user override when provided; Scale=0 disables preview; Scale<0 emits warning and disables preview), Show Values (bool, default false, nickname "V" — displays numeric magnitude adjacent to each arrow/arc tip in "G4" format). `IsPreviewCapable = true`. `ClippingBox` expanded to include preview geometry extents. Description updated to mention viewport preview. Node Points output populated on first branch only (identical across load cases — avoids heavy GH point preview on repeated geometry). Node IDs still on all branches for correlation.
Viewport preview (when component preview enabled and Scale ≠ 0): Force arrows (Fx, Fy, Fz) drawn as straight arrows from node location in force direction, length = |magnitude| × forceScale. Moment arcs (Mx, My, Mz) drawn as ¾-circle arcs around the moment axis at node location, radius = |magnitude| × momentScale. Forces and moments auto-scale independently (different units — e.g. kN vs kN·m); when user provides Scale, it applies uniformly to both. Per-axis colours: X=Red (255,0,0), Y=Green (0,150,0), Z=Blue (0,0,255). Line weight: 2px. Arrowheads at tips. Zero-magnitude components skipped. Unmatched node IDs skipped. All queried reactions previewed across all load cases (user filters via Load Cases input for specific cases). Preview geometry pre-computed during SetData and stored for DrawViewportWires — zero per-frame computation.
Auto-scale formula: `(0.1 × modelBboxDiagonal) / maxMagnitude`, computed separately for forces and moments. Degenerate inputs (zero bbox or magnitude) default to scale = 1.0.
Core: `PreviewScaleHelper` (reusable: `ComputeAutoScale`, `ComputeBboxDiagonal`), `ReactionPreviewBuilder.Build` (pure data → `PreviewArrowResult` with `List<PreviewArrow>` + `ForceScale` + `MomentScale`; returns empty when userScale ≤ 0), `PreviewArrow` model (Origin, Dx/Dy/Dz, Magnitude, ArrowType, Axis), `ArrowType` enum (Force/Moment). 686 passing unit tests total (23 new).

- [ ] **Slice 34** — Results viewport preview — node displacement vectors
- [ ] **Slice 35** — Results viewport preview — member displaced shape
- [ ] **Slice 36** — Results viewport preview — member force diagrams
- [ ] **Slice 37** — Add a component for the query of Steel Design Results - List Steel Member Check Summary
- [ ] **Slice 38** — Zoom UI for analysis settings components (show common inputs by default, reveal all on zoom)
