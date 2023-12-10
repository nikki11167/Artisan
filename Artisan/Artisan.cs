﻿using Artisan.Autocraft;
using Artisan.ContextMenus;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Artisan.Universalis;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using OtterGui.Classes;
using PunishLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SoundPlayer = Artisan.Sounds.SoundPlayer;

namespace Artisan;

public unsafe class Artisan : IDalamudPlugin
{
    public string Name => "Artisan";
    private const string commandName = "/artisan";
    internal static Artisan P = null!;
    internal PluginUI PluginUi;
    internal WindowSystem ws;
    internal Configuration Config;
    internal CraftingWindow cw;
    internal RecipeInformation ri;
    internal TaskManager TM;
    internal TaskManager CTM;
    internal IconStorage Icons;
    internal UniversalisClient UniversalsisClient;

    public static bool currentCraftFinished = false;
    public static readonly object _lockObj = new();
    public static List<Task> Tasks = new();
    public static bool macroWarning = false;
    public static bool brokenWarning = false;

    internal FontManager fm;
    internal StyleModel Style;
    internal ImFontPtr CustomFont;
    internal bool StylePushed = false;

    public List<ISolver> Solvers = new();

    public Artisan([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.All);
        PunishLibMain.Init(pluginInterface, "Artisan", new AboutPlugin() { Sponsor = "https://ko-fi.com/taurenkey" });
        P = this;

        P.Config = Configuration.Load();

        Solvers.Add(new StandardSolver());
        Solvers.Add(new ExpertSolver());
        Solvers.Add(new MacroSolver());

        TM = new();
        TM.TimeLimitMS = 1000;
        CTM = new();
        TM.ShowDebug = false;
        CTM.ShowDebug = false;
        ws = new();
        cw = new();
        ri = new();
        Icons = new(pluginInterface, Svc.Data, Svc.Texture);
        PluginUi = new();
        Config = P.Config;
        fm = new FontManager();
        Svc.Commands.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Artisan menu.\n" +
            "/artisan lists → Open Lists.\n" +
            "/artisan lists <ID> → Opens specific list by ID.\n" +
            "/artisan lists <ID> start → Starts specific list by ID.\n" +
            "/artisan macros → Open Macros.\n" +
            "/artisan macros <ID> → Opens specific macro by ID.\n" +
            "/artisan endurance → Open Endurance.\n" +
            "/artisan endurance start|stop → Starts or stops endurance mode.\n" +
            "/artisan settings → Open Settings.\n" +
            "/artisan workshops → Open FC Workshops.\n" +
            "/artisan builder → Open List Builder.\n" +
            "/artisan automode → Toggles Automatic Action Execution Mode on/off.",
            ShowInHelp = true,
        });

        Svc.PluginInterface.UiBuilder.BuildFonts += AddCustomFont;
        Svc.PluginInterface.UiBuilder.Draw += ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Svc.Condition.ConditionChange += CheckForCraftedState;
        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.ClientState.Logout += DisableEndurance;
        Svc.ClientState.Login += DisableEndurance;
        Svc.Condition.ConditionChange += Condition_ConditionChange;
        Svc.Chat.ChatMessage += ScanForHQItems;
        ActionWatching.Enable();
        CurrentCraft.StepChanged += ResetRecommendation;
        ConsumableChecker.Init();
        Endurance.Init();
        IPC.IPC.Init();
        RetainerInfo.Init();
        CraftingListContextMenu.Init();
        UniversalsisClient = new();

        ws.AddWindow(new RecipeWindowUI());
        ws.AddWindow(new ProcessingWindow());
        ws.AddWindow(new QuestHelper());
        ws.AddWindow(cw);

        Style = StyleModel.Deserialize("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA")!;

        Svc.PluginInterface.UiBuilder.RebuildFonts();
    }

    private void AddCustomFont()
    {
        Svc.Log.Debug("Adding custom font");
        if (Svc.ClientState.ClientLanguage == Dalamud.ClientLanguage.Japanese) return;

        string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Fonts", "CaviarDreams_Bold.ttf");
        if (File.Exists(path))
        {
            CustomFont = fm.CustomFont;
        }

    }
    private void ScanForHQItems(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type == (XivChatType)2242 && Svc.Condition[ConditionFlag.Crafting])
        {
            if (message.Payloads.Any(x => x.Type == PayloadType.Item))
            {
                var item = (ItemPayload)message.Payloads.First(x => x.Type == PayloadType.Item);
                CurrentCraft.SetLastCraftedItem(item.Item, item.Item.CanBeHq && item.IsHQ);
            }
        }
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        Endurance.Tasks.Clear();

        if (P.Config.RequestToStopDuty)
        {
            if (flag == ConditionFlag.WaitingForDutyFinder && value)
            {
                IPC.IPC.StopCraftingRequest = true;
            }

            if (flag == ConditionFlag.WaitingForDutyFinder && !value)
            {
                IPC.IPC.StopCraftingRequest = false;
            }

            if (flag == ConditionFlag.BoundByDuty && !value && IPC.IPC.StopCraftingRequest && P.Config.RequestToResumeDuty)
            {
                var resumeDelay = P.Config.RequestToResumeDelay;
                Svc.Framework.RunOnTick(() => { IPC.IPC.StopCraftingRequest = false; }, TimeSpan.FromSeconds(resumeDelay));
            }
        }


        if (Svc.Condition[ConditionFlag.PreparingToCraft] && IPC.IPC.StopCraftingRequest)
        {
            Svc.Framework.RunOnTick(CraftingListFunctions.CloseCraftingMenu, TimeSpan.FromSeconds(1));
        }
    }

    private void DisableEndurance()
    {
        Endurance.Enable = false;
        CraftingListUI.Processing = false;
    }

    private void ResetRecommendation()
    {
        if (CurrentCraft.CurStepState == null)
        {
            macroWarning = false;
            brokenWarning = false;
        }
        else
        {
            Tasks.Clear();
        }
    }

    public static bool CheckIfCraftFinished()
    {
        //if (QuickSynthMax > 0 && QuickSynthCurrent == QuickSynthMax) return true;
        if (CurrentCraft.CurCraftState == null || CurrentCraft.CurStepState == null) return false;
        if (CurrentCraft.CurStepState.Progress == CurrentCraft.CurCraftState.CraftProgress) return true;
        if (CurrentCraft.CurStepState.Progress < CurrentCraft.CurCraftState.CraftProgress && CurrentCraft.CurStepState.Durability == 0) return true;
        currentCraftFinished = false;
        return false;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            Endurance.Enable = false;
            CraftingListUI.Processing = false;
            return;
        }

        if (CraftingWindow.MacroTime.Ticks > 0)
            CraftingWindow.MacroTime -= framework.UpdateDelta;

        if (cw.repeatTrial && !Endurance.Enable)
        {
            CraftingOperations.RepeatTrialCraft();
        }

        PluginUi.CraftingVisible = Svc.Condition[ConditionFlag.Crafting] && !Svc.Condition[ConditionFlag.PreparingToCraft];

        if (!PluginUi.CraftingVisible)
            ActionWatching.TryDisable();
        else
            ActionWatching.TryEnable();

        if (!Endurance.Enable)
            Endurance.DrawRecipeData();

        if (!PluginUi.CraftingVisible) return;

        CurrentCraft.Update();
        if (CurrentCraft.CurStepState != null && CurrentCraft.CurrentRecommendation == Skills.None && Tasks.Count == 0)
        {
            var delay = P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0;
            Tasks.Add(Svc.Framework.RunOnTick(FetchRecommendation, TimeSpan.FromMilliseconds(delay)));
        }

        if (CheckIfCraftFinished() && !currentCraftFinished)
        {
            Svc.Log.Debug($"Craft Finished");
            currentCraftFinished = true;

            if (CraftingListUI.Processing && !CraftingListFunctions.Paused)
            {
                Svc.Log.Verbose("Advancing Crafting List");
                CraftingListFunctions.CurrentIndex++;
            }

            if (Endurance.Enable && P.Config.CraftingX && P.Config.CraftX > 0)
            {
                P.Config.CraftX -= 1;
                if (P.Config.CraftX == 0)
                {
                    P.Config.CraftingX = false;
                    Endurance.Enable = false;
                    if (P.Config.PlaySoundFinishEndurance)
                        SoundPlayer.PlaySound();
                    DuoLog.Information("Craft X has completed.");

                }
            }
        }
    }

    public void FetchRecommendation()
    {
        if (Tasks.Count > 1)
            return;

        try
        {
            if (RepairManager.GetMinEquippedPercent() == 0)
            {
                if (!brokenWarning)
                {
                    Svc.Toasts.ShowError("You have broken gear. Artisan will not continue.");
                    Svc.Chat.PrintError("You have broken gear. Artisan will not continue.");
                }

                brokenWarning = true;
                return;
            }

            if (CurrentCraft.CurrentRecipe != null && CurrentCraft.CurCraftState != null && CurrentCraft.CurStepState != null)
            {
                var solver = GetSolverForRecipe(CurrentCraft.CurrentRecipe.RowId, CurrentCraft.CurCraftState);
                if (solver.unsupportedReason.Length > 0)
                {
                    if (!macroWarning)
                    {
                        Svc.Toasts.ShowError($"{solver.unsupportedReason}. Artisan will not continue.");
                        Svc.Chat.PrintError($"{solver.unsupportedReason}. Artisan will not continue.");
                    }
                    macroWarning = true;
                    return;
                }

                (CurrentCraft.CurrentRecommendation, CurrentCraft.CurrentRecommendationComment) = solver.solver.Solve(CurrentCraft.CurCraftState, CurrentCraft.CurStepState, CurrentCraft.PrevStepStates, solver.flavour);
            }

            if (CurrentCraft.CurrentRecommendation != Skills.None)
            {
                if (!P.Config.DisableToasts)
                {
                    QuestToastOptions options = new() { IconId = CurrentCraft.CurrentRecommendation.IconOfAction(CharacterInfo.JobID) };
                    Svc.Toasts.ShowQuest($"Use {CurrentCraft.CurrentRecommendation.NameOfAction()}", options);
                }

                if (P.Config.AutoMode)
                {
                    if (CurrentCraft.CanUse(CurrentCraft.CurrentRecommendation))
                        ActionWatching.BlockAction = true;

                    P.CTM.DelayNext(P.Config.AutoDelay);
                    P.CTM.Enqueue(() => Hotbars.ExecuteRecommended(CurrentCraft.CurrentRecommendation));
                    //Svc.Framework.RunOnTick(() => , TimeSpan.FromMilliseconds(P.Config.AutoDelay));

                    //Svc.Plugin.BotTask.Schedule(() => Hotbars.ExecuteRecommended(CurrentRecommendation), P.Config.AutoDelay);
                }

                return;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "Crafting Step Change");
        }
    }

    public IEnumerable<(ISolver solver, int flavour, int priority, string unsupportedReason)> GetAvailableSolversForRecipe(CraftState craft, bool returnUnsupported, ISolver? skipSolver = null)
    {
        foreach (var solver in Solvers)
        {
            if (solver == skipSolver)
                continue;

            foreach (var f in solver.Flavours(craft))
            {
                if (returnUnsupported || f.unsupportedReason.Length == 0)
                {
                    yield return (solver, f.flavour, f.priority, f.unsupportedReason);
                }
            }
        }
    }

    public (ISolver solver, int flavour, string unsupportedReason) GetSolverForRecipe(uint recipeID, CraftState craft)
    {
        if (P.Config.RecipeSolverAssignment.TryGetValue(recipeID, out var assignment))
        {
            var solver = Solvers.Find(s => s.GetType().FullName == assignment.type);
            if (solver != null)
            {
                foreach (var f in solver.Flavours(craft).Where(f => f.flavour == assignment.flavour))
                {
                    return (solver, f.flavour, f.unsupportedReason);
                }
            }
        }

        var best = GetAvailableSolversForRecipe(craft, false).MaxBy(f => f.priority);
        return (best.solver, best.flavour, best.unsupportedReason);
    }

    private void CheckForCraftedState(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.Crafting && value)
        {
            PluginUi.CraftingVisible = true;
        }
    }

    public void Dispose()
    {
        PluginUi.Dispose();
        Endurance.Dispose();
        RetainerInfo.Dispose();
        IPC.IPC.Dispose();

        Svc.Commands.RemoveHandler(commandName);
        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Chat.ChatMessage -= ScanForHQItems;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Svc.PluginInterface.UiBuilder.Draw -= ws.Draw;
        Svc.Framework.Update -= OnFrameworkUpdate;
        CurrentCraft.StepChanged -= ResetRecommendation;

        Svc.PluginInterface.UiBuilder.BuildFonts -= AddCustomFont;
        ActionWatching.Dispose();
        ws.RemoveAllWindows();
        ws = null!;

        ECommonsMain.Dispose();
        CustomFont = null;
        LuminaSheets.Dispose();
        CraftingListContextMenu.Dispose();
        UniversalsisClient.Dispose();
        P = null!;

    }

    private void OnCommand(string command, string args)
    {
        var subcommands = args.Split(' ');

        if (subcommands.Length == 0)
        {
            PluginUi.IsOpen = !PluginUi.IsOpen;
            return;
        }

        var firstArg = subcommands[0];

        if (firstArg.ToLower() == "automode")
        {
            P.Config.AutoMode = !P.Config.AutoMode;
            P.Config.Save();
            return;
        }
        if (subcommands.Length >= 2)
        {
            Svc.Log.Debug($"{subcommands[1]}");
            if (firstArg.ToLower() == "lists")
            {
                if (!CraftingListUI.Processing)
                {
                    if (int.TryParse(subcommands[1], out int id))
                    {
                        if (P.Config.CraftingLists.Any(x => x.ID == id))
                        {
                            if (subcommands.Length >= 3 && subcommands[2].ToLower() == "start")
                            {
                                if (!Endurance.Enable)
                                {
                                    CraftingListUI.selectedList = P.Config.CraftingLists.First(x => x.ID == id);
                                    CraftingListUI.StartList();
                                    return;
                                }
                            }
                            else
                            {
                                ListEditor editor = new(id);
                                return;
                            }
                        }
                        else
                        {
                            DuoLog.Error("List ID does not exist.");
                            return;
                        }
                    }
                    else
                    {
                        DuoLog.Error("Unable to parse ID as a number.");
                        return;
                    }
                }
                else
                {
                    DuoLog.Error("Unable to open list whilst processing.");
                    return;
                }
            }

            if (firstArg.ToLower() == "macros")
            {
                if (CurrentCraft.State != CraftingState.Crafting)
                {
                    if (int.TryParse(subcommands[1], out int id))
                    {
                        var macro = P.Config.MacroSolverConfig.FindMacro(id);
                        if (macro != null)
                        {
                            MacroEditor editor = new(macro);
                            return;
                        }
                        else
                        {
                            DuoLog.Error("Macro ID does not exist.");
                            return;
                        }
                    }
                    else
                    {
                        DuoLog.Error("Unable to parse ID as a number.");
                        return;
                    }
                }
                else
                {
                    DuoLog.Error("Unable to open edit macros whilst crafting.");
                    return;
                }
            }

            if (firstArg.ToLower() == "endurance")
            {
                if (subcommands[1].ToLower() is "start")
                {
                    if (CraftingListUI.Processing)
                    {
                        DuoLog.Error("Cannot start endurance whilst processing a list.");
                        return;
                    }
                    if (Endurance.RecipeID == 0)
                    {
                        DuoLog.Error("Cannot start endurance without setting a recipe.");
                        return;
                    }
                    if (!CraftingListFunctions.HasItemsForRecipe((uint)Endurance.RecipeID))
                    {
                        DuoLog.Error("Cannot start endurance as you do not possess all ingredients for your recipe in your inventory.");
                        return;
                    }

                    if (!CraftingListUI.Processing && Endurance.RecipeID != 0)
                    {
                        Endurance.ToggleEndurance(true);
                        return;
                    }
                }

                if (subcommands[1].ToLower() is "stop")
                {
                    if (!Endurance.Enable)
                    {
                        DuoLog.Error("Endurance is not running so cannot be stopped.");
                        return;
                    }
                    if (Endurance.Enable)
                    {
                        Endurance.ToggleEndurance(false);
                        return;
                    }
                }
            }
        }

        PluginUi.IsOpen = true;
        PluginUi.OpenWindow = firstArg.ToLower() switch
        {
            "lists" => OpenWindow.Lists,
            "endurance" => OpenWindow.Endurance,
            "settings" => OpenWindow.Main,
            "macros" => OpenWindow.Macro,
            "builder" => OpenWindow.SpecialList,
            "workshop" => OpenWindow.FCWorkshop,
            _ => OpenWindow.None
        };
    }

    private void DrawConfigUI()
    {
        PluginUi.IsOpen = true;
    }

    internal static void StopCrafting()
    {
        SetMode();

        switch (IPC.IPC.CurrentMode)
        {
            case IPC.IPC.ArtisanMode.Endurance:
                Endurance.Enable = false;
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = true;
                break;
        }


    }

    private static void SetMode()
    {
        if (Endurance.Enable)
        {
            IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.Endurance;
            return;
        }

        if (CraftingListUI.Processing)
        {
            IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.Lists;
            return;
        }

        IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.None;
    }

    internal static void ResumeCrafting()
    {
        switch (IPC.IPC.CurrentMode)
        {
            case IPC.IPC.ArtisanMode.Endurance:
                Endurance.Enable = true;
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = false;
                break;
        }
    }
}

