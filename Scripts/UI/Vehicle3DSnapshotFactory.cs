using System;
using System.Threading.Tasks;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.IO;
using WastelandSurvivor.Core.State;
using WastelandSurvivor.Game.Arena;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Renders a real <see cref="VehiclePawn"/> into an off-screen 3D SubViewport and returns a
/// frozen <see cref="Texture2D"/> the menus can show as a vehicle portrait. Used so the City Hub
/// showcase reads as an actual rendered vehicle instead of a flat top-down silhouette.
/// </summary>
public static class Vehicle3DSnapshotFactory
{
    private const string VehiclePawnScenePath = "res://Scenes/Arena/VehiclePawn.tscn";

    public static async Task<Texture2D?> CreateAsync(
        Node host,
        DefDatabase? defs,
        VehicleDefinition? definition,
        VehicleInstanceState? vehicle,
        Color bodyColor,
        Vector2I size)
    {
        if (host == null || host.GetTree() == null || size.X <= 0 || size.Y <= 0)
            return null;

        var pawnScene = GD.Load<PackedScene>(VehiclePawnScenePath);
        if (pawnScene == null)
            return null;

        var viewport = new SubViewport
        {
            Name = "Vehicle3DSnapshotViewport",
            Size = size,
            TransparentBg = true,
            GuiDisableInput = true,
            HandleInputLocally = false,
            PhysicsObjectPicking = false,
        };
        viewport.Set("render_target_update_mode", 4); // Always
        viewport.OwnWorld3D = true;

        var rigRoot = new Node3D { Name = "Rig" };

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            LightEnergy = 1.4f,
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-50f, -35f, 0f),
        };

        var fillLight = new DirectionalLight3D
        {
            Name = "Fill",
            LightEnergy = 0.45f,
            LightColor = new Color(0.78f, 0.86f, 1f),
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-15f, 140f, 0f),
        };

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0f, 0f, 0f, 0f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.7f),
            AmbientLightEnergy = 0.45f,
        };

        var worldEnv = new WorldEnvironment
        {
            Name = "Env",
            Environment = environment,
        };

        var camera = new Camera3D
        {
            Name = "Camera",
            Current = true,
            Fov = 32f,
            Position = new Vector3(3.6f, 2.05f, 4.4f),
        };
        camera.LookAt(new Vector3(0f, 0.55f, 0f), Vector3.Up);

        VehiclePawn? pawn = null;
        try
        {
            pawn = pawnScene.Instantiate<VehiclePawn>();
        }
        catch
        {
            // If the script type cast fails, give up gracefully.
        }
        if (pawn == null)
            return null;

        pawn.Name = "Pawn";
        pawn.ProcessMode = Node.ProcessModeEnum.Disabled;
        pawn.BodyColor = bodyColor;

        host.AddChild(viewport);
        viewport.AddChild(worldEnv);
        viewport.AddChild(rigRoot);
        rigRoot.AddChild(sun);
        rigRoot.AddChild(fillLight);
        rigRoot.AddChild(camera);
        rigRoot.AddChild(pawn);

        // Remove the engine audio node a frame after _Ready runs so the snapshot pawn never
        // emits sound while it's parented under the menu host.
        var audioNode = pawn.GetNodeOrNull<Node>("EngineAudio");
        if (audioNode != null && GodotObject.IsInstanceValid(audioNode))
            audioNode.QueueFree();

        try
        {
            if (defs != null && vehicle != null)
            {
                pawn.ConfigureLoadout(defs, vehicle);
            }

            // Give the imported gltf time to finish setting up its visuals + body color.
            for (int i = 0; i < 6; i++)
                await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

            var image = viewport.GetTexture()?.GetImage();
            if (image == null || image.IsEmpty())
                return null;

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Vehicle3DSnapshotFactory] Failed to render snapshot: {ex}");
            return null;
        }
        finally
        {
            viewport.QueueFree();
        }
    }
}
