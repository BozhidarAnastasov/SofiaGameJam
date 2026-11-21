# Hospital rooms (Lobby, SurgicalWing, CorridorOffice)

Built by **Tools > Hospital Rooms > Build All Room Scenes** (Assets/Editor/HospitalRoomsBuilder.cs).
Scenes go to Assets/Scenes/Rooms/ and are added to Build Settings.

Per room folder:
- `<Prop>.png` furniture cut out of the room painting, transparent, pivot at the feet.
- `<Room>_SouthWall.png` front wall only, drawn above the girl, fades to 35% when she is behind it.
- `<Room>_layout.json` positions, pivots, footprint colliders, walkable floor outline, blockers, fade zones, markers.
  Values are world units (background centred at 0,0). Edit and re-run the build to change them.

Backgrounds: Lobby uses Assets/Images/Backgrounds/Lobby_Bed_Hole.png; the others are SurgicalWing.png and CorridorOffice.png there.
The props are still painted in the background too; the cut-outs sit exactly on top so the girl can walk behind them.

Scale (pixels per unit, girl = 2 units tall): Lobby 28, SurgicalWing 40, CorridorOffice 45 (set from door sizes).
To change a room's scale, scale the Room_<Name> object in the scene (everything is under it), or ask Claude to regenerate the json.
