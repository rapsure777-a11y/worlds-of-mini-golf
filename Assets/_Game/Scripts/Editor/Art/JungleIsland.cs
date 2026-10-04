using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HoleFrame = Gamebreak.MiniGolf.Editor.Art.TropicalWorld.HoleFrame;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Jungle Island (cluster "jungle", Holes 3-4): dense, vertical rainforest cut by a ravine, plus Hole 3's wooden bridge
    /// supports and set dressing and the pier that links it to the Starting Island. Hole-local coordinates as elsewhere:
    /// x across the first lane, z down it. The ravine runs along local z at <see cref="BridgeAxisX"/>; the bridge crosses it.
    /// </summary>
    public static class JungleIsland
    {
        public const float BridgeAxisX = 5.2f, RavineHalfWidth = 3.5f, RavineDepth = 3.3f;

        // ------------------------------------------------------------------ terrain

        public static IslandGen CreateIsland()
        {
            var c = TropicalCourse.JungleCentre;
            var g = new IslandGen { centre = c, radius = 25f, seed = 19, hillHeight = 6.5f, extent = 34f, splat = true, skipDeepSea = true };
            // Steep jungle hills framing the lanes (kept clear of Hole 3's plateau, which overrides them locally).
            g.mounds.Add((c + new Vector2(14f, 16f), 13f, 4.5f));
            g.mounds.Add((c + new Vector2(12f, -18f), 11f, 4.0f));
            g.mounds.Add((c + new Vector2(-10f, 17f), 9f, 3.0f));
            return g;
        }

        /// <summary>Flattens a plateau under the hole, makes the lane areas follow the hole's height function, and cuts the ravine under the bridge.</summary>
        public static void ConfigureTerrain(IslandGen island, HoleFrame f, HoleDefinition def)
        {
            var (centre, half) = TropicalWorld.LayoutBounds(def.layout);
            float baseY = def.origin.y - TropicalCourse.GreenElevation - 0.06f;
            island.zones.Add(new IslandGen.Zone
            {
                centre = f.L2(centre.x, centre.y), halfSize = half + new Vector2(3.5f, 3.5f), yaw = f.yaw,
                height = baseY - 0.25f, feather = 10f, plateau = true,
            });
            if (def.layout.deckAreas.Count > 0) // only holes with a bridge cut the ravine
                island.channels.Add(new IslandGen.Channel
                {
                    a = f.L2(BridgeAxisX, -16f), b = f.L2(BridgeAxisX, 24f), halfWidth = RavineHalfWidth, depth = RavineDepth,
                    floorFraction = 0.35f, endTaper = 7f,
                });
            // Lane areas (not the deck, which floats over the ravine) sit on the ground at their own surface height.
            var layout = def.layout;
            float originY = def.origin.y;
            foreach (var r in layout.areas)
            {
                bool isDeck = false;
                foreach (var dk in layout.deckAreas) if (dk == r) isDeck = true;
                if (isDeck) continue;
                island.zones.Add(new IslandGen.Zone
                {
                    // Tight blend (Local, M3 cp1): a 2.2 m feather from the lanes at both bridge ends filled the ravine under the deck to 1.6 m.
                    centre = f.L2(r.center.x, r.center.y), halfSize = r.size * 0.5f + new Vector2(0.3f, 0.3f), yaw = f.yaw, feather = 1.0f,
                    heightFn = w => { var l = f.ToLocal2(w); return originY + layout.Height(l.x, l.y) - TropicalCourse.GreenElevation - 0.06f; },
                });
            }
        }

        // ------------------------------------------------------------------ hole 3

        public static void DressHole3(Dresser d, HoleFrame f, HoleDefinition def)
        {
            var root = new GameObject("Hole03_Dressing").transform;
            root.SetParent(d.Root, false);
            var rnd = new System.Random(303);
            Vector3 start = f.L(0f, -0.3f);
            float FaceStart(Vector3 p) => Quaternion.LookRotation((p - start).WithY(0f)).eulerAngles.y;
            float Yaw360() => Dresser.Range(rnd, 0f, 360f);

            // Foliage keeps 90 cm clear of every lane area (sightlines, putter swing); set-pieces register their own volumes.
            foreach (var r in def.layout.areas)
                d.KeepOut(f.L2(r.center.x, r.center.y), r.size * 0.5f, f.yaw, 0.9f);
            d.KeepOut(f.L2(17.5f, 4.5f), new Vector2(3.5f, 8.2f), f.yaw, 0.3f);          // backdrop cliff
            d.KeepOut(f.L2(-14f, 12f), new Vector2(3f, 3f), f.yaw, 0.5f);                // mesas
            d.KeepOut(f.L2(24f, -6f), new Vector2(3f, 3f), f.yaw, 0.5f);
            d.KeepOut(f.L2(22f, 16f), new Vector2(3f, 3f), f.yaw, 0.5f);

            bool Free(float x, float z, float minH = 0.3f)
            {
                var p = f.L(x, z);
                return d.IsFree(new Vector2(p.x, p.z)) && d.Ground(p.x, p.z) > minH;
            }

            // Signs: the island's name board beside the tee and the hole sign.
            var boardPos = f.L(-1.8f, -0.9f);
            var board = d.Place("SignLarge", d.Kit.Solid, boardPos, FaceStart(boardPos), 1f, collider: true, parent: root);
            TropicalWorld.AddSignText(board.transform, new Vector3(0f, 0.75f + 0.475f, -0.032f), new Vector2(1.4f, 0.88f),
                "<size=50><b>JUNGLE ISLAND</b></size>\n<size=34><i>Holes 3 and 4</i></size>\n\n<size=30>Mind the ravine:\nthe bridge is part of the course.</size>", 30);
            var holeSignPos = f.L(1.5f, 1.0f);
            var holeSign = d.Place("SignSmall", d.Kit.Solid, holeSignPos, FaceStart(holeSignPos), 1f, parent: root);
            TropicalWorld.AddSignText(holeSign.transform, new Vector3(0f, 0.65f + 0.225f, -0.032f), new Vector2(0.76f, 0.42f),
                $"<size=46><b>HOLE {def.number}</b></size>\n{def.name}  ·  Par {def.par}", 34);

            BuildBridge(d, f, def, root);

            // Torches flanking both ends of the bridge.
            foreach (var (x, z) in new[] { (3.0f, 3.75f), (3.0f, 5.45f), (7.5f, 3.75f), (7.5f, 5.45f) })
                d.Torch(f.L(x, z));

            // ---- Canopy trees (Blender hero trees; hero palms stand in until their FBX exist).
            bool haveTrees = d.HasModel("HeroJungleTree_0") && d.HasModel("HeroJungleTree_1");
            var trees = new (float x, float z, int v, float s)[]
            {
                (-3.8f, 1.5f, 0, 1.0f), (-4.8f, 8.0f, 1, 1.1f), (-2.8f, 13.5f, 0, 0.95f), (12.8f, 0.8f, 1, 1.0f),
                (14.2f, 7.5f, 0, 1.15f), (12.0f, 13.5f, 1, 0.9f), (0.5f, -6.5f, 1, 1.05f), (10.5f, -4.5f, 0, 1.0f), (4.5f, 14.5f, 0, 1.0f),
            };
            foreach (var t in trees)
            {
                var p = f.L(t.x, t.z);
                if (d.Island.ChannelWeight(p.x, p.z) > 0.12f || !d.IsFree(new Vector2(p.x, p.z)) || d.Ground(p.x, p.z) < 0.3f) continue;
                if (haveTrees) d.Model($"HeroJungleTree_{t.v}", p, Yaw360(), t.s, sink: 0.25f, parent: root, cull: 0.005f, seat: true, seatExtraSink: 0.3f);
                else d.Model($"HeroPalm_{t.v}", p, Yaw360(), 1.5f * t.s, sink: 0.06f, parent: root, cull: 0.005f);
            }

            // ---- Rocks: ravine rim boulders, a terraced cliff behind the landing pad, mesas on the skyline.
            foreach (var (x, z, k) in new[] { (1.4f, -1.5f, "medium"), (1.3f, 10.5f, "medium"), (9.0f, -1.2f, "medium"), (9.2f, 12.0f, "large"), (-3.2f, 5.0f, "small"), (12.5f, 10.5f, "medium") })
                if (Free(x, z, 0.0f)) d.Rock(k, f.L(x, z), Yaw360(), k == "large" ? 0.7f : Dresser.Range(rnd, 0.8f, 1.2f), root, collider: true, outOfBounds: true, variant: rnd.Next(0, 2));
            d.Model("HeroCliffWall", f.L(17.5f, 4.5f), f.Yaw(90f), 0.9f, sink: 0.3f, parent: root, seat: true, lods: true, lodScale: 0.6f, seatExtraSink: 0.3f);
            d.Model("HeroMesa_0", f.L(-14f, 12f), f.Yaw(70f), 1.0f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);
            d.Model("HeroMesa_1", f.L(24f, -6f), f.Yaw(200f), 1.1f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);
            d.Model("HeroMesa_0", f.L(22f, 16f), f.Yaw(300f), 0.9f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);

            // ---- Vines hanging over the ravine rims.
            if (d.HasModel("HeroVines_0"))
                foreach (var (x, z) in new[] { (2.0f, 0.6f), (2.1f, 8.4f), (8.5f, 0.2f), (8.4f, 9.0f), (2.2f, 3.0f), (8.3f, 6.9f) })
                {
                    var p = f.L(x, z);
                    if (d.Island.ChannelWeight(p.x, p.z) < 0.02f) continue;
                    p.y = d.Ground(p.x, p.z) + 0.15f;
                    d.Model("HeroVines_0", p, Yaw360(), 1f, snap: false, shadows: false, parent: root, cull: 0.006f);
                }

            // ---- Layered rainforest floor.
            string[] shrubs = { "LeafBush0", "FlowerShrub0", "LeafBush1", "FlowerShrub1", "LeafBush2", "FlowerShrub2" };
            for (int i = 0; i < 110; i++)
            {
                float x = Dresser.Range(rnd, -8f, 16f), z = Dresser.Range(rnd, -6f, 14f);
                if (!Free(x, z)) continue;
                string m = rnd.NextDouble() < 0.45 ? "Fern0" : $"GrassClump{rnd.Next(0, 2)}";
                d.Leaf(m, f.L(x, z), Yaw360(), Dresser.Range(rnd, 0.9f, 1.7f), shadows: false, parent: root, cull: 0.014f);
            }
            for (int i = 0; i < 70; i++)
            {
                float x = Dresser.Range(rnd, -9f, 17f), z = Dresser.Range(rnd, -7f, 15f);
                if (!Free(x, z)) continue;
                d.Leaf(shrubs[rnd.Next(0, shrubs.Length)], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.2f, 2.0f), shadows: false, parent: root);
            }
            string[] tall = { "Banana0", "BigLeaf1", "BigLeaf0", "Banana0", "BigLeaf1" };
            for (int i = 0; i < 46; i++)
            {
                float x = Dresser.Range(rnd, -9f, 18f), z = Dresser.Range(rnd, -7f, 16f);
                if (!Free(x, z)) continue;
                d.Leaf(tall[i % tall.Length], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.2f, 1.9f), shadows: false, parent: root, cull: 0.009f);
            }
            // Oversized shoulder foliage close to the lanes' edges (just outside the clear margin).
            foreach (var (x, z) in new[] { (-2.2f, 0.5f), (-2.4f, 4.5f), (-2.0f, 7.5f), (11.8f, 4.0f), (11.6f, 9.0f), (4.0f, 8.6f), (6.8f, 8.4f), (3.2f, 1.6f), (7.2f, 1.6f) })
                if (Free(x, z)) d.Leaf(rnd.NextDouble() < 0.5 ? "BigLeaf1" : "Banana0", f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.2f, 1.6f), shadows: false, parent: root, cull: 0.009f);
        }

        // ------------------------------------------------------------------ hole 4

        /// <summary>Hole 4 "Hollow Drop": a sunken basin ringed by jungle on the island's east side.</summary>
        public static void DressHole4(Dresser d, HoleFrame f, HoleDefinition def)
        {
            var root = new GameObject("Hole04_Dressing").transform;
            root.SetParent(d.Root, false);
            var rnd = new System.Random(404);
            Vector3 start = f.L(0f, -0.3f);
            float FaceStart(Vector3 p) => Quaternion.LookRotation((p - start).WithY(0f)).eulerAngles.y;
            float Yaw360() => Dresser.Range(rnd, 0f, 360f);

            foreach (var r in def.layout.areas)
                d.KeepOut(f.L2(r.center.x, r.center.y), r.size * 0.5f, f.yaw, 0.9f);
            d.KeepOut(f.L2(-14f, 8f), new Vector2(3f, 3f), f.yaw, 0.5f);   // mesas
            d.KeepOut(f.L2(18f, 12f), new Vector2(3f, 3f), f.yaw, 0.5f);
            d.KeepOut(f.L2(4f, 28f), new Vector2(3f, 3f), f.yaw, 0.5f);

            bool Free(float x, float z, float minH = 0.3f)
            {
                var p = f.L(x, z);
                return d.IsFree(new Vector2(p.x, p.z)) && d.Ground(p.x, p.z) > minH;
            }

            var holeSignPos = f.L(1.7f, 0.9f);
            var holeSign = d.Place("SignSmall", d.Kit.Solid, holeSignPos, FaceStart(holeSignPos), 1f, parent: root);
            TropicalWorld.AddSignText(holeSign.transform, new Vector3(0f, 0.65f + 0.225f, -0.032f), new Vector2(0.76f, 0.42f),
                $"<size=46><b>HOLE {def.number}</b></size>\n{def.name}  ·  Par {def.par}", 34);

            // A tiki pole beside the tee and a mask above the basin's far end (the Starting Island's totems follow you to the jungle).
            TropicalWorld.Tiki(d, "HeroTikiPole_1", f.L(-2.4f, 0.8f), FaceStart(f.L(-2.4f, 0.8f)), root, 0.2f, 1.7f);
            TropicalWorld.Tiki(d, "HeroTikiMask_0", f.L(5.6f, 8.6f), FaceStart(f.L(5.6f, 8.6f)), root, 0.07f, 1.7f);

            // Torches: the tee, the basin's two ends, and the final lane.
            foreach (var (x, z) in new[] { (-1.2f, 1.6f), (1.2f, 1.6f), (-1.9f, 9.8f), (5.3f, 9.8f), (-2.0f, 13.2f), (0.8f, 13.2f) })
                d.Torch(f.L(x, z));

            bool haveTrees = d.HasModel("HeroJungleTree_0") && d.HasModel("HeroJungleTree_1");
            var trees = new (float x, float z, int v, float s)[]
            {
                (-4.2f, 2.0f, 0, 1.0f), (-4.6f, 8.0f, 1, 1.1f), (7.0f, 1.5f, 1, 1.0f), (7.6f, 7.5f, 0, 1.1f), (7.0f, 13.0f, 1, 0.95f),
                (-4.4f, 13.5f, 0, 1.0f), (1.5f, 17.5f, 1, 1.0f), (2.0f, -4.0f, 0, 1.0f), (9.5f, 4.0f, 1, 1.1f), (4.0f, 16.0f, 0, 0.9f),
            };
            foreach (var t in trees)
            {
                var p = f.L(t.x, t.z);
                if (!d.IsFree(new Vector2(p.x, p.z)) || d.Ground(p.x, p.z) < 0.3f) continue;
                if (haveTrees) d.Model($"HeroJungleTree_{t.v}", p, Yaw360(), t.s, sink: 0.25f, parent: root, cull: 0.005f, seat: true, seatExtraSink: 0.3f);
                else d.Model($"HeroPalm_{t.v}", p, Yaw360(), 1.5f * t.s, sink: 0.06f, parent: root, cull: 0.005f);
            }

            // Boulders on the rim of the basin and the mesas on the skyline.
            foreach (var (x, z, k) in new[] { (3.0f, 2.2f, "medium"), (-2.6f, 7.0f, "medium"), (6.2f, 5.5f, "large"), (2.2f, 12.6f, "medium"), (6.0f, 11.5f, "small") })
                if (Free(x, z, 0.0f)) d.Rock(k, f.L(x, z), Yaw360(), k == "large" ? 0.7f : Dresser.Range(rnd, 0.8f, 1.2f), root, collider: true, outOfBounds: true, variant: rnd.Next(0, 2));
            d.Model("HeroMesa_0", f.L(-14f, 8f), f.Yaw(70f), 1.0f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);
            d.Model("HeroMesa_1", f.L(18f, 12f), f.Yaw(200f), 1.1f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);
            d.Model("HeroMesa_0", f.L(4f, 28f), f.Yaw(300f), 0.9f, sink: 0.5f, shadows: false, parent: root, seat: true, lods: true, seatExtraSink: 0.4f);

            // Layered rainforest floor, same recipe as Hole 3.
            string[] shrubs = { "LeafBush0", "FlowerShrub0", "LeafBush1", "FlowerShrub1", "LeafBush2", "FlowerShrub2" };
            for (int i = 0; i < 100; i++)
            {
                float x = Dresser.Range(rnd, -8f, 12f), z = Dresser.Range(rnd, -6f, 20f);
                if (!Free(x, z)) continue;
                string m = rnd.NextDouble() < 0.45 ? "Fern0" : $"GrassClump{rnd.Next(0, 2)}";
                d.Leaf(m, f.L(x, z), Yaw360(), Dresser.Range(rnd, 0.9f, 1.7f), shadows: false, parent: root, cull: 0.014f);
            }
            for (int i = 0; i < 60; i++)
            {
                float x = Dresser.Range(rnd, -9f, 13f), z = Dresser.Range(rnd, -7f, 21f);
                if (!Free(x, z)) continue;
                d.Leaf(shrubs[rnd.Next(0, shrubs.Length)], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.2f, 2.0f), shadows: false, parent: root);
            }
            string[] tall = { "Banana0", "BigLeaf1", "BigLeaf0", "Banana0", "BigLeaf1" };
            for (int i = 0; i < 40; i++)
            {
                float x = Dresser.Range(rnd, -9f, 14f), z = Dresser.Range(rnd, -7f, 22f);
                if (!Free(x, z)) continue;
                d.Leaf(tall[i % tall.Length], f.L(x, z), Yaw360(), Dresser.Range(rnd, 1.2f, 1.9f), shadows: false, parent: root, cull: 0.009f);
            }
        }

        // ------------------------------------------------------------------ bridge supports

        /// <summary>
        /// Wooden trestle under the bridge deck: stringers, cross beams, bents (posts to the ravine floor with rungs and X braces),
        /// rope handrail posts and ropes. Built with world-scale box UVs against Hero_Wood / Hero_Thatch (rope), following the hump.
        /// The deck surface itself is the green mesh (deck material); these meshes are visual only and have no colliders.
        /// </summary>
        static void BuildBridge(Dresser d, HoleFrame f, HoleDefinition def, Transform root)
        {
            if (def.layout.deckAreas.Count == 0) return;
            var deck = def.layout.deckAreas[0];
            float x0 = deck.xMin, x1 = deck.xMax, z0 = deck.yMin, z1 = deck.yMax;
            const float tile = 1.6f;
            Vector3 S(float x, float z) { var p = f.L(x, z); p.y = def.origin.y + def.layout.Height(x, z); return p; }
            float Gnd(float x, float z) { var p = f.L(x, z); return d.Ground(p.x, p.z); }
            Vector3 At(float x, float z, float y) { var p = f.L(x, z); p.y = y; return p; }
            var wood = new MeshBuilder();
            var rope = new MeshBuilder();

            // Stringers under the deck along its length, following the hump.
            foreach (float z in new[] { z0 + 0.12f, z1 - 0.12f })
                for (float x = x0 - 0.2f; x < x1 + 0.2f - 1e-3f; x += 0.3f)
                {
                    float xa = x, xb = Mathf.Min(x + 0.3f, x1 + 0.2f);
                    Beam(wood, At(xa, z, S(xa, z).y - 0.15f), At(xb, z, S(xb, z).y - 0.15f), 0.14f, 0.22f, tile);
                }
            // Floor beams across.
            for (float x = x0 + 0.05f; x <= x1; x += 0.7f)
                Beam(wood, At(x, z0 - 0.2f, S(x, 0.5f * (z0 + z1)).y - 0.36f), At(x, z1 + 0.2f, S(x, 0.5f * (z0 + z1)).y - 0.36f), 0.16f, 0.16f, tile);

            // Bents: two posts each, down to the ground under them, with rungs, a cap and X bracing.
            float[] bentX = { x0 + 1.0f, 0.5f * (x0 + x1), x1 - 1.0f };
            float zl = z0 - 0.2f, zr = z1 + 0.2f;
            foreach (float x in bentX)
            {
                float top = S(x, 0.5f * (z0 + z1)).y - 0.46f;
                float gl = Gnd(x, zl) - 0.3f, gr = Gnd(x, zr) - 0.3f;
                Beam(wood, At(x, zl, top + 0.3f), At(x, zl, gl), 0.24f, 0.24f, tile);
                Beam(wood, At(x, zr, top + 0.3f), At(x, zr, gr), 0.24f, 0.24f, tile);
                Beam(wood, At(x, zl - 0.12f, top), At(x, zr + 0.12f, top), 0.2f, 0.2f, tile);
                for (int k = 1; k <= 2; k++)
                {
                    float yl = Mathf.Lerp(top, gl, k / 3f), yr = Mathf.Lerp(top, gr, k / 3f);
                    Beam(wood, At(x, zl, yl), At(x, zr, yr), 0.12f, 0.12f, tile);
                    float yl2 = Mathf.Lerp(top, gl, (k + 1) / 3f), yr2 = Mathf.Lerp(top, gr, (k + 1) / 3f);
                    Beam(wood, At(x, zl, yl), At(x, zr, yr2), 0.09f, 0.09f, tile);
                    Beam(wood, At(x, zr, yr), At(x, zl, yl2), 0.09f, 0.09f, tile);
                }
            }
            // Longitudinal ties between bents on both sides.
            for (int i = 0; i + 1 < bentX.Length; i++)
                foreach (float z in new[] { zl, zr })
                {
                    float ya = Mathf.Lerp(S(bentX[i], z).y - 0.46f, Gnd(bentX[i], z), 0.4f), yb = Mathf.Lerp(S(bentX[i + 1], z).y - 0.46f, Gnd(bentX[i + 1], z), 0.4f);
                    Beam(wood, At(bentX[i], z, ya), At(bentX[i + 1], z, yb), 0.12f, 0.12f, tile);
                }

            // Rope handrails just outside the rails: posts every ~0.9 m, two ropes with a little sag.
            foreach (float z in new[] { z0 - 0.15f, z1 + 0.15f })
            {
                int n = Mathf.Max(2, Mathf.RoundToInt((x1 - x0 + 0.2f) / 0.9f));
                var tops = new Vector3[n + 1]; var mids = new Vector3[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float x = Mathf.Lerp(x0 - 0.1f, x1 + 0.1f, i / (float)n);
                    float y = S(x, 0.5f * (z0 + z1)).y;
                    Beam(wood, At(x, z, y - 0.1f), At(x, z, y + 0.66f), 0.08f, 0.08f, tile);
                    tops[i] = At(x, z, y + 0.62f); mids[i] = At(x, z, y + 0.34f);
                }
                for (int i = 0; i < n; i++) { Sag(rope, tops[i], tops[i + 1]); Sag(rope, mids[i], mids[i + 1]); }
            }

            AddMesh(wood, "Hero_BridgeWood", d.Hero.Wood, root, true);
            AddMesh(rope, "Hero_BridgeRope", d.Hero.Thatch, root, false);
        }

        static void AddMesh(MeshBuilder mb, string name, Material mat, Transform parent, bool shadows)
        {
            var mesh = mb.ToMesh(name);
            TropicalWorld.SaveMesh(mesh, name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>A rope between two points: four short square-section beams along a shallow parabola.</summary>
        static void Sag(MeshBuilder mb, Vector3 a, Vector3 b)
        {
            const int segs = 4;
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (0.07f * 4f * t * (1f - t));
                Beam(mb, prev, p, 0.035f, 0.035f, 0.5f);
                prev = p;
            }
        }

        /// <summary>
        /// Oriented square-section beam from a to b with flat-shaded faces and world-scale UVs (one texture tile per
        /// <paramref name="tile"/> metres along the length), offset per beam so repeated beams do not look cloned.
        /// </summary>
        static void Beam(MeshBuilder mb, Vector3 a, Vector3 b, float w, float h, float tile)
        {
            Vector3 axis = b - a; float len = axis.magnitude;
            if (len < 1e-4f) return;
            Vector3 dir = axis / len;
            Vector3 refUp = Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 side = Vector3.Cross(refUp, dir).normalized;   // width axis
            Vector3 up = Vector3.Cross(dir, side).normalized;      // height axis
            Vector3 hs = side * (w * 0.5f), hu = up * (h * 0.5f);
            float off = Mathf.Abs(a.x * 7.31f + a.z * 3.17f + a.y * 1.9f) % 1f;
            float u0 = off, u1 = off + len / tile, vh = h / tile, vw = w / tile;

            void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
            {
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0f) { (p1, p3) = (p3, p1); (uv1, uv3) = (uv3, uv1); }
                int i0 = mb.Vertex(p0, outward, uv0, Color.white), i1 = mb.Vertex(p1, outward, uv1, Color.white);
                int i2 = mb.Vertex(p2, outward, uv2, Color.white), i3 = mb.Vertex(p3, outward, uv3, Color.white);
                mb.Quad(i0, i1, i2, i3);
            }

            // Four long faces.
            Face(a + hs + hu, b + hs + hu, b + hs - hu, a + hs - hu, side, new Vector2(u0, vh), new Vector2(u1, vh), new Vector2(u1, 0f), new Vector2(u0, 0f));
            Face(a - hs + hu, b - hs + hu, b - hs - hu, a - hs - hu, -side, new Vector2(u0, vh), new Vector2(u1, vh), new Vector2(u1, 0f), new Vector2(u0, 0f));
            Face(a + hu + hs, b + hu + hs, b + hu - hs, a + hu - hs, up, new Vector2(u0, vw), new Vector2(u1, vw), new Vector2(u1, 0f), new Vector2(u0, 0f));
            Face(a - hu + hs, b - hu + hs, b - hu - hs, a - hu - hs, -up, new Vector2(u0, vw), new Vector2(u1, vw), new Vector2(u1, 0f), new Vector2(u0, 0f));
            // End caps.
            Face(a + hs + hu, a - hs + hu, a - hs - hu, a + hs - hu, -dir, new Vector2(0f, vh), new Vector2(vw, vh), new Vector2(vw, 0f), new Vector2(0f, 0f));
            Face(b + hs + hu, b - hs + hu, b - hs - hu, b + hs - hu, dir, new Vector2(0f, vh), new Vector2(vw, vh), new Vector2(vw, 0f), new Vector2(0f, 0f));
        }

        // ------------------------------------------------------------------ island cover and link to the Starting Island

        /// <summary>General Jungle Island cover away from the holes: denser and taller than the Starting Island's.</summary>
        public static void DressIsland(Dresser d, HoleFrame hole3)
        {
            var root = new GameObject("Jungle_Island_Dressing").transform;
            root.SetParent(d.Root, false);
            var rnd = new System.Random(2027);
            var c = d.Island.centre;
            float r = d.Island.radius;
            Vector2 focus = new Vector2(hole3.origin.x, hole3.origin.z);
            string[] shrubs = { "LeafBush0", "FlowerShrub0", "LeafBush1", "FlowerShrub1", "LeafBush2", "FlowerShrub2" };
            d.Scatter(rnd, c, r * 0.9f, 34, (q, p) =>
            {
                var pos = new Vector3(p.x, 0f, p.y);
                if (d.Island.ChannelWeight(p.x, p.y) > 0.1f) return false;
                if ((p - focus).magnitude < 26f && d.HasModel("HeroJungleTree_0")) d.Model($"HeroJungleTree_{q.Next(0, 2)}", pos, Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.25f), sink: 0.25f, parent: root, cull: 0.005f);
                else d.Model($"HeroPalm_{q.Next(0, 3)}", pos, Dresser.Range(q, 0f, 360f), Dresser.Range(q, 1.1f, 1.7f), sink: 0.06f, parent: root, cull: 0.005f);
                return true;
            }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 0.95f, 90, (q, p) => { d.Leaf(shrubs[q.Next(0, shrubs.Length)], new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 1.2f, 2.2f), shadows: false, parent: root); return true; }, minHeight: 0.4f);
            d.Scatter(rnd, c, r * 0.95f, 60, (q, p) => { d.Leaf(q.NextDouble() < 0.5 ? "Banana0" : "BigLeaf1", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 1.2f, 2.0f), shadows: false, parent: root, cull: 0.009f); return true; }, minHeight: 0.4f);
            d.Scatter(rnd, c, r * 0.95f, 150, (q, p) => { d.Leaf(q.NextDouble() < 0.5 ? "Fern0" : $"GrassClump{q.Next(0, 2)}", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.9f, 1.6f), shadows: false, parent: root, cull: 0.014f); return true; }, minHeight: 0.3f);
            d.Scatter(rnd, c, r * 0.7f, 6, (q, p) => { d.Rock("large", new Vector3(p.x, 0f, p.y), Dresser.Range(q, 0f, 360f), Dresser.Range(q, 0.5f, 0.9f), root, collider: true, outOfBounds: true, variant: q.Next(0, 2)); return true; }, minHeight: 1.5f);
            TropicalWorld.DressCoast(d, root);
        }

        /// <summary>
        /// A boardwalk across the channel between the Starting Island and the Jungle Island, with torches. Visual and walkable
        /// (teleport) but not part of any green; players are carried between islands by the hole transition (fade + teleport).
        /// Registers a keep-out on both dressers so scatter stays off it.
        /// </summary>
        public static void BuildPier(Dresser from, Dresser to, Transform root)
        {
            Vector2 a = Shore(from, to.Island.centre), b = Shore(to, from.Island.centre);
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 3f) return;
            Vector2 dir = delta / length;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            const float deckY = 0.38f;
            int sections = Mathf.CeilToInt(length / 6f);
            Vector2 mid = (a + b) * 0.5f;
            from.KeepOut(mid, new Vector2(1.2f, length * 0.5f + 1f), yaw, 0.6f);
            to.KeepOut(mid, new Vector2(1.2f, length * 0.5f + 1f), yaw, 0.6f);
            var pier = new GameObject("IslandPier").transform;
            pier.SetParent(root, false);
            Vector2 side = new Vector2(dir.y, -dir.x);
            for (int s = 0; s < sections; s++)
            {
                Vector2 p = a + dir * (s * 6f + 3f);
                from.Place("Boardwalk6", from.Kit.Solid, new Vector3(p.x, deckY, p.y), yaw, 1f, snap: false, collider: true, parent: pier);
                if (s % 2 == 0)
                {
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        Vector2 t = p + side * (0.55f * sgn);
                        from.Torch(new Vector3(t.x, deckY + 0.35f, t.y));
                    }
                }
            }
        }

        /// <summary>The first land point (ground above the waterline) on the line from an island's centre toward a target.</summary>
        static Vector2 Shore(Dresser d, Vector2 toward)
        {
            Vector2 c = d.Island.centre;
            Vector2 dir = (toward - c).normalized;
            Vector2 last = c;
            for (float r = 4f; r < 60f; r += 0.5f)
            {
                Vector2 p = c + dir * r;
                if (d.Ground(p.x, p.y) < 0.12f) break;
                last = p;
            }
            return last;
        }
    }
}
