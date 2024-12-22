using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

// Todo: what Namespace should this be in?

public partial class BaseQuestPool : Node
{
	private List<Quest> _quests = new();

	public BaseQuestPool(string poolName)
	{
		SetName(poolName);
	}

	public virtual Quest AddQuest(Quest quest)
	{
		Debug.Assert(quest != null);
		
		_quests.Add(quest);
		return quest;
	}

	public virtual Quest RemoveQuest(Quest quest)
	{
		Debug.Assert(quest != null);
		
		_quests.Remove(quest);
		return quest;
	}

	public virtual Quest GetQuestFromId(int id)
	{
		return _quests.Find(q => q.Id == id);
	}

	public virtual bool IsQuestInside(Quest quest)
	{
		return _quests.Contains(quest);
	}

	public virtual int[] GetIdsFromQuests()
	{
		return _quests.Select(quest => quest.Id).ToArray();
	}

	public virtual List<Quest> GetAllQuests() // Todo: maybe this should be done a better way
	{
		return _quests;
	}

	public virtual void Reset()
	{
		_quests.Clear();
	}
}