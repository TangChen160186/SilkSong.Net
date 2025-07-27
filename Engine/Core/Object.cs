namespace Engine.Core;

public class EngineObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public String Name { get; set; } = "New Object";
    public virtual void Update(float deltaTime)
    {

    }
    public virtual void BeginPlay()
    {

    }
    
    public virtual void EndPlay()
    {

    }
}
