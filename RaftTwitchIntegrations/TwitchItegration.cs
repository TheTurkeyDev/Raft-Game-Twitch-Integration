using FMOD;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AzureSky;
using HarmonyLib;
using Debug = UnityEngine.Debug;
using Newtonsoft.Json.Linq;

public class TwitchItegration : Mod
{
    private ChatManager chat;
    private readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
    private ChannelGroup channels;
    private FMOD.System system = FMODUnity.RuntimeManager.LowlevelSystem;

    public static ConcurrentQueue<RewardData> rewardsQueue = new ConcurrentQueue<RewardData>();
    public static List<StatData> statsEdited = new List<StatData>();
    public static List<TempEntity> tempEntities = new List<TempEntity>();
    public static List<Meteor> meteors = new List<Meteor>();
    public static List<PushData> pushData = new List<PushData>();

    private StoneDrop stoneDropPrefab = null;


    // The Start() method is being called when your mod gets loaded.
    public void Start()
    {
        Debug.Log("Twitch Integration has been loaded!");
        channels = new ChannelGroup();

        system.getMasterChannelGroup(out channels);

        if (!Directory.Exists("mods/ModData/TwitchIntegration"))
            Directory.CreateDirectory("mods/ModData/TwitchIntegration");

        foreach (string f in Directory.GetFiles("mods/ModData/TwitchIntegration"))
        {
            string fileName = f.Replace("mods/ModData/TwitchIntegration\\", "");
            system.createSound(f, MODE.DEFAULT, out Sound newSound);
            sounds.Add(fileName, newSound);
            Debug.Log("Added sound: " + fileName);
        }
        chat = ComponentManager<ChatManager>.Value;
        IntegationSocket.Start("raft", 23491);
    }

    public override void WorldEvent_WorldLoaded()
    {
        chat = ComponentManager<ChatManager>.Value;
    }


    // The OnModUnload() method is being called when your mod gets unloaded.
    public void OnModUnload()
    {
        Debug.Log("Twitch Itegration has been unloaded!");
        Shutdown();
        Destroy(gameObject); // Please do not remove that line!
    }

    // The Update() method is being called every frame. Have fun!
    public void Update()
    {
        DateTime currentTime = DateTime.UtcNow;
        Network_Player player = RAPI.GetLocalPlayer();
        Network_Host_Entities nhe = ComponentManager<Network_Host_Entities>.Value;
        Raft raft = ComponentManager<Raft>.Value;
        Rigidbody body = Traverse.Create(raft).Field("body").GetValue() as Rigidbody;

        if (rewardsQueue.TryDequeue(out RewardData reward))
        {
            if ((currentTime - reward.added).TotalMilliseconds < reward.delay)
            {
                rewardsQueue.Enqueue(reward);
            }
            else
            {
                string userName = (string)reward.data["metadata"]["user"];
                var values = reward.data["values"];
                switch (reward.action)
                {
                    case "PlaySound":
                        system.playSound(sounds[(string)values["sound"]], channels, false, out Channel _);
                        break;
                    case "ChatMessage":
                        chat.SendChatMessage((string)values["message"], SteamUser.GetSteamID());
                        break;
                    case "SpawnItem":
                        Item_Base item = ItemManager.GetItemByName((string)values["item"]);
                        int amount = (int)values["amount"];
                        Helper.DropItem(new ItemInstance(item, amount, item.MaxUses), player.transform.position, player.CameraTransform.forward, player.transform.ParentedToRaft());
                        break;
                    case "InventoryBomb":
                        chat.SendChatMessage("Inventory Bomb!", SteamUser.GetSteamID());
                        foreach (Slot s in player.Inventory.allSlots)
                            player.Inventory.DropItem(s);
                        foreach (Slot s in player.Inventory.equipSlots)
                            player.Inventory.DropItem(s);
                        break;
                    case "StatEdit":
                        //TODO
                        //player.PersonController.gravity = 20;
                        //player.PersonController.swimSpeed = 2;
                        //player.PersonController.normalSpeed = 3;
                        //player.PersonController.jumpSpeed = 8;
                        //player.Stats.stat_thirst.Value -= 5;
                        string action = (string)values["action"];
                        string stat = (string)values["stat"];
                        float changeAmount = (float)values["changeAmount"];
                        int duration = (int)values["duration"];

                        bool contained = false;
                        foreach (StatData data in statsEdited)
                        {
                            if (data.stat.Equals(stat))
                            {
                                data.duration += duration;
                                contained = true;
                            }
                        }

                        if (!contained)
                        {
                            StatData data = GetStatData(player, stat, action, changeAmount);
                            data.duration = duration * 1000;
                            data.timeStarted = DateTime.UtcNow;
                            SetStatVal(player, stat, data.currentValue);
                            if (duration != -1)
                                statsEdited.Add(data);
                        }

                        break;
                    case "PushPlayer":
                        float force = (float)(values["force"] ?? 8);
                        int dur = (int)(values["duration"] ?? 500);
                        pushData.Add(new PushData(GetBoundedRandVector(0.5f, 1, true) * force, dur));
                        break;
                    case "SpawnEntity":
                        float scale = (float)values["scale"];
                        int amountFromEntries = (int)values["amount"];
                        int spawnDuration = (int)values["spawnDuration"];
                        TempEntity tempEnt;
                        foreach (AI_NetworkBehaviourType value in Enum.GetValues(typeof(AI_NetworkBehaviourType)))
                        {
                            if (!value.ToString().Contains((string)values["entity"], StringComparison.OrdinalIgnoreCase))
                                continue;

                            for (int i = 0; i < amountFromEntries; i++)
                            {
                                Vector3 spawnPosition = player.FeetPosition + player.transform.forward * 2f;
                                switch (value)
                                {
                                    case AI_NetworkBehaviourType.Shark:
                                        spawnPosition = nhe.GetSharkSpawnPosition();
                                        break;
                                    case AI_NetworkBehaviourType.PufferFish:
                                        spawnPosition = player.FeetPosition + player.transform.forward * 4f;
                                        if (spawnPosition.y > -1f)
                                            spawnPosition.y = -1f;
                                        break;
                                    case AI_NetworkBehaviourType.StoneBird:
                                    case AI_NetworkBehaviourType.StoneBird_Caravan:
                                        spawnPosition = player.FeetPosition + new Vector3(UnityEngine.Random.Range(3f, 10f), 10f, UnityEngine.Random.Range(3f, 10f));
                                        if (spawnPosition.y < 15f)
                                            spawnPosition.y = 15f;
                                        break;
                                    case AI_NetworkBehaviourType.Llama:
                                    case AI_NetworkBehaviourType.Goat:
                                    case AI_NetworkBehaviourType.Chicken:
                                        spawnPosition = player.FeetPosition + player.transform.forward;
                                        break;
                                    case AI_NetworkBehaviourType.Dolphin:
                                    case AI_NetworkBehaviourType.Turtle:
                                    case AI_NetworkBehaviourType.Stingray:
                                        if (!nhe.GetSpawnPositionDontCollideWithChunkPoint(ref spawnPosition, 50f))
                                            return;
                                        break;
                                    case AI_NetworkBehaviourType.Whale:
                                        if (!nhe.GetSpawnPositionDontCollideWithChunkPoint(ref spawnPosition, 300f))
                                            return;
                                        break;
                                    case AI_NetworkBehaviourType.BirdPack:
                                        if (!nhe.GetBirdpackSpawnPosition(ref spawnPosition))
                                            return;
                                        break;
                                }

                                AI_NetworkBehaviour ainb = nhe.CreateAINetworkBehaviour(value, spawnPosition, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                                if (ainb is AI_NetworkBehaviour_Domestic)
                                    (ainb as AI_NetworkBehaviour_Domestic).QuickTameLate();

                                int health = (int)(values["health"] ?? -1);
                                if (health != -1)
                                {
                                    ainb.networkEntity.stat_health.SetMaxValue(health);
                                    ainb.networkEntity.stat_health.Value = health;
                                }

                                tempEnt = new TempEntity(ainb);

                                if (tempEnt != null && spawnDuration != -1)
                                {
                                    tempEnt.spawned = DateTime.UtcNow;
                                    tempEnt.duration = spawnDuration * 1000;
                                    tempEntities.Add(tempEnt);
                                }
                            }

                            break;
                        }
                        break;
                    case "ChangeWeather":
                        Debug.Log("Weather!");
                        string weatherName = (string)values["weather"];
                        bool instant = (bool)values["instant"];
                        WeatherManager wm = ComponentManager<WeatherManager>.Value;
                        wm.SetWeather(weatherName, instant);
                        break;
                    case "SetTime":
                        AzureSkyController skyController = ComponentManager<AzureSkyController>.Value;

                        int hours = (int)(values["hours"] ?? 0);
                        int minutes = (int)(values["minutes"] ?? 0);
                        skyController.timeOfDay.GotoTime(hours, minutes);
                        break;
                    case "PickupTrash":
                        WaterFloatSemih2[] floatingObjects = FindObjectsOfType<WaterFloatSemih2>();
                        float radius = (float)values["radius"];
                        foreach (WaterFloatSemih2 trash in floatingObjects)
                        {
                            try
                            {
                                if (!trash.GetComponent<PickupItem>().isDropped && Vector3.Distance(trash.transform.position, player.FeetPosition) < radius)
                                {

                                    PickupItem_Networked pickup = trash.GetComponentInParent<PickupItem_Networked>();
                                    PickupObjectManager.RemovePickupItemNetwork(pickup, SteamUser.GetSteamID());
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                    case "RunCommand":
                        Traverse.Create(RConsole.instance).Method("SilentlyRunCommand", new Type[] { typeof(string) }, new object[] { values["command"] }).GetValue<string>();
                        break;
                    case "MeteorShower":
                        int meteorsToSpawn = (int)values["meteors"];
                        int spawnRadius = (int)values["spawnRadius"];
                        int meteorDamage = (int)values["meteorDamage"];
                        int delay = (int)values["meteorInterval"];

                        if (stoneDropPrefab == null)
                        {
                            AI_NetworkBehaviour_StoneBird ainbsb = (AI_NetworkBehaviour_StoneBird)nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.StoneBird, player.FeetPosition, 0, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            stoneDropPrefab = Traverse.Create(ainbsb.stateMachineStoneBird.dropStoneState).Field("stoneDropPrefab").GetValue() as StoneDrop;
                            ainbsb.Kill();
                        }

                        meteors.Add(new Meteor(meteorsToSpawn, spawnRadius, meteorDamage, delay));
                        break;
                    case "PushRaft":
                        float pushForce = (float)values["force"];
                        body.AddForce(GetBoundedRandVector(0.5f, 1) * pushForce, ForceMode.Impulse);
                        break;
                    case "RotateRaft":
                        float rotationForce = (float)values["force"];
                        body.AddTorque(new Vector3(0, rotationForce, 0), ForceMode.Impulse);
                        break;
                }
            }
        }


        for (int i = statsEdited.Count - 1; i >= 0; i--)
        {
            StatData data = statsEdited[i];
            if ((currentTime - data.timeStarted).TotalMilliseconds > data.duration)
            {
                SetStatVal(player, data.stat, data.originalValue);
                statsEdited.RemoveAt(i);
                chat.SendChatMessage(data.stat + " back to normal!", SteamUser.GetSteamID());
            }
        }

        for (int i = tempEntities.Count - 1; i >= 0; i--)
        {
            TempEntity ent = tempEntities[i];
            if ((currentTime - ent.spawned).TotalMilliseconds > ent.duration)
            {
                if (ent.ent != null)
                {
                    var network_Entity = ent.ent.networkEntity;
                    ComponentManager<Network_Host>.Value.DamageEntity(network_Entity, network_Entity.transform, 9999f, network_Entity.transform.position, Vector3.up, EntityType.Player);
                }
                tempEntities.RemoveAt(i);
            }
        }

        if (meteors.Count > 0)
        {
            for (int m = meteors.Count - 1; m >= 0; m--)
            {
                Meteor me = meteors[m];
                if ((currentTime - me.lastSpawned).TotalMilliseconds > me.delay)
                {
                    Vector3 dropPosition = player.FeetPosition + new Vector3(UnityEngine.Random.Range(-me.radius, me.radius), 200, UnityEngine.Random.Range(-me.radius, me.radius));
                    StoneDrop stone = Instantiate(stoneDropPrefab, dropPosition, Quaternion.identity);
                    float scale = UnityEngine.Random.Range(0.5f, 4f);
                    stone.rigidBody.transform.localScale = new Vector3(scale, scale, scale);
                    stone.rigidBody.AddForce(Vector3.down * meteors.ElementAt(0).damage, ForceMode.Impulse);
                    me.count--;
                    if (me.count == 0)
                        meteors.RemoveAt(0);
                }
            }
        }

        if (pushData.Count > 0)
        {
            var push = pushData[0];
            if (push.startTime == DateTime.MinValue)
                push.startTime = currentTime;

            player.PersonController.transform.position += push.push * Time.deltaTime;
            if ((currentTime - push.startTime).TotalMilliseconds > push.duration)
                pushData.Remove(push);
        }
    }

    public async void logItems()
    {
        using (StreamWriter file = new StreamWriter("items.txt"))
        {
            foreach (var it in ItemManager.GetAllItems())
            {
                await file.WriteLineAsync(it.name);
            }
        }
    }

    public async void logMobs()
    {
        using (StreamWriter file = new StreamWriter("mobs.txt"))
        {
            foreach (AI_NetworkBehaviourType value in Enum.GetValues(typeof(AI_NetworkBehaviourType)))
            {
                await file.WriteLineAsync(value.ToString());
            }
        }
    }

    public Vector3 GetBoundedRandVector(float min, float max, bool inclY = false)
    {
        float x;
        if (UnityEngine.Random.value > 0.5)
            x = UnityEngine.Random.Range(-max, -min);
        else
            x = UnityEngine.Random.Range(min, max);

        float y = 0;
        if (inclY)
            y = UnityEngine.Random.Range(min, max);

        float z;
        if (UnityEngine.Random.value > 0.5)
            z = UnityEngine.Random.Range(-max, -min);
        else
            z = UnityEngine.Random.Range(min, max);

        return new Vector3(x, y, z);
    }

    public void SetStatVal(Network_Player player, string stat, float amount)
    {
        switch (stat)
        {
            case "gravity":
                player.PersonController.gravity = amount;
                break;
            case "swim_speed":
                player.PersonController.swimSpeed = amount;
                break;
            case "walk_speed":
                player.PersonController.normalSpeed = amount;
                break;
            case "jump_speed":
                player.PersonController.jumpSpeed = amount;
                break;
            case "thirst":
                player.Stats.stat_thirst.Normal.Value = amount;
                break;
            case "hunger":
                player.Stats.stat_hunger.Normal.Value = amount;
                break;
            case "oxygen":
                player.Stats.stat_oxygen.Value = amount;
                break;
            case "health":
                player.Stats.stat_health.Value = amount;
                break;
        }
    }

    public StatData GetStatData(Network_Player player, string stat, string action, float amount)
    {
        StatData data = new StatData(stat);
        float val = 1;
        switch (stat)
        {
            case "gravity":
                val = player.PersonController.gravity;
                break;
            case "swim_speed":
                val = player.PersonController.swimSpeed;
                break;
            case "walk_speed":
                val = player.PersonController.normalSpeed;
                break;
            case "jump_speed":
                val = player.PersonController.jumpSpeed;
                break;
            case "thirst":
                val = player.Stats.stat_thirst.Normal.Value;
                break;
            case "hunger":
                val = player.Stats.stat_hunger.Normal.Value;
                break;
            case "oxygen":
                val = player.Stats.stat_oxygen.Value;
                break;
            case "health":
                val = player.Stats.stat_health.Value;
                break;
        }

        data.originalValue = val;
        data.currentValue = GetAdjustedValue(val, action, amount);

        return data;
    }

    public float GetAdjustedValue(float val, string action, float amount)
    {
        switch (action)
        {
            case "set":
                return amount;
            case "add":
                return val + amount;
            case "subtract":
                return val - amount;
            default:
                return amount;
        }
    }

    public IEnumerator Spawner(Network_Player player)
    {
        yield return new WaitForSeconds(0.05f);
        Instantiate(FindObjectOfType<AI_NetworkBehavior_Shark>().transform.gameObject, FindObjectOfType<Network_Player>().transform.position, player.transform.rotation);
    }

    private static readonly CancellationTokenSource source = new CancellationTokenSource();

    //Tells the connection to shut down
    public static void Shutdown()
    {
        source.Cancel();
    }


    public class RewardData
    {
        public string action;
        public JObject data;
        public DateTime added;
        public int delay;

        public RewardData(string action, JObject data, int delay, DateTime added)
        {
            this.action = action;
            this.data = data;
            this.delay = delay;
            this.added = added;
        }
    }

    public class StatData
    {
        public string stat;
        public float originalValue;
        public float currentValue;
        public DateTime timeStarted;
        public int duration;

        public StatData(string stat)
        {
            this.stat = stat;
        }
    }

    public class TempEntity
    {
        public AI_NetworkBehaviour ent;
        public DateTime spawned;
        public int duration;

        public TempEntity(AI_NetworkBehaviour ent)
        {
            this.ent = ent;
        }
    }

    public class Meteor
    {
        public int count;
        public int radius;
        public int damage;
        public int delay;
        public DateTime lastSpawned;

        public Meteor(int count, int radius, int damage, int delay)
        {
            this.count = count;
            this.radius = radius;
            this.damage = damage;
            this.delay = delay;
        }
    }

    public class PushData
    {
        public Vector3 push;
        public DateTime startTime = DateTime.MinValue;
        public int duration;

        public PushData(Vector3 push, int duration)
        {
            this.push = push;
            this.duration = duration;
        }
    }
}