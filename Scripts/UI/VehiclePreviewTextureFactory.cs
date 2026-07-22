using System;
using System.Threading.Tasks;
using Godot;
using WastelandSurvivor.Core.Defs;
using WastelandSurvivor.Core.State;

namespace WastelandSurvivor.Game.UI;

/// <summary>
/// Produces frozen 2D preview textures by rendering <see cref="VehiclePreviewCanvas"/> into a tiny
/// off-screen SubViewport. This keeps garage list icons deterministic and decoupled from arena-world cameras.
/// </summary>
public static class VehiclePreviewTextureFactory
{
    public static async Task<Texture2D?> CreateAsync(Node host, VehicleDefinition definition, VehicleInstanceState vehicle, Color bodyColor, Vector2I size)
    {
        if (host == null || host.GetTree() == null || size.X <= 0 || size.Y <= 0)
            return null;

        var viewport = new SubViewport
        {
            Name = "VehiclePreviewIconViewport",
            Size = size,
            TransparentBg = true,
            Disable3D = true,
            GuiDisableInput = true,
            HandleInputLocally = false,
            PhysicsObjectPicking = false,
        };
        viewport.Set("render_target_update_mode", 4);

        var root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(size.X, size.Y),
            CustomMinimumSize = new Vector2(size.X, size.Y),
        };

        var preview = new VehiclePreviewCanvas
        {
            Name = "VehiclePreviewCanvas",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(size.X, size.Y),
            CustomMinimumSize = new Vector2(size.X, size.Y),
            Position = Vector2.Zero,
        };
        preview.SetVehicle(definition, vehicle, bodyColor);
        preview.SetLiveYaw(0f);
        preview.SetSnapshotTexture(null);
        preview.SetEmbeddedPreviewActive(false);

        host.AddChild(viewport);
        viewport.AddChild(root);
        root.AddChild(preview);

        try
        {
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

            var image = viewport.GetTexture()?.GetImage();
            if (image == null || image.IsEmpty())
                return null;

            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VehiclePreviewTextureFactory] Failed to build vehicle preview texture: {ex}");
            return null;
        }
        finally
        {
            viewport.QueueFree();
        }
    }
}
