using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SeniorS.SMarketplace.Helpers;
public class RaycastHelper
{
    public static Transform GetDoorTransform(Player player, out BarricadeData barricadeData, out BarricadeDrop drop)
    {
        barricadeData = null;
        drop = null;
        if (Physics.Raycast(player.look.aim.position, player.look.aim.forward, out RaycastHit hit, 5, RayMasks.BARRICADE | RayMasks.STRUCTURE | RayMasks.BARRICADE_INTERACT | RayMasks.STRUCTURE_INTERACT))
        {
            Transform transform = hit.transform;
            InteractableDoorHinge doorHinge = hit.transform.GetComponent<InteractableDoorHinge>();
            if (doorHinge != null)
            {
                transform = doorHinge.door.transform;
            }

            drop = BarricadeManager.FindBarricadeByRootTransform(transform);
            if (drop != null)
            {
                barricadeData = drop.GetServersideData();
                return drop.model;
            }
        }

        return null;
    }

    public static Transform GetBarricadeTransform(Player player, out BarricadeData barricadeData, out BarricadeDrop drop)
    {
        barricadeData = null;
        drop = null;
        if (Physics.Raycast(player.look.aim.position, player.look.aim.forward, out RaycastHit hit, 8f, RayMasks.BARRICADE | RayMasks.BARRICADE_INTERACT))
        {
            Transform transform = hit.transform;

            drop = BarricadeManager.FindBarricadeByRootTransform(transform);
            if (drop != null)
            {
                barricadeData = drop.GetServersideData();
                return drop.model;
            }
        }

        return null;
    }

    public static Transform GetStructureTransform(Player player, out StructureData structureData, out StructureDrop drop)
    {
        structureData = null;
        drop = null;
        if (Physics.Raycast(player.look.aim.position, player.look.aim.forward, out RaycastHit hit, 8f, RayMasks.BARRICADE | RayMasks.BARRICADE_INTERACT))
        {
            Transform transform = hit.transform;

            drop = StructureManager.FindStructureByRootTransform(transform);
            if (drop != null)
            {
                structureData = drop.GetServersideData();
                return drop.model;
            }
        }

        return null;
    }

    public static Transform GetLockerTransform(Player player, out BarricadeData barricadeData, out BarricadeDrop drop)
    {
        barricadeData = null;
        drop = null;
        if (Physics.Raycast(player.look.aim.position, player.look.aim.forward, out RaycastHit hit, 5, RayMasks.BARRICADE | RayMasks.BARRICADE_INTERACT))
        {
            Transform transform = hit.transform;
            var locker = hit.transform.GetComponent<InteractableStorage>();

            drop = BarricadeManager.FindBarricadeByRootTransform(transform);
            if (drop != null && locker != null)
            {
                barricadeData = drop.GetServersideData();
                return drop.model;
            }
        }

        return null;
    }
}