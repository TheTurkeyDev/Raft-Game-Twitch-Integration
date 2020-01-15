# Raft Twitch Integration

## Playing
This mod adds Twitch integration to Raft.

This mod also Requires Blargerist's 7DaysToStream application to interface with Twitch and the mod.

To use, copy the Twitch_Itegration.cs file located inside the RaftTwitchIntegrations folder to your games mod folder.

Next in Blargerist's 7DaysToStream, create a folder named Raft inside of the Integrations folder and copy the RaftTwitchIntegrationActions.dll file andp ut it inside the Raft folder.

To setup the Events.txt file checkout the wiki for more info.

---

## Contributing
### Requirements:
Contributing to the development of this mod requires:
* Visual Studio 2017 or later
* A copy of the game
* An installation of Raft Modloader

### Setup:
1. Install Raft
2. Install Raft Modloader
3. Run Raft Modloader at least once so it has a chance to update
4. Fork this repo
5. If your Raft installation is not in the Steam Default location:
    1. Open the csproj file as a file (not a project)
    2. Change the ``RaftInstallation`` variable to point to your raft installation
6. Open the solution or csproj as a solution or project, respectively
7. Have Fun!