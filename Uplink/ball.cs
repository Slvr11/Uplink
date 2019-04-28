using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InfinityScript;
//TODO: Grab flag obj hud and assign it to ballObj huds instead of creating new ones
namespace Uplink
{
    public class ball : BaseScript
    {
        private static Entity level = Entity.GetEntity(2046);
        private static int ballGlow;
        private static int ballSiteFX;
        private static int alliesSiteFX;
        private static int axisSiteFX;
        private static int ballContrail;
        private static int ballExplodeFX;
        private static Vector3 alliesSite;
        private static Entity alliesStation;
        private static Vector3 axisSite;
        private static Entity axisStation;
        private static Entity baseFX;
        private static Entity ballEnt;
        //private Entity ballFX;
        public static bool gameEnded = false;
        private static bool prematchOver = false;
        private static bool isHalftime = false;
        private static int ballObjID = 31;
        private static int ballObjIDAllies = 30;
        private static int alliesSiteAttackerID = 29;
        private static int axisSiteAttackerID = 28;
        private static int alliesSiteDefenderID = 27;
        private static int axisSiteDefenderID = 26;
        private static HudElem alliesSiteAttackerWaypoint;
        private static HudElem alliesSiteDefenderWaypoint;
        private static HudElem axisSiteAttackerWaypoint;
        private static HudElem axisSiteDefenderWaypoint;
        private static HudElem ballObjAllies_kill;
        private static HudElem ballObjAllies_defend;
        private static HudElem ballObjAxis_kill;
        private static HudElem ballObjAxis_defend;
        private static HudElem ballIcon_allies;
        private static HudElem ballText_allies;
        private static HudElem ballIcon_axis;
        private static HudElem ballText_axis;
        private static Vector3 site;
        private static Entity alliesFlag;
        private static Entity alliesFlagBase;
        private static Entity alliesFlagTrig;
        private static Entity alliesFlagTrig2 = null;
        private static Entity axisFlag;
        private static Entity axisFlagBase;
        private static Entity axisFlagTrig;
        private static Entity axisFlagTrig2 = null;
        private static string ballWeapon = "strike_marker_mp";
        public ball()
        {
            string gametype = GSCFunctions.GetDvar("g_gametype");
            if (gametype != "ctf")
            {
                Log.Write(LogLevel.Info, "Gametype must be set to CTF for Uplink. Restarting...");
                GSCFunctions.SetDvar("g_gametype", "ctf");
                Utilities.ExecuteCommand("map_restart");
                return;
            }

            GSCFunctions.PreCacheItem(ballWeapon);
            GSCFunctions.PreCacheShader("waypoint_defend");
            GSCFunctions.PreCacheShader("waypoint_target");
            GSCFunctions.PreCacheShader("waypoint_kill");
            GSCFunctions.PreCacheShader("waypoint_targetneutral");
            GSCFunctions.PreCacheShader("equipment_emp_grenade");

            ballGlow = GSCFunctions.LoadFX("misc/aircraft_light_wingtip_green");
            ballSiteFX = GSCFunctions.LoadFX("misc/ui_flagbase_gold");
            alliesSiteFX = GSCFunctions.LoadFX("misc/ui_flagbase_red");
            axisSiteFX = GSCFunctions.LoadFX("misc/ui_flagbase_silver");
            ballContrail = GSCFunctions.LoadFX("misc/light_semtex_geotrail");
            ballExplodeFX = GSCFunctions.LoadFX("explosions/emp_grenade");

            GSCFunctions.SetDevDvarIfUninitialized("scr_ball_scorelimit", 10);
            //GSCFunctions.SetDevDvarIfUninitialized("scr_ball_halftime", 0);
            //GSCFunctions.SetDevDvarIfUninitialized("scr_ball_overtime", 0);
            StartAsync(setGameScoreLimit());
            StartAsync(setGameHalftimeSetting());
            Log.Debug(isHalftime.ToString());

            //Delete flags
            Entity obj = GSCFunctions.GetEnt("ctf_zone_axis", "targetname");
            axisSite = obj.Origin;
            //obj.Delete();
            axisFlagBase = Entity.GetEntity(obj.EntRef);
            Entity flag = GSCFunctions.GetEnt("ctf_flag_axis", "targetname");
            //flag.Delete();
            axisFlag = Entity.GetEntity(flag.EntRef);
            Entity trig = GSCFunctions.GetEnt("ctf_trig_axis", "targetname");
            axisFlagTrig = Entity.GetEntity(trig.EntRef);
            obj = GSCFunctions.GetEnt("ctf_zone_allies", "targetname");
            alliesSite = obj.Origin;
            //obj.Delete();
            alliesFlagBase = Entity.GetEntity(obj.EntRef);
            flag = GSCFunctions.GetEnt("ctf_flag_allies", "targetname");
            //flag.Delete();
            alliesFlag = Entity.GetEntity(flag.EntRef);
            trig = GSCFunctions.GetEnt("ctf_trig_allies", "targetname");
            alliesFlagTrig = Entity.GetEntity(trig.EntRef);

            StartAsync(getFlagTriggers());

            //Teleport flags under map and hide them
            axisFlagBase.Origin -= new Vector3(0, 0, 1000);
            axisFlagBase.Hide();
            axisFlag.Origin -= new Vector3(0, 0, 1000);
            axisFlag.Hide();
            axisFlagTrig.Origin -= new Vector3(0, 0, 1000);
            alliesFlagBase.Origin -= new Vector3(0, 0, 1000);
            alliesFlagBase.Hide();
            alliesFlag.Origin -= new Vector3(0, 0, 1000);
            alliesFlag.Hide();
            alliesFlagTrig.Origin -= new Vector3(0, 0, 1000);

            //Init stations and ball locations
            site = GSCFunctions.GetEnt("sab_bomb", "targetname").Origin;
            spawnBall(site);
            StartAsync(spawnStations(alliesSite, axisSite));

            OnNotify("game_ended", (reason) =>
            {
                gameEnded = true;

                if ((int)GSCFunctions.GetMatchData("alliesScore") == 1)
                    //GSCFunctions.SetDvar("scr_ball_halftime", 0);//Reset dvar if it's set
                    GSCFunctions.SetMatchData("alliesScore", 0);

                if ((string)reason == "halftime")
                {
                    //GSCFunctions.SetDvar("scr_ball_halftime", 1);
                    GSCFunctions.SetMatchData("alliesScore", 1);
                }
            });
            OnNotify("prematch_over", () => prematchOver = true);

            //Set ball throw time
            GSCFunctions.SetDvar("perk_weapRateMultiplier", 0.3f);

            PlayerConnected += onPlayerConnect;
            Notified += onNotify;

            StartAsync(initGameHud());
        }

        private static void onPlayerConnect(Entity player)
        {
            player.SetField("hasBall", false);
            player.SpawnedPlayer += () => OnPlayerSpawned(player);
            player.SetClientDvar("cg_objectiveText", "Upload the satellite drone into the enemy Uplink Station.");
            //Set ball throw time
            //entity.SetClientDvar("perk_weapRateMultiplier", 0.3f);
            monitorBallThrow(player);
            AfterDelay(2000, () =>
                playBallFX());
        }
        private static void onNotify(int entRef, string message, Parameter[] param)
        {
            if (message == "joined_team" && entRef < 18)
            {
                Entity player = Entity.GetEntity(entRef);
                AfterDelay(1000, () =>
                {
                    playStationFXForPlayer(player);
                    /*
                    if (GSCFunctions.GetDvarInt("scr_ball_halftime") == 1)
                    {
                        if (player.SessionTeam == "allies")
                            player.SessionTeam = "axis";
                        else if (player.SessionTeam == "axis")
                            player.SessionTeam = "allies";
                    }
                    */
                });
            }
        }

        public override void OnSay(Entity player, string name, string message)
        {
            if (message == "showFX")
            {
                Log.Write(LogLevel.All, "Playing FX");
                playBallFX();
                playStationFX();
            }
            if (message == "win")
            {
                GSCFunctions.SetTeamScore(player.SessionTeam, 10);
                StartAsync(checkRoundWin(player));
            }
        }

        public override void OnPlayerKilled(Entity player, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
        {
            AfterDelay(0, () =>
            {
                if (player.GetField<bool>("hasBall"))
                    playerDropBall(player);
            });
        }

        public static void OnPlayerSpawned(Entity player)
        {
            player.SetClientDvar("cg_objectiveText", "Upload the satellite drone into the enemy Uplink Station.");
        }

        private static IEnumerator setGameScoreLimit()
        {
            yield return Wait(1);

            //Promode for lulz
            GSCFunctions.SetDynamicDvar("scr_ctf_promode", 1);

            int scoreLimit = GSCFunctions.GetDvarInt("scr_ball_scorelimit");
            GSCFunctions.SetDynamicDvar("scr_ctf_scorelimit", scoreLimit);
        }
        private static IEnumerator setGameHalftimeSetting()
        {
            yield return WaitForFrame();
            isHalftime = (int)GSCFunctions.GetMatchData("alliesScore") == 1;
        }

        private static IEnumerator initGameHud()
        {
            yield return Wait(1);

            //Delete GSC hud and set custom hud
            int startOfIcons = 0;

            for (int i = 65536; i < 65800; i++)
            {
                HudElem temp = HudElem.GetHudElem(i);
                if (temp.FontScale == 1.6f && (string)temp.GetField(4) == "small")
                {
                    startOfIcons = i - 1;
                    break;
                }
            }

            if (startOfIcons == 0) yield break;

            ballIcon_allies = HudElem.GetHudElem(startOfIcons);
            ballText_allies = HudElem.GetHudElem(startOfIcons + 1);
            ballIcon_axis = HudElem.GetHudElem(startOfIcons + 4);
            ballText_axis = HudElem.GetHudElem(startOfIcons + 5);
            HudElem ballIcon2_allies = HudElem.GetHudElem(startOfIcons + 2);
            HudElem ballText2_allies = HudElem.GetHudElem(startOfIcons + 3);
            HudElem ballIcon2_axis = HudElem.GetHudElem(startOfIcons + 6);
            HudElem ballText2_axis = HudElem.GetHudElem(startOfIcons + 7);
            //ballIcon2_allies.Destroy();
            ballIcon2_allies.Alpha = 0;
            //ballIcon2_axis.Destroy();
            ballIcon2_axis.Alpha = 0;
            //ballText2_allies.Destroy();
            ballText2_allies.Alpha = 0;
            //ballText2_axis.Destroy();
            ballText2_axis.Alpha = 0;

            ballIcon_allies.SetShader("equipment_emp_grenade", 32, 32);
            ballIcon_axis.SetShader("equipment_emp_grenade", 32, 32);
            ballText_allies.SetText("HOME");
            ballText_axis.SetText("HOME");

            //axisSiteAttackerWaypoint = HudElem.GetHudElem(65540);//Allies attacker
            //axisSiteDefenderWaypoint = HudElem.GetHudElem(65541);//Axis defend
            //alliesSiteDefenderWaypoint = HudElem.GetHudElem(65542);//Allies defend
            //alliesSiteAttackerWaypoint = HudElem.GetHudElem(65543);//Axis attack

            startOfIcons = 0;
            HudElem[] gameHud = new HudElem[4] { alliesSiteAttackerWaypoint, alliesSiteDefenderWaypoint, axisSiteAttackerWaypoint, axisSiteDefenderWaypoint };

            for (int i = 65544; i < 65800; i++)
            {
                HudElem temp = HudElem.GetHudElem(i);
                if ((string)temp.GetField(4) == "default" && temp.Alpha == 0.5019608f && !gameHud.Contains(temp))
                {
                    temp = HudElem.GetHudElem(i + 1);
                    if ((string)temp.GetField(4) == "default" && temp.Alpha == 0.5019608f && !gameHud.Contains(temp))//Check for the second one after
                    {
                        startOfIcons = i;
                        break;
                    }
                }
            }

            if (startOfIcons == 0) yield break;

            HudElem flagIcon = HudElem.GetHudElem(startOfIcons);
            flagIcon.Destroy();
            flagIcon = HudElem.GetHudElem(startOfIcons + 1);
            flagIcon.Destroy();
            flagIcon = HudElem.GetHudElem(startOfIcons + 2);
            flagIcon.Destroy();
            flagIcon = HudElem.GetHudElem(startOfIcons + 3);
            flagIcon.Destroy();
        }
        private static IEnumerator getFlagTriggers()
        {
            yield return Wait(1);

            Entity trig = null;
            for (int i = 18; i < 2000; i++)
            {
                trig = Entity.GetEntity(i);

                if (trig.Classname != "trigger_radius") continue;
                if (trig.TargetName.Contains("ctf_")) continue;

                if (trig.Origin.Equals(alliesFlagTrig.Origin) && alliesFlagTrig2 == null)
                    alliesFlagTrig2 = Entity.GetEntity(trig.EntRef);
                else if (trig.Origin.Equals(axisFlagTrig.Origin) && axisFlagTrig2 == null)
                    axisFlagTrig2 = Entity.GetEntity(trig.EntRef);

                if (alliesFlagTrig2 != null && axisFlagTrig2 != null)
                    break;
            }
        }

        private static void spawnBall(Vector3 location)
        {
            ballEnt = GSCFunctions.Spawn("script_model", location + new Vector3(0, 0, 1030));
            ballEnt.SetModel("viewmodel_light_marker");
            ballEnt.SetField("beingCarried", false);
            ballEnt.SetField("carrier", level);
            ballEnt.SetField("parentEnt", level);
            ballEnt.SetField("lastThrow", 999999999);
            ballEnt.MoveTo(ballEnt.Origin - new Vector3(0, 0, 1000), 10, .5f, 1);
            ballEnt.EnableLinkTo();
            ballEnt.NotSolid();
            baseFX = GSCFunctions.SpawnFX(ballSiteFX, location);

            //ballFX = GSCFunctions.Spawn("script_model", ballEnt.Origin);
            //ballFX.SetModel("tag_origin");
            //ballFX.LinkTo(ballEnt, "tag_origin");
            StartAsync(ball_waitForPrematch());

            //Huds
            ballObjAllies_defend = GSCFunctions.NewTeamHudElem("allies");
            ballObjAllies_defend.Alpha = 0.5f;
            ballObjAllies_defend.Archived = true;
            ballObjAllies_defend.HideIn3rdPerson = false;
            ballObjAllies_defend.HideWhenDead = false;
            ballObjAllies_defend.HideWhenInDemo = false;
            ballObjAllies_defend.HideWhenInMenu = false;
            ballObjAllies_defend.LowResBackground = false;
            ballObjAllies_defend.SetShader("waypoint_targetneutral", 10, 10);
            ballObjAllies_defend.SetTargetEnt(ballEnt);
            ballObjAllies_defend.SetWaypoint(true, true, false, false);
            ballObjAllies_defend.SetWaypointEdgeStyle_RotatingIcon();

            ballObjAllies_kill = GSCFunctions.NewTeamHudElem("allies");
            ballObjAllies_kill.Alpha = 0f;
            ballObjAllies_kill.Archived = true;
            ballObjAllies_kill.HideIn3rdPerson = false;
            ballObjAllies_kill.HideWhenDead = false;
            ballObjAllies_kill.HideWhenInDemo = false;
            ballObjAllies_kill.HideWhenInMenu = false;
            ballObjAllies_kill.LowResBackground = false;
            ballObjAllies_kill.SetShader("waypoint_kill", 10, 10);
            //ballObjAllies_kill.SetTargetEnt(ballEnt);
            //ballObjAllies_kill.SetWaypoint(true, true, false, false);
            //ballObjAllies_kill.SetWaypointEdgeStyle_RotatingIcon();

            ballObjAxis_defend = GSCFunctions.NewTeamHudElem("axis");
            ballObjAxis_defend.Alpha = 0.5f;
            ballObjAxis_defend.Archived = true;
            ballObjAxis_defend.HideIn3rdPerson = false;
            ballObjAxis_defend.HideWhenDead = false;
            ballObjAxis_defend.HideWhenInDemo = false;
            ballObjAxis_defend.HideWhenInMenu = false;
            ballObjAxis_defend.LowResBackground = false;
            ballObjAxis_defend.SetShader("waypoint_targetneutral", 10, 10);
            ballObjAxis_defend.SetTargetEnt(ballEnt);
            ballObjAxis_defend.SetWaypoint(true, true, false, false);
            ballObjAxis_defend.SetWaypointEdgeStyle_RotatingIcon();

            ballObjAxis_kill = GSCFunctions.NewTeamHudElem("axis");
            ballObjAxis_kill.Alpha = 0f;
            ballObjAxis_kill.Archived = true;
            ballObjAxis_kill.HideIn3rdPerson = false;
            ballObjAxis_kill.HideWhenDead = false;
            ballObjAxis_kill.HideWhenInDemo = false;
            ballObjAxis_kill.HideWhenInMenu = false;
            ballObjAxis_kill.LowResBackground = false;
            ballObjAxis_kill.SetShader("waypoint_kill", 10, 10);
        }
        private static IEnumerator ball_waitForPrematch()
        {
            while (!prematchOver)
            {
                yield return Wait(.1f);
            }

            GSCFunctions.Objective_Add(ballObjID, "active");
            GSCFunctions.Objective_Icon(ballObjID, "waypoint_targetneutral");
            GSCFunctions.Objective_Position(ballObjID, ballEnt.Origin);

            GSCFunctions.Objective_Add(ballObjIDAllies, "invisible");
            GSCFunctions.Objective_Icon(ballObjIDAllies, "waypoint_escort");
            GSCFunctions.Objective_Position(ballObjIDAllies, ballEnt.Origin);
            //ballFX.Origin = ballEnt.Origin;
            //GSCFunctions.PlayFXOnTag(ballGlow, ballFX, "tag_origin");
            playBallFX();

            GSCFunctions.TriggerFX(baseFX);

            OnInterval(50, () => monitorBallPickup(ballEnt));
        }
        private static void respawnBall()
        {
            //ballEnt = Call<Entity>("spawn", "script_model", location + new Vector3(0, 0, 1030));
            if (ballEnt.GetField<Entity>("parentEnt") != level)
            {
                ballEnt.GetField<Entity>("parentEnt").Delete();
                ballEnt.SetField("parentEnt", level);
            }
            ballEnt.Unlink();
            ballEnt.SetModel("viewmodel_light_marker");
            ballEnt.Angles = Vector3.Zero;
            ballEnt.SetField("beingCarried", false);
            ballEnt.SetField("carrier", level);
            ballEnt.Origin = site + new Vector3(0, 0, 1030);
            ballEnt.MoveTo(ballEnt.Origin - new Vector3(0, 0, 1000), 5, .5f, 1);
            baseFX.Show();
            playBallFX();
            GSCFunctions.TriggerFX(baseFX);
            AfterDelay(5000, () => ballEnt.SetField("isBeingCarried", false));

            ballText_allies.SetText("HOME");
            ballText_axis.SetText("HOME");
        }
        private static IEnumerator spawnStations(Vector3 alliesPos, Vector3 axisPos)
        {
            yield return Wait(.1f);

            if (isHalftime)
                alliesStation = GSCFunctions.Spawn("script_model", axisPos + new Vector3(0, 0, 100));
            else
                alliesStation = GSCFunctions.Spawn("script_model", alliesPos + new Vector3(0, 0, 100));

            alliesStation.SetModel("tag_origin");
            Entity alliesStationBack = GSCFunctions.Spawn("script_model", alliesStation.Origin);
            alliesStationBack.SetModel("tag_origin");
            alliesStation.SetField("back", alliesStationBack);
            alliesStation.SetField("team", "allies");
            OnInterval(50, () => monitorZone(alliesStation));

            if (isHalftime)
                axisStation = GSCFunctions.Spawn("script_model", alliesPos + new Vector3(0, 0, 100));
            else
                axisStation = GSCFunctions.Spawn("script_model", axisPos + new Vector3(0, 0, 100));

            axisStation.SetModel("tag_origin");
            Entity axisStationBack = GSCFunctions.Spawn("script_model", axisStation.Origin);
            axisStationBack.SetModel("tag_origin");
            axisStation.SetField("back", axisStationBack);
            axisStation.SetField("team", "axis");
            OnInterval(50, () => monitorZone(axisStation));

            alliesStation.SetField("isScoring", false);
            alliesStation.SetField("team", "allies");
            axisStation.SetField("isScoring", false);
            axisStation.SetField("team", "axis");

            GSCFunctions.Objective_Add(alliesSiteAttackerID, "active", alliesStation.Origin, "waypoint_target");
            GSCFunctions.Objective_Team(alliesSiteAttackerID, "allies");
            GSCFunctions.Objective_Add(alliesSiteDefenderID, "active", alliesStation.Origin, "waypoint_defend");
            GSCFunctions.Objective_Team(alliesSiteDefenderID, "axis");
            GSCFunctions.Objective_Add(axisSiteAttackerID, "active", axisStation.Origin, "waypoint_target");
            GSCFunctions.Objective_Team(axisSiteAttackerID, "axis");
            GSCFunctions.Objective_Add(axisSiteDefenderID, "active", axisStation.Origin, "waypoint_defend");
            GSCFunctions.Objective_Team(axisSiteDefenderID, "allies");

            //Hud
            alliesSiteAttackerWaypoint = GSCFunctions.NewTeamHudElem("allies");
            alliesSiteAttackerWaypoint.Alpha = 0.5f;
            alliesSiteAttackerWaypoint.Archived = true;
            alliesSiteAttackerWaypoint.HideIn3rdPerson = false;
            alliesSiteAttackerWaypoint.HideWhenDead = false;
            alliesSiteAttackerWaypoint.HideWhenInDemo = false;
            alliesSiteAttackerWaypoint.HideWhenInMenu = false;
            alliesSiteAttackerWaypoint.LowResBackground = false;
            alliesSiteAttackerWaypoint.SetShader("waypoint_target", 10, 10);
            alliesSiteAttackerWaypoint.X = alliesStation.Origin.X;
            alliesSiteAttackerWaypoint.Y = alliesStation.Origin.Y;
            alliesSiteAttackerWaypoint.Z = alliesStation.Origin.Z;
            alliesSiteAttackerWaypoint.SetWaypoint(true, true, false, false);
            alliesSiteAttackerWaypoint.SetWaypointEdgeStyle_RotatingIcon();

            alliesSiteDefenderWaypoint = GSCFunctions.NewTeamHudElem("axis");
            alliesSiteDefenderWaypoint.Alpha = 0.5f;
            alliesSiteDefenderWaypoint.Archived = true;
            alliesSiteDefenderWaypoint.HideIn3rdPerson = false;
            alliesSiteDefenderWaypoint.HideWhenDead = false;
            alliesSiteDefenderWaypoint.HideWhenInDemo = false;
            alliesSiteDefenderWaypoint.HideWhenInMenu = false;
            alliesSiteDefenderWaypoint.LowResBackground = false;
            alliesSiteDefenderWaypoint.SetShader("waypoint_defend", 10, 10);
            alliesSiteDefenderWaypoint.X = alliesStation.Origin.X;
            alliesSiteDefenderWaypoint.Y = alliesStation.Origin.Y;
            alliesSiteDefenderWaypoint.Z = alliesStation.Origin.Z;
            alliesSiteDefenderWaypoint.SetWaypoint(true, true, false, false);
            alliesSiteDefenderWaypoint.SetWaypointEdgeStyle_RotatingIcon();

            axisSiteAttackerWaypoint = GSCFunctions.NewTeamHudElem("axis");
            axisSiteAttackerWaypoint.Alpha = 0.5f;
            axisSiteAttackerWaypoint.Archived = true;
            axisSiteAttackerWaypoint.HideIn3rdPerson = false;
            axisSiteAttackerWaypoint.HideWhenDead = false;
            axisSiteAttackerWaypoint.HideWhenInDemo = false;
            axisSiteAttackerWaypoint.HideWhenInMenu = false;
            axisSiteAttackerWaypoint.LowResBackground = false;
            axisSiteAttackerWaypoint.SetShader("waypoint_target", 10, 10);
            axisSiteAttackerWaypoint.X = axisStation.Origin.X;
            axisSiteAttackerWaypoint.Y = axisStation.Origin.Y;
            axisSiteAttackerWaypoint.Z = axisStation.Origin.Z;
            axisSiteAttackerWaypoint.SetWaypoint(true, true, false, false);
            axisSiteAttackerWaypoint.SetWaypointEdgeStyle_RotatingIcon();

            axisSiteDefenderWaypoint = GSCFunctions.NewTeamHudElem("allies");
            axisSiteDefenderWaypoint.Alpha = 0.5f;
            axisSiteDefenderWaypoint.Archived = true;
            axisSiteDefenderWaypoint.HideIn3rdPerson = false;
            axisSiteDefenderWaypoint.HideWhenDead = false;
            axisSiteDefenderWaypoint.HideWhenInDemo = false;
            axisSiteDefenderWaypoint.HideWhenInMenu = false;
            axisSiteDefenderWaypoint.LowResBackground = false;
            axisSiteDefenderWaypoint.SetShader("waypoint_defend", 10, 10);
            axisSiteDefenderWaypoint.X = axisStation.Origin.X;
            axisSiteDefenderWaypoint.Y = axisStation.Origin.Y;
            axisSiteDefenderWaypoint.Z = axisStation.Origin.Z;
            axisSiteDefenderWaypoint.SetWaypoint(true, true, false, false);
            axisSiteDefenderWaypoint.SetWaypointEdgeStyle_RotatingIcon();

            playStationFX();
        }

        private static void playBallFX()
        {
            if (!ballEnt.GetField<bool>("beingCarried"))
            {
                stopBallFX();
                //Log.Write(LogLevel.All, "Playing Ball FX");
                AfterDelay(200, () => GSCFunctions.PlayFXOnTag(ballGlow, ballEnt, "j_gun"));
            }
        }
        private static void stopBallFX()
        {
            if (!ballEnt.GetField<bool>("beingCarried"))
            {
                //Log.Write(LogLevel.All, "Stopping Ball FX");
                GSCFunctions.StopFXOnTag(ballGlow, ballEnt, "j_gun");
            }
        }
        private static void updateBallObjPoint()
        {
            if (ballEnt.GetField<bool>("beingCarried"))
            {
                Entity player = ballEnt.GetField<Entity>("carrier");
                GSCFunctions.Objective_Position(ballObjID, player.Origin);
                GSCFunctions.Objective_Icon(ballObjID, "waypoint_kill");
                GSCFunctions.Objective_Team(ballObjID, player.SessionTeam == "allies" ? "axis" : "allies");

                GSCFunctions.Objective_OnEntity(ballObjIDAllies, player);
                GSCFunctions.Objective_State(ballObjIDAllies, "active");
                GSCFunctions.Objective_Team(ballObjIDAllies, player.SessionTeam);

                ballObjAllies_defend.SetShader("waypoint_defend");
                ballObjAllies_defend.SetTargetEnt(player);
                ballObjAllies_defend.Alpha = player.SessionTeam == "allies" ? 0.5f : 0f;
                ballObjAllies_defend.SetWaypoint(true, true, false, false);
                ballObjAxis_defend.SetShader("waypoint_defend");
                ballObjAxis_defend.SetTargetEnt(player);
                ballObjAxis_defend.Alpha = player.SessionTeam == "axis" ? 0.5f : 0f;
                ballObjAxis_defend.SetWaypoint(true, true, false, false);

                //ballObjAllies_kill.SetTargetEnt(player);
                ballObjAllies_kill.Alpha = player.SessionTeam == "axis" ? 0.5f : 0f;
                //ballObjAxis_kill.SetTargetEnt(player);
                ballObjAxis_kill.Alpha = player.SessionTeam == "allies" ? 0.5f : 0f;

                StartAsync(updateBallObjWorld());

                ballText_allies.SetPlayerNameString(player);
                ballText_axis.SetPlayerNameString(player);
            }
            else
            {
                GSCFunctions.Objective_Icon(ballObjID, "waypoint_targetneutral");
                GSCFunctions.Objective_OnEntity(ballObjID, ballEnt);
                GSCFunctions.Objective_Team(ballObjID, "none");

                GSCFunctions.Objective_State(ballObjIDAllies, "invisible");

                ballObjAllies_defend.SetShader("waypoint_targetneutral");
                ballObjAllies_defend.SetTargetEnt(ballEnt);
                ballObjAllies_defend.SetWaypoint(true, true, false, false);
                ballObjAllies_defend.SetWaypointEdgeStyle_RotatingIcon();
                ballObjAllies_defend.Alpha = 0.5f;
                ballObjAxis_defend.SetShader("waypoint_targetneutral");
                ballObjAxis_defend.SetTargetEnt(ballEnt);
                ballObjAxis_defend.SetWaypoint(true, true, false, false);
                ballObjAxis_defend.SetWaypointEdgeStyle_RotatingIcon();
                ballObjAxis_defend.Alpha = 0.5f;

                //ballObjAllies_kill.ClearTargetEnt();
                ballObjAllies_kill.Alpha = 0f;
                //ballObjAxis_kill.ClearTargetEnt();
                ballObjAxis_kill.Alpha = 0f;

                ballText_allies.SetText("AWAY");
                ballText_axis.SetText("AWAY");
            }
        }
        private static IEnumerator updateBallObjWorld()
        {
            Entity carrier = ballEnt.GetField<Entity>("carrier");
            if (carrier == level) yield break;

            ballObjAllies_kill.X = carrier.Origin.X;
            ballObjAllies_kill.Y = carrier.Origin.Y;
            ballObjAllies_kill.Z = carrier.Origin.Z + 60;
            ballObjAllies_kill.SetWaypoint(true, true, false, false);
            ballObjAxis_kill.X = carrier.Origin.X;
            ballObjAxis_kill.Y = carrier.Origin.Y;
            ballObjAxis_kill.Z = carrier.Origin.Z + 60;
            ballObjAxis_kill.SetWaypoint(true, true, false, false);

            GSCFunctions.Objective_Position(ballObjID, carrier.Origin);

            yield return Wait(2);

            if (ballEnt.GetField<bool>("beingCarried"))
                StartAsync(updateBallObjWorld());
        }

        private static void playStationFX()
        {
            Entity alliesBack = alliesStation.GetField<Entity>("back");
            Entity axisBack = axisStation.GetField<Entity>("back");

            GSCFunctions.StopFXOnTag(alliesSiteFX, alliesStation, "tag_origin");
            GSCFunctions.StopFXOnTag(alliesSiteFX, alliesBack, "tag_origin");
            GSCFunctions.StopFXOnTag(axisSiteFX, alliesStation, "tag_origin");
            GSCFunctions.StopFXOnTag(axisSiteFX, alliesBack, "tag_origin");
            GSCFunctions.StopFXOnTag(alliesSiteFX, axisStation, "tag_origin");
            GSCFunctions.StopFXOnTag(alliesSiteFX, axisBack, "tag_origin");
            GSCFunctions.StopFXOnTag(axisSiteFX, axisStation, "tag_origin");
            GSCFunctions.StopFXOnTag(axisSiteFX, axisBack, "tag_origin");
            AfterDelay(200, () =>
            {
                /*
                GSCFunctions.PlayFXOnTag(alliesSiteFX, alliesStation, "tag_origin");
                GSCFunctions.PlayFXOnTag(alliesSiteFX, alliesBack, "tag_origin");
                GSCFunctions.PlayFXOnTag(axisSiteFX, axisStation, "tag_origin");
                GSCFunctions.PlayFXOnTag(axisSiteFX, axisBack, "tag_origin");
                */
                foreach (Entity player in Players)
                {
                    if (player.SessionTeam != "allies" && player.SessionTeam != "axis") continue;

                    if (player.SessionTeam == "allies")
                    {
                        //Log.Debug("Playing ally fx");
                        GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, alliesStation, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, alliesBack, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(axisSiteFX, axisStation, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(axisSiteFX, axisBack, "tag_origin", player);
                    }
                    else if (player.SessionTeam == "axis")
                    {
                        //Log.Debug("Playing axis fx");
                        GSCFunctions.PlayFXOnTagForClients(axisSiteFX, alliesStation, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(axisSiteFX, alliesBack, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, axisStation, "tag_origin", player);
                        GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, axisBack, "tag_origin", player);
                    }
                }


            });
        }
        private static void playStationFXForPlayer(Entity player)
        {
            Entity alliesBack = alliesStation.GetField<Entity>("back");
            Entity axisBack = axisStation.GetField<Entity>("back");

            AfterDelay(200, () =>
            {
                if (player.SessionTeam == "allies")
                {
                    GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, alliesStation, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, alliesBack, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(axisSiteFX, axisStation, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(axisSiteFX, axisBack, "tag_origin", player);
                }
                else if (player.SessionTeam == "axis")
                {
                    GSCFunctions.PlayFXOnTagForClients(axisSiteFX, alliesStation, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(axisSiteFX, alliesBack, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, axisStation, "tag_origin", player);
                    GSCFunctions.PlayFXOnTagForClients(alliesSiteFX, axisBack, "tag_origin", player);
                }
            });
        }

        private static void detachBallFX()
        {
            //ballFX.Unlink();
            //ballFX.Origin -= new Vector3(0, 0, 999999);
        }

        private static bool monitorBallPickup(Entity ballTrigger)
        {
            if (ballTrigger.GetField<int>("lastThrow") + 25000 < GSCFunctions.GetTime())
            {
                ballTrigger.SetField("lastThrow", 999999999);
                respawnBall();
                return true;
            }
            foreach (Entity player in Players)
            {
                if (!player.IsPlayer) continue;
                if (player.CurrentWeapon == "none") continue;

                bool isTouching = ballTrigger.Origin.DistanceTo(player.Origin) < 50;
                if (player.IsAlive && isTouching && !ballEnt.GetField<bool>("beingCarried"))
                {
                    ballEnt.SetField("beingCarried", true);
                    ballEnt.SetField("carrier", player);
                    player.SetField("hasBall", true);
                    player.Health += 100;
                    ballEnt.Hide();
                    ballEnt.Unlink();
                    Entity parent = ballEnt.GetField<Entity>("parentEnt");
                    if (parent != level)
                    {
                        parent.Delete();
                        ballEnt.SetField("parentEnt", level);
                    }
                    //detachBallFX();
                    updateBallObjPoint();
                    baseFX.Hide();
                    player.SetField("lastWeapon", player.CurrentWeapon);
                    player.GiveWeapon(ballWeapon);
                    player.SwitchToWeapon(ballWeapon);
                    player.DisableWeaponSwitch();
                    player.DisableOffhandWeapons();
                    player.DisableWeaponPickup();
                    player.SetPerk("specialty_rof", true, false);
                    return false;
                }
            }

            if (gameEnded) return false;
            else return true;
        }
        private static void monitorBallThrow(Entity carrier)
        {
            carrier.OnNotify("grenade_fire", (ent, grenade, weapon) =>
                {
                    if ((string)weapon == ballWeapon)
                    {
                        ballEnt.Origin = grenade.As<Entity>().Origin;
                        grenade.As<Entity>().EnableLinkTo();
                        ballEnt.Show();
                        //GSCFunctions.Objective_State(ballObjID, "active");
                        ballEnt.Origin = grenade.As<Entity>().Origin;
                        ballEnt.LinkTo(grenade.As<Entity>());
                        ballEnt.SetField("parentEnt", grenade);
                        ballEnt.SetField("lastThrow", GSCFunctions.GetTime());
                        grenade.As<Entity>().Hide();
                        ballEnt.SetField("beingCarried", false);
                        playBallFX();
                        updateBallObjPoint();
                        carrier.SetField("hasBall", false);
                        if (carrier.Health > carrier.MaxHealth) carrier.Health = carrier.MaxHealth;
                        carrier.EnableWeaponSwitch();
                        carrier.EnableOffhandWeapons();
                        carrier.EnableWeaponPickup();
                        carrier.UnSetPerk("specialty_rof", true);
                        carrier.SwitchToWeapon(carrier.GetField<string>("lastWeapon"));
                        AfterDelay(1000, () =>
                            {
                                carrier.TakeWeapon(ballWeapon);
                                OnInterval(50, () => monitorBallPickup(ballEnt));
                                //ballEnt.SetField("carrier", level);//Keep this set as last play so that scores can be called remotely
                            });
                    }
                });
        }
        private static void playerDropBall(Entity player)
        {
            ballEnt.Origin = player.Origin;
            ballEnt.Show();
            ballEnt.SetField("parentEnt", level);
            ballEnt.SetField("lastThrow", GSCFunctions.GetTime());
            ballEnt.SetField("beingCarried", false);
            playBallFX();
            updateBallObjPoint();
            player.SetField("hasBall", false);
            if (player.IsAlive && player.Health > player.MaxHealth) player.Health = player.MaxHealth;
            player.EnableWeaponSwitch();
            player.EnableOffhandWeapons();
            player.EnableWeaponPickup();
            player.UnSetPerk("specialty_rof", true);
            player.SwitchToWeapon(player.GetField<string>("lastWeapon"));
            player.TakeWeapon(ballWeapon);
            AfterDelay(1000, () =>
            {
                OnInterval(50, () => monitorBallPickup(ballEnt));
                //ballEnt.SetField("carrier", level);//Keep this set as last play so that scores can be called remotely
            });
        }
        private static bool monitorZone(Entity zone)
        {
            if (zone.Origin.DistanceTo(ballEnt.Origin) < 75 && !zone.GetField<bool>("isScoring") && ballEnt.GetField<Entity>("carrier").SessionTeam == zone.GetField<string>("team"))
            {
                ballScore(zone);
                zone.SetField("isScoring", true);
                AfterDelay(5000, () => zone.SetField("isScoring", false));
            }
            else if (ballEnt.GetField<bool>("beingCarried") && zone.Origin.DistanceTo(ballEnt.GetField<Entity>("carrier").Origin) < 100 && !zone.GetField<bool>("isScoring") && ballEnt.GetField<Entity>("carrier").SessionTeam == zone.GetField<string>("team"))
            {
                playerDropBall(ballEnt.GetField<Entity>("carrier"));
                ballScore(zone, 2);
                zone.SetField("isScoring", true);
                AfterDelay(5000, () => zone.SetField("isScoring", false));
            }

            Vector3 dir;
            Vector3 oppositeDir;
            if (!ballEnt.GetField<bool>("beingCarried"))
            {
                dir = GSCFunctions.VectorToAngles(ballEnt.Origin - zone.Origin);
                oppositeDir = GSCFunctions.VectorToAngles(zone.Origin - ballEnt.Origin);
            }
            else
            {
                dir = GSCFunctions.VectorToAngles(ballEnt.GetField<Entity>("carrier").Origin - zone.Origin);
                oppositeDir = GSCFunctions.VectorToAngles(zone.Origin - ballEnt.GetField<Entity>("carrier").Origin);
            }
            zone.RotateTo(dir, .2f, .1f, .1f);
            zone.GetField<Entity>("back").RotateTo(oppositeDir, .2f, .1f, .1f);

            if (gameEnded) return false;
            else return true;
        }
        private static void ballScore(Entity zone, int points = 1)
        {
            if (!ballEnt.HasField("carrier"))
            {
                Log.Write(LogLevel.Error, "Ball scored without proper setup! Carrier was not set");
                return;
            }
            Entity scorer = ballEnt.GetField<Entity>("carrier");
            if (scorer == null || scorer == level)
            {
                Log.Write(LogLevel.Error, "Ball scored with no valid carrier!");
                return;
            }
            GSCFunctions.PlayFX(ballExplodeFX, zone.Origin);
            stopBallFX();
            ballEnt.PlaySound("mp_capture_flag");
            ballEnt.Unlink();//Unlink from parent ent
            ballEnt.MoveTo(zone.Origin, 1, .5f, .5f);
            scorer.Notify("objective", "captured");
            AfterDelay(1000, () => ballEnt.MoveTo(zone.Origin + new Vector3(0, 0, 5000), 3, 1));
            AfterDelay(4000, () => respawnBall());
            string team = zone.GetField<string>("team");
            int score = GSCFunctions.GetTeamScore(team);
            GSCFunctions.SetTeamScore(team, score + points);

            StartAsync(checkRoundWin(scorer));
        }

        private static IEnumerator checkRoundWin(Entity winner)
        {
            string team = winner.SessionTeam;
            if (GSCFunctions.GetTeamScore(team) >= GSCFunctions.GetDvarInt("scr_ctf_scorelimit"))
            {
                //HACK, set scorelimit to 1 then teleport the corresponding flag and enemy base to the winning player to tell GSC we scored a flag
                GSCFunctions.SetDynamicDvar("scr_ctf_scorelimit", "1");
                GSCFunctions.SetTeamScore(winner.SessionTeam, 0);
                yield return Wait(.5f);//Wait a frame or two to let scorelimit update

                Notify("update_scorelimit", 1);
                if (isHalftime)
                {
                    if (team == "allies")
                    {
                        OnInterval(50, () =>
                        {
                            alliesFlagBase.Origin = winner.Origin;
                            if (gameEnded) return false;
                            return true;
                        });
                        axisFlagTrig2.LinkTo(winner, "tag_origin", Vector3.Zero, Vector3.Zero);
                    }
                    else if (team == "axis")
                    {
                        OnInterval(50, () =>
                        {
                            axisFlagBase.Origin = winner.Origin;
                            if (gameEnded) return false;
                            return true;
                        });
                        alliesFlagTrig2.LinkTo(winner, "tag_origin", Vector3.Zero, Vector3.Zero);
                    }
                    else
                        GSCFunctions.SetDynamicDvar("scr_ctf_timelimit", "0.01");//Failsafe
                }
                else
                {
                    if (team == "allies")
                    {
                        OnInterval(50, () =>
                        {
                            axisFlagBase.Origin = winner.Origin;
                            if (gameEnded) return false;
                            return true;
                        });
                        alliesFlagTrig2.LinkTo(winner, "tag_origin", Vector3.Zero, Vector3.Zero);
                    }
                    else if (team == "axis")
                    {
                        OnInterval(50, () =>
                        {
                            alliesFlagBase.Origin = winner.Origin;
                            if (gameEnded) return false;
                            return true;
                        });
                        axisFlagTrig2.LinkTo(winner, "tag_origin", Vector3.Zero, Vector3.Zero);
                    }
                    else
                        GSCFunctions.SetDynamicDvar("scr_ctf_timelimit", "0.01");//Failsafe
                }
            }
        }
    }
}
