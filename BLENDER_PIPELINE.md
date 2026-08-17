# BLENDER_PIPELINE.md — Asset Production Guide (Art Validation Sprint)

Scope: the three sprint assets in order — Cretaceous prop cluster → Cole → Swiftjaw.
Constraint recap: box-modeling only, palette atlas texturing, toon shader as equalizer,
game camera is fixed top-down ~50° pitch / FOV 30–35°. Every rule below is derived
from those constraints, not from generic low-poly tutorials.

---

## 0. Triangle budgets (decide these BEFORE modeling)

At 50° pitch and FOV ~32 on a 1080p screen, a 1.8 m character occupies roughly
120–200 px of screen height. At that size, any polygon smaller than ~2 px is wasted
work. The toon shader flattens interior shading, so **the silhouette IS the asset**.
Detail that doesn't change the silhouette is invisible.

| Asset | Budget (tris) | Why |
|---|---|---|
| Rock (each) | 60–150 | Reads as a blob of palette color; only outline matters |
| Fallen log | 200–400 | Long silhouette, needs bark-step outline breaks |
| Fern / plant | 150–300 | Geometry fronds, NO alpha cards (see §5) |
| Amber shard / temporal debris | 50–120 | Faceted crystal reads better with FEWER tris |
| Prop cluster total | < 1,500 | It's set dressing, not a hero asset |
| Cole | 4,000–6,000 | Hero character, but budget goes to joints + silhouette, not face |
| Swiftjaw | 2,500–4,000 | Quadruped-ish, long tail/neck eat loops; no fingers to spend on |

Rule of thumb: if you're adding a loop cut and can't name the silhouette change or
the deformation it enables, delete it.

Turn on the live counter now: **Viewport overlays dropdown (top right) → Statistics**.
It shows tris for the selection and scene at all times. Log against budget per asset.

---

## 1. Scene setup (do once, save as startup or template .blend)

1. **Units**: Scene Properties → Units → Metric, Unit Scale 1.0. Model at real-world
   scale (Cole = 1.8 m tall). Reason: Mixamo auto-rigging misbehaves at wrong scale,
   and Unity import at scale 1 with no fudge factors is one less bug source.
2. **Reference images**: in front ortho (**Numpad 1**), `Shift+A → Image → Reference`,
   pick the front sheet. Repeat in side ortho (**Numpad 3**) for the side sheet.
   In the image's Object Data properties: enable **Opacity ~0.3** and
   **Display Only in Orthographic**. Reason: refs must never block your view in
   perspective, and at 30% you can see your mesh through them.
   Align both refs so the character's feet sit at Z=0 and the centerline sits at X=0
   (**G**, then **X**/**Z** to constrain the move). If front and side sheets disagree
   on height, scale one to match — trust ONE sheet as canon and note which.
3. **The game-camera check rig (the most important item in this file)**:
   `Shift+A → Camera`. Set rotation X = **40°** (a camera pitched 50° down from
   horizontal is 40° up from straight-down; in Blender's convention, RotX 40 with
   RotY/Z = 0 pointing at the model gives you the gameplay angle). In camera data:
   Lens Unit → **Field of View**, set **32°**. Position it back and up until the
   asset is framed at roughly the on-screen size it'll have in game.
   Bind a key habit: **Numpad 0** to jump into this camera constantly.
   Reason: every proportion and detail decision must be judged from THIS angle.
   Top-down at 50° means you mostly see the top and front of everything — undersides,
   chin, soles, belly are nearly invisible. If it doesn't read in Numpad 0, it
   doesn't exist. This single habit prevents the classic failure of modeling a
   beautiful asset that turns to mush at gameplay distance.
4. **Silhouette check shading**: Viewport shading dropdown → Solid → Lighting: Flat,
   Color: Single, set the color to near-black, background light. Toggling this while
   in the game camera gives you a pure silhouette read. Do this at the end of every
   blockout.
5. **Face orientation overlay**: Overlays dropdown → Face Orientation ON while
   modeling (blue = outward normal, red = flipped). Flipped normals render invisible
   or shade wrong in Unity with backface culling. Fix anytime with select all
   (**A**) → **Shift+N** (Recalculate Outside).

---

## 2. The core box-modeling loop (applies to every asset)

The discipline: **blockout → silhouette → deformation loops → cleanup**. Never skip
ahead. Detail added before proportions are locked is detail you'll delete.

### 2.1 Blockout (proportions only, primitives only)
- `Shift+A → Mesh → Cube`, **Tab** into Edit Mode.
- **Delete half for the Mirror modifier**: **Numpad 1** front view, **Alt+Z** (X-ray
  ON — critical, otherwise box-select only grabs front-facing verts), box-select
  (**B**) the -X half, **X → Vertices**. Add **Mirror modifier** (wrench icon),
  Axis X, enable **Clipping**.
  - *Why Mirror*: you model one half, the other half is generated live. Halves your
    work AND guarantees the symmetry a bilateral creature/character must have.
    *Why Clipping*: it welds and locks center-seam verts to X=0 so you can't
    accidentally drag them across the centerline and create a cracked seam.
  - *When to apply it*: props — before UV collapse (§6). Cole — before FBX export to
    Mixamo (Mixamo needs one real watertight mesh, and you'll want to break symmetry
    slightly afterward anyway if the design calls for it, e.g. the Second Hand on one
    wrist — model that asymmetric piece as a separate object AFTER applying).
- Rough the mass in with **only** these: **G** (move), **S** (scale), **R** (rotate),
  **E** (extrude), **Ctrl+R** (loop cut). Extrude in ortho views against the refs.
  - *How much to extrude*: extrude at every point where the reference silhouette
    changes direction. Torso example: one extrusion pelvis→waist, waist→ribcage,
    ribcage→shoulders. That's it — 3 segments, because the torso outline has 3
    direction changes. You are digitizing the outline, not sculpting anatomy.
- **Check in game camera (Numpad 0) + silhouette shading before moving on.**
  Blockout is DONE when the black silhouette from the gameplay angle is
  unmistakably "raptor" / "martial artist" / "fallen log". Budget 20–30 min max per
  asset for blockout; if it takes longer, the reference sheet is ambiguous — fix
  the reference, not the mesh.

### 2.2 Silhouette refinement
- **Ctrl+R** loop cuts ONLY where the outline needs a new direction change
  (scroll wheel while placing to add multiples, but you almost never want more
  than 1 at a time; **Esc** after click to leave the cut centered).
- Slide existing geometry before adding new: **GG** (edge slide) moves a
  loop along the surface without changing topology. Cheaper than a new cut.
- **Alt+Click** selects an edge loop; **Alt+Shift+Click** adds loops to selection.
  Select a loop, **S** to fatten/thin that cross-section against the ref.
- **Ctrl+B** bevel on hard corners that the camera sees as outline — a single-segment
  bevel turns a 90° corner into two 45° ones and reads dramatically softer for
  +2 tris. Use on shoulder pads, log ends, rock edges. Never bevel edges the
  camera can't see.
- **Proportional editing (O)** for organic swells: with it on, **G** on one vert
  drags neighbors with falloff (scroll to change radius). Ideal for the belly curve
  of the Swiftjaw or a rock's lump. Turn it OFF (**O** again) immediately after —
  leaving it on causes mystery mesh damage later.

### 2.3 Deformation loops (characters/creatures only — skip for props)
- Every bending joint needs **3 loops**: one at the crease, one ~5–8 cm above,
  one below. Elbows, knees, shoulders, hips, neck root, tail segments, jaw hinge.
  *Why*: the auto-rigger (Mixamo) assigns skin weights across nearby loops.
  One loop = the joint collapses into a paper crease when animated. Three loops =
  the bend distributes and holds volume. This is where most of Cole's tri budget
  goes, and it's the one place where "invisible" geometry is mandatory.
- Loops must be **quads** in deforming regions. N-gons and long skinny triangles
  at a joint produce ugly weight artifacts. On static props, n-gons on flat faces
  are fine — Unity triangulates them harmlessly.
- Tail/neck (Swiftjaw): extrude a chain of segments following the side ref, one
  segment per direction change of the curve, then one extra loop mid-segment on
  the sections that will flex most (base of tail, mid-neck).

### 2.4 Cleanup pass (every asset, before UV)
1. **A** (select all) → **M → By Distance** (merge doubles). Default 0.0001 m is fine.
2. **Shift+N** recalculate normals outside; scan with Face Orientation overlay.
3. Delete never-seen faces: soles of feet, log underside, rock bottoms. From the
   game camera the ground plane hides them forever. Free tris.
4. Select all → **Mesh menu → Sort/Cleanup → Degenerate Dissolve** kills zero-area
   junk faces.
5. Shading: Object Mode → right-click → **Shade Auto Smooth**, angle **45–60°**.
   *Why*: pure flat shading screams "programmer low-poly"; pure smooth melts your
   forms. Auto Smooth keeps intentional hard edges (crystal facets, armor plates)
   crisp while rounding organic surfaces — and the toon shader's ramp needs those
   smooth normals to produce clean banding. For the amber shards specifically,
   use **Shade Flat** — facets are the point.

---

## 3. Asset 1: Cretaceous prop cluster (do FIRST — it's pipeline training)

The props exist to teach you the full loop (model → palette UV → export → Unity →
toon shader) on assets where mistakes cost 20 minutes, not 8 hours. Do the entire
pipeline end-to-end on ONE rock before modeling anything else. Log hours from the
first keystroke.

- **Rocks**: Cube → **Ctrl+2** (Subsurf level 2 as a starting ball — yes, this is
  the one sanctioned Subsurf use) → apply it (**Ctrl+A** in the modifier dropdown) →
  then push verts with proportional editing (**O**) to break the symmetry, scale
  flat on Z a bit (**S, Z, 0.7**). Then **decimate if over budget**: Decimate
  modifier, Collapse ~0.5, apply. Shade Auto Smooth. 5–10 minutes each. Make 3
  size variants; rotation/scale variation in Unity multiplies them for free.
- **Fallen log**: Cylinder (**Shift+A**), set vertices to **8** in the redo panel
  (bottom-left, immediately after adding — it disappears once you do anything else).
  *Why 8*: from the game camera a log's roundness reads at 8 sides; 16 doubles cost
  for zero visible gain. Extrude the ends, **Ctrl+B** bevel the end rim once,
  extrude 2–3 stub branches, one loop cut mid-length and **R** a few degrees so it
  isn't a perfect tube.
- **Fern**: NO alpha cards. *Why*: alpha textures fight the palette-atlas workflow
  (they need dedicated texture space), cost overdraw on tile-density foliage, and
  alpha-clipped edges break the clean toon outline. Instead: one plane, edit mode,
  scale to frond shape, 2 loop cuts along its length, curl it with **R** per
  segment, taper the tip (**S** on the end verts, **M → At Center** to close the
  point). Then **Shift+D** duplicate + **R,Z** rotate to fan 5–7 fronds around a
  center. ~30 tris per frond. Give fronds slight droop variance so the cluster
  silhouette isn't a perfect starburst.
- **Amber/temporal debris**: Ico Sphere subdivisions 1 (80 tris), scale-stretch,
  Shade Flat, done. Its palette cell should be the single most saturated color in
  the cluster — this is the "time residue" visual language seed that pays off at
  the Tyrant.

Cluster composition check: arrange all props together, view from **Numpad 0**
game camera, silhouette shading ON. Each prop type must be identifiable in pure
black. This is the same one-shape-class-per-enemy rule applied to set dressing.

---

## 4. Asset 2: Cole

Order of operations matters here because of Mixamo.

1. **Pose to model in: T-pose.** Arms straight out horizontal, palms down, legs
   hip-width, fingers (such as they are) slightly spread. *Why T over A*: Mixamo's
   auto-rigger places shoulder/armpit weights most reliably from a T-pose; A-poses
   sometimes fuse armpit weights on low-poly meshes.
2. **Mitten hands.** Palm + thumb as one extruded block, a single crease loop where
   knuckles would be. *Why*: full fingers cost 800–1,500 tris and 10 bones of
   Mixamo weighting that will NEVER be readable at 150 px tall. Mixamo rigs
   mittens fine (it just skips finger bones). The Second Hand device on the wrist
   is a bigger, more readable "hand detail" than fingers ever would be.
3. **Face is palette, not geometry.** Head is a beveled box: brow plane, cheek
   plane, jaw. Eyes = dark palette cells on flat faces, or at most a shallow
   **I** (inset) + tiny push. No nose loop, no mouth loop. From 50° above you see
   hair and brow, period. Spend the saved tris on the hair silhouette — that's
   what identifies Cole from the gameplay camera.
4. **Where the budget actually goes**: 3-loop joints (§2.3) at shoulders, elbows,
   wrists, hips, knees, ankles, neck; torso twist loop at the waist (martial
   artist — the spine twist in attack animations needs it); clothing silhouette
   breaks (jacket hem, belt, boot cuffs) as real geometry steps, because the toon
   shader will draw those as clean outline lines.
5. **Second Hand device**: separate object, modeled after Mirror is applied,
   parented later or joined (**Ctrl+J**) before export. Slight oversize — 1.3–1.5x
   "realistic" — or it vanishes at gameplay distance, and it's the single most
   plot-critical prop in the game.
6. **Pre-export checklist**: apply Mirror; **Ctrl+A → All Transforms** in Object
   Mode (*why*: unapplied rotation/scale is the #1 cause of Mixamo importing your
   character lying down or 100x too big); merge by distance; one mesh
   (**Ctrl+J** if the device is separate); feet at Z=0; facing **-Y** (Blender
   front).
7. **Export**: File → Export → FBX. Limit to Selected Objects, Apply Scalings =
   FBX All, Forward -Z, Up Y (defaults are correct), Apply Transform checkbox ON.
   Upload to Mixamo, place the markers (chin, wrists, elbows, knees, groin), and
   test with a run cycle AND a fast attack-like animation (e.g. a punch) — run
   cycles hide bad shoulder weights, punches expose them. If shoulders candy-wrap,
   add one more loop around the deltoid and re-export; that fixes 90% of cases.

UV Cole with the palette method (§6) **before** the Mixamo trip — Mixamo preserves
UVs, and it's easier to select color regions on the un-rigged mesh.

---

## 5. Asset 3: Swiftjaw

- Model in a **neutral standing pose** (legs under body, tail straight back, neck
  in a relaxed S), not a lunge. *Why*: the sprint uses a static pose, but this
  mesh becomes the Rigify/hand-key base later — a mesh modeled mid-lunge can never
  be rigged cleanly. For the sprint screenshot, pose it with simple object-mode
  rotation of duplicated body parts if needed, or just place it neutral;
  readability of the SHAPE is what the r/DestroyMyGame test measures.
- Build order: torso box → extrude neck chain forward-up → head box (jaw as a
  separate lower extrusion with its own hinge loop — you WILL want to open that
  mouth in animation) → extrude tail chain (5–7 segments, taper with **S** each
  extrusion, ~0.8x per segment for a natural taper) → legs extruded DOWN from the
  torso underside (select the underside face, **I** inset first so the leg has
  its own face to grow from, then **E**), 3 loops at knee and ankle, two-toe foot
  block with one big sickle-claw wedge — that claw is the Swiftjaw's
  silhouette signature; make it 2x anatomical size.
- Silhouette test from the game camera: horizontal, low, arrow-like. It must be
  un-confusable with Cerashorn (bulk-forward wedge) and Sailspit (vertical sail).
  If the black shape could be any of the three, the neck/tail line is too thick
  or the claw too small.

---

## 6. Palette atlas UV workflow (the whole texturing pipeline)

One texture for the entire game. Zero texture painting. This is how the sprint's
28–40 hour estimate is even possible.

1. **Make the atlas once**: a 256×256 PNG, 8×8 grid = 64 cells of 32 px, each a
   flat color. Build it in any image editor from the game palette. Leave 2–3 rows
   empty for future biomes. Optional: one row of 2-tone gradient cells for subtle
   ramps (skin, amber) — place UVs on the gradient axis for a free painted look.
2. **One material, every object**: New material → Base Color → Image Texture →
   the atlas. Name it `M_Palette`. Every asset in the game uses this one material.
   *Why this matters in Unity*: one material = one draw-call batch group for all
   static props, and the toon shader is configured exactly once.
3. **Assigning colors**: in Edit Mode, select all faces of one color region
   (hover + **L** selects linked; **Shift+L** adds; for regions within a connected
   mesh, select faces manually with **3** face mode + **C** circle select).
   Open a UV Editor area. With faces selected: **U → Reset** (stacks every face
   onto the full 0–1 square), then in the UV editor **S → 0.05** (collapse to a
   dot — actually keep it ~5% size, not literally zero, to avoid degenerate-UV
   warnings), then **G** and snap the dot into the target color cell.
   *Why collapsing works*: every vertex of those faces samples one flat pixel
   region → perfectly flat color, no seams possible, no unwrapping skill needed,
   and mirrored/overlapping UVs are a non-issue because there's nothing directional
   to mirror.
4. Repeat per color region. A prop is 2–4 regions; Cole maybe 8–10 (hair, skin,
   jacket, jacket trim, pants, boots, belt, device body, device glow).
5. **Device glow / amber glow cells**: reserve specific atlas cells for emissive
   colors and note their UV coordinates — in Unity the toon shader (or a second
   emissive material variant) keys emission off those cells. Decide once, document
   in this file's appendix, never move those cells again.

---

## 7. Export to Unity + gotchas

- **Ctrl+A → All Transforms** on everything, always, before export. Tattoo this.
- Props: export the cluster as one FBX with each prop a separately named object
  (`SM_Rock_A`, `SM_Log_A`, `SM_Fern_A`...) — Unity splits them, and you keep one
  import to manage. Origins at the base of each prop (**right-click → Set Origin →
  Origin to 3D Cursor** after **Shift+S → Cursor to World Origin** with the prop
  moved so its base sits at 0) — *why*: base-origin props snap to terrain sanely.
- Characters: origin between the feet at Z=0 for the same reason.
- FBX scale sanity check in Unity: drop the mesh next to a default 1 m cube. Cole
  should be ~1.8 cubes tall. If he's 100x off, the Apply Transform export setting
  was missed.
- Toon shader check happens in the GAME scene with the GAME camera, not a Blender
  render. Blender's viewport is never the ground truth for this project;
  the composed combat screenshot is.

---

## 8. Sprint discipline

- **Log hours per phase per asset** (blockout / refine / cleanup / UV / export-fix).
  The decision gate needs hours-per-asset, but the phase breakdown tells you WHERE
  time goes — if UV is eating hours, the palette process needs fixing before
  scaling to 7 enemies × 5 biomes; if blockout is, the reference sheets do.
- Hard stop rule per asset: props 6–8 h total, Cole 14–18 h, Swiftjaw 8–12 h.
  Hitting a stop isn't failure — it's the data point the sprint exists to produce.
- The first rock will take 3x longer than the last rock. Don't extrapolate the
  timeline from asset #1; extrapolate from asset #3 onward.
