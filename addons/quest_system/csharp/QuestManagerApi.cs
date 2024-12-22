using System.Collections.Generic;
using Godot;
using System.Diagnostics;
using System.Linq;
using Godot.Collections;

public partial class QuestManagerApi : Node
{
	private const string ConfigCategory = "quest_system/config/";
	
	public static QuestManagerApi Instance;
	[Signal] public delegate void QuestAcceptedEventHandler(Quest quest);
	[Signal] public delegate void QuestCompletedEventHandler(Quest quest);
	[Signal] public delegate void NewAvailableQuestEventHandler(Quest quest);
	
	public AvailablePool availablePool = new("available");
	public ActivePool activePool = new("active");
	public CompletedPool completedPool = new("completed");
		
	// Called when the node enters the scene tree for the first time.
	public QuestManagerApi()
	{
		var loadAvailablePoolPath = ProjectSettings.GetSetting(ConfigCategory + "available_quest_pool_path", "").ToString();
		CSharpScript loadedAvailablePoolScript = null;
		if (loadAvailablePoolPath != "")
		{
			loadedAvailablePoolScript = (CSharpScript)GD.Load(loadAvailablePoolPath);
		}
		if (loadedAvailablePoolScript != null && !((CSharpScript)availablePool.GetScript()).Equals(loadedAvailablePoolScript))
		{
			availablePool.QueueFree();
			availablePool = (AvailablePool)loadedAvailablePoolScript.New("available");
		}
		
		
		var loadActivePoolPath = ProjectSettings.GetSetting(ConfigCategory + "active_quest_pool_path", "").ToString();
		CSharpScript loadedActivePoolScript = null;
		if (loadActivePoolPath != "")
		{
			loadedActivePoolScript = (CSharpScript)GD.Load(loadActivePoolPath);
		}
		if (loadedActivePoolScript != null && !((CSharpScript)activePool.GetScript()).Equals(loadedActivePoolScript))
		{
			activePool.QueueFree();
			activePool = (ActivePool)loadedActivePoolScript.New("available");
		}
		
		var loadCompletedPoolPath = ProjectSettings.GetSetting(ConfigCategory + "completed_quest_pool_path", "").ToString();
		CSharpScript loadedCompletedPoolScript = null;
		if (loadCompletedPoolPath != "")
		{
			loadedCompletedPoolScript = (CSharpScript)GD.Load(loadCompletedPoolPath);
		}
		if (loadedCompletedPoolScript != null && !((CSharpScript)completedPool.GetScript()).Equals(loadedCompletedPoolScript))
		{
			completedPool.QueueFree();
			completedPool = (CompletedPool)loadedCompletedPoolScript.New("available");
		}
		
		AddChild(availablePool);
		AddChild(activePool);
		AddChild(completedPool);

		var paths = (Array)ProjectSettings.GetSetting(ConfigCategory + "additional_pools", new Array());
		foreach (var path in paths)
		{
			var poolName = path.ToString().GetFile().Split(".")[0].ToPascalCase();
			AddNewPool(path.ToString(), poolName);
		}
	}

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		foreach (var pool in GetAllPools())
		{
			pool.Reset();
			pool.QueueFree();
		}
	}

	#region Quest API

	public Quest StartQuest(Quest quest, Dictionary args = null)
	{
		Debug.Assert(quest != null);
		
		if (activePool.IsQuestInside(quest))
			return quest;

		if (completedPool.IsQuestInside(quest) 
		    || (bool)ProjectSettings.GetSetting(ConfigCategory + "allow_repeating_completed_quests", false))
			return quest;

		availablePool.RemoveQuest(quest);
		activePool.AddQuest(quest);
		EmitSignal(SignalName.QuestAccepted, quest);
		
		quest.Start(args);
		
		return quest;
	}

	public Quest CompleteQuest(Quest quest, Dictionary args = null)
	{
		Debug.Assert(quest != null);

		if (!activePool.IsQuestInside(quest))
			return quest;

		if (!quest.ObjectiveCompleted && (bool)ProjectSettings.GetSetting(ConfigCategory + "require_objective_completed"))
			return quest;
		
		quest.Complete(args);
		
		activePool.RemoveQuest(quest);
		completedPool.AddQuest(quest);
		EmitSignal(SignalName.QuestCompleted, quest);
		
		return quest;
	}

	public Quest UpdateQuest(Quest quest, Dictionary args = null)
	{
		var poolWithQuest = GetAllPools().FirstOrDefault(pool => pool.IsQuestInside(quest));

		if (poolWithQuest == null)
		{
			GD.PushWarning("Tried calling update on a Quest that is not in any pool.");
			return quest;
		}
		
		quest.Update(args);
		return quest;
	}

	public void MarkQuestAsAvailable(Quest quest)
	{
		if (availablePool.IsQuestInside(quest) ||
		    completedPool.IsQuestInside(quest) ||
		    activePool.IsQuestInside(quest))
		{
			return;
		}
		
		availablePool.AddQuest(quest);
		EmitSignal(SignalName.NewAvailableQuest, quest);
	}

	public List<Quest> GetAvailableQuests()
	{
		return availablePool.GetAllQuests();
	}

	public List<Quest> GetActiveQuests()
	{
		return activePool.GetAllQuests();
	}

	public bool IsQuestAvailable(Quest quest)
	{
		return availablePool.IsQuestInside(quest);
	}

	public bool IsQuestActive(Quest quest)
	{
		return activePool.IsQuestInside(quest);
	}

	public bool IsQuestCompleted(Quest quest)
	{
		return completedPool.IsQuestInside(quest);
	}

	public bool IsQuestInPool(Quest quest, string poolName = "")
	{
		if (string.IsNullOrEmpty(poolName))
		{
			return GetAllPools().Any(pool => pool.IsQuestInside(quest));
		}
		
		var pool = GetNode(poolName);
		return ((BaseQuestPool)pool).IsQuestInside(quest);
	}

	public void CallQuestMethod(int questId, string methodName, Array args)
	{
		var quest = GetQuestById(questId);
		
		if(quest == null)
			return;
		
		if(quest.HasMethod(methodName))
			quest.Callv(methodName, args);
	}

	public void SetQuestProperty(int questId, string propertyName, Variant value)
	{
		var quest = GetQuestById(questId);
		if (quest == null)
			return;
		
		if(!QuestHasProperty(quest, propertyName))
			return;
		
		quest.Set(propertyName, value);
	}

	public bool TryGetQuestProperty(int questId, string propertyName, out Variant value)
	{
		value = 0;
		
		var quest = GetQuestById(questId);
		if (quest == null)
			return false;

		if (!QuestHasProperty(quest, propertyName))
			return false;
		
		value = quest.Get(propertyName);
		return true;
	}

	public bool QuestHasProperty(Quest quest, string propertyName)
	{
		if (string.IsNullOrEmpty(propertyName))
			return false;

		return quest.GetPropertyList().Any(property => (string)property["name"] == propertyName);
	}

	public Quest GetQuestById(int questId)
	{
		Quest quest = null;
		
		var pool = GetAllPools().FirstOrDefault(pool => pool.GetQuestFromId(questId) != null);
		if (pool != null)
			quest = pool.GetQuestFromId(questId);
		
		return quest;
	}

	#endregion

	#region Manager API

	public void AddNewPool(string poolPath, string poolName)
	{
		var poolScript = GD.Load<CSharpScript>(poolPath);
		
		if (poolScript == null)
			return;
		
		var pool = (BaseQuestPool)poolScript.New("poolName");
		
		foreach (var pools in GetAllPools())
		{
			if (pool.GetScript().Equals(pools.GetScript()) && poolName != pools.Name)
				return;
		}

		AddChild(pool, true);
	}

	public void RemovePool(string poolName)
	{
		var pool = GetPool(poolName);
		pool?.QueueFree();
	}

	public BaseQuestPool GetPool(string poolName)
	{
		return GetNodeOrNull<BaseQuestPool>(poolName);
	}

	public BaseQuestPool[] GetAllPools()
	{
		var pools = new List<BaseQuestPool>();
		foreach (var child in GetChildren())
		{
			if(child is BaseQuestPool pool)
				pools.Add(pool);
		}
		return pools.ToArray();
	}

	public Quest MoveQuestToPool(Quest quest, string oldPoolName, string newPoolName)
	{
		if (oldPoolName == newPoolName)
			return quest;
		
		var oldPoolInstance = GetNodeOrNull<BaseQuestPool>(oldPoolName);
		var newPoolInstance = GetNodeOrNull<BaseQuestPool>(newPoolName);
		Debug.Assert(oldPoolInstance != null && newPoolInstance != null);
		
		oldPoolInstance.RemoveQuest(quest);
		newPoolInstance.AddQuest(quest);

		return quest;
	}

	public void ResetPool(string poolName = "")
	{
		if (poolName == "")
		{
			foreach (var pool in GetAllPools())
				pool.Reset();
			
			return;
		}
		
		var poolToReset = GetPool(poolName);
		poolToReset.Reset();
	}

	public Dictionary QuestsAsDictionary()
	{
		var questDictionary = new Dictionary();
		foreach (var pool in GetAllPools())
			questDictionary.Add(pool.Name, pool.GetIdsFromQuests());

		return questDictionary;
	}

	public void DictionaryToQuests(Dictionary dictionary, List<Quest> quests)
	{
		foreach (var pool in GetAllPools())
		{
			if (!dictionary.ContainsKey(pool.Name))
				continue;
			
			var poolIds = new List<int>();
			poolIds.AddRange((int[])dictionary[pool.Name]);
			for (var i = quests.Count - 1; i >= 0; i--)
			{
				if (!poolIds.Contains(quests[i].Id)) 
					continue;
				
				pool.AddQuest(quests[i]);
				quests.RemoveAt(i);
			}
		}
	}

	public Dictionary SerializeQuests(string poolName = "")
	{
		var questDictionary = new Dictionary();
		var quests = new List<Quest>();
		if(poolName == "")
		{
			foreach (var pool in GetAllPools())
			{
				quests.AddRange(pool.GetAllQuests());
			}
		}
		else
		{
			var pool = GetPool(poolName);
			if (pool == null)
				return questDictionary;
			
			quests.AddRange(pool.GetAllQuests());
		}
		
		foreach (var quest in quests)
		{
			var questData = quest.Serialize();
			questDictionary.Add(quest.Id.ToString(), questData);
		}
		
		return questDictionary;
	}

	public Error DeserializeQuests(Dictionary data, string poolName = "")
	{
		var quests = new List<Quest>();
		if (poolName == "")
		{
			foreach (var pool in GetAllPools())
				quests.AddRange(pool.GetAllQuests());
		}
		else
		{
			var pool = GetPool(poolName);
			if (pool == null)
				return Error.DoesNotExist;
			
			quests.AddRange(pool.GetAllQuests());
		}
		
		foreach (var quest in quests)
			quest.Deserialize(data);

		return Error.Ok;
	}

	#endregion
}
