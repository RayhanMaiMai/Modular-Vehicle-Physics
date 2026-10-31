# DriveFeel canonical vehicle prefab setup

## Status

The canonical prefab target is `Assets/Contents/DriveFeel_Car.prefab`.

It is intentionally generated through the Unity Editor from the active, unpacked `Car` in
`Assets/Scenes/Develop_Test Drive Mechanic.unity`. The repository does not hand-edit the
scene's serialized YAML or replace the active object automatically. Until the migration
menu command has been run and the result has been manually inspected, the canonical prefab
is **pending manual generation and verification**.

The legacy assets remain unchanged:

- `Assets/Contents/Car.prefab`
- `Assets/Contents/CarTest2.prefab`

They are retained for comparison and must not be presented as the canonical setup.

## Create the canonical prefab

Use Unity `6000.3.13f1`:

1. Open `Assets/Scenes/Develop_Test Drive Mechanic.unity`.
2. Do not enter Play Mode.
3. Run **Tools > DriveFeel > Create Canonical Vehicle Prefab**.
4. If validation reports a missing or scene-external reference, stop and inspect the named
   field. Do not bypass the check or overwrite an existing canonical prefab.
5. Confirm that `Assets/Contents/DriveFeel_Car.prefab` is created and selected.
6. Open it in Prefab Mode and compare it to the still-unpacked active scene `Car`.

The migration command uses `PrefabUtility.SaveAsPrefabAsset`. It does not connect, replace,
delete, move, or modify the active scene object, and it refuses to overwrite an existing
canonical prefab.

## Prefab-internal dependencies

The extraction validator requires these references to remain inside the `Car` hierarchy:

- Root `Rigidbody`, `CarController`, `CarCenterOfMass`, `CarGearbox`, and
  `CarAudioManager`.
- Four `TirePhysics` components, their visual transforms, the root Rigidbody, and the root
  car Transform.
- Front-left and front-right steering transforms.
- Front and rear `AntiRollBar` components and their paired tires.
- `VehicleInteract`, its root `CarController` reference, and `getOutPosition`.
- Four child `AudioSource` references used by `CarAudioManager`.
- The complete visual, collider, and Transform hierarchy under the active `Car`.

The following are persistent project assets rather than scene-injected objects and are
valid prefab references:

- `CarProfile_CityCar.asset`.
- Move, Jump/handbrake, Horn, ShiftUp, ShiftDown, and Interact sub-assets from
  `Assets/InputSystem_Actions.inputactions`.
- Meshes, materials, audio clips, and other imported visual/audio assets.

## Hosting-scene dependencies

The vehicle prefab does not own the following objects:

- `PossessionManager`, its starting Driver, and its `mainCamera` reference.
- `TiltShiftCamera`. The demo camera initially references the active car Transform, while
  `PossessionManager` changes the target when possession changes.
- The Driver object, its Rigidbody/collider, action references, interaction range, and layer
  masks.
- Terrain or another collider on the tire `drivableSurfaceLayer`.
- Lighting, volume, UI, and scene environment. No vehicle UI dependency was found.

To use the prefab in a fresh scene, add a suitable ground collider/layer and either provide
the existing possession/Driver/camera setup or invoke `CarController.OnPossess` through a
project-compatible host. Do not assume that merely dropping the prefab into an empty scene
will enable player input: the current design enables vehicle actions during possession.

## Known unresolved fields preserved by this migration

- The active car's `accelRevClip` is unassigned. This is reserved for P0-C.
- The handbrake reference uses the same action used by on-foot Jump.
- ShiftUp and Interact both use `E` on keyboard.
- Input fixes are reserved for P0-B.
- Physics, steering, gearing, force scaling, suspension, anti-roll, and audio rules are
  copied as-is and are not retuned here.

## Manual verification — not run

Record PASS or FAIL for every item; this document does not claim runtime equivalence.

1. In the original scene, inspect the active unpacked `Car` and the generated prefab side by
   side for hierarchy, layer, transforms, colliders, Rigidbody mass/interpolation, COM,
   controller, gearbox, tires, anti-roll bars, interaction, audio sources, and clips.
2. Confirm all prefab component references resolve internally and the Inspector shows no
   missing scripts.
3. Create an unsaved empty scene with a ground collider on the expected drivable layer.
4. Instantiate `DriveFeel_Car.prefab`; add the existing Driver, PossessionManager, and camera
   setup or an equivalent scene host.
5. Enter Play Mode and record console output before applying input.
6. Verify grounding and suspension travel for all four tires.
7. Verify forward power, braking, reverse, steering, automatic shifting, and engine braking.
8. Verify handbrake, horn, engine start/idle/high-RPM audio, vehicle entry, and safe exit.
9. Compare the generated prefab instance against the original scene car under the same
   ground and host setup.
10. Exit without saving the temporary scene or any test-only Inspector changes.

Automated Unity tests are outside this migration and must not be generated or run.
