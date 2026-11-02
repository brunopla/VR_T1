using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Acceso a las manos del rig y a los objetos que se pueden guardar o llamar
// (armas, cuchillos, martillo, gancho). Lo usan las fundas y el HUD de muneca.
public static class PlayerRig
{
    public static XROrigin Origin => Object.FindFirstObjectByType<XROrigin>();

    // Interactor de agarre directo de una mano (o el primero que tenga esa mano)
    public static XRBaseInputInteractor Hand(bool left)
    {
        var origin = Origin;
        if (origin == null)
            return null;
        string side = left ? "left" : "right";
        var interactors = origin.GetComponentsInChildren<XRBaseInputInteractor>(true)
            .Where(i => i.handedness == (left ? InteractorHandedness.Left : InteractorHandedness.Right)
                        || NameChain(i.transform).Contains(side))
            .ToList();
        return interactors.FirstOrDefault(i => i is XRDirectInteractor) ?? interactors.FirstOrDefault();
    }

    // Transform del control (el padre "LeftHand Controller" / "RightHand Controller")
    public static Transform Controller(bool left)
    {
        var hand = Hand(left);
        if (hand == null)
            return null;
        for (var t = hand.transform; t != null; t = t.parent)
            if (t.name.ToLowerInvariant().Contains("controller"))
                return t;
        return hand.transform;
    }

    static string NameChain(Transform t)
    {
        var name = "";
        for (; t != null; t = t.parent)
            name += t.name.ToLowerInvariant() + "/";
        return name;
    }

    // Nombre del tipo de objeto, o null si no es algo que se guarde/llame
    public static string EquipmentName(XRGrabInteractable grab)
    {
        if (grab == null)
            return null;
        var weapon = grab.GetComponent<Weapon>();
        if (weapon != null && weapon.data != null)
            return weapon.data.displayName;
        if (grab.GetComponent<ThorHammer>() != null)
            return "Martillo";
        if (grab.GetComponentInChildren<GrapplingGun>() != null)
            return "Gancho";
        var melee = grab.GetComponent<MeleeWeapon>();
        if (melee != null)
            return melee.stickOnThrow ? "Cuchillo" : "Cuerpo a cuerpo";
        return null;
    }

    // Largo del objeto: los largos van a la espalda, los cortos a la cadera
    public static float Length(XRGrabInteractable grab)
    {
        var renderers = grab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 0f;
        var bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);
        return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
    }

    public static bool IsHeldByHand(XRGrabInteractable grab)
    {
        return grab.interactorsSelecting.Any(i => !(i is XRSocketInteractor));
    }
}
