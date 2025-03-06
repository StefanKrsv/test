using System;
using System.Collections.Generic;

using CodeHatch.Common;
using CodeHatch.Engine.Behaviours;
using CodeHatch.Engine.Networking;
using CodeHatch.TerrainAPI;
using CodeHatch.UserInterface.Dialogues;

using Oxide.Core.Libraries.Covalence;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("3D Teleporter", "GeniusPlayUnique", "2.0.0")]
    [Description("A three dimentional teleporter.")] /**(© GeniusPlayUnique)*/
    class Teleporter : ReignOfKingsPlugin
    {
        readonly static string license = ""
            + "License Agreement for (the) 'Teleporter.cs' [ReignOfKingsPlugin]: "
            + "With uploading this plugin to umod.org the rights holder (GeniusPlayUnique) grants the user the right to download and for usage of this plugin. "
            + "However these rights do NOT include the right to (re-)upload this plugin nor any modified version of it to umod.org or any other webside or to distribute it further in any way without written permission of the rights holder. "
            + "It is explicity allowed to modify this plugin at any time for personal usage ONLY. "
            + "If a modification should be made available for all users, please contact GeniusPlayUnique (rights holder) via umod.org to discuss the matter and the terms under which to gain permission to do so. "
            + "By changing 'Accept License Agreement' below to 'true' you accept this License Agreement.";
        static bool LicenseAgreementAccepted;

        protected override void LoadDefaultConfig()
        {
            Config["EULA", "License", "License Agreement"] = license;
            Config["EULA", "License Acceptance", "Accept License Agreement"] = new bool();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LicenseAgreementError"] = "Before using the [i]Teleporter.cs[/i] - plugin you need to accept the 'License Agreement' in the config-file.",
                ["TargetPlayerNotFound"] = "Unable to find target player with the name '{0}'.",
                ["OnlineTargetPlayerNotFound"] = "Unable to find player online with the name '{0}'.",
                ["UnableToTeleport"] = "Unable to teleport.",
                ["HeightWarningTitle"] = "[FF0000][i][u]Height Warning[/u]:[/i][FFFFFF]",
                ["HeightWarningSolidStructureText"] = "You are trying to teleport into a solid structure at the target coordinates. \n\n\n Resume teleportation process?",
                ["HeightWarningTerrainText"] = "You are trying to teleport {0}m below the calculated terrain height at the target coordinates. \n\n\n Resume teleportation process?",
                ["Confirm"] = "Confirm",
                ["Cancel"] = "Cancel",
                ["TeleportLog"] = "{0} teleported {1} to x: {2} y: {3} z: {4}."
            }, this, "en");
        }

        void Init()
        {
            if (!Config["EULA", "License", "License Agreement"].Equals(license))
            {
                LoadDefaultConfig();
            }

            LoadConfig();
            LicenseAgreementAccepted = Convert.ToBoolean(Config["EULA", "License Acceptance", "Accept License Agreement"]);

            permission.RegisterPermission("3D.teleport", this);
        }

        [ChatCommand("tpdelay")]
        void GetCoordinatesAndDelay(Player player, string command, string[] args)
        {
            if (!LicenseAgreementAccepted)
            {
                player.SendError(lang.GetMessage("LicenseAgreementError", this, player.Id.ToString()));
                return;
            }

            IPlayer iPlayer = covalence.Players.FindPlayerById(player.Id.ToString());

            if (!player.HasPermission("rok.teleport") && !iPlayer.HasPermission("3D.teleport")) return;

            LockTargetCoordinates(player, command, args);
        }

        [ChatCommand("tp")]
        void LockTargetCoordinates(Player player, string command, string[] args)
        {
            if (!LicenseAgreementAccepted)
            {
                player.SendError(lang.GetMessage("LicenseAgreementError", this, player.Id.ToString()));
                return;
            }

            IPlayer iPlayer = covalence.Players.FindPlayerById(player.Id.ToString());

            if (!player.HasPermission("rok.teleport") && !iPlayer.HasPermission("3D.teleport")) return;

            float f = 0;
            float x = 0;
            float y = 0;
            float z = 0;
            float delay = 0;

            if(args.Length == 0)
            {
                if (command.ToLower().Equals("tpdelay"))
                {
                    player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                    return;
                }

                x = 0;
                z = 0;

                y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                {
                    y--;
                }

                while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                {
                    y++;
                }

                Teleport(player, iPlayer, x, y, z);
            }
            else if (args.Length == 1)
            {
                if (command.ToLower().Equals("tpdelay"))
                {
                    if (float.TryParse(args[args.Length - 1], out f))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }
                    }
                    else
                    {
                        player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                        return;
                    }

                    x = 0;
                    z = 0;

                    y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y;

                    while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                    {
                        y--;
                    }

                    while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                    {
                        y++;
                    }

                    float xNew = player.Entity.Position.x;
                    float yNew = player.Entity.Position.y;
                    float zNew = player.Entity.Position.z;

                    timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                    Teleport(player, iPlayer, x, y, z);
                    return;
                }

                try
                {
                    Player targetPlayer = Server.GetPlayerByName(args[0]);

                    x = targetPlayer.Entity.Position.x;
                    y = targetPlayer.Entity.Position.y;
                    z = targetPlayer.Entity.Position.z;
                }
                catch (Exception)
                {
                    player.SendError(string.Format(lang.GetMessage("TargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                    return;
                }

                GetBestPointAndTeleport(player, iPlayer, x, y, z);
            }
            else if (args.Length == 2)
            {
                if (float.TryParse(args[0], out f) && float.TryParse(args[1], out f))
                {
                    float.TryParse(args[0], out x);
                    float.TryParse(args[1], out z);

                    y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                    while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                    {
                        y--;
                    }

                    while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                    {
                        y++;
                    }

                    Teleport(player, iPlayer, x, y, z);
                }
                else
                {
                    Player targetPlayerOne;

                    try
                    {
                        targetPlayerOne = Server.GetPlayerByName(args[0]);
                        iPlayer = covalence.Players.FindPlayerById(targetPlayerOne.Id.ToString());
                    }
                    catch (Exception)
                    {
                        player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                        return;
                    }

                    if (command.ToLower().Equals("tpdelay"))
                    {
                        iPlayer = covalence.Players.FindPlayerById(player.Id.ToString());

                        if (float.TryParse(args[args.Length - 1], out f))
                        {
                            float.TryParse(args[args.Length - 1], out delay);

                            if (delay < 0)
                            {
                                player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                                return;
                            }

                            x = targetPlayerOne.Entity.Position.x;
                            y = targetPlayerOne.Entity.Position.y;
                            z = targetPlayerOne.Entity.Position.z;

                            float xNew = player.Entity.Position.x;
                            float yNew = player.Entity.Position.y;
                            float zNew = player.Entity.Position.z;

                            timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                            GetBestPointAndTeleport(player, iPlayer, x, y, z);
                        }
                        else
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }
                    }
                    else
                    {
                        try
                        {
                            Player targetPlayerTwo = Server.GetPlayerByName(args[1]);

                            x = targetPlayerTwo.Entity.Position.x;
                            y = targetPlayerTwo.Entity.Position.y;
                            z = targetPlayerTwo.Entity.Position.z;
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("TargetPlayerNotFound", this, player.Id.ToString()), args[1]));
                            return;
                        }
                    }

                    GetBestPointAndTeleport(player, iPlayer, x, y, z);
                }
            }
            else if (args.Length == 3)
            {
                if (float.TryParse(args[0], out f) && float.TryParse(args[1], out f) && float.TryParse(args[2], out f))
                {
                    if (command.ToLower().Equals("tpdelay"))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }

                        float.TryParse(args[0], out x);
                        float.TryParse(args[1], out z);

                        y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                        while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                        {
                            y--;
                        }

                        while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                        {
                            y++;
                        }

                        float xNew = player.Entity.Position.x;
                        float yNew = player.Entity.Position.y;
                        float zNew = player.Entity.Position.z;

                        timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                        Teleport(player, iPlayer, x, y, z);
                    }
                    else
                    {
                        float.TryParse(args[0], out x);
                        float.TryParse(args[1], out y);
                        float.TryParse(args[2], out z);

                        Vector3 targetVector = new Vector3(x, z);

                        if (TeleportUtil.IsPointBlocked(new Vector3(x, y, z), 0.5f) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z), 0.5f))
                        {
                            if ((y - TerrainAPIBase.GetTerrainHeightAt(targetVector)) <= 0)
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), string.Format(lang.GetMessage("HeightWarningTerrainText", this, player.Id.ToString()), (Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(targetVector))).ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, 0, 0, 0, 0, options, dialogue, data));
                            }
                            else
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), lang.GetMessage("HeightWarningSolidStructureText", this, player.Id.ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, 0, 0, 0, 0, options, dialogue, data));
                            }
                        }
                        else
                        {
                            Teleport(player, iPlayer, x, y, z);
                        }
                    }
                }
                else if (float.TryParse(args[1], out f) && float.TryParse(args[2], out f))
                {
                    try
                    {
                        Player targetPlayer = Server.GetPlayerByName(args[0]);
                        iPlayer = covalence.Players.FindPlayerById(targetPlayer.Id.ToString());
                    }
                    catch (Exception)
                    {
                        player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                        return;
                    }

                    float.TryParse(args[1], out x);
                    float.TryParse(args[2], out z);

                    y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y;

                    while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                    {
                        y--;
                    }

                    while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                    {
                        y++;
                    }

                    Teleport(player, iPlayer, x, y, z);
                }
                else if (float.TryParse(args[args.Length - 1], out f))
                {
                    if (command.ToLower().Equals("tpdelay"))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }

                        Player targetPlayerOne;

                        try
                        {
                            targetPlayerOne = Server.GetPlayerByName(args[0]);
                            iPlayer = covalence.Players.FindPlayerById(targetPlayerOne.Id.ToString());
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                            return;
                        }

                        try
                        {
                            Player targetPlayerTwo = Server.GetPlayerByName(args[1]);

                            x = targetPlayerTwo.Entity.Position.x;
                            y = targetPlayerTwo.Entity.Position.y;
                            z = targetPlayerTwo.Entity.Position.z;
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("TargetPlayerNotFound", this, player.Id.ToString()), args[1]));
                            return;
                        }

                        float xNew = targetPlayerOne.Entity.Position.x;
                        float yNew = targetPlayerOne.Entity.Position.y;
                        float zNew = targetPlayerOne.Entity.Position.z;

                        timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                        GetBestPointAndTeleport(player, iPlayer, x, y, z);
                    }
                    else
                    {
                        player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                        return;
                    }
                }
                else
                {
                    player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                    return;
                }
            }
            else if (args.Length == 4)
            {
                if (float.TryParse(args[0], out f) && float.TryParse(args[1], out f) && float.TryParse(args[2], out f) && float.TryParse(args[3], out f))
                {
                    if (command.ToLower().Equals("tpdelay"))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }

                        float.TryParse(args[0], out x);
                        float.TryParse(args[1], out y);
                        float.TryParse(args[2], out z);

                        float xNew = player.Entity.Position.x;
                        float yNew = player.Entity.Position.y;
                        float zNew = player.Entity.Position.z;

                        Vector3 targetVector = new Vector3(x, z);

                        if (TeleportUtil.IsPointBlocked(new Vector3(x, y, z), 0.5f) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z), 0.5f))
                        {
                            if ((y - TerrainAPIBase.GetTerrainHeightAt(targetVector)) <= 0)
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), string.Format(lang.GetMessage("HeightWarningTerrainText", this, player.Id.ToString()), (Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(targetVector))).ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, xNew, yNew, zNew, delay, options, dialogue, data));
                            }
                            else
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), lang.GetMessage("HeightWarningSolidStructureText", this, player.Id.ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, delay, xNew, yNew, zNew, options, dialogue, data));
                            }
                        }
                        else
                        {
                            timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                            Teleport(player, iPlayer, x, y, z);
                        }
                    }
                }
                else if (float.TryParse(args[1], out f) && float.TryParse(args[2], out f) && float.TryParse(args[3], out f))
                {
                    if (command.ToLower().Equals("tpdelay"))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }

                        Player targetPlayer;

                        try
                        {
                            targetPlayer = Server.GetPlayerByName(args[0]);
                            iPlayer = covalence.Players.FindPlayerById(targetPlayer.Id.ToString());
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                            return;
                        }

                        float.TryParse(args[1], out x);
                        float.TryParse(args[2], out z);

                        y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                        while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                        {
                            y--;
                        }

                        while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                        {
                            y++;
                        }

                        float xNew = targetPlayer.Entity.Position.x;
                        float yNew = targetPlayer.Entity.Position.y;
                        float zNew = targetPlayer.Entity.Position.z;

                        timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                        Teleport(player, iPlayer, x, y, z);
                    }
                    else
                    {
                        try
                        {
                            Player targetPlayer = Server.GetPlayerByName(args[0]);
                            iPlayer = covalence.Players.FindPlayerById(targetPlayer.Id.ToString());
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                            return;
                        }

                        float.TryParse(args[1], out x);
                        float.TryParse(args[2], out y);
                        float.TryParse(args[3], out z);

                        Vector3 targetVector = new Vector3(x, z);

                        if (TeleportUtil.IsPointBlocked(new Vector3(x, y, z), 0.5f) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z), 0.5f))
                        {
                            if ((y - TerrainAPIBase.GetTerrainHeightAt(targetVector)) <= 0)
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), string.Format(lang.GetMessage("HeightWarningTerrainText", this, player.Id.ToString()), (Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(targetVector))).ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, 0, 0, 0, 0, options, dialogue, data));
                            }
                            else
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), lang.GetMessage("HeightWarningSolidStructureText", this, player.Id.ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, 0, 0, 0, 0, options, dialogue, data));
                            }
                        }
                        else
                        {
                            Teleport(player, iPlayer, x, y, z);
                        }
                    }
                }
                else
                {
                    player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                }
            }
            else if (args.Length == 5)
            {
                if (float.TryParse(args[1], out f) && float.TryParse(args[2], out f) && float.TryParse(args[3], out f) && float.TryParse(args[4], out f))
                {
                    if (command.ToLower().Equals("tpdelay"))
                    {
                        float.TryParse(args[args.Length - 1], out delay);

                        if (delay <= 0)
                        {
                            player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                            return;
                        }

                        Player targetPlayer;

                        try
                        {
                            targetPlayer = Server.GetPlayerByName(args[0]);
                            iPlayer = covalence.Players.FindPlayerById(targetPlayer.Id.ToString());
                        }
                        catch (Exception)
                        {
                            player.SendError(string.Format(lang.GetMessage("OnlineTargetPlayerNotFound", this, player.Id.ToString()), args[0]));
                            return;
                        }

                        float.TryParse(args[1], out x);
                        float.TryParse(args[2], out y);
                        float.TryParse(args[3], out z);

                        float xNew = targetPlayer.Entity.Position.x;
                        float yNew = targetPlayer.Entity.Position.y;
                        float zNew = targetPlayer.Entity.Position.z;

                        Vector3 targetVector = new Vector3(x, z);

                        if (TeleportUtil.IsPointBlocked(new Vector3(x, y, z), 0.5f) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z), 0.5f))
                        {
                            if ((y - TerrainAPIBase.GetTerrainHeightAt(targetVector)) <= 0)
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), string.Format(lang.GetMessage("HeightWarningTerrainText", this, player.Id.ToString()), (Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(targetVector))).ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, delay, xNew, yNew, zNew, options, dialogue, data));
                            }
                            else
                            {
                                player.ShowConfirmPopup(lang.GetMessage("HeightWarningTitle", this, player.Id.ToString()), lang.GetMessage("HeightWarningSolidStructureText", this, player.Id.ToString()), lang.GetMessage("Confirm", this, player.Id.ToString()), lang.GetMessage("Cancel", this, player.Id.ToString()), (options, dialogue, data) => HeightWarning(player, iPlayer, command, x, y, z, delay, xNew, yNew, zNew, options, dialogue, data));
                            }
                        }
                        else
                        {
                            timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));

                            Teleport(player, iPlayer, x, y, z);
                        }
                    }
                    else
                    {
                        player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                    }
                }
                else
                {
                    player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
                }
            }
            else
            {
                player.SendError(lang.GetMessage("UnableToTeleport", this, player.Id.ToString()));
            }
        }

        void GetBestPointAndTeleport(Player player, IPlayer iPlayer, float x, float y, float z)
        {
            double ii = Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(new Vector3((x + 1), (z + 1))));
            double id = Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(new Vector3((x + 1), (z - 1))));
            double di = Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(new Vector3((x - 1), (z + 1))));
            double dd = Math.Abs(y - TerrainAPIBase.GetTerrainHeightAt(new Vector3((x - 1), (z - 1))));

            double Iii;
            double Iid;
            double Idi;
            double Idd;

            double Dii;
            double Did;
            double Ddi;
            double Ddd;

            float newY = y;

            while (TeleportUtil.IsPointBlocked(new Vector3((x + 1), newY, (z + 1))) || TeleportUtil.IsPointBlocked(new Vector3((x + 1), (newY + 1), (z + 1))))
            {
                newY++;
            }

            Iii = Math.Abs(y - newY);

            newY = y;

            while (TeleportUtil.IsPointBlocked(new Vector3((x + 1), newY, (z - 1))) || TeleportUtil.IsPointBlocked(new Vector3((x + 1), (newY + 1), (z - 1))))
            {
                newY++;
            }

            Iid = Math.Abs(y - newY);

            newY = y;

            while (TeleportUtil.IsPointBlocked(new Vector3((x - 1), newY, (z + 1))) || TeleportUtil.IsPointBlocked(new Vector3((x - 1), (newY + 1), (z + 1))))
            {
                newY++;
            }

            Idi = Math.Abs(y - newY);

            newY = y;

            while (TeleportUtil.IsPointBlocked(new Vector3((x - 1), newY, (z - 1))) || TeleportUtil.IsPointBlocked(new Vector3((x - 1), (newY + 1), (z - 1))))
            {
                newY++;
            }

            Idd = Math.Abs(y - newY);

            newY = y;

            while (!TeleportUtil.IsPointBlocked(new Vector3((x + 1), (newY - 1), (z + 1))))
            {
                newY--;
            }

            Dii = Math.Abs(y - newY);

            newY = y;

            while (!TeleportUtil.IsPointBlocked(new Vector3((x + 1), (newY - 1), (z - 1))))
            {
                newY--;
            }

            Did = Math.Abs(y - newY);

            newY = y;

            while (!TeleportUtil.IsPointBlocked(new Vector3((x - 1), (newY - 1), (z + 1))))
            {
                newY--;
            }

            Ddi = Math.Abs(y - newY);

            newY = y;

            while (!TeleportUtil.IsPointBlocked(new Vector3((x - 1), (newY - 1), (z - 1))))
            {
                newY--;
            }

            Ddd = Math.Abs(y - newY);

            if (Iii >= Dii)
            {
                ii = Iii;
            }
            else
            {
                ii = Dii;
            }

            if (Iid >= Did)
            {
                id = Iid;
            }
            else
            {
                id = Did;
            }

            if (Idi >= Ddi)
            {
                di = Idi;
            }
            else
            {
                di = Ddi;
            }

            if (Idd >= Ddd)
            {
                dd = Idd;
            }
            else
            {
                dd = Ddd;
            }

            if (ii <= id && ii <= di && ii <= dd)
            {
                x++;
                z++;

                y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                {
                    y--;
                }

                while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                {
                    y++;
                }
            }
            else if (id <= ii && id <= di && id <= dd)
            {
                x++;
                z--;

                y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                {
                    y--;
                }

                while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                {
                    y++;
                }
            }
            else if (di <= ii && id <= di && di <= dd)
            {
                x--;
                z++;

                y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                {
                    y--;
                }

                while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                {
                    y++;
                }
            }
            else if (dd <= ii && dd <= id && dd <= di)
            {
                x--;
                z--;

                y = TerrainAPIExtensions.GetTerrainSurfaceCoordinate(new Vector2(x, z)).y + 1000;

                while (!TeleportUtil.IsPointBlocked(new Vector3(x, (y - 1), z)))
                {
                    y--;
                }

                while (TeleportUtil.IsPointBlocked(new Vector3(x, y, z)) || TeleportUtil.IsPointBlocked(new Vector3(x, (y + 1), z)))
                {
                    y++;
                }
            }

            Teleport(player, iPlayer, x, y, z);
        }

        void HeightWarning(Player player, IPlayer iPlayer, string command, float x, float y, float z, float delay, float xNew, float yNew, float zNew, Options options, Dialogue dialogue, object data)
        {
            if (options.Equals(Options.Cancel) || options.Equals(Options.No))
            {
                return;
            }
            else
            {
                if (command.ToLower().Equals("tpdelay"))
                {
                    timer.Once(delay, () => Teleport(player, iPlayer, xNew, yNew, zNew));
                }

                Teleport(player, iPlayer, x, y, z);
            }
        }

        void Teleport(Player player, IPlayer iPlayer, float x, float y, float z)
        {
            iPlayer.Teleport(x, y, z);

            Puts(string.Format(lang.GetMessage("TeleportLog", this), player.Name, iPlayer.Name, x.ToString(), y.ToString(), z.ToString()));
        }
    }
}