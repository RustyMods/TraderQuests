using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace TraderQuests.Quest;

public static class Commands
{
    private static readonly Dictionary<string, TraderCommand> TerminalCommands = new();
    
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    private static class Terminal_Awake_Patch
    {
        private static void Postfix()
        {
            Terminal.ConsoleCommand command = new Terminal.ConsoleCommand("traderquest",
                "use [help] to get list of commands", (Terminal.ConsoleEventFailable)(
                    args =>
                    {
                        if (args.Length < 2) return false;
                        if (TerminalCommands.TryGetValue(args[1], out TraderCommand command))
                        {
                            command.Action.Invoke(args);
                            return true;
                        }
                        return false;
                    }), optionsFetcher: () => TerminalCommands.Keys.ToList());

            TraderCommand help = new TraderCommand("help", "list of trader quest commands", args =>
            {
                foreach (var kvp in TerminalCommands)
                {
                    if (kvp.Key == "help") continue;
                    Debug.Log($"{kvp.Key}: {kvp.Value.Description}");
                }
            });

            TraderCommand clearBounties = new TraderCommand("clear_bounties", "clears active bounties from board and local player",
                args =>
                {
                    if (!Player.m_localPlayer) return;
                    foreach (var bounty in BountySystem.AllBounties.Values.Where(bounty => bounty.Active))
                    {
                        bounty.Deactivate(false);
                    }
                    BountySystem.ClearPlayerData(Player.m_localPlayer);
                });
        }
    }

    private class TraderCommand
    {
        public readonly string Description;
        public readonly Action<Terminal.ConsoleEventArgs> Action;

        public TraderCommand(string name, string desc, Action<Terminal.ConsoleEventArgs> action)
        {
            Description = desc;
            Action = action;
            TerminalCommands[name] = this;
        }
    }
    
}