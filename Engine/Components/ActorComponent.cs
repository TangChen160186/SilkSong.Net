using System;
using Engine.Core;
using Engine.Ojbects;

namespace Engine.Components;

public class ActorComponent : EngineObject
{
    public Actor Owner { get; set; }


    public virtual void Destruct()
    {

    }
    public virtual void Activate()
    {

    }
    public virtual void Deactivate()
    {

    }


}
