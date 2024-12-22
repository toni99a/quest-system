using Godot;
using Godot.Collections;

public partial class Quest : Resource
{
	[Export] public int Id;
	[Export] public string QuestName;
	[Export] public string QuestDescription;
	[Export] public string QuestObjective;

	[Signal] public delegate void StartedEventHandler();
	[Signal] public delegate void UpdatedEventHandler();
	[Signal] public delegate void CompletedEventHandler();
	[Signal] public delegate void ObjectiveStatusUpdatedEventHandler(bool value);

	public bool ObjectiveCompleted
	{
		get => _objectiveCompleted;
		set
		{
			_objectiveCompleted = value;
			EmitSignal(SignalName.ObjectiveStatusUpdated, value);
		}
	}
	
	private bool _objectiveCompleted;

	public virtual void Update(Dictionary args = null)
	{
		EmitSignal(SignalName.Updated);
	}

	public virtual void Start(Dictionary args = null)
	{
		EmitSignal(SignalName.Started);
	}

	public virtual void Complete(Dictionary args = null)
	{
		EmitSignal(SignalName.Completed);
	}

	public virtual Dictionary Serialize() // Todo: this part needs to be adapted more
	{
		var questData = new Dictionary();
		foreach (var propertyInfo in GetGodotPropertyList())
		{
			if(propertyInfo.Usage is PropertyUsageFlags.ScriptVariable or PropertyUsageFlags.Storage or PropertyUsageFlags.Editor)
				questData.Add(propertyInfo.Name, Get(propertyInfo.Name));
		}

		questData.Remove("id");
		return questData;
	}

	public virtual void Deserialize(Dictionary data)
	{
		foreach (var (key, value) in data)
		{
			Set(key.ToString(), value); // Todo: check if this works
		}
	}
}
