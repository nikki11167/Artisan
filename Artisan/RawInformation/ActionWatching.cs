﻿using Artisan.CraftingLogic;
using Artisan.RawInformation.Character;
using Dalamud.Hooking;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;

namespace Artisan.RawInformation
{
    internal unsafe class ActionWatching
    {
        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
        public static Hook<UseActionDelegate> UseActionHook;
        public static uint LastUsedAction = 0;
        public static TaskManager ATM = new();
        public static bool BlockAction = false;

        private delegate void* ClickSynthesisButton(void* a1, void* a2);
        private static Hook<ClickSynthesisButton> clickSynthesisButtonHook;
        private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            try
            {
                if (BlockAction)
                    return UseActionHook.Original(actionManager, (uint)ActionType.Action, 7, targetObjectID, param, useType, pvp, isGroundTarget);

                if (actionManager->GetActionStatus((ActionType)actionType, actionID) == 0)
                {
                    CurrentCraft.NotifyUsedAction(SkillActionMap.ActionToSkill(actionID));
                }
                return UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "UseActionDetour");
                return UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            }
        }

        static ActionWatching()
        {
            UseActionHook = Svc.Hook.HookFromSignature<UseActionDelegate>(ActionManager.Addresses.UseAction.String, UseActionDetour);
            clickSynthesisButtonHook = Svc.Hook.HookFromSignature<ClickSynthesisButton>("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2", ClickSynthesisButtonDetour);
            clickSynthesisButtonHook.Enable();
        }

        private static void* ClickSynthesisButtonDetour(void* a1, void* a2)
        {
            try
            {
                if (P.Config.DontEquipItems)
                    return clickSynthesisButtonHook.Original(a1, a2);

                uint requiredClass = 0;
                var readyState = GetCraftReadyState(ref requiredClass, out var selectedRecipeId);
                var recipe = LuminaSheets.RecipeSheet[selectedRecipeId];
                if (recipe.ItemRequired.Row > 0)
                {
                    bool hasItem = InventoryManager.Instance()->GetInventoryItemCount(recipe.ItemRequired.Row) +
                        InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryMainHand) +
                        InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryHands) >= 1;

                    if (hasItem)
                    {
                        if (InventoryManager.Instance()->GetInventoryItemCount(recipe.ItemRequired.Row, false, false, false) == 1)
                        {
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory1, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory2)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory2)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory2, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory3)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory3)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory3, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory4)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory4)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory4, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }
                        if (InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryHands) == 1)
                        {

                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryHands)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryHands)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.ArmoryHands, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.ArmouryBoard)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }
                        if (InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryMainHand) == 1)
                        {

                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryMainHand)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryMainHand)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.ArmoryMainHand, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.ArmouryBoard)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }

                    }
                }

            }
            catch
            {

            }
            return clickSynthesisButtonHook.Original(a1, a2);
        }

        public static void TryEnable()
        {
            if (!UseActionHook.IsEnabled)
                UseActionHook.Enable();
        }

        public static void TryDisable()
        {
            if (UseActionHook.IsEnabled)
                UseActionHook.Disable();
        }
        public static void Enable()
        {
            UseActionHook.Enable();
        }

        public static void Disable()
        {
            UseActionHook.Disable();
        }

        public static void Dispose()
        {
            TryDisable();

            UseActionHook.Dispose();
            clickSynthesisButtonHook.Dispose();
        }

        public enum CraftReadyState
        {
            NotReady,
            Ready,
            WrongClass,
            AlreadyCrafting,
        }

        private static CraftReadyState GetCraftReadyState(out ushort selectedRecipeId)
        {
            uint requiredClass = 0;
            return GetCraftReadyState(ref requiredClass, out selectedRecipeId);
        }

        private static CraftReadyState GetCraftReadyState(ref uint requiredClass, out ushort selectedRecipeId)
        {
            selectedRecipeId = 0;
            if (Svc.ClientState.LocalPlayer == null) return CraftReadyState.NotReady;
            var uiRecipeNote = RecipeNote.Instance();
            if (uiRecipeNote == null || uiRecipeNote->RecipeList == null) return CraftReadyState.NotReady;
            var selectedRecipe = uiRecipeNote->RecipeList->SelectedRecipe;
            if (selectedRecipe == null) return CraftReadyState.NotReady;
            selectedRecipeId = selectedRecipe->RecipeId;
            requiredClass = uiRecipeNote->Jobs[selectedRecipe->CraftType];
            var requiredJob = Svc.Data.Excel.GetSheet<ClassJob>()?.GetRow(requiredClass);
            if (requiredJob == null) return CraftReadyState.NotReady;
            if (Svc.ClientState.LocalPlayer.ClassJob.Id == requiredClass) return CraftReadyState.Ready;
            var localPlayer = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)Svc.ClientState.LocalPlayer.Address;
            return localPlayer->EventState == 5 ? CraftReadyState.AlreadyCrafting : CraftReadyState.WrongClass;
        }

    }
}
