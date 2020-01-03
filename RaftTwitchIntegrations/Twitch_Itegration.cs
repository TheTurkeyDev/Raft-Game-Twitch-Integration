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
using System.Threading.Tasks;
using System.IO.Pipes;
using Harmony;
using UnityEngine.AzureSky;

[ModTitle("Raft Twitch Integration")] // The mod name.
[ModDescription("Adds Twitch Integration to Raft")] // Short description for the mod.
[ModAuthor("Turkey2349")] // The author name of the mod.
[ModIconUrl("http://files.theprogrammingturkey.com/images/raft_twitch_integration_mod_logo.jpg")] // An icon for your mod. Its recommended to be 128x128px and in .jpg format.
[ModWallpaperUrl("https://files.theprogrammingturkey.com/images/raft_twitch_integration_mod_banner.jpg")] // A banner for your mod. Its recommended to be 330x100px and in .jpg format.
[ModVersionCheckUrl("")] // This is for update checking. Needs to be a .txt file with the latest mod version.
[ModVersion("1.0")] // This is the mod version.
[RaftVersion("Update Latest")] // This is the recommended raft version.
[ModIsPermanent(true)] // If your mod add new blocks, new items or just content you should set that to true. It loads the mod on start and prevents unloading.
public class Twitch_Itegration : Mod
{
    // The Start() method is being called when your mod gets loaded.
    private ChatManager chat;
    private Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
    private ChannelGroup channels;
    private Channel newChannel;
    private FMOD.System system = FMODUnity.RuntimeManager.LowlevelSystem;

    public static ConcurrentQueue<RewardData> rewardsQueue = new ConcurrentQueue<RewardData>();
    public static List<StatData> statsEdited = new List<StatData>();


    // The Start() method is being called when your mod gets loaded.
    public void Start()
    {
        RConsole.Log("Twitch Integration has been loaded!");
        channels = new ChannelGroup();
        newChannel = new Channel();

        system.getMasterChannelGroup(out channels);

        foreach (string f in Directory.GetFiles("mods/ModData/TwitchIntegration"))
        {
            string fileName = f.Replace("mods/ModData/TwitchIntegration\\", "");
            Sound newSound = new Sound();
            system.createSound(f, MODE.DEFAULT, out newSound);
            sounds.Add(fileName, newSound);
            RConsole.Log("Added sound: " + fileName);
        }
        this.chat = ComponentManager<ChatManager>.Value;
        StartConnection();
    }

    public override void WorldEvent_WorldLoaded()
    {
        this.chat = ComponentManager<ChatManager>.Value;
    }


    // The OnModUnload() method is being called when your mod gets unloaded.
    public void OnModUnload()
    {
        RConsole.Log("Twitch Itegration has been unloaded!");
        Shutdown();
        Destroy(gameObject); // Please do not remove that line!
    }

    // The Update() method is being called every frame. Have fun!
    public void Update()
    {
        Network_Player player = RAPI.getLocalPlayer();
        RewardData reward;
        if (rewardsQueue.TryDequeue(out reward))
        {
            switch (reward.action)
            {
                case "sound":
                    system.playSound(sounds[reward.args[0]], channels, false, out newChannel);
                    break;
                case "message":
                    chat.SendChatMessage(string.Join(" ", reward.args), SteamUser.GetSteamID());
                    break;
                case "item":
                    Item_Base item = ItemManager.GetItemByName(reward.args[0]);
                    int amount = 1;
                    int.TryParse(reward.args[1], out amount);
                    Helper.DropItem(new ItemInstance(item, amount, item.MaxUses), player.transform.position, player.CameraTransform.forward, player.transform.ParentedToRaft());
                    break;
                case "inventory_bomb":
                    chat.SendChatMessage("Inventory Bomb!", SteamUser.GetSteamID());
                    foreach (Slot s in player.Inventory.allSlots)
                        player.Inventory.DropItem(s);
                    foreach (Slot s in player.Inventory.equipSlots)
                        player.Inventory.DropItem(s);
                    break;
                case "stat_edit":
                    //TODO
                    //player.PersonController.gravity = 20;
                    //player.PersonController.swimSpeed = 2;
                    //player.PersonController.normalSpeed = 3;
                    //player.PersonController.jumpSpeed = 8;
                    //player.Stats.stat_thirst.Value -= 5;
                    string action = reward.args[0];
                    string stat = reward.args[1];
                    float changeAmount = 1;
                    float.TryParse(reward.args[2], out changeAmount);
                    int duration = 1;
                    int.TryParse(reward.args[3], out duration);

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
                        StatData data = getStatData(player, stat, action, changeAmount);
                        data.duration = duration * 1000;
                        data.timeStarted = DateTime.UtcNow;
                        setStatVal(player, stat, data.currentValue);
                        if (duration != -1)
                            statsEdited.Add(data);
                    }

                    break;
                case "move":
                    //player.PersonController.controller.SimpleMove(new Vector3(100, 0, 0));
                    break;
                case "spawn_entity":
                    Network_Host_Entities nhe = ComponentManager<Network_Host_Entities>.Value;
                    Vector3 pos = player.FeetPosition + new Vector3(0, 1, 0);
                    float scale = 1;
                    if (reward.args.Length > 1)
                        float.TryParse(reward.args[1], out scale);
                    switch (reward.args[0])
                    {
                        case "stone_bird":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.StoneBird, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "puffer_fish":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.PufferFish, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "llama":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Llama, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "goat":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Goat, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "chicken":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Chicken, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "boar":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Boar, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "rat":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Rat, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "shark":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Shark, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "bear":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.Bear, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "mama_bear":
                            nhe.CreateAINetworkBehaviour(AI_NetworkBehaviourType.MamaBear, pos, scale, SaveAndLoad.GetUniqueObjectIndex(), SaveAndLoad.GetUniqueObjectIndex(), null);
                            break;
                        case "seagull":
                            //TODO: It's not like the others for some reason.....
                            break;
                    }
                    break;
                case "set_weather":
                    string weatherName = reward.args[0];
                    bool instant;
                    bool.TryParse(reward.args[1], out instant);
                    WeatherManager wm = ComponentManager<WeatherManager>.Value;
                    Randomizer weather = Traverse.Create(ComponentManager<WeatherManager>.Value).Field("weatherConnections").GetValue() as Randomizer;
                    Weather w = null;
                    foreach (Weather we in weather.GetAllItems<Weather>())
                        if (we.name.Equals(weatherName))
                            w = we;

                    if (w != null)
                    {
                        wm.StopAllCoroutines();
                        wm.StartCoroutine(wm.StartNewWeather(w, instant));
                    }
                    break;
                case "set_time":
                    AzureSkyController skyController = ComponentManager<AzureSkyController>.Value;
                    int hours = 1;
                    int.TryParse(reward.args[0], out hours);
                    int minutes = 1;
                    int.TryParse(reward.args[1], out minutes);
                    skyController.timeOfDay.GotoTime(hours, minutes);
                    break;
            }
        }

        DateTime currentTime = DateTime.UtcNow;
        for (int i = statsEdited.Count - 1; i >= 0; i--)
        {
            StatData data = statsEdited[i];
            if ((currentTime - data.timeStarted).TotalMilliseconds > data.duration)
            {
                setStatVal(player, data.stat, data.originalValue);
                statsEdited.RemoveAt(i);
                chat.SendChatMessage(data.stat + " back to normal!", SteamUser.GetSteamID());
            }
        }

        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            //Randomizer weather = Traverse.Create(ComponentManager<WeatherManager>.Value).Field("weatherConnections").GetValue() as Randomizer;
            //List<string> output = new List<string>();
            //foreach (Weather w in weather.GetAllItems<Weather>())
            // output.Add(w.name);
            //File.WriteAllLines(@"D:\Dumps\Weather.txt", output.ToArray());
        }
        else if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            //AzureSkyController skyController = ComponentManager<AzureSkyController>.Value;
            //chat.SendChatMessage(skyController.timeOfDay.GetTime().ToString(), SteamUser.GetSteamID());
        }
    }

    public void setStatVal(Network_Player player, string stat, float amount)
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
                player.Stats.stat_thirst.Value = amount;
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

    public StatData getStatData(Network_Player player, string stat, string action, float amount)
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
                val = player.Stats.stat_thirst.Value;
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
        data.currentValue = getAdjustedValue(val, action, amount);

        return data;
    }

    public float getAdjustedValue(float val, string action, float amount)
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

    public IEnumerator spawner(Network_Player player)
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

    //Starts and handles the connection
    public static void StartConnection()
    {
        CancellationToken token = source.Token;
        if (!token.IsCancellationRequested)
        {
            //Starts the connection task on a new thread
            Task.Factory.StartNew(() =>
            {

                //Keep making new pipes
                while (!token.IsCancellationRequested)
                {
                    //Catch any errors
                    try
                    {
                        //pipeName is the same as your subfolder name in the Integrations folder of the app
                        using (NamedPipeClientStream client = new NamedPipeClientStream(".", "Raft", PipeDirection.In))
                        {
                            using (StreamReader reader = new StreamReader(client))
                            {
                                //Keep trying to connect
                                while (!token.IsCancellationRequested && !client.IsConnected)
                                {
                                    try
                                    {
                                        client.Connect(1000);//Don't wait too long, so mod can shut down quickly if still trying to connect
                                    }
                                    catch (TimeoutException)
                                    {
                                        //Ignore
                                    }
                                    catch (System.ComponentModel.Win32Exception)
                                    {
                                        //Ignore and sleep for a bit, since the connection didn't time out
                                        Thread.Sleep(500);
                                    }
                                }
                                //Keep trying to read
                                while (!token.IsCancellationRequested && client.IsConnected && !reader.EndOfStream)
                                {

                                    //Read line from stream
                                    string line = reader.ReadLine();

                                    if (line != null)
                                    {
                                        if (line.StartsWith("Action: "))
                                        {
                                            string[] data = line.Substring(8).Split(' ');
                                            rewardsQueue.Enqueue(new RewardData(data[0], data.Skip(1).ToArray()));
                                            //Handle action message. This is what was generated by your IntegrationAction Execute method
                                            //Make sure you handle it on the correct thread
                                        }
                                        else if (line.StartsWith("Message: "))
                                        {
                                            string message = line.Substring(9);
                                            rewardsQueue.Enqueue(new RewardData("message", message.Split(' ')));
                                            //Handle message. This would generally be sent to game chat
                                            //Make sure you handle it on the correct thread

                                        }
                                    }
                                    //Only read every 50ms
                                    Thread.Sleep(50);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //Ignore
                    }
                }

            }, token);
        }
    }

    public class RewardData
    {
        public string action;
        public string[] args;

        public RewardData(string action, string[] args)
        {
            this.action = action;
            if (args == null)
                this.args = new string[0];
            else
                this.args = args;
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
}
