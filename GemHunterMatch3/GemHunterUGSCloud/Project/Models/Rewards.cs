using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public struct CommandReward
{
    public List<Reward> Rewards;
}

public struct Reward
{
    public string Service;
    public string Id;
    public int Amount;
}