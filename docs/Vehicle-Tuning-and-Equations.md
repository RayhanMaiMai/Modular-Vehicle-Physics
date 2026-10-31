# DriveFeel — Vehicle Tuning & Mathematical Reference

**Scope:** A designer-facing explanation of the major parameters, how each influences the vehicle, and the mathematical relationships implemented or described in the project's source audits. This covers `CarProfile`, `DrivingStyleProfile`, drivetrain, tire, gearbox, suspension and anti-roll.

**Important evidence boundary:** This document is based on the accepted CODEX-05/08/12/13R/14/15 implementation reports and static equations in the earlier force-budget audit. It is **not** a fresh line-by-line validation against the live GitHub branch (which was inaccessible during drafting). Equations identified as **implementation-based** are directly supported by those reports; relationships marked **conceptual** describe intended behavior and should be checked against the current source before treating their exact operators as an API contract. No physical unit system has been formally established: treat forces as *Unity force-like units* and velocities as *Unity distance units per second*, not experimentally calibrated real-world values.

## 1. Configuration hierarchy and notation

- `CarProfile`: **identity/performance**, shared ScriptableObject. Example accepted CityCar whole-car acceleration and service-brake budgets: `70 / 100`; Trueno: `40 / 60`.
- `DrivingStyleProfile`: **optional handling behavior** with five independent override flags.
- Prefab components: authored **baseline** and physical geometry (wheel transforms, tire radius, rest distance, chassis Rigidbody/mass, contact masks and references).
- Runtime snapshot: effective per-vehicle values resolved once when the vehicle initializes. Disabling a style section preserves its authored baseline; an invalid section falls back instead of partially applying.

Notation:

| Symbol | Meaning |
| --- | --- |
| `u` | Signed throttle/brake input; `|u|` is input magnitude |
| `v_f` | Chassis forward velocity (`dot(chassis.forward, rigidbody.linearVelocity)`) |
| `v_lat,i` | Lateral velocity of tire `i` at its contact/axle point |
| `Δt` | Unity fixed physics timestep (`0.02 s` in audited project) |
| `C(x)` | Curve evaluation at normalized coordinate `x` |
| `clamp(x,a,b)` | Restrict a value to the inclusive interval `[a,b]` |
| `K` | A configurable response coefficient, dependent on the section |
| `FL, FR, RL, RR` | Front-left, front-right, rear-left, rear-right tires |

To compare styles fairly, keep the same test scene, car identity, road and entry speed, and change one override section at a time. **Leave Play Mode and re-enter after editing profile values.**

---

## 2. Vehicle identity: `CarProfile`

### 2.1 Power-curve reference speed (`topSpeed`)

**Implementation-based:**

$$x_v=\operatorname{clamp01}\!\left(\frac{|v_f|}{V_{ref}}\right)$$

$$P(v_f)=C_{power}(x_v)$$

- `V_ref = topSpeed` is a **curve normalization point**, **not a guaranteed top-speed cap**.
- Increasing `V_ref` makes the curve reach its high-speed end at a greater absolute speed; decreasing it makes the curve advance more quickly.
- Avoid zero/negative reference speed. Current profile values from accepted audits: CityCar `45`, Trueno `30`.

### 2.2 Vehicle-wide motor-force request

**Implementation-based:**

$$M = A_{car}\;T_{out}\;P(v_f)$$

- `A_car = maxAccelerationForce` is the whole-vehicle budget (`70` CityCar; `40` Trueno).
- `T_out` is the signed gearbox output multiplier; positive forward and negative reverse signs are resolved through the controller.
- The actual acceleration is affected by Rigidbody mass, grounded tires, force directions and the gearbox, not by `A_car` alone.

### 2.3 Vehicle-wide service-brake request

**Implementation-based:**

$$B_{request}=B_{car}|u|\;a(t)$$

where `B_car = maxBrakingForce` (`100` CityCar; `60` Trueno) and `a(t)=1` unless the existing brake modulation branch applies. The controller assigns **one quarter of this budget to each tire** before any driven-axle engine-brake contribution:

$$B_{service,i}=\frac{B_{request}}{4}$$

A force *request* is not a guaranteed braking force: grounded state and local wheel-point velocity constrain the force applied.

### 2.4 FWD/RWD/AWD distribution

**Implementation-based:** For front bias `b`, define `(β_F, β_R)` as `(1,0)` for FWD, `(0,1)` for RWD, and `(b,1-b)` for AWD. Then:

$$M_{FL}=M_{FR}=\frac{M\beta_F}{2}$$

$$M_{RL}=M_{RR}=\frac{M\beta_R}{2}$$

For an all-grounded, straight-wheel case, the total requested longitudinal motor force equals `M`. **Airborne tires do not cause automatic redistribution.**

**Engine braking:** With `e` the normalized `EngineBrakeMultiplier`, the controller uses the whole-car request `E=eB_car` and distributes it by the same driven-axle bias:

$$E_{FL}=E_{FR}=\frac{E\beta_F}{2},\quad E_{RL}=E_{RR}=\frac{E\beta_R}{2}$$

**Designer effects:** Changing AWD bias changes which axle is driven, not the car-wide total commanded budget. The project does not simulate an open/LSD differential.

---

## 3. Driving-style section 1 — Steering

| Style variable | Math role | Higher setting generally does... |
| --- | --- | --- |
| `lowSpeedSteerAngle` | Maximum steering at low speed, `θ_low` | Allows sharper low-speed turning |
| `highSpeedSteerAngle` | Maximum steering at reference/high speed, `θ_high` | Retains more steering authority at speed |
| `highSpeedThreshold` | Speed normalization divisor, `V_steer` | Delays reduction toward high-speed angle |
| `steerSmoothTime` | `SmoothDamp` smoothing time, `τ` | Makes steering respond more slowly/smoothly |

**Implementation-based:** The speed-dependent steering limit uses interpolation between the two endpoints:

$$q=\operatorname{clamp01}\!\left(\frac{|v_f|}{V_{steer}}\right)$$

$$\theta_{limit}=(1-q)\theta_{low}+q\theta_{high}$$

A signed steering input then determines the requested steering direction. `Mathf.SmoothDamp` filters the steering state using `τ` and the fixed-step delta `Δt`; **this is a stateful Unity function, not ordinary linear interpolation**.

The existing Ackermann-style geometry uses prefab-owned wheelbase `L` and track width `W` to produce distinct left/right wheel angles. An *idealized Ackermann reference* is:

$$\tan\theta_{inside}=\frac{L}{R-W/2},\qquad\tan\theta_{outside}=\frac{L}{R+W/2}$$

This idealized identity illustrates why the inner tire turns more than the outer tire; **it is not a claim that the code implements these exact two equations**. Geometry stays on the prefab (audited canonical `L=2.25`, `W=2`).

**Baseline / provisional style samples** (used in the manual comparison workflow; confirm exact committed assets):

| Setting | Balanced | Responsive | Progressive |
| --- | ---: | ---: | ---: |
| Low-speed angle | `35°` | `38°` | `30°` |
| High-speed angle | `18°` | `22°` | `14°` |
| Threshold | `25` | `30` | `20` |
| Smooth time | `0.08` | `0.04` | `0.18` |

---

## 4. Driving-style section 2 — Tire response

Each tire has its own resolved runtime settings, and the front/rear style values are applied to the matching pair. Grip curves are **deep-copied** for each tire so one vehicle cannot mutate another through shared assets.

| Style variable | Mathematical effect | Tuning consequence |
| --- | --- | --- |
| Front/rear `gripModifier` | Multiplies evaluated lateral grip (`g`) | More lateral correction at the axle; changes cornering balance |
| Front/rear `maxSlipSpeed` | Normalizes slip speed (`V_slip`) | Larger value delays progression along grip curve |
| Front/rear `gripCurve` | Nonlinear grip response `C_grip(s)` | Shapes how grip changes as lateral slip grows |

**Implementation-based normalization:**

$$s_i=\operatorname{clamp01}\!\left(\frac{|v_{lat,i}|}{V_{slip,i}}\right)$$

$$G_{base,i}=g_i\;C_{grip,i}(s_i)$$

The current arcade model uses lateral velocity cancellation as its base demand:

$$F_{lat,cancel,i}=-m_{t,i}\frac{v_{lat,i}}{\Delta t}$$

where `m_t` is a **tire force-scaling coefficient**, not the mass of a wheel Rigidbody. The evaluated grip response scales the lateral correction, also subject to handbrake/surface/longitudinal-effort modifiers:

$$F_{lat,i}\approx F_{lat,cancel,i}\;G_{effective,i}$$

**Conceptual, not exact operator-level code:** The implementation includes a hardcoded longitudinal-effort reference `25` and a maximum lateral reduction factor `0.4`. Their broad effect can be represented by an attenuation term such as:

$$R_{arcade}=1-0.4\;\operatorname{clamp01}\!\left(\frac{|F_{long}|}{25}\right)$$

The precise computation order, signs and normalization should be checked against current `TirePhysics.cs` before presenting this *illustrative* term as exact source behavior. These two numbers were intentionally **not** exposed as style settings by CODEX-14/15.

**Important:** This is not a validated load-sensitive friction circle or real tire slip-ratio model. A larger grip multiplier may change yaw/slide recovery as much as maximum cornering ability. Use a repeatable turn to judge results.

**Accepted authored baseline from audits:** front `gripModifier=1`, rear `0.95`, all `maxSlipSpeed=8`, same four-key grip curve. For style overrides, copy the accepted curve rather than inventing its keyframes.

---

## 5. Driving-style section 3 — Rear handbrake

| Style variable | Role | Higher value generally does... |
| --- | --- | --- |
| `rearHandbrakeGripMultiplier` | Scales rear lateral response while handbrake is active (`h`) | Retains more lateral grip (less free rotation) |
| `rearHandbrakeDragForce` | Absolute rear **per-tire** cancellation cap (`H`) | Permits stronger longitudinal slowing under handbrake |

**Implementation-based:** On a grounded handbraked rear tire, the handbrake branch takes priority over normal drive/service-brake longitudinal requests:

$$F_{HB,i}=\operatorname{clamp}\left(-m_{t,i}\frac{v_{f,i}}{\Delta t},\,-H_i,\,+H_i\right)$$

$$G_{HB,i}=G_{normal,i}\;h_i$$

With two grounded rear tires at cap, total absolute rear longitudinal handbrake request can reach approximately `2H`, because `H` is **per tire**, not a whole-car budget. Front normal service braking is handled independently.

**Sample values** (provisional comparison profiles, not physical calibration):

| Setting | Balanced | Responsive | Progressive |
| --- | ---: | ---: | ---: |
| Rear grip multiplier | `0.80` | `0.55` | `0.90` |
| Rear drag cap / tire | `15` | `12` | `18` |

These handbrake settings **do not change** the ordinary whole-car service-brake budget in `CarProfile`.

---

## 6. Driving-style section 4 — Suspension

| Style variable | Formula parameter | What changes |
| --- | --- | --- |
| Front spring strength | `k_F` | Force response to front suspension compression |
| Front damper strength | `c_F` | Opposes front suspension travel velocity |
| Rear spring strength | `k_R` | Force response to rear suspension compression |
| Rear damper strength | `c_R` | Opposes rear suspension travel velocity |

**Implementation-based equation (unchanged by CODEX-15):**

$$F_{s,i}=\big(d_{rest,i}-d_{current,i}\big)k_i-v_{s,i}c_i$$

- `d_rest` is prefab-authored suspension rest distance, and `d_current` comes from the tire spherecast/contact geometry.
- `v_s` is the suspension-point motion component used for damping (exact sign convention follows the implementation).
- `k>0`, `c≥0` for valid style overrides; if any section value is invalid, **the entire suspension style section falls back** to per-tire authored baselines.
- `d_rest`, tire radius, wheel placement and contact masks are **not style settings**.

**Canonical authored baseline:** all four tires `spring=250`, `damper=45`. A stronger spring usually resists compression more; excessive damping can reduce responsiveness or cause harsh transient behavior. Test flat-ground height, one-wheel bump, recontact and cornering.

---

## 7. Driving-style section 5 — Bounded anti-roll

| Style variable | Role | Safety boundary |
| --- | --- | --- |
| Front anti-roll strength | `K_F` | Nonnegative, validated |
| Front max force per side | `Fmax_F` | Cannot exceed prefab-authored front cap |
| Rear anti-roll strength | `K_R` | Nonnegative, validated |
| Rear max force per side | `Fmax_R` | Cannot exceed prefab-authored rear cap |

**Implementation-based algorithm:** Only when the prefab bar is enabled, both wheels on that axle are grounded, linkage is valid and parameters are finite:

$$L=\operatorname{clamp01}\left(\frac{d_L}{d_{rest,L}}\right),\quad R=\operatorname{clamp01}\left(\frac{d_R}{d_{rest,R}}\right)$$

$$F=\operatorname{clamp}\big((L-R)K,\,-F_{max},\,+F_{max}\big)$$

$$\vec F_L=-F\;\widehat{up}_{chassis},\qquad \vec F_R=+F\;\widehat{up}_{chassis}$$

The two forces are applied at separate left/right tire positions on the **root chassis Rigidbody**. They produce a **restoring roll torque** but cancel their net vector force:

$$\vec F_L+\vec F_R=0,\qquad \vec\tau=\vec r_L\times\vec F_L+\vec r_R\times\vec F_R$$

**If either tire is airborne, the entire axle produces zero anti-roll force.** A style cannot turn an authored OFF gate ON or exceed the vehicle's cap. The anti-roll section is validated as a pair and rejected atomically for invalid or unsafe overrides.

**Accepted canonical prefab:** front `K=100`, cap `10`; rear `K=60`, cap `6`, both enabled, main chassis Rigidbody mass `12`. These caps are **ceilings**, not recommended maximum settings for every style. The previous high values `10000/8000` are historical and must not be restored.

---

## 8. Gearbox and braking parameters (not DrivingStyleProfile overrides)

These remain vehicle/component configuration, not part of the first Option C handling-style overlay.

| Parameter | Current mechanism and mathematical role | Design effect |
| --- | --- | --- |
| Transmission mode | Automatic selects reverse/neutral/forward according to signed speed/pedal; manual uses shift-edge commands | Determines direction/shift interaction |
| Gear speed threshold `V_g` | Normalizes gameplay RPM/limits forward drive near gear ceiling | Changes when a gear reaches its useful range |
| Gear torque multiplier `r_g` | Multiplies engine response | Stronger/weaker drive in each gear |
| Engine torque curve | `q=C_engine(RPM_norm)` | Shapes drive response across gameplay RPM |
| Shift delay | `t_shift` | Momentary torque interruption while shifting |
| Engine braking strength | `k_E` | Scales coast deceleration in forward gear |
| Reverse speed reference | `V_reverse` | Reverse pseudo-RPM normalization, **not a strict reverse limiter** |
| ABS modulation | `a(t)` | Pulses normal service-brake request with time, not measured slip |

**Conceptual/implementation-derived gearbox relation for forward drive:**

$$T_{out}=u\,r_g\,C_{engine}(RPM_{norm})\,L_g$$

where `L_g` is a forward-gear limiting factor and `RPM_norm` is normalized pseudo-RPM (`0–1`), **not real engine revolutions per minute**. When shifting, output torque and engine-brake multipliers are set to zero during the cut.

For forward-gear coasting, CODEX-08/09 support:

$$e=k_E\,r_g\,RPM_{norm},\qquad E=B_{car}\,e$$

Neutral and Reverse explicitly reset stale engine-brake output to zero. The canonical engine-brake strength remains `0.075`; canonical shift delay is `0.3 s`.

**Time-sine brake modulation:** Under the configured ABS-like input branch, the existing model is approximately documented as:

$$a(t)=\operatorname{lerp}\left(a_{min},\,1,\,\frac{\sin(t\,f)+1}{2}\right)$$

with canonical `a_min=0.5`, `f=25`, and `t` sampled in the fixed-step physics clock after P1-B. Here `f` is the **raw sine input coefficient**, not necessarily a calibrated frequency in Hz. This is **not slip-aware ABS**.

The standard tire local braking demand is a bounded local-velocity cancellation:

$$F_{brake,i}=\operatorname{clamp}\left(-m_{t,i}\frac{v_{f,i}}{\Delta t},\,-B_i,\,+B_i\right)$$

where `B_i` is the controller-provided per-tire cap (service brake and any applicable engine-brake contribution). The branch may be bypassed below the implementation's low-speed/input thresholds.

---

## 9. Geometry and other prefab-owned controls

These settings influence the same simulation but are intentionally **not** part of the current style overlay.

| Setting | Key relationship | Why prefab-owned |
| --- | --- | --- |
| Rigidbody mass `m` | Net translational response approximately `a=F_net/m` | Defines chassis inertia and response to all applied forces |
| Chassis center of mass | Torque/handling from `τ=r×F` around COM | Controls roll/pitch leverage and balance |
| Wheelbase and track width | Determines Ackermann geometry and force moments | Must match actual wheel positions |
| Suspension rest distance | Compression: `d_rest-d_current` | Depends on wheel/cast geometry and ride height |
| Tire radius | Part of spherecast/visual rolling geometry | Geometry, not a behavior overlay |
| Ground/surface layer masks | Select contact and optional slippery-surface response | Environment/contact authority |
| Tire mass coefficient | Enters lateral and longitudinal cancellation force | Physics coefficient, not literal wheel Rigidbody mass |

Changing chassis dimensions, masses or wheel placement while assessing a driving-style override prevents a clean style comparison; separate those experiments.

---

## 10. Tuning methodology and diagnosis

1. **Begin with null-style baseline.** Confirm no warnings and unchanged authored/effective logs.
2. **Steering only:** test low-speed turning, then high-speed response; vary one steering parameter at a time.
3. **Tire only:** use the same grip curve initially and change front/rear modifiers/slip-speed normalization; evaluate understeer, oversteer, recovery and lateral slide.
4. **Handbrake only:** compare identical entry-speed tests; distinguish handbrake grip reduction from ordinary service braking.
5. **Suspension only:** test idle ride height, small one-wheel bump, recontact and cornering before increasing spring/damper differences.
6. **Anti-roll only:** keep both force caps at or below the authored ceilings. Test one axle at a time; stop at the first launch, violent oscillation, wheel separation or numerical warning.
7. **Combined style:** enable all sections only after isolated checks pass. Use one-shots diagnostics to verify the effective settings.
8. **Regression:** verify whole-car force budgets, normal service brakes, gearbox shifts, input possession and bounded anti-roll still behave as accepted.

### Quick troubleshooting

| Observation | Check first |
| --- | --- |
| Inspector baseline unchanged after assigning style | Expected: runtime uses resolved snapshot; enable `Log Resolved Style On Awake` |
| Style changes appear to do nothing | Override flag off, invalid section fallback, not restarted Play Mode |
| Grip override ignored | Curve invalid/empty, slip-speed zero, section rejected |
| Different handbrake but normal brakes unchanged | Expected; rear handbrake style does not alter `maxBrakingForce` |
| Suspension feels extreme | Spring/damper values, grounded state, rest distance and cast alignment |
| Anti-roll force stays zero | Prefab gate off, either axle tire ungrounded, cap zero, linkage invalid |
| Anti-roll style falls back | Requested cap exceeds the car's authored cap, invalid strength, ambiguous wheel-pair linkage |
| High-speed vehicle exceeds `topSpeed` | That field normalizes a power curve, not a hard speed cap |
| Different vehicles affect one another | Verify each controller resolves its own settings and curves; do not edit shared ScriptableObjects as runtime state |

### Suggested manual tuning record

```text
Unity version / scene / Git revision:
Vehicle identity (CarProfile):
Style asset and enabled sections:
Authored baseline values:
Resolved effective values (Awake diagnostic):
Test road / surface / entry speed / gear:
Changed one parameter from -> to:
Observed effect and unintended changes:
PASS / FAIL / revert / accept:
```

## 11. Reference implementation files

Source files reported by CODEX-13R/14/15:

- `Assets/Scripts/Car Systems/CarProfile.cs`
- `Assets/Scripts/Car Systems/DrivingStyleProfile.cs`
- `Assets/Scripts/Car Systems/CarController.cs`
- `Assets/Scripts/Car Systems/CarGearbox.cs`
- `Assets/Scripts/Car Systems/TirePhysics.cs`
- `Assets/Scripts/Car Systems/AntiRollBar.cs`
- `Assets/Scripts/Car Systems/ResolvedSteeringSettings.cs`
- `Assets/Scripts/Car Systems/ResolvedTireStyleSettings.cs`
- `Assets/Scripts/Car Systems/ResolvedSuspensionAntiRollSettings.cs`

**Documentation maintenance rule:** When the implementation changes, recheck the corresponding equation and update this guide as part of the same review. For publication, also verify the actual Input Actions bindings and third-party asset licenses rather than treating historical audits as the current checked-out source.
