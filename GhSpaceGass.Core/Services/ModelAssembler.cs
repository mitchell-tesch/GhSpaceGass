using GhSpaceGass.Core.Models;
using Microsoft.Kiota.Abstractions;
using SpaceGassApi.Models;

namespace GhSpaceGass.Core.Services;

/// <summary>
///     Collects in-memory member definitions, deduplicates geometry and properties,
///     and pushes the complete structural model to SpaceGass in dependency order.
/// </summary>
public class ModelAssembler
{
    /// <summary>
    ///     Assembles the model from the given members and pushes to SpaceGass.
    /// </summary>
    internal async Task<AssemblyResult> AssembleAsync(
        ISpaceGassApi api,
        IReadOnlyList<SgMemberData> members,
        double tolerance,
        IReadOnlyList<SgRestraintData>? restraints = null,
        IReadOnlyList<SgNodeLoadData>? nodeLoads = null,
        IReadOnlyList<SgMemberDistributedLoadData>? memberDistributedLoads = null,
        IReadOnlyList<SgSelfWeightLoadData>? selfWeightLoads = null,
        IReadOnlyList<SgCombinationLoadCaseData>? combinationLoadCases = null,
        IReadOnlyList<SgLumpedMassLoadData>? lumpedMassLoads = null,
        IReadOnlyList<SgPrescribedDisplacementData>? prescribedDisplacements = null,
        IReadOnlyList<SgMemberConcentratedLoadData>? memberConcentratedLoads = null,
        IReadOnlyList<SgMemberPrestressLoadData>? memberPrestressLoads = null,
        IReadOnlyList<SgNodeConstraintData>? nodeConstraints = null,
        IReadOnlyList<SgPlateData>? plates = null,
        IReadOnlyList<SgPlatePressureLoadData>? platePressureLoads = null,
        IReadOnlyList<SgThermalLoadData>? thermalLoads = null,
        IReadOnlyList<SgMovingLoadScenarioData>? movingLoadScenarios = null,
        bool appendMode = false,
        CancellationToken ct = default)
    {
        var model = new SgModelData();
        var result = new AssemblyResult(model);

        // ── Early exit if no members and no plates ──────────────────────
        var effectivePlates = plates ?? Array.Empty<SgPlateData>();
        if (members.Count == 0 && effectivePlates.Count == 0)
        {
            result.Warnings.Add("No members or plates provided — model is empty.");
            return result;
        }

        // ── Step 1: Clear existing data (ADR-0001 — Clear & Rebuild) ──
        // In append mode, skip the clear to add data alongside existing job content.
        if (!appendMode)
        {
            try
            {
                await api.ClearJobDataAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "clearing job data"), ex);
            }
        }

        // ── Step 2: Deduplicate and create materials ──────────────────
        var allMaterials = new List<SgMaterialData>(members.Select(m => m.Material));
        allMaterials.AddRange(effectivePlates.Select(p => p.Material));
        var uniqueMaterials = DeduplicateByKey(
            allMaterials,
            m => m.Key,
            "material", result.Warnings);

        // Partition into library vs. custom materials
        var libraryMaterials = uniqueMaterials.Where(m => m.IsLibrary).ToList();
        var customMaterials = uniqueMaterials.Where(m => !m.IsLibrary).ToList();

        // Create library materials
        if (libraryMaterials.Count > 0)
        {
            var materialCreates = libraryMaterials
                .Select(m => new MaterialLibraryCreate { Library = m.Library, Name = m.Name })
                .ToList();

            List<Material> createdLibMaterials;
            try
            {
                createdLibMaterials = await api.CreateMaterialsFromLibraryAsync(materialCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating library materials"), ex);
            }

            ValidateBulkResult(createdLibMaterials.Count, libraryMaterials.Count, "library materials");

            for (var i = 0; i < libraryMaterials.Count; i++)
                model.MaterialMap[libraryMaterials[i].Key] = createdLibMaterials[i].Id!.Value;
        }

        // Create custom materials
        if (customMaterials.Count > 0)
        {
            var materialUserCreates = customMaterials
                .Select(m => new MaterialUserCreate
                {
                    Name = m.Name,
                    YoungsModulus = m.YoungsModulus,
                    PoissonsRatio = m.PoissonsRatio,
                    MassDensity = m.MassDensity,
                    ThermalCoeff = m.ThermalCoeff,
                    ConcreteStrength = m.ConcreteStrength
                })
                .ToList();

            List<Material> createdCustomMaterials;
            try
            {
                createdCustomMaterials = await api.CreateMaterialsFromUserAsync(materialUserCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating custom materials"), ex);
            }

            ValidateBulkResult(createdCustomMaterials.Count, customMaterials.Count, "custom materials");

            for (var i = 0; i < customMaterials.Count; i++)
                model.MaterialMap[customMaterials[i].Key] = createdCustomMaterials[i].Id!.Value;
        }

        // ── Step 3: Deduplicate and create sections (only if members exist) ──
        if (members.Count > 0)
        {
            var uniqueSections = DeduplicateByKey(
            members.Select(m => m.Section),
            s => s.Key,
            "section", result.Warnings);

        // Partition into library vs. custom sections
        var librarySections = uniqueSections.Where(s => s.IsLibrary).ToList();
        var customSections = uniqueSections.Where(s => !s.IsLibrary).ToList();

        // Create library sections
        if (librarySections.Count > 0)
        {
            var sectionCreates = librarySections
                .Select(s => new SectionLibraryCreate
                {
                    Library = s.Library,
                    Name = s.Name,
                    Mark = s.Mark,
                    AngleType = s.AngleType,
                    AreaFactor = s.AreaFactor,
                    Ay = s.Ay,
                    Az = s.Az,
                    IyFactor = s.IyFactor,
                    IzFactor = s.IzFactor,
                    TorsionFactor = s.TorsionFactor,
                    Transposed = s.Transposed
                })
                .ToList();

            List<Section> createdLibSections;
            try
            {
                createdLibSections = await api.CreateSectionsFromLibraryAsync(sectionCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating library sections"), ex);
            }

            ValidateBulkResult(createdLibSections.Count, librarySections.Count, "library sections");

            for (var i = 0; i < librarySections.Count; i++)
                model.SectionMap[librarySections[i].Key] = createdLibSections[i].Id!.Value;
        }

        // Create custom sections
        if (customSections.Count > 0)
        {
            var sectionUserCreates = customSections
                .Select(s => new SectionUserCreate
                {
                    Name = s.Name,
                    A = s.Area,
                    Iy = s.Iy,
                    Iz = s.Iz,
                    J = s.J,
                    Ay = s.Ay,
                    Az = s.Az,
                    PrincipalAngle = s.PrincipalAngle,
                    Mark = s.Mark,
                    AreaFactor = s.AreaFactor,
                    IyFactor = s.IyFactor,
                    IzFactor = s.IzFactor,
                    TorsionFactor = s.TorsionFactor
                })
                .ToList();

            List<Section> createdCustomSections;
            try
            {
                createdCustomSections = await api.CreateSectionsFromUserAsync(sectionUserCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating custom sections"), ex);
            }

            ValidateBulkResult(createdCustomSections.Count, customSections.Count, "custom sections");

            for (var i = 0; i < customSections.Count; i++)
                model.SectionMap[customSections[i].Key] = createdCustomSections[i].Id!.Value;
        }
        } // end if (members.Count > 0) — sections

        // ── Step 4: Deduplicate and create nodes ──────────────────────
        // Collect member endpoints and plate corner points
        var allPoints = new List<SgPoint3D>(members.Count * 2 + effectivePlates.Count * 4);
        foreach (var m in members)
        {
            allPoints.Add(m.Start);
            allPoints.Add(m.End);
        }

        allPoints.AddRange(effectivePlates.SelectMany(p => p.Nodes));

        // Identify orphan restraint points (ADR-0002) and add them to the
        // node pool so they get created as standalone nodes.
        var effectiveRestraints = restraints ?? Array.Empty<SgRestraintData>();
        var effectiveNodeLoads = nodeLoads ?? Array.Empty<SgNodeLoadData>();
        var effectiveLumpedMassLoadsForOrphan = lumpedMassLoads ?? Array.Empty<SgLumpedMassLoadData>();
        var effectivePrescribedDisplacementsForOrphan = prescribedDisplacements ?? Array.Empty<SgPrescribedDisplacementData>();
        var effectiveConstraintsForOrphan = nodeConstraints ?? Array.Empty<SgNodeConstraintData>();

        if (effectiveRestraints.Count > 0 || effectiveNodeLoads.Count > 0
            || effectiveLumpedMassLoadsForOrphan.Count > 0
            || effectivePrescribedDisplacementsForOrphan.Count > 0
            || effectiveConstraintsForOrphan.Count > 0)
        {
            // Build a temporary grid from member endpoints for coincidence check
            var (_, memberGrid) = DeduplicatePoints(allPoints, tolerance);

            void CheckOrphan(SgPoint3D point, string description)
            {
                if (IsPointInGrid(point, memberGrid, tolerance)) return;
                allPoints.Add(point);
                // Add to grid incrementally instead of rebuilding the entire grid
                var key = QuantiseKey(point, tolerance);
                memberGrid.TryAdd(key, point);
                result.Warnings.Add(
                    $"Orphan {description} ({point.X:F3}, {point.Y:F3}, {point.Z:F3}) " +
                    "does not coincide with any member endpoint — a standalone node was created.");
            }

            foreach (var r in effectiveRestraints)
                CheckOrphan(r.Point, "restraint point");

            foreach (var nl in effectiveNodeLoads)
                CheckOrphan(nl.Point, "node load point");

            foreach (var lm in effectiveLumpedMassLoadsForOrphan)
                CheckOrphan(lm.Point, "lumped mass load point");

            foreach (var pd in effectivePrescribedDisplacementsForOrphan)
                CheckOrphan(pd.Point, "prescribed displacement point");

            foreach (var nc in effectiveConstraintsForOrphan)
            {
                CheckOrphan(nc.SlavePoint, "constraint slave point");
                CheckOrphan(nc.MasterPoint, "constraint master point");
            }
        }

        // Collect direction node points (ADR-0010) — these need to be created
        // as SpaceGass nodes so they can be referenced by MemberCreate.Direction.DirNode
        foreach (var m in members)
            if (m.Direction?.NodePoint != null)
                allPoints.Add(m.Direction.NodePoint.Value);

        var (uniquePoints, pointToCanonical) = DeduplicatePoints(allPoints, tolerance);

        var nodeCreates = uniquePoints
            .Select(p => new NodeCreate { X = p.X, Y = p.Y, Z = p.Z })
            .ToList();

        List<Node> createdNodes;
        try
        {
            createdNodes = await api.CreateNodesAsync(nodeCreates, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FormatApiError(ex, "creating nodes"), ex);
        }

        ValidateBulkResult(createdNodes.Count, uniquePoints.Count, "nodes");

        for (var i = 0; i < uniquePoints.Count; i++)
            model.NodeMap[uniquePoints[i]] = createdNodes[i].Id!.Value;

        // ── Step 5: Create members (only if members exist) ────────────
        if (members.Count > 0)
        {
            var memberCreates = new List<MemberCreate>(members.Count);
            foreach (var m in members)
            {
                var startCanonical = pointToCanonical[QuantiseKey(m.Start, tolerance)];
                var endCanonical = pointToCanonical[QuantiseKey(m.End, tolerance)];
                var startNode = model.NodeMap[startCanonical];
                var endNode = model.NodeMap[endCanonical];
                var sectionId = model.SectionMap[m.Section.Key];
                var materialId = model.MaterialMap[m.Material.Key];

                var create = new MemberCreate
                {
                    NodeA = startNode,
                    NodeB = endNode,
                    Section = sectionId,
                    Material = materialId,
                    Type = m.Type
                };

                // Map releases to API model (ADR-0013)
                // Skip when both ends are fully fixed — no structural effect
                var hasEffectiveA = m.ReleaseA != null && !m.ReleaseA.IsFullyFixed;
                var hasEffectiveB = m.ReleaseB != null && !m.ReleaseB.IsFullyFixed;
                if (hasEffectiveA || hasEffectiveB)
                    create.Releases = new MemberReleaseUpdate
                    {
                        FixityCodeAtA = m.ReleaseA?.ReleaseCode,
                        FixityCodeAtB = m.ReleaseB?.ReleaseCode,
                        TxStiffnessAtA = m.ReleaseA?.KTx,
                        TyStiffnessAtA = m.ReleaseA?.KTy,
                        TzStiffnessAtA = m.ReleaseA?.KTz,
                        RxStiffnessAtA = m.ReleaseA?.KRx,
                        RyStiffnessAtA = m.ReleaseA?.KRy,
                        RzStiffnessAtA = m.ReleaseA?.KRz,
                        TxStiffnessAtB = m.ReleaseB?.KTx,
                        TyStiffnessAtB = m.ReleaseB?.KTy,
                        TzStiffnessAtB = m.ReleaseB?.KTz,
                        RxStiffnessAtB = m.ReleaseB?.KRx,
                        RyStiffnessAtB = m.ReleaseB?.KRy,
                        RzStiffnessAtB = m.ReleaseB?.KRz
                    };

                // Map direction to API model (ADR-0010)
                // Skip when direction is default (angle=0, axis=NA, no node)
                if (m.Direction != null && !m.Direction.IsDefault)
                {
                    create.Direction = new DirectionUpdate
                    {
                        DirAngle = m.Direction.Angle,
                        DirAxis = m.Direction.Axis
                    };

                    // Resolve direction node point to node ID
                    if (m.Direction.NodePoint != null)
                    {
                        var dirCanonical = ResolveCanonicalPoint(
                            m.Direction.NodePoint.Value, pointToCanonical, tolerance);
                        create.Direction.DirNode = model.NodeMap[dirCanonical];
                    }
                }

                memberCreates.Add(create);
            }

            List<Member> createdMembers;
            try
            {
                createdMembers = await api.CreateMembersAsync(memberCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating members"), ex);
            }

            ValidateBulkResult(createdMembers.Count, members.Count, "members");

            for (var i = 0; i < members.Count; i++)
            {
                var id = createdMembers[i].Id!.Value;
                model.MemberMap[id] = (members[i].Start, members[i].End);
            }

            // ── Step 5b: Create member offsets ─────────────────────────────────
            var offsetCreates = new List<MemberOffsetCreate>();
            for (var i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m.Offset == null || m.Offset.IsZero) continue;
                var memberId = createdMembers[i].Id!.Value;
                offsetCreates.Add(new MemberOffsetCreate
                {
                    Member = memberId,
                    XOffsetAtA = m.Offset.XOffsetAtA,
                    YOffsetAtA = m.Offset.YOffsetAtA,
                    ZOffsetAtA = m.Offset.ZOffsetAtA,
                    XOffsetAtB = m.Offset.XOffsetAtB,
                    YOffsetAtB = m.Offset.YOffsetAtB,
                    ZOffsetAtB = m.Offset.ZOffsetAtB,
                    Axes = m.Offset.Axes
                });
            }

            if (offsetCreates.Count > 0)
            {
                List<MemberOffset> createdOffsets;
                try
                {
                    createdOffsets = await api.CreateMemberOffsetsAsync(offsetCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating member offsets"), ex);
                }

                ValidateBulkResult(createdOffsets.Count, offsetCreates.Count, "member offsets");
            }
        } // end if (members.Count > 0) — members + offsets

        // ── Step 5c: Create plates ────────────────────────────────────────
        if (effectivePlates.Count > 0)
        {
            var plateCreates = new List<PlateCreate>(effectivePlates.Count);
            foreach (var p in effectivePlates)
            {
                var nodeIds = new int[p.Nodes.Length];
                for (var j = 0; j < p.Nodes.Length; j++)
                {
                    var canonical = ResolveCanonicalPoint(p.Nodes[j], pointToCanonical, tolerance);
                    nodeIds[j] = model.NodeMap[canonical];
                }

                var materialId = model.MaterialMap[p.Material.Key];

                var create = new PlateCreate
                {
                    NodeA = nodeIds[0],
                    NodeB = nodeIds[1],
                    NodeC = nodeIds[2],
                    NodeD = p.Nodes.Length == 4 ? nodeIds[3] : null,
                    Material = materialId,
                    ActualThickness = p.ActualThickness,
                    BendingThickness = p.BendingThickness,
                    MembraneThickness = p.MembraneThickness,
                    ShearThickness = p.ShearThickness,
                    Offset = p.Offset,
                    Theory = p.Theory
                };

                plateCreates.Add(create);
            }

            List<Plate> createdPlates;
            try
            {
                createdPlates = await api.CreatePlatesAsync(plateCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating plates"), ex);
            }

            ValidateBulkResult(createdPlates.Count, effectivePlates.Count, "plates");

            for (var i = 0; i < effectivePlates.Count; i++)
            {
                var plateId = createdPlates[i].Id!.Value;
                model.PlateMap[plateId] = effectivePlates[i].Nodes;
            }
        }

        // ── Step 6: Create restraints ───────────────────────────────────
        if (effectiveRestraints.Count > 0)
        {
            var restraintCreates = new List<NodeRestraintCreate>(effectiveRestraints.Count);
            var seenNodes = new HashSet<int>();
            var duplicateNodeIds = new HashSet<int>();

            foreach (var r in effectiveRestraints)
            {
                var canonical = ResolveCanonicalPoint(r.Point, pointToCanonical, tolerance);
                var nodeId = model.NodeMap[canonical];
                var create = new NodeRestraintCreate
                {
                    Node = nodeId,
                    RestraintCode = r.RestraintCode
                };

                // Map spring stiffness values
                if (r.Stiffness != null)
                {
                    create.TxStiffness = r.Stiffness.KTx;
                    create.TyStiffness = r.Stiffness.KTy;
                    create.TzStiffness = r.Stiffness.KTz;
                    create.RxStiffness = r.Stiffness.KRx;
                    create.RyStiffness = r.Stiffness.KRy;
                    create.RzStiffness = r.Stiffness.KRz;
                }

                // Map friction parameters
                if (r.Friction != null)
                {
                    if (r.Friction.X != null)
                    {
                        create.XFrictionFactor = r.Friction.X.Factor;
                        create.XFrictionNormalAxis = r.Friction.X.NormalAxis;
                        create.XFrictionNormalDirection = r.Friction.X.NormalDirection;
                    }

                    if (r.Friction.Y != null)
                    {
                        create.YFrictionFactor = r.Friction.Y.Factor;
                        create.YFrictionNormalAxis = r.Friction.Y.NormalAxis;
                        create.YFrictionNormalDirection = r.Friction.Y.NormalDirection;
                    }

                    if (r.Friction.Z != null)
                    {
                        create.ZFrictionFactor = r.Friction.Z.Factor;
                        create.ZFrictionNormalAxis = r.Friction.Z.NormalAxis;
                        create.ZFrictionNormalDirection = r.Friction.Z.NormalDirection;
                    }
                }

                restraintCreates.Add(create);

                if (!seenNodes.Add(nodeId))
                    duplicateNodeIds.Add(nodeId);

                // Last-write wins for the map when multiple restraints target the same point
                model.RestraintMap[canonical] = r.RestraintCode;
            }

            if (duplicateNodeIds.Count > 0)
                result.Warnings.Add(
                    $"Multiple restraints target the same node(s) (node IDs: {string.Join(", ", duplicateNodeIds)}). " +
                    "Only the last restraint per node will take effect.");

            List<NodeRestraint> createdRestraints;
            try
            {
                createdRestraints = await api.CreateNodeRestraintsAsync(restraintCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating restraints"), ex);
            }

            ValidateBulkResult(createdRestraints.Count, effectiveRestraints.Count, "restraints");
        }

        // ── Step 6b: Create node constraints ──────────────────────────────
        var effectiveConstraints = nodeConstraints ?? Array.Empty<SgNodeConstraintData>();
        if (effectiveConstraints.Count > 0)
        {
            var constraintCreates = new List<NodeConstraintCreate>(effectiveConstraints.Count);
            var seenSlaveNodes = new HashSet<int>();
            var duplicateSlaveNodeIds = new HashSet<int>();

            foreach (var nc in effectiveConstraints)
            {
                var slaveCanonical = ResolveCanonicalPoint(nc.SlavePoint, pointToCanonical, tolerance);
                var masterCanonical = ResolveCanonicalPoint(nc.MasterPoint, pointToCanonical, tolerance);
                var slaveNodeId = model.NodeMap[slaveCanonical];
                var masterNodeId = model.NodeMap[masterCanonical];

                var create = new NodeConstraintCreate
                {
                    SlaveNode = slaveNodeId,
                    MasterNode = masterNodeId,
                    ConstraintCode = nc.ConstraintCode,
                    Axes = nc.Axes
                };

                if (nc.XVector != null) create.XVector = nc.XVector;
                if (nc.YVector != null) create.YVector = nc.YVector;
                if (nc.ZVector != null) create.ZVector = nc.ZVector;

                constraintCreates.Add(create);

                if (!seenSlaveNodes.Add(slaveNodeId))
                    duplicateSlaveNodeIds.Add(slaveNodeId);
            }

            if (duplicateSlaveNodeIds.Count > 0)
                result.Warnings.Add(
                    $"Multiple constraints target the same slave node(s) (node IDs: {string.Join(", ", duplicateSlaveNodeIds)}). " +
                    "Only the last constraint per slave node will take effect.");

            List<NodeConstraint> createdConstraints;
            try
            {
                createdConstraints = await api.CreateNodeConstraintsAsync(constraintCreates, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(FormatApiError(ex, "creating node constraints"), ex);
            }

            ValidateBulkResult(createdConstraints.Count, effectiveConstraints.Count, "node constraints");

            model.ConstraintCount = createdConstraints.Count;
        }

        // ── Step 7, 7b, 7c, 8, 9, 10: Create load cases, combinations, categories, and all load types ──
        var effectiveMemberDistLoads = memberDistributedLoads ?? Array.Empty<SgMemberDistributedLoadData>();
        var effectiveSelfWeightLoads = selfWeightLoads ?? Array.Empty<SgSelfWeightLoadData>();
        var effectiveCombinations = combinationLoadCases ?? Array.Empty<SgCombinationLoadCaseData>();
        var effectiveLumpedMassLoads = lumpedMassLoads ?? Array.Empty<SgLumpedMassLoadData>();
        var effectivePrescribedDisplacements = prescribedDisplacements ?? Array.Empty<SgPrescribedDisplacementData>();
        var effectiveMemberConcLoads = memberConcentratedLoads ?? Array.Empty<SgMemberConcentratedLoadData>();
        var effectiveMemberPrestressLoads = memberPrestressLoads ?? Array.Empty<SgMemberPrestressLoadData>();
        var effectivePlatePressureLoads = platePressureLoads ?? Array.Empty<SgPlatePressureLoadData>();
        var effectiveThermalLoads = thermalLoads ?? Array.Empty<SgThermalLoadData>();
        var effectiveMovingLoadScenarios = movingLoadScenarios ?? Array.Empty<SgMovingLoadScenarioData>();

        // Moving-load resources (vehicles, pressures, travel paths) are discovered by walking
        // each scenario's Loads[] rather than being fed as separate lists — scenarios are the
        // single entry point for the moving-load workflow. Reference-equal duplicates (same
        // resource shared across multiple loads / scenarios) are silently collapsed; key-equal
        // duplicates with different instances still warn via the existing DeduplicateByKey step.
        var effectiveMovingLoadVehicles = new List<SgMovingLoadVehicleData>();
        var effectiveMovingLoadPressures = new List<SgMovingLoadPressureData>();
        var effectiveMovingLoadTravelPaths = new List<SgMovingLoadTravelPathData>();
        {
            var seenVehicleRefs = new HashSet<SgMovingLoadVehicleData>();
            var seenPressureRefs = new HashSet<SgMovingLoadPressureData>();
            var seenPathRefs = new HashSet<SgMovingLoadTravelPathData>();
            foreach (var scen in effectiveMovingLoadScenarios)
            foreach (var load in scen.Loads)
            {
                if (load.Vehicle != null && seenVehicleRefs.Add(load.Vehicle))
                    effectiveMovingLoadVehicles.Add(load.Vehicle);
                if (load.Pressure != null && seenPressureRefs.Add(load.Pressure))
                    effectiveMovingLoadPressures.Add(load.Pressure);
                if (seenPathRefs.Add(load.TravelPath))
                    effectiveMovingLoadTravelPaths.Add(load.TravelPath);
            }
        }

        var hasAnyLoads = effectiveNodeLoads.Count > 0
                          || effectiveMemberDistLoads.Count > 0
                          || effectiveSelfWeightLoads.Count > 0
                          || effectiveCombinations.Count > 0
                          || effectiveLumpedMassLoads.Count > 0
                          || effectivePrescribedDisplacements.Count > 0
                          || effectiveMemberConcLoads.Count > 0
                          || effectiveMemberPrestressLoads.Count > 0
                          || effectivePlatePressureLoads.Count > 0
                          || effectiveThermalLoads.Count > 0
                          || effectiveMovingLoadScenarios.Count > 0;

        // Member lookup for member-based loads (distributed, concentrated, prestress, thermal)
        Dictionary<(int, int), int>? memberLookup = null;

        if (hasAnyLoads)
        {
            // Warn about zero loads
            foreach (var nl in effectiveNodeLoads)
                if (nl.IsZero)
                {
                    var pt = nl.Point;
                    result.Warnings.Add(
                        $"Node load at ({pt.X:F3}, {pt.Y:F3}, {pt.Z:F3}) has zero force and moment — " +
                        "it will have no effect on the analysis.");
                }

            foreach (var dl in effectiveMemberDistLoads)
                if (dl.IsZero)
                    result.Warnings.Add(
                        $"Member distributed load on member ({dl.MemberStart.X:F3}, {dl.MemberStart.Y:F3}, {dl.MemberStart.Z:F3}) → " +
                        $"({dl.MemberEnd.X:F3}, {dl.MemberEnd.Y:F3}, {dl.MemberEnd.Z:F3}) has zero force and moment — " +
                        "it will have no effect on the analysis.");

            foreach (var cl in effectiveMemberConcLoads)
                if (cl.IsZero)
                    result.Warnings.Add(
                        $"Member concentrated load on member ({cl.MemberStart.X:F3}, {cl.MemberStart.Y:F3}, {cl.MemberStart.Z:F3}) → " +
                        $"({cl.MemberEnd.X:F3}, {cl.MemberEnd.Y:F3}, {cl.MemberEnd.Z:F3}) has zero force and moment — " +
                        "it will have no effect on the analysis.");

            foreach (var pl in effectiveMemberPrestressLoads)
                if (pl.IsZero)
                    result.Warnings.Add(
                        $"Member prestress load on member ({pl.MemberStart.X:F3}, {pl.MemberStart.Y:F3}, {pl.MemberStart.Z:F3}) → " +
                        $"({pl.MemberEnd.X:F3}, {pl.MemberEnd.Y:F3}, {pl.MemberEnd.Z:F3}) has zero prestress — " +
                        "it will have no effect on the analysis.");

            foreach (var pp in effectivePlatePressureLoads)
                if (pp.IsZero)
                    result.Warnings.Add(
                        "Plate pressure load has zero pressure — it will have no effect on the analysis.");

            foreach (var tl in effectiveThermalLoads)
                if (tl.IsZero)
                    result.Warnings.Add(
                        "Thermal load has zero temperature and gradients — it will have no effect on the analysis.");

            foreach (var sw in effectiveSelfWeightLoads)
                if (sw.IsZero)
                    result.Warnings.Add(
                        "Self-weight load has zero acceleration — it will have no effect on the analysis.");

            foreach (var lm in effectiveLumpedMassLoads)
                if (lm.IsZero)
                {
                    var pt = lm.Point;
                    result.Warnings.Add(
                        $"Lumped mass load at ({pt.X:F3}, {pt.Y:F3}, {pt.Z:F3}) has zero mass — " +
                        "it will have no effect on the analysis.");
                }

            foreach (var pd in effectivePrescribedDisplacements)
                if (pd.IsZero)
                {
                    var pt = pd.Point;
                    result.Warnings.Add(
                        $"Prescribed displacement at ({pt.X:F3}, {pt.Y:F3}, {pt.Z:F3}) has zero displacement — " +
                        "it will have no effect on the analysis.");
                }

            // Step 7: Collect and deduplicate load cases from ALL sources
            var allLoadCases = new List<SgLoadCaseData>();
            allLoadCases.AddRange(effectiveNodeLoads.Select(nl => nl.LoadCase));
            allLoadCases.AddRange(effectiveMemberDistLoads.Select(dl => dl.LoadCase));
            allLoadCases.AddRange(effectiveSelfWeightLoads.Select(sw => sw.LoadCase));
            allLoadCases.AddRange(effectiveLumpedMassLoads.Select(lm => lm.LoadCase));
            allLoadCases.AddRange(effectivePrescribedDisplacements.Select(pd => pd.LoadCase));
            allLoadCases.AddRange(effectiveMemberConcLoads.Select(cl => cl.LoadCase));
            allLoadCases.AddRange(effectiveMemberPrestressLoads.Select(pl => pl.LoadCase));
            allLoadCases.AddRange(effectivePlatePressureLoads.Select(pp => pp.LoadCase));
            allLoadCases.AddRange(effectiveThermalLoads.Select(tl => tl.LoadCase));
            // Also collect primary load case constituents from combinations (not combination references)
            foreach (var combo in effectiveCombinations)
                allLoadCases.AddRange(combo.Constituents
                    .Where(c => !c.IsCombinationReference)
                    .Select(c => c.LoadCase!));
            // Moving load scenarios: starting LC + primary LCs referenced by combination entries
            foreach (var scen in effectiveMovingLoadScenarios)
            {
                if (scen.StartingLoadCase != null)
                    allLoadCases.Add(scen.StartingLoadCase);
                foreach (var entry in scen.Combinations)
                    if (!entry.IsCombinationReference)
                        allLoadCases.Add(entry.LoadCase!);
            }

            var uniqueLoadCases = DeduplicateByKey(
                allLoadCases, lc => lc.Key,
                "load case", result.Warnings);

            if (uniqueLoadCases.Count > 0)
            {
                var loadCaseCreates = uniqueLoadCases
                    .Select(lc => new LoadCaseCreate { Title = lc.Name, Notes = lc.Notes })
                    .ToList();

                List<LoadCase> createdLoadCases;
                try
                {
                    createdLoadCases = await api.CreateLoadCasesAsync(loadCaseCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(FormatApiError(ex, "creating load cases"), ex);
                }

                ValidateBulkResult(createdLoadCases.Count, uniqueLoadCases.Count, "load cases");

                for (var i = 0; i < uniqueLoadCases.Count; i++)
                    model.LoadCaseMap[uniqueLoadCases[i].Key] = createdLoadCases[i].Id!.Value;
            }

            // Step 7c: Create combination load cases (after primaries — they reference IDs)
            // Combinations may reference other combinations, so we create them in topological order.
            if (effectiveCombinations.Count > 0)
            {
                var uniqueCombinations = DeduplicateByKey(
                    effectiveCombinations, c => c.Key,
                    "combination load case", result.Warnings);

                // Topological sort: combinations with no combination dependencies first,
                // then those depending on already-created combinations.
                var remaining = new List<SgCombinationLoadCaseData>(uniqueCombinations);
                var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (remaining.Count > 0)
                {
                    var batch = remaining
                        .Where(c => c.Constituents
                            .Where(x => x.IsCombinationReference)
                            .All(x => created.Contains(x.CombinationLoadCase!.Key)))
                        .ToList();

                    if (batch.Count == 0)
                    {
                        // Circular dependency or unresolvable references — warn and break
                        foreach (var c in remaining)
                            result.Warnings.Add(
                                $"Combination '{c.Name}' has unresolvable combination references " +
                                "(possible circular dependency) — skipped.");
                        break;
                    }

                    var comboCreates = new List<CombinationLoadCaseCreate>(batch.Count);
                    foreach (var combo in batch)
                    {
                        var items = new List<CombinationLoadCaseItem>();
                        foreach (var constituent in combo.Constituents)
                        {
                            int? resolvedId = null;

                            if (constituent.IsCombinationReference)
                            {
                                // Look up in CombinationLoadCaseMap
                                if (model.CombinationLoadCaseMap.TryGetValue(constituent.Key, out var comboId))
                                    resolvedId = comboId;
                            }
                            else
                            {
                                // Look up in LoadCaseMap (primary)
                                if (model.LoadCaseMap.TryGetValue(constituent.Key, out var lcId))
                                    resolvedId = lcId;
                            }

                            if (resolvedId == null)
                            {
                                result.Warnings.Add(
                                    $"Combination '{combo.Name}' references load case '{constituent.Name}' " +
                                    "which was not found — constituent skipped.");
                                continue;
                            }

                            items.Add(new CombinationLoadCaseItem
                            {
                                LoadCase = resolvedId.Value,
                                MultiplyingFactor = constituent.Factor
                            });
                        }

                        comboCreates.Add(new CombinationLoadCaseCreate
                        {
                            Title = combo.Name,
                            Notes = combo.Notes,
                            CombinationItems = items
                        });
                    }

                    List<LoadCase> createdCombinations;
                    try
                    {
                        createdCombinations = await api.CreateCombinationLoadCasesAsync(comboCreates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating combination load cases"), ex);
                    }

                    ValidateBulkResult(createdCombinations.Count, batch.Count,
                        "combination load cases");

                    for (var i = 0; i < batch.Count; i++)
                    {
                        model.CombinationLoadCaseMap[batch[i].Key] =
                            createdCombinations[i].Id!.Value;
                        created.Add(batch[i].Key);
                    }

                    foreach (var c in batch)
                        remaining.Remove(c);
                }
            }

            // Step 7d-vehicles: Create moving load vehicles (before scenarios so scenario
            // Loads[] entries — populated in a future release — can resolve vehicle IDs)
            if (effectiveMovingLoadVehicles.Count > 0)
            {
                var uniqueVehicles = DeduplicateByKey(
                    effectiveMovingLoadVehicles, v => v.Key,
                    "moving load vehicle", result.Warnings);

                var libraryVehicles = uniqueVehicles.Where(v => v.IsLibrary).ToList();
                var userVehicles = uniqueVehicles.Where(v => !v.IsLibrary).ToList();

                foreach (var lv in libraryVehicles)
                {
                    MovingLoadVehicle created;
                    try
                    {
                        created = await api.CreateMovingLoadVehicleFromLibraryAsync(
                            new MovingLoadVehicleLibraryCreate { Library = lv.Library, Name = lv.Name },
                            ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating moving load vehicles"), ex);
                    }

                    if (created.Id == null)
                        throw new InvalidOperationException(
                            $"SpaceGass returned no ID for library moving load vehicle '{lv.Key}'.");
                    model.MovingLoadVehicleMap[lv.Key] = created.Id!.Value;
                }

                if (userVehicles.Count > 0)
                {
                    var creates = userVehicles.Select(v => new MovingLoadVehicleCreate
                    {
                        Name = v.Name,
                        LoadUnits = new VehicleLoadUnits
                        {
                            Force = v.ForceUnit,
                            Length = v.LengthUnit,
                            Moment = v.MomentUnit
                        },
                        Loads = v.WheelLoads.Select(w => new VehicleWheelLoad
                        {
                            X = w.X, Y = w.Y,
                            Fx = w.Fx, Fy = w.Fy, Fz = w.Fz,
                            Mx = w.Mx, My = w.My, Mz = w.Mz
                        }).ToList()
                    }).ToList();

                    List<MovingLoadVehicle> createdVehicles;
                    try
                    {
                        createdVehicles = await api.CreateMovingLoadVehiclesFromUserAsync(creates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating moving load vehicles"), ex);
                    }

                    ValidateBulkResult(createdVehicles.Count, userVehicles.Count, "moving load vehicles");

                    for (var i = 0; i < userVehicles.Count; i++)
                        model.MovingLoadVehicleMap[userVehicles[i].Key] =
                            createdVehicles[i].Id!.Value;
                }
            }

            // Step 7d-pressures: Create moving load pressures (before scenarios)
            if (effectiveMovingLoadPressures.Count > 0)
            {
                var uniquePressures = DeduplicateByKey(
                    effectiveMovingLoadPressures, p => p.Key,
                    "moving load pressure", result.Warnings);

                var creates = uniquePressures.Select(p => new MovingLoadPressureCreate
                {
                    Name = p.Name,
                    Width = p.Width,
                    Length = p.Length,
                    LoadSpacing = p.LoadSpacing,
                    Px = p.Px,
                    Py = p.Py,
                    Pz = p.Pz
                }).ToList();

                List<MovingLoadPressure> createdPressures;
                try
                {
                    createdPressures = await api.CreateMovingLoadPressuresAsync(creates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating moving load pressures"), ex);
                }

                ValidateBulkResult(createdPressures.Count, uniquePressures.Count, "moving load pressures");

                for (var i = 0; i < uniquePressures.Count; i++)
                    model.MovingLoadPressureMap[uniquePressures[i].Key] =
                        createdPressures[i].Id!.Value;
            }

            // Step 7d-paths: Create moving load travel paths (bulk create name-only, then per-path
            // stations PUT — the SpaceGass API separates the two). Runs before scenarios so
            // scenario Loads[] entries — populated in a future release — can resolve path IDs.
            if (effectiveMovingLoadTravelPaths.Count > 0)
            {
                var uniquePaths = DeduplicateByKey(
                    effectiveMovingLoadTravelPaths, p => p.Key,
                    "moving load travel path", result.Warnings);

                var pathCreates = uniquePaths
                    .Select(p => new MovingLoadTravelPathCreate { Name = p.Name })
                    .ToList();

                List<MovingLoadTravelPath> createdPaths;
                try
                {
                    createdPaths = await api.CreateMovingLoadTravelPathsAsync(pathCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating moving load travel paths"), ex);
                }

                ValidateBulkResult(createdPaths.Count, uniquePaths.Count, "moving load travel paths");

                for (var i = 0; i < uniquePaths.Count; i++)
                    model.MovingLoadTravelPathMap[uniquePaths[i].Key] = createdPaths[i].Id!.Value;

                // Push stations per path — the stations endpoint is not bulk.
                for (var i = 0; i < uniquePaths.Count; i++)
                {
                    var path = uniquePaths[i];
                    var pathId = createdPaths[i].Id!.Value;

                    var stationPayloads = path.Stations.Select(s => new MovingLoadStation
                    {
                        // NodeKey is a station identifier local to the travel path — SpaceGass
                        // assigns it; we leave it null and always send absolute coordinates.
                        NodeKey = null,
                        X = s.Position.X,
                        Y = s.Position.Y,
                        Z = s.Position.Z,
                        Radius = s.Radius
                    }).ToList();

                    try
                    {
                        await api.SetMovingLoadTravelPathStationsAsync(pathId, stationPayloads, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, $"setting stations on moving load travel path '{path.Name}'"), ex);
                    }
                }
            }

            // Step 7d: Create moving load scenarios (after primary + combination load cases —
            // scenarios may reference either kind via their Combinations entries and StartingLoadCase)
            if (effectiveMovingLoadScenarios.Count > 0)
            {
                var uniqueScenarios = DeduplicateByKey(
                    effectiveMovingLoadScenarios, s => s.Key,
                    "moving load scenario", result.Warnings);

                var scenarioCreates = new List<MovingLoadScenarioCreate>(uniqueScenarios.Count);
                foreach (var scen in uniqueScenarios)
                {
                    int? startingLoadCaseId = null;
                    if (scen.StartingLoadCase != null &&
                        model.LoadCaseMap.TryGetValue(scen.StartingLoadCase.Key, out var startId))
                        startingLoadCaseId = startId;

                    var combinations = new List<MovingLoadCombination>(scen.Combinations.Count);
                    foreach (var entry in scen.Combinations)
                    {
                        int? refId = null;
                        if (entry.IsCombinationReference)
                        {
                            if (model.CombinationLoadCaseMap.TryGetValue(entry.Key, out var comboId))
                                refId = comboId;
                            else
                                result.Warnings.Add(
                                    $"Moving load scenario '{scen.Name}' references combination load case " +
                                    $"'{entry.Name}' which was not created — combination entry skipped.");
                        }
                        else
                        {
                            if (model.LoadCaseMap.TryGetValue(entry.Key, out var lcId))
                                refId = lcId;
                            else
                                result.Warnings.Add(
                                    $"Moving load scenario '{scen.Name}' references load case " +
                                    $"'{entry.Name}' which was not created — combination entry skipped.");
                        }

                        if (refId == null) continue;

                        combinations.Add(new MovingLoadCombination
                        {
                            CombineWithLoadCase = refId,
                            LoadCaseFactor = entry.LoadCaseFactor,
                            ScenarioFactor = entry.ScenarioFactor,
                            StartingCombinationCase = entry.StartingCombinationCase
                        });
                    }

                    var create = new MovingLoadScenarioCreate
                    {
                        Name = scen.Name,
                        Include = scen.Include,
                        StartingLoadCase = startingLoadCaseId,
                        TimeInterval = scen.TimeInterval,
                        Combinations = combinations,
                        Loads = new List<MovingLoadScenarioLoad>()
                    };
                    scenarioCreates.Add(create);
                }

                List<MovingLoadScenario> createdScenarios;
                try
                {
                    createdScenarios = await api.CreateMovingLoadScenariosAsync(scenarioCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating moving load scenarios"), ex);
                }

                ValidateBulkResult(createdScenarios.Count, uniqueScenarios.Count,
                    "moving load scenarios");

                for (var i = 0; i < uniqueScenarios.Count; i++)
                    model.MovingLoadScenarioMap[uniqueScenarios[i].Key] =
                        createdScenarios[i].Id!.Value;

                // Step 7d-scenario-loads: PUT each scenario's Loads[] with resolved
                // vehicle / pressure / travel-path IDs. Empty scenarios are warned and skipped.
                foreach (var scen in uniqueScenarios)
                {
                    if (scen.Loads.Count == 0)
                    {
                        result.Warnings.Add(
                            $"Moving load scenario '{scen.Name}' has no moving loads and will " +
                            "produce no generated load cases.");
                        continue;
                    }

                    var scenarioId = model.MovingLoadScenarioMap[scen.Key];
                    var loadPayloads = new List<MovingLoadScenarioLoad>(scen.Loads.Count);
                    foreach (var load in scen.Loads)
                    {
                        var payload = new MovingLoadScenarioLoad
                        {
                            LoadType = load.LoadType,
                            Speed = load.Speed,
                            StartPosition = load.StartPosition,
                            Delay = load.Delay,
                            LoadFactor = load.LoadFactor,
                            LaneFactor = load.LaneFactor,
                            DynamicFactor = load.DynamicFactor,
                            GenerateStationaryLc = load.GenerateStationaryLc
                        };

                        if (load.Vehicle != null &&
                            model.MovingLoadVehicleMap.TryGetValue(load.Vehicle.Key, out var vehicleId))
                            payload.VehicleId = vehicleId;
                        if (load.Pressure != null &&
                            model.MovingLoadPressureMap.TryGetValue(load.Pressure.Key, out var pressureId))
                            payload.PressureId = pressureId;
                        if (model.MovingLoadTravelPathMap.TryGetValue(load.TravelPath.Key, out var pathId))
                            payload.TravelPathId = pathId;

                        loadPayloads.Add(payload);
                    }

                    try
                    {
                        await api.SetMovingLoadScenarioLoadsAsync(scenarioId, loadPayloads, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex,
                                $"setting moving loads on scenario '{scen.Name}'"), ex);
                    }
                }
            }

            // Step 7b: Collect and deduplicate load categories from ALL sources
            var allCategories = new List<SgLoadCategoryData>();
            allCategories.AddRange(
                effectiveNodeLoads.Where(nl => nl.LoadCategory != null).Select(nl => nl.LoadCategory!));
            allCategories.AddRange(
                effectiveMemberDistLoads.Where(dl => dl.LoadCategory != null).Select(dl => dl.LoadCategory!));
            allCategories.AddRange(
                effectiveSelfWeightLoads.Where(sw => sw.LoadCategory != null).Select(sw => sw.LoadCategory!));
            allCategories.AddRange(
                effectiveLumpedMassLoads.Where(lm => lm.LoadCategory != null).Select(lm => lm.LoadCategory!));
            allCategories.AddRange(
                effectivePrescribedDisplacements.Where(pd => pd.LoadCategory != null).Select(pd => pd.LoadCategory!));
            allCategories.AddRange(
                effectiveMemberConcLoads.Where(cl => cl.LoadCategory != null).Select(cl => cl.LoadCategory!));
            allCategories.AddRange(
                effectiveMemberPrestressLoads.Where(pl => pl.LoadCategory != null).Select(pl => pl.LoadCategory!));
            allCategories.AddRange(
                effectivePlatePressureLoads.Where(pp => pp.LoadCategory != null).Select(pp => pp.LoadCategory!));
            allCategories.AddRange(
                effectiveThermalLoads.Where(tl => tl.LoadCategory != null).Select(tl => tl.LoadCategory!));

            if (allCategories.Count > 0)
            {
                var uniqueCategories = DeduplicateByKey(
                    allCategories, cat => cat.Key,
                    "load category", result.Warnings);

                var categoryCreates = uniqueCategories
                    .Select(cat => new LoadCategoryCreate { Title = cat.Name, Notes = cat.Notes })
                    .ToList();

                List<LoadCategory> createdCategories;
                try
                {
                    createdCategories = await api.CreateLoadCategoriesAsync(categoryCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(FormatApiError(ex, "creating load categories"), ex);
                }

                ValidateBulkResult(createdCategories.Count, uniqueCategories.Count, "load categories");

                for (var i = 0; i < uniqueCategories.Count; i++)
                    model.LoadCategoryMap[uniqueCategories[i].Key] = createdCategories[i].Id!.Value;
            }

            // Step 8: Create node loads
            if (effectiveNodeLoads.Count > 0)
            {
                var nodeLoadCreates = new List<NodeLoadCreate>(effectiveNodeLoads.Count);
                foreach (var nl in effectiveNodeLoads)
                {
                    var canonical = ResolveCanonicalPoint(nl.Point, pointToCanonical, tolerance);
                    var nodeId = model.NodeMap[canonical];
                    var loadCaseId = model.LoadCaseMap[nl.LoadCase.Key];

                    var create = new NodeLoadCreate
                    {
                        Node = nodeId,
                        LoadCase = loadCaseId,
                        Fx = nl.Fx,
                        Fy = nl.Fy,
                        Fz = nl.Fz,
                        Mx = nl.Mx,
                        My = nl.My,
                        Mz = nl.Mz
                    };

                    if (nl.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[nl.LoadCategory.Key];

                    nodeLoadCreates.Add(create);
                }

                List<NodeLoad> createdNodeLoads;
                try
                {
                    createdNodeLoads = await api.CreateNodeLoadsAsync(nodeLoadCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(FormatApiError(ex, "creating node loads"), ex);
                }

                ValidateBulkResult(createdNodeLoads.Count, effectiveNodeLoads.Count, "node loads");

                model.NodeLoadCount = createdNodeLoads.Count;
            }

            // Step 9: Create member distributed loads (forces + moments) and concentrated loads
            // Build reverse member map once — shared by distributed, concentrated, prestress, and thermal loads
            if (effectiveMemberDistLoads.Count > 0 || effectiveMemberConcLoads.Count > 0
                || effectiveMemberPrestressLoads.Count > 0
                || effectiveThermalLoads.Any(t => t.ElementType == ThermalElementType.Member))
            {
                memberLookup = new Dictionary<(int, int), int>();
                foreach (var kvp in model.MemberMap)
                {
                    var startCanonical = ResolveCanonicalPoint(kvp.Value.Start, pointToCanonical, tolerance);
                    var endCanonical = ResolveCanonicalPoint(kvp.Value.End, pointToCanonical, tolerance);
                    var startNodeId = model.NodeMap[startCanonical];
                    var endNodeId = model.NodeMap[endCanonical];
                    memberLookup[(startNodeId, endNodeId)] = kvp.Key;
                }
            }

            if (effectiveMemberDistLoads.Count > 0)
            {
                var distLoadCreates = new List<MemberDistributedLoadCreate>();
                var distMomentCreates = new List<MemberDistributedMomentCreate>();
                foreach (var dl in effectiveMemberDistLoads)
                {
                    // Resolve member endpoints to node IDs (safely — may not exist)
                    var startCanonical = TryResolveCanonicalPoint(dl.MemberStart, pointToCanonical, tolerance);
                    var endCanonical = TryResolveCanonicalPoint(dl.MemberEnd, pointToCanonical, tolerance);

                    if (startCanonical == null || endCanonical == null ||
                        !model.NodeMap.TryGetValue(startCanonical.Value, out var startNodeId) ||
                        !model.NodeMap.TryGetValue(endCanonical.Value, out var endNodeId))
                    {
                        result.Warnings.Add(
                            $"Member distributed load references endpoints that don't match any node — skipped. " +
                            $"Start: ({dl.MemberStart.X:F3}, {dl.MemberStart.Y:F3}, {dl.MemberStart.Z:F3}), " +
                            $"End: ({dl.MemberEnd.X:F3}, {dl.MemberEnd.Y:F3}, {dl.MemberEnd.Z:F3})");
                        continue;
                    }

                    if (!memberLookup!.TryGetValue((startNodeId, endNodeId), out var memberId))
                    {
                        result.Warnings.Add(
                            $"Member distributed load references a member that doesn't exist — skipped. " +
                            $"Start: ({dl.MemberStart.X:F3}, {dl.MemberStart.Y:F3}, {dl.MemberStart.Z:F3}), " +
                            $"End: ({dl.MemberEnd.X:F3}, {dl.MemberEnd.Y:F3}, {dl.MemberEnd.Z:F3})");
                        continue;
                    }

                    var loadCaseId = model.LoadCaseMap[dl.LoadCase.Key];
                    int? categoryId = dl.LoadCategory != null
                        ? model.LoadCategoryMap[dl.LoadCategory.Key]
                        : null;

                    // Create distributed force call if any forces are non-zero
                    if (dl.HasForces)
                    {
                        var create = new MemberDistributedLoadCreate
                        {
                            Member = memberId,
                            LoadCase = loadCaseId,
                            FxStart = dl.FxStart,
                            FyStart = dl.FyStart,
                            FzStart = dl.FzStart,
                            FxFinish = dl.FxEnd,
                            FyFinish = dl.FyEnd,
                            FzFinish = dl.FzEnd,
                            StartPosition = dl.StartPosition,
                            FinishPosition = dl.EndPosition,
                            PositionUnits = dl.PositionUnits,
                            Axes = dl.Axes
                        };
                        if (categoryId != null) create.LoadCategory = categoryId;
                        distLoadCreates.Add(create);
                    }

                    // Create distributed moment call if any moments are non-zero
                    if (dl.HasMoments)
                    {
                        var momentCreate = new MemberDistributedMomentCreate
                        {
                            Member = memberId,
                            LoadCase = loadCaseId,
                            MxStart = dl.MxStart,
                            MyStart = dl.MyStart,
                            MzStart = dl.MzStart,
                            MxFinish = dl.MxEnd,
                            MyFinish = dl.MyEnd,
                            MzFinish = dl.MzEnd,
                            StartPosition = dl.StartPosition,
                            FinishPosition = dl.EndPosition,
                            PositionUnits = dl.PositionUnits,
                            Axes = dl.Axes
                        };
                        if (categoryId != null) momentCreate.LoadCategory = categoryId;
                        distMomentCreates.Add(momentCreate);
                    }
                }

                if (distLoadCreates.Count > 0)
                {
                    List<MemberDistributedLoad> createdDistLoads;
                    try
                    {
                        createdDistLoads = await api.CreateMemberDistributedLoadsAsync(distLoadCreates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating member distributed loads"), ex);
                    }

                    ValidateBulkResult(createdDistLoads.Count, distLoadCreates.Count, "member distributed loads");

                    model.MemberDistributedLoadCount = createdDistLoads.Count;
                }

                if (distMomentCreates.Count > 0)
                {
                    List<MemberDistributedMoment> createdDistMoments;
                    try
                    {
                        createdDistMoments = await api.CreateMemberDistributedMomentsAsync(distMomentCreates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating member distributed moments"), ex);
                    }

                    ValidateBulkResult(createdDistMoments.Count, distMomentCreates.Count,
                        "member distributed moments");

                    model.MemberDistributedMomentCount = createdDistMoments.Count;
                }
            }

            // Step 9b: Create member concentrated loads
            if (effectiveMemberConcLoads.Count > 0)
            {
                var concLoadCreates = new List<MemberConcentratedLoadCreate>();
                foreach (var cl in effectiveMemberConcLoads)
                {
                    var startCanonical = TryResolveCanonicalPoint(cl.MemberStart, pointToCanonical, tolerance);
                    var endCanonical = TryResolveCanonicalPoint(cl.MemberEnd, pointToCanonical, tolerance);

                    if (startCanonical == null || endCanonical == null ||
                        !model.NodeMap.TryGetValue(startCanonical.Value, out var startNodeId) ||
                        !model.NodeMap.TryGetValue(endCanonical.Value, out var endNodeId))
                    {
                        result.Warnings.Add(
                            $"Member concentrated load references endpoints that don't match any node — skipped. " +
                            $"Start: ({cl.MemberStart.X:F3}, {cl.MemberStart.Y:F3}, {cl.MemberStart.Z:F3}), " +
                            $"End: ({cl.MemberEnd.X:F3}, {cl.MemberEnd.Y:F3}, {cl.MemberEnd.Z:F3})");
                        continue;
                    }

                    if (!memberLookup!.TryGetValue((startNodeId, endNodeId), out var memberId))
                    {
                        result.Warnings.Add(
                            $"Member concentrated load references a member that doesn't exist — skipped. " +
                            $"Start: ({cl.MemberStart.X:F3}, {cl.MemberStart.Y:F3}, {cl.MemberStart.Z:F3}), " +
                            $"End: ({cl.MemberEnd.X:F3}, {cl.MemberEnd.Y:F3}, {cl.MemberEnd.Z:F3})");
                        continue;
                    }

                    var loadCaseId = model.LoadCaseMap[cl.LoadCase.Key];
                    var create = new MemberConcentratedLoadCreate
                    {
                        Member = memberId,
                        LoadCase = loadCaseId,
                        Fx = cl.Fx,
                        Fy = cl.Fy,
                        Fz = cl.Fz,
                        Mx = cl.Mx,
                        My = cl.My,
                        Mz = cl.Mz,
                        Position = cl.Position,
                        PositionUnits = cl.PositionUnits,
                        Axes = cl.Axes
                    };

                    if (cl.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[cl.LoadCategory.Key];

                    concLoadCreates.Add(create);
                }

                if (concLoadCreates.Count > 0)
                {
                    List<MemberConcentratedLoad> createdConcLoads;
                    try
                    {
                        createdConcLoads = await api.CreateMemberConcentratedLoadsAsync(concLoadCreates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating member concentrated loads"), ex);
                    }

                    ValidateBulkResult(createdConcLoads.Count, concLoadCreates.Count,
                        "member concentrated loads");

                    model.MemberConcentratedLoadCount = createdConcLoads.Count;
                }
            }

            // Step 9c: Create member prestress loads
            if (effectiveMemberPrestressLoads.Count > 0)
            {
                var prestressCreates = new List<MemberPrestressLoadCreate>();
                foreach (var pl in effectiveMemberPrestressLoads)
                {
                    var startCanonical = TryResolveCanonicalPoint(pl.MemberStart, pointToCanonical, tolerance);
                    var endCanonical = TryResolveCanonicalPoint(pl.MemberEnd, pointToCanonical, tolerance);

                    if (startCanonical == null || endCanonical == null ||
                        !model.NodeMap.TryGetValue(startCanonical.Value, out var startNodeId) ||
                        !model.NodeMap.TryGetValue(endCanonical.Value, out var endNodeId))
                    {
                        result.Warnings.Add(
                            $"Member prestress load references endpoints that don't match any node — skipped. " +
                            $"Start: ({pl.MemberStart.X:F3}, {pl.MemberStart.Y:F3}, {pl.MemberStart.Z:F3}), " +
                            $"End: ({pl.MemberEnd.X:F3}, {pl.MemberEnd.Y:F3}, {pl.MemberEnd.Z:F3})");
                        continue;
                    }

                    if (!memberLookup!.TryGetValue((startNodeId, endNodeId), out var memberId))
                    {
                        result.Warnings.Add(
                            $"Member prestress load references a member that doesn't exist — skipped. " +
                            $"Start: ({pl.MemberStart.X:F3}, {pl.MemberStart.Y:F3}, {pl.MemberStart.Z:F3}), " +
                            $"End: ({pl.MemberEnd.X:F3}, {pl.MemberEnd.Y:F3}, {pl.MemberEnd.Z:F3})");
                        continue;
                    }

                    var loadCaseId = model.LoadCaseMap[pl.LoadCase.Key];
                    var create = new MemberPrestressLoadCreate
                    {
                        Member = memberId,
                        LoadCase = loadCaseId,
                        Prestress = pl.Prestress
                    };

                    if (pl.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[pl.LoadCategory.Key];

                    prestressCreates.Add(create);
                }

                if (prestressCreates.Count > 0)
                {
                    List<MemberPrestressLoad> createdPrestressLoads;
                    try
                    {
                        createdPrestressLoads = await api.CreateMemberPrestressLoadsAsync(prestressCreates, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            FormatApiError(ex, "creating member prestress loads"), ex);
                    }

                    ValidateBulkResult(createdPrestressLoads.Count, prestressCreates.Count,
                        "member prestress loads");

                    model.MemberPrestressLoadCount = createdPrestressLoads.Count;
                }
            }

            // Step 10: Create self-weight loads
            if (effectiveSelfWeightLoads.Count > 0)
            {
                var swCreates = new List<SelfWeightLoadCreate>(effectiveSelfWeightLoads.Count);
                foreach (var sw in effectiveSelfWeightLoads)
                {
                    var loadCaseId = model.LoadCaseMap[sw.LoadCase.Key];
                    var create = new SelfWeightLoadCreate
                    {
                        LoadCase = loadCaseId,
                        AccelerationX = sw.AccelerationX,
                        AccelerationY = sw.AccelerationY,
                        AccelerationZ = sw.AccelerationZ
                    };

                    if (sw.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[sw.LoadCategory.Key];

                    swCreates.Add(create);
                }

                List<SelfWeightLoad> createdSwLoads;
                try
                {
                    createdSwLoads = await api.CreateSelfWeightLoadsAsync(swCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating self-weight loads"), ex);
                }

                ValidateBulkResult(createdSwLoads.Count, effectiveSelfWeightLoads.Count, "self-weight loads");

                model.SelfWeightLoadCount = createdSwLoads.Count;
            }

            // Step 11: Create lumped mass loads
            if (effectiveLumpedMassLoads.Count > 0)
            {
                var lmCreates = new List<LumpedMassLoadCreate>(effectiveLumpedMassLoads.Count);
                foreach (var lm in effectiveLumpedMassLoads)
                {
                    var canonical = ResolveCanonicalPoint(lm.Point, pointToCanonical, tolerance);
                    var nodeId = model.NodeMap[canonical];
                    var loadCaseId = model.LoadCaseMap[lm.LoadCase.Key];

                    var create = new LumpedMassLoadCreate
                    {
                        Node = nodeId,
                        LoadCase = loadCaseId,
                        Tmx = lm.Tmx,
                        Tmy = lm.Tmy,
                        Tmz = lm.Tmz,
                        Rmx = lm.Rmx,
                        Rmy = lm.Rmy,
                        Rmz = lm.Rmz
                    };

                    if (lm.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[lm.LoadCategory.Key];

                    lmCreates.Add(create);
                }

                List<LumpedMassLoad> createdLumpedMassLoads;
                try
                {
                    createdLumpedMassLoads = await api.CreateLumpedMassLoadsAsync(lmCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating lumped mass loads"), ex);
                }

                ValidateBulkResult(createdLumpedMassLoads.Count, effectiveLumpedMassLoads.Count,
                    "lumped mass loads");

                model.LumpedMassLoadCount = createdLumpedMassLoads.Count;
            }

            // Step 12: Create prescribed displacements
            if (effectivePrescribedDisplacements.Count > 0)
            {
                var pdCreates = new List<PrescribedDisplacementCreate>(effectivePrescribedDisplacements.Count);
                foreach (var pd in effectivePrescribedDisplacements)
                {
                    var canonical = ResolveCanonicalPoint(pd.Point, pointToCanonical, tolerance);
                    var nodeId = model.NodeMap[canonical];
                    var loadCaseId = model.LoadCaseMap[pd.LoadCase.Key];

                    var create = new PrescribedDisplacementCreate
                    {
                        Node = nodeId,
                        LoadCase = loadCaseId,
                        Tx = pd.Tx,
                        Ty = pd.Ty,
                        Tz = pd.Tz,
                        Rx = pd.Rx,
                        Ry = pd.Ry,
                        Rz = pd.Rz
                    };

                    if (pd.LoadCategory != null)
                        create.LoadCategory = model.LoadCategoryMap[pd.LoadCategory.Key];

                    pdCreates.Add(create);
                }

                List<PrescribedDisplacement> createdPrescribedDisplacements;
                try
                {
                    createdPrescribedDisplacements = await api.CreatePrescribedDisplacementsAsync(pdCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating prescribed displacements"), ex);
                }

                ValidateBulkResult(createdPrescribedDisplacements.Count, effectivePrescribedDisplacements.Count,
                    "prescribed displacements");

                model.PrescribedDisplacementCount = createdPrescribedDisplacements.Count;
            }
        }

        // ── Build plate node-ID lookup for pressure and thermal loads ──────
        Dictionary<string, int>? plateLookup = null;
        if (effectivePlatePressureLoads.Count > 0 || effectiveThermalLoads.Any(t => t.ElementType != ThermalElementType.Member))
        {
            plateLookup = new Dictionary<string, int>();
            foreach (var kvp in model.PlateMap)
            {
                var nodeIds = new List<int>();
                foreach (var pn in kvp.Value)
                {
                    var pc = TryResolveCanonicalPoint(pn, pointToCanonical, tolerance);
                    if (pc != null && model.NodeMap.TryGetValue(pc.Value, out var pnId))
                        nodeIds.Add(pnId);
                }
                if (nodeIds.Count == kvp.Value.Length)
                {
                    var key = string.Join(",", nodeIds);
                    plateLookup.TryAdd(key, kvp.Key);
                }
            }
        }

        // ── Step 13: Create plate pressure loads ──────────────────────────
        if (effectivePlatePressureLoads.Count > 0)
        {
            var platePressureCreates = new List<PlatePressureLoadCreate>();
            foreach (var pp in effectivePlatePressureLoads)
            {
                // Resolve plate corner points to node IDs
                var resolvedNodeIds = new List<int>();
                var resolved = true;
                foreach (var node in pp.PlateNodes)
                {
                    var canonical = TryResolveCanonicalPoint(node, pointToCanonical, tolerance);
                    if (canonical == null || !model.NodeMap.TryGetValue(canonical.Value, out var nodeId))
                    {
                        resolved = false;
                        break;
                    }
                    resolvedNodeIds.Add(nodeId);
                }

                if (!resolved)
                {
                    result.Warnings.Add(
                        "Plate pressure load references a plate that doesn't match any model plate — skipped.");
                    continue;
                }

                // Find plate ID by matching node IDs via lookup
                int? plateId = null;
                var nodeIdKey = string.Join(",", resolvedNodeIds);
                if (plateLookup != null && plateLookup.TryGetValue(nodeIdKey, out var foundPlateId))
                    plateId = foundPlateId;

                if (plateId == null)
                {
                    result.Warnings.Add(
                        "Plate pressure load references a plate that doesn't match any model plate — skipped.");
                    continue;
                }

                var loadCaseId = model.LoadCaseMap[pp.LoadCase.Key];
                var create = new PlatePressureLoadCreate
                {
                    Plate = plateId.Value,
                    LoadCase = loadCaseId,
                    Px = pp.Px,
                    Py = pp.Py,
                    Pz = pp.Pz,
                    Axes = pp.Axes
                };

                if (pp.LoadCategory != null)
                    create.LoadCategory = model.LoadCategoryMap[pp.LoadCategory.Key];

                platePressureCreates.Add(create);
            }

            if (platePressureCreates.Count > 0)
            {
                List<PlatePressureLoad> createdPlatePressureLoads;
                try
                {
                    createdPlatePressureLoads = await api.CreatePlatePressureLoadsAsync(platePressureCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating plate pressure loads"), ex);
                }

                ValidateBulkResult(createdPlatePressureLoads.Count, platePressureCreates.Count,
                    "plate pressure loads");

                model.PlatePressureLoadCount = createdPlatePressureLoads.Count;
            }
        }

        // ── Step 14: Create thermal loads ─────────────────────────────────
        if (effectiveThermalLoads.Count > 0)
        {
            var thermalCreates = new List<ThermalLoadCreate>();
            foreach (var tl in effectiveThermalLoads)
            {
                int? elementId = null;

                if (tl.ElementType == ThermalElementType.Member)
                {
                    // Resolve member geometry to member ID
                    var startCanonical = TryResolveCanonicalPoint(tl.MemberStart!.Value, pointToCanonical, tolerance);
                    var endCanonical = TryResolveCanonicalPoint(tl.MemberEnd!.Value, pointToCanonical, tolerance);

                    if (startCanonical != null && endCanonical != null &&
                        model.NodeMap.TryGetValue(startCanonical.Value, out var startNodeId) &&
                        model.NodeMap.TryGetValue(endCanonical.Value, out var endNodeId) &&
                        memberLookup != null && memberLookup.TryGetValue((startNodeId, endNodeId), out var memberId))
                    {
                        elementId = memberId;
                    }
                    else
                    {
                        result.Warnings.Add(
                            $"Thermal load references a member that doesn't exist — skipped. " +
                            $"Start: ({tl.MemberStart.Value.X:F3}, {tl.MemberStart.Value.Y:F3}, {tl.MemberStart.Value.Z:F3}), " +
                            $"End: ({tl.MemberEnd.Value.X:F3}, {tl.MemberEnd.Value.Y:F3}, {tl.MemberEnd.Value.Z:F3})");
                        continue;
                    }
                }
                else // Plate
                {
                    // Resolve plate corner points to plate ID
                    var resolvedNodeIds = new List<int>();
                    var resolved = true;
                    foreach (var node in tl.PlateNodes!)
                    {
                        var canonical = TryResolveCanonicalPoint(node, pointToCanonical, tolerance);
                        if (canonical == null || !model.NodeMap.TryGetValue(canonical.Value, out var nodeId))
                        {
                            resolved = false;
                            break;
                        }
                        resolvedNodeIds.Add(nodeId);
                    }

                    if (resolved)
                    {
                        var nodeIdKey = string.Join(",", resolvedNodeIds);
                        if (plateLookup != null && plateLookup.TryGetValue(nodeIdKey, out var foundPlateId))
                            elementId = foundPlateId;
                    }

                    if (elementId == null)
                    {
                        result.Warnings.Add(
                            "Thermal load references a plate that doesn't match any model plate — skipped.");
                        continue;
                    }
                }

                var loadCaseId = model.LoadCaseMap[tl.LoadCase.Key];
                var create = new ThermalLoadCreate
                {
                    ElementId = elementId.Value,
                    ElementType = tl.ElementType,
                    LoadCase = loadCaseId,
                    ThermalLoad = tl.ThermalLoad,
                    YThermalGradient = tl.YGradient,
                    ZThermalGradient = tl.ZGradient
                };

                if (tl.LoadCategory != null)
                    create.LoadCategory = model.LoadCategoryMap[tl.LoadCategory.Key];

                thermalCreates.Add(create);
            }

            if (thermalCreates.Count > 0)
            {
                List<ThermalLoad> createdThermalLoads;
                try
                {
                    createdThermalLoads = await api.CreateThermalLoadsAsync(thermalCreates, ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        FormatApiError(ex, "creating thermal loads"), ex);
                }

                ValidateBulkResult(createdThermalLoads.Count, thermalCreates.Count, "thermal loads");

                model.ThermalLoadCount = createdThermalLoads.Count;
            }
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    ///     Validates that the API returned the expected number of items.
    ///     Throws if there's a mismatch (partial failure).
    /// </summary>
    private static void ValidateBulkResult(int returned, int expected, string entityType)
    {
        if (returned != expected)
            throw new InvalidOperationException(
                $"SpaceGass returned {returned} {entityType} but {expected} were sent. " +
                "Check the SpaceGass job errors for details.");
    }

    /// <summary>
    ///     Extracts a meaningful error message from a SpaceGass API exception.
    /// </summary>
    public static string FormatApiError(Exception ex, string operation)
    {
        switch (ex)
        {
            case ErrorResponse errorResponse:
            {
                var parts = new List<string> { $"SpaceGass error during {operation}:" };
                if (!string.IsNullOrEmpty(errorResponse.Title))
                    parts.Add(errorResponse.Title);
                if (!string.IsNullOrEmpty(errorResponse.Detail))
                    parts.Add(errorResponse.Detail);
                if (errorResponse.Errors?.Count > 0)
                    foreach (var err in errorResponse.Errors.Take(5))
                        parts.Add($"  - {err.Message}");
                if (!string.IsNullOrEmpty(errorResponse.Message) &&
                    errorResponse.Message != ex.GetType().FullName)
                    parts.Add(errorResponse.Message);
                return string.Join("\n", parts);
            }
            case ApiException apiEx:
                return $"SpaceGass API error during {operation}: {apiEx.Message} (HTTP {apiEx.ResponseStatusCode})";
            default:
                return $"Error during {operation}: {ex.Message}";
        }
    }

    /// <summary>
    ///     Returns a list of unique items by key (first occurrence wins) and adds
    ///     warnings for any duplicates (ADR-0006).
    /// </summary>
    private static List<T> DeduplicateByKey<T>(
        IEnumerable<T> items, Func<T, string> keySelector,
        string entityType, List<string> warnings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<T>();
        var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var key = keySelector(item);
            if (seen.Add(key))
                unique.Add(item);
            else duplicateKeys.Add(key);
        }

        foreach (var dup in duplicateKeys)
            warnings.Add($"Duplicate {entityType} '{dup}' — only the first occurrence was used.");

        return unique;
    }

    /// <summary>
    ///     Formats a set of ascending integer IDs into a SpaceGass selection string, collapsing
    ///     adjacent runs into <c>N-M</c> ranges. Input can be unsorted and may contain
    ///     duplicates. Empty input returns <see cref="string.Empty"/>.
    /// </summary>
    /// <example>
    ///     <c>[1, 2, 3, 5, 7, 8, 9]</c> → <c>"1-3,5,7-9"</c>.
    /// </example>
    public static string FormatIdSelectionString(IEnumerable<int> ids)
    {
        var sorted = ids.Distinct().OrderBy(i => i).ToList();
        if (sorted.Count == 0) return string.Empty;

        var parts = new List<string>();
        var rangeStart = sorted[0];
        var rangeEnd = sorted[0];
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == rangeEnd + 1)
            {
                rangeEnd = sorted[i];
            }
            else
            {
                parts.Add(rangeStart == rangeEnd
                    ? rangeStart.ToString()
                    : $"{rangeStart}-{rangeEnd}");
                rangeStart = sorted[i];
                rangeEnd = sorted[i];
            }
        }
        parts.Add(rangeStart == rangeEnd
            ? rangeStart.ToString()
            : $"{rangeStart}-{rangeEnd}");
        return string.Join(",", parts);
    }

    /// <summary>
    ///     Resolves a point to its canonical form using the spatial grid.
    ///     Returns null if the point is not in the grid (safe version).
    /// </summary>
    private static SgPoint3D? TryResolveCanonicalPoint(
        SgPoint3D pt,
        Dictionary<(long, long, long), SgPoint3D> pointToCanonical,
        double tolerance)
    {
        var key = QuantiseKey(pt, tolerance);

        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            var neighbourKey = (key.X + dx, key.Y + dy, key.Z + dz);
            if (pointToCanonical.TryGetValue(neighbourKey, out var canonical) &&
                pt.IsCoincident(canonical, tolerance))
                return canonical;
        }

        return null;
    }

    /// <summary>
    ///     Resolves a point to its canonical form using the spatial grid.
    ///     Falls back to direct grid lookup if the point is already canonical.
    /// </summary>
    private static SgPoint3D ResolveCanonicalPoint(
        SgPoint3D pt,
        Dictionary<(long, long, long), SgPoint3D> pointToCanonical,
        double tolerance)
    {
        var key = QuantiseKey(pt, tolerance);

        // Check this cell and all 26 neighbours
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            var neighbourKey = (key.X + dx, key.Y + dy, key.Z + dz);
            if (pointToCanonical.TryGetValue(neighbourKey, out var canonical) &&
                pt.IsCoincident(canonical, tolerance))
                return canonical;
        }

        // Should not happen if the point was included in allPoints
        return pointToCanonical[key];
    }

    /// <summary>
    ///     Returns true if the given point coincides with any point in the spatial grid.
    /// </summary>
    private static bool IsPointInGrid(
        SgPoint3D pt,
        Dictionary<(long, long, long), SgPoint3D> grid,
        double tolerance)
    {
        var key = QuantiseKey(pt, tolerance);
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        for (var dz = -1; dz <= 1; dz++)
        {
            var neighbourKey = (key.X + dx, key.Y + dy, key.Z + dz);
            if (grid.TryGetValue(neighbourKey, out var existing) &&
                pt.IsCoincident(existing, tolerance))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Quantises a point to a grid cell key for O(1) spatial lookup.
    ///     Points within the same tolerance-sized cell map to the same key.
    /// </summary>
    private static (long X, long Y, long Z) QuantiseKey(SgPoint3D pt, double tolerance)
    {
        // Cell size equals tolerance; coincident points span at most ±1 cell in each axis.
        // Multi-cell probe in DeduplicatePoints handles cross-cell coincidence.
        return (
            (long)Math.Floor(pt.X / tolerance),
            (long)Math.Floor(pt.Y / tolerance),
            (long)Math.Floor(pt.Z / tolerance));
    }

    /// <summary>
    ///     Returns unique points using a spatial grid for O(n) average deduplication.
    ///     Also returns a mapping from quantised key → canonical point for fast lookup.
    /// </summary>
    private static (List<SgPoint3D> UniquePoints, Dictionary<(long, long, long), SgPoint3D> PointToCanonical)
        DeduplicatePoints(IReadOnlyList<SgPoint3D> points, double tolerance)
    {
        var unique = new List<SgPoint3D>();
        // Map from grid cell → canonical point that owns that cell
        var grid = new Dictionary<(long, long, long), SgPoint3D>();

        foreach (var pt in points)
        {
            var key = QuantiseKey(pt, tolerance);

            // Check this cell and all 26 neighbours (3x3x3 cube)
            var found = false;
            for (var dx = -1; dx <= 1 && !found; dx++)
            for (var dy = -1; dy <= 1 && !found; dy++)
            for (var dz = -1; dz <= 1 && !found; dz++)
            {
                var neighbourKey = (key.X + dx, key.Y + dy, key.Z + dz);
                if (grid.TryGetValue(neighbourKey, out var existing) &&
                    pt.IsCoincident(existing, tolerance))
                {
                    // Already have a coincident point — map this cell to the same canonical
                    grid.TryAdd(key, existing);
                    found = true;
                }
            }

            if (found) continue;
            unique.Add(pt);
            grid[key] = pt;
        }

        return (unique, grid);
    }
}