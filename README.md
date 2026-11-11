# 3-DOF Upper-Limb Rehabilitation Robot — TwinCAT 3 Control Software, HMI & Unity Games

Real-time control software and operator interface for a custom **3-DOF planar upper-limb rehabilitation robot**, developed in **Beckhoff TwinCAT 3** with **EtherCAT** servo control, a **TwinCAT HMI (TE2000)** operator interface, and four **Unity** rehabilitation games connected to the PLC through **TwinCAT ADS**.

**Author:** Hamid Khabiri, PhD — Robotics Engineer

**Portfolio:** [hamid-khabiri.github.io](https://hamid-khabiri.github.io)

**Project page with videos:** [3-DOF Upper-Limb Rehabilitation Robot](https://hamid-khabiri.github.io/projects/upper-limb-rehabilitation-robot.html)

| What | Where |
|---|---|
| PLC control software (Structured Text) | [`TwinCAT Robot-V1/`](TwinCAT%20Robot-V1) |
| Operator interface (TwinCAT HMI) | [`TcHmiProject1/`](TcHmiProject1) |
| Unity games and PLC ↔ Unity ADS link (C#) | [`Unity-Games/`](Unity-Games) — see its [README](Unity-Games/README.md) |

---

## System overview

| Layer | Implementation |
|---|---|
| Actuation | Three Maxon BLDC motors with **EPOS4 EtherCAT** drives; capstan-based rotary transmission and differential timing-belt mechanism |
| Fieldbus | **EtherCAT** — PDO/SDO configuration, synchronized I/O, drive and fault diagnostics |
| Real-time control | **TwinCAT 3 PLC/NC** in Structured Text |
| Sensing | Absolute encoders, 6-axis **force/torque sensor**, limit (micro) switches |
| Operator interface | **TwinCAT HMI (TE2000)** — preparation, calibration, therapy configuration, monitoring |
| Games | Four Unity rehabilitation games exchanging data with the PLC via **TwinCAT ADS** (`Unity-Games/`) |

### Control architecture
- **Cascaded control:** EPOS4 inner current / velocity / position loops, with outer-loop joint PID controllers in TwinCAT.
- **Interaction control:** impedance control from joint position/velocity feedback, and admittance control using force/torque sensor feedback.
- **Kinematics:** forward/inverse kinematics, Jacobian-based control and trajectory planning.
- **Model-based feedforward:** rigid-body dynamics from a URDF/Pinocchio model streamed via ADS, combined with Coulomb–viscous friction modeling.

### Layered safety
- **Hardware layer:** emergency stop, hardware limit switches, Safe Torque Off (STO) on the drives.
- **Software layer:** virtual walls (workspace limits), force limits, HMI stop and fault handling.

---

## Repository structure

```
TwinCAT Robot-V1.sln                 Visual Studio / TwinCAT XAE solution
TwinCAT Robot-V1/                    TwinCAT 3 system project (EtherCAT I/O, NC axes, tasks)
  TwinCAT Robot-V1.tsproj
  Untitled1/                         PLC project
    POUs/                            Function blocks and programs
    DUTs/                            Data types (joint space, Cartesian space, enums, configs)
    GVLs/                            Global variable lists
    VISUs/                           PLC visualizations
TcHmiProject1/                       TwinCAT HMI project (pages, views, images, themes)
Unity-Games/                         C# source of the four Unity games and their ADS connectors
  CrazyCar/  FireflyGarden/  FruitPeeler/  CargoSorter/
```

### Main PLC function blocks

| Function block | Purpose |
|---|---|
| `MAIN` | Main program and top-level state machine |
| `FB_DriveInterface` | EPOS4 drive interface over EtherCAT (enable, modes, status) |
| `FB_SwitchOpMode` | Switching between position / velocity / torque operation |
| `FB_DoHoming` | Homing sequence and homing result handling |
| `FB_JointStateReader` | Joint position / velocity acquisition from encoders |
| `FB_RobotKinematics` | Forward / inverse kinematics and Jacobian |
| `FB_ImpedanceController` | Impedance control |
| `FB_AdmittanceController` | Admittance control using force/torque sensor input |
| `FB_ForceController` | Force control |
| `FB_DynamicsCompensator` | Model-based feedforward and friction compensation |
| `FB_ForceTorqueSensor` | Force/torque sensor reading and calibration |
| `FB_VirtuallWall`, `HardwareWallCheck` | Software virtual walls and hardware limit checks |
| `FB_ThreeAxisSafety`, `StopAllMotion` | Safety monitoring and safe stopping |
| `FB_TherapyManager` | Therapy session configuration and flow |
| `FB_LaunchGame`, `FB_StartGame` | Game launching and session start |
| `Crazy Car/`, `Firrfly Game/`, `PeelerGame/`, `Sorter Game/` | Game logic and PLC ↔ Unity data structures |
| `HMI_FunBlock/` | HMI support: monitoring, therapy configuration, user securing, micro-switch check |

### HMI pages
Welcome and login · Preparation (micro-switch check, robot homing, sensor calibration, user securing, workspace, force limits) · Games · Monitoring · Reports · Settings · Emergency-stop and homing-error pop-ups.

---

## Unity games (PLC ↔ Unity over ADS)

Each game has a function block in the PLC (`MAIN.fbCrazyCar`, `MAIN.fbFireflyGame`, `MAIN.fbPeelingGame`, `MAIN.fbSorterGame`) with a `stToUnity` structure (robot end-effector position / wrist angle, normalized for the game) and a `stFromUnity` structure (game status and events). The Unity side connects with `TcAdsClient`, reads the robot data every frame and writes the game state back; the PLC adapts the robot to it, for example by selecting the resistance torque for the level being played.

| Game | Movement trained | Game design and development |
|---|---|---|
| Crazy Car | Forward/backward reach + wrist rotation | Simon Corazza |
| Firefly Garden | 2-D reaching to targets | Grecia Michelle Luis |
| Fruit Peeler | Repeated vertical strokes | Grecia Michelle Luis |
| Cargo Sorter | Medial–lateral reaching with graded load | Grecia Michelle Luis |

PLC side, ADS communication and robot integration: Hamid Khabiri. Details, data tables and code locations: [`Unity-Games/README.md`](Unity-Games/README.md).

---

## Opening the project

1. Install **TwinCAT 3.1 XAE** and **TwinCAT HMI Engineering (TE2000)**.
2. Open `TwinCAT Robot-V1.sln`.
3. Restore the HMI NuGet packages when prompted (they are not stored in this repository).
4. The HMI server configuration (certificates, user accounts) is **not included** for security reasons; TwinCAT HMI Engineering generates a new default configuration on first build.

> Running the software requires the matching hardware (Maxon EPOS4 drives, EtherCAT I/O, force/torque sensor) and a TwinCAT runtime license.

## Not included
Unity scenes, art, audio and third-party Unity packages (only the games' C# scripts are included); build output, TwinCAT boot files and licenses, HMI packages and publish logs, measurement/scope projects and logged data, and HMI server security files (private key and user password hashes).

---

## Copyright

© 2026 Hamid Khabiri. **All rights reserved.** (The Unity games are © 2026 Hamid Khabiri, Simon Corazza and Grecia Michelle Luis.)
This code is published for portfolio and review purposes only. No license is granted to copy, modify, distribute or use it, in whole or in part, without written permission.
