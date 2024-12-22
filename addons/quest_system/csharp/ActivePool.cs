using Godot;
using System;

public partial class ActivePool : BaseQuestPool
{
	public ActivePool(string poolName) : base(poolName)
	{
	}

	public virtual void UpdateObjective(int questId)
	{
		var quest = GetQuestFromId(questId);
		quest.Update();
	}
}
