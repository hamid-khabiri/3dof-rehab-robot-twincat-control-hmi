# Rehabilitation Games for a 3-DOF Upper-Limb Robot — Unity ↔ TwinCAT ADS Integration

C# source code of four **Unity** rehabilitation games that run with a custom **3-DOF planar upper-limb rehabilitation robot**. The robot is controlled in real time by a **Beckhoff TwinCAT 3** PLC; the games exchange data with the PLC through **TwinCAT ADS** (Automation Device Specification).

**Robot control software (TwinCAT 3 PLC + HMI):** in this same repository, see the [main README](../README.md). The PLC side of each game is in `TwinCAT Robot-V1/Untitled1/POUs/` (`Crazy Car/`, `Firrfly Game/`, `PeelerGame/`, `Sorter Game/`).

**Portfolio:** [hamid-khabiri.github.io](https://hamid-khabiri.github.io)

**Project page with videos:** [3-DOF Upper-Limb Rehabilitation Robot](https://hamid-khabiri.github.io/projects/upper-limb-rehabilitation-robot.html)

> The games need the robot and the TwinCAT runtime to be played as intended. This repository is published to show the **PLC ↔ Unity data exchange** and the game software structure, not as a stand-alone playable build.

---

## System architecture

```
 ┌───────────────────────────────┐        TwinCAT ADS         ┌───────────────────────────────┐
 │  TwinCAT 3 PLC (real time)    │  (AMS Net ID, port 851)    │  Unity game (C#)              │
 │                               │                            │                               │
 │  EtherCAT → EPOS4 drives      │   stToUnity  ────────────► │  Connector reads robot data   │
 │  Kinematics, impedance /      │   (robot position, wrist)  │  every frame → game input     │
 │  admittance control, safety   │                            │                               │
 │                               │   stFromUnity ◄─────────── │  Game status, level, events   │
 │  Game FB per game             │   (status, events)         │  (written on change / pulse)  │
 │  (MAIN.fbCrazyCar, ...)       │                            │                               │
 └───────────────────────────────┘                            └───────────────────────────────┘
```

- **Robot → game:** the PLC computes the end-effector position (and the wrist angle) from the joint encoders and publishes it, normalized for the game, in a `stToUnity` structure.
- **Game → robot:** the game writes its state (menu, active level, paused, closed) and game events to a `stFromUnity` structure, and the PLC adapts the robot to it. Example (Crazy Car): the vehicle chosen in Unity (`nGameStatus` 1 = taxi, 2 = van, 3 = truck) selects the resistance torque the PLC applies on the wrist joints while the patient turns the wrist; in the menu (`0`) or after `bGameClosed`, no resistance is applied.
- Each game has its own function block in the PLC (`MAIN.fbCrazyCar`, `MAIN.fbFireflyGame`, `MAIN.fbPeelingGame`, `MAIN.fbSorterGame`), so the four games share one robot-side pattern.

### How the ADS link is implemented (C#)

- `TcAdsClient.Connect(amsNetId, 851)` to the TwinCAT 3 PLC runtime (local AMS Net ID by default, configurable in the Inspector).
- `CreateVariableHandle("MAIN.fb....stToUnity.fX")` once at start-up; then `ReadAny(handle, typeof(float))` every frame in `Update()`.
- `WriteAny(handle, value)` for game status and events, written when the game state changes.
- **Structured exchange:** Fruit Peeler reads/writes whole PLC structures using C# structs with `[StructLayout(LayoutKind.Sequential, Pack = 1)]` and `MarshalAs(UnmanagedType.I1)` for `BOOL`, matching the TwinCAT `STRUCT` layout byte-for-byte.
- **Event pulses:** Firefly Garden sends one-shot `BOOL` pulses (e.g. wrong choice, exit pressed) with a defined high/low time so the PLC cycle cannot miss them (`BooleanPulse.cs`).
- **Robustness:** status symbols are optional, so a missing symbol never stops the position from being read; read/write errors are logged once per failure streak instead of every frame; handles are deleted and the game sends a "closed" state on exit.
- **Hardware-free testing:** each game also has a mouse/keyboard input provider behind the same interface (`IFireflyPositionInput`, `IHorizontalInputProvider`, `RobotInterface` / `MKBRobotInterface`), so the game logic can be developed and tested without the robot.

---

## The four games

| Game | Movement trained | Robot → Unity (`stToUnity`) | Unity → Robot (`stFromUnity`) | ADS connector |
|---|---|---|---|---|
| **Crazy Car** | Forward/backward reach (gas pedal) + wrist rotation (lane choice) | `fY` (REAL, 0–100 %), `fWristAngle` (REAL, deg) | `nGameStatus` (INT), `bGameClosed` (BOOL) | `CrazyCar/.../CrazyCar/TwincatConnectorCrazyCar.cs` |
| **Firefly Garden** | 2-D reaching to targets (reach, find the match, remember) | `fX`, `fY` (REAL) | `nGameStatus` (INT: 0 menu, 1–3 level, 4 paused), `bExitPressed`, `bWrongFlower` (BOOL pulses) | `FireflyGarden/.../Robot/TwinCatFireflyConnector.cs`, `TwinCatSettings.cs` |
| **Fruit Peeler** | Repeated vertical (forward/backward) strokes | `ST_PeelerToUnity`: `fY` (REAL, 0–100 %), `GameIsActive` (BOOL) | `ST_PeelerFromUnity`: `bInContact` (BOOL), `nObjectType` (INT), `bGameFinished` (BOOL) | `FruitPeeler/Assets/Scripts/TwinCATManager.cs` |
| **Cargo Sorter** | Medial–lateral reaching with graded load | `fX` (REAL, 0–100 %) | `nGameStatus` (INT: 0 not playing, 1–3 bin weight), `bGameClosed` (BOOL) | `CargoSorter/Assets/Scripts/TwincatConnectorSorter.cs` |

---

## Folder structure

```
CrazyCar/        Crazy Car (built on the Highway Drive framework): road, traffic, control math,
                 session flow, data logger, ADS connector, editor scene builders, unit tests
FireflyGarden/   Firefly Garden: configuration, trial generation, session, presentation,
                 data recording, robot input and ADS connector
FruitPeeler/     Fruit Peeler: game and level managers, peeler controller, session flow,
                 metrics, ADS manager
CargoSorter/     Cargo Sorter: trial manager, product movers, trolley controller, UI,
                 session flow, ADS connector
```

Only the project's own C# scripts are included. **Not included:** Unity scenes, prefabs, art, audio and fonts; third-party packages (TextMesh Pro, the "Flat game GUI pack" asset pack); and Beckhoff's `TwinCAT.Ads` library (available from Beckhoff / NuGet). Logged session data are not included.

---

## Contributors

| Person | Role |
|---|---|
| **Hamid Khabiri, PhD** | **Project lead and supervisor.** Designed the four games (concepts, rules, levels and the therapy movements they train); revised and extended the Unity implementation; designed and implemented the Unity ↔ TwinCAT ADS connectors and data structures and the PLC side of every game; integrated, tested and tuned the games on the robot |
| **Simon Corazza** | Co-developer — initial Unity implementation of **Crazy Car** |
| **Grecia Michelle Luis** | Co-developer — initial Unity implementation of **Firefly Garden**, **Fruit Peeler** and **Cargo Sorter** |

Developed at the University of Alberta, Edmonton, Canada.

---

## Copyright

© 2026 Hamid Khabiri, Simon Corazza and Grecia Michelle Luis. **All rights reserved.**
This code is published for portfolio and review purposes only. No license is granted to copy, modify, distribute or use it, in whole or in part, without written permission.
