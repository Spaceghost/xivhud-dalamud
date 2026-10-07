using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XivHud.Core;

namespace XivHud.Plugin;

/// <summary>
/// Owns one native category-hide operation. Call Update and Dispose on the
/// framework thread. This never changes global UI visibility or saved layouts.
/// </summary>
internal sealed unsafe class HudVisibility(IPluginLog log) : IDisposable
{
    private const UiFlags Categories = UiFlags.Hud | UiFlags.Nameplates | UiFlags.Chat | UiFlags.ActionBars;
    private readonly HudVisibilityLease lease = new();
    private bool disposed;
    private bool faulted;

    public bool IsApplied { get; private set; }
    public string Status { get; private set; } = "Native HUD unchanged.";

    public void Update(bool requested, bool loggedIn)
    {
        if (disposed) return;
        IsApplied = false;
        try
        {
            var identity = Current(out var module, out var manager);
            var previous = lease.Identity;
            var wasHeld = lease.IsHeld;
            lease.Reconcile(identity);
            if (wasHeld && !lease.IsHeld)
                log.Debug("XivHud dropped an expired HUD lease after the native UI changed ({Module:X}/{Manager:X}).",
                    previous.Module, previous.Manager);

            // Logout ends the addon generation even if UIModule storage is
            // reused. Attempt one validated release while the old UI is still
            // ready, then retire it; never replay that restore after login.
            if (!loggedIn)
            {
                if (lease.IsHeld && !TryRelease(identity, module, manager))
                    log.Warning("XivHud retired its HUD lease at logout before the native UI could restore it.");
                lease.Drop();
                faulted = false;
                Status = requested ? "Waiting for a character." : "Native HUD unchanged.";
                return;
            }

            // Within the same login, retain a pending restore through a
            // temporarily absent UI without dereferencing the stored pointers.
            if (!requested)
            {
                if (lease.IsHeld && !TryRelease(identity, module, manager))
                {
                    Status = "Waiting for the native UI to restore the HUD.";
                    return;
                }
                faulted = false;
                Status = "Native HUD unchanged.";
                return;
            }

            if (faulted)
            {
                Status = "HUD change stopped after an error; switch the mode off before retrying.";
                return;
            }
            if (!identity.IsValid || !CanToggle(manager))
            {
                Status = "Waiting for the native HUD.";
                return;
            }
            if (!lease.IsHeld)
            {
                if (!module->IsUIReady())
                {
                    Status = "Waiting for the native HUD.";
                    return;
                }
                lease.TryAcquire(identity, () =>
                {
                    module->ToggleUi(Categories, false, true);
                    return true;
                });
                log.Information("XivHud acquired native HUD categories {Categories} on {Module:X}/{Manager:X}.",
                    (int)Categories, identity.Module, identity.Manager);
            }

            // A category toggle increments addon hide counts. Never reissue it
            // every frame, including when another native owner changes flags.
            IsApplied = lease.IsHeld && (manager->UiFlags & Categories) == Categories;
            Status = IsApplied
                ? "HUD, nameplates, chat and action bars hidden."
                : "HUD lease active; the game has changed category visibility.";
        }
        catch (Exception ex)
        {
            if (!loggedIn) lease.Drop();
            if (!faulted) log.Error(ex, "XivHud could not change native HUD visibility.");
            faulted = true;
            Status = "HUD change stopped after an error; switch the mode off before retrying.";
        }
    }

    private bool TryRelease(HudModuleIdentity identity, UIModule* module, RaptureAtkUnitManager* manager)
    {
        if (!lease.IsHeld) return true;
        if (!identity.IsValid || identity != lease.Identity || !CanToggle(manager)) return false;
        // The lease relinquishes ownership before invoking the callback, so a
        // throwing restore cannot decrement the same native counters twice.
        lease.Release(identity, () => module->ToggleUi(Categories, true, true));
        log.Information("XivHud released native HUD categories {Categories} on {Module:X}/{Manager:X}.",
            (int)Categories, identity.Module, identity.Manager);
        return true;
    }

    private static HudModuleIdentity Current(out UIModule* module, out RaptureAtkUnitManager* manager)
    {
        module = UIModule.Instance();
        manager = null;
        if (module == null) return default;
        var atk = module->GetRaptureAtkModule();
        if (atk == null) return default;
        manager = &atk->RaptureAtkUnitManager;
        return new((nint)module, (nint)manager);
    }

    private static bool CanToggle(RaptureAtkUnitManager* manager)
    {
        // The installed native ToggleUi checks this bit before touching its
        // aggregate flags or addon UiFlagsHideCount. It can be unset at login.
        return manager != null && (manager->Flags & AtkUnitManagerFlags.Unk40) != 0;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        IsApplied = false;
        try
        {
            var identity = Current(out var module, out var manager);
            lease.Reconcile(identity);
            if (!TryRelease(identity, module, manager))
                log.Warning("XivHud could not restore its HUD lease on unload because the original native UI was unavailable.");
        }
        catch (Exception ex)
        {
            log.Error(ex, "XivHud could not restore its native HUD lease on unload.");
        }
        finally
        {
            lease.Drop();
            Status = "Unloaded.";
        }
    }
}
